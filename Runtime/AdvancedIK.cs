using UnityEngine;
using System;
using Advanced;

namespace Advanced
{
    [Serializable]
    public class TwoBoneConstraint
    {
        public Transform root;
        public Transform mid;
        public Transform tip;
        public Transform target;
        public Transform hint;

        // private variables
        float legLength; // pre-computed constants
        float c2_sub_a2, c_mul_2; // pre-computed constants for law of cosines
        Quaternion rotationOffset;
        Transform smoothedTarget; // aka the last tranform of the tip

        // temporary data
        [HideInInspector] public float groundHeight; // used in body height adjustment

        public void Init(Quaternion bodyRotation)
        {
            // pre-compute constants
            float thighLength = Vector3.Distance(root.position, mid.position);
            float calfLength = Vector3.Distance(mid.position, tip.position);
            legLength = thighLength + calfLength;
            c2_sub_a2 = thighLength * thighLength - calfLength * calfLength;
            c_mul_2 = thighLength * 2;

            // used to fix the mis-alignment of tip and body rotation
            rotationOffset = Quaternion.RotateTowards(bodyRotation, tip.rotation, Mathf.Infinity);

            // automatic target creation
            smoothedTarget = new GameObject($"{tip.name}_smoothedTarget").transform;
            if (target == null)
            {
                target = new GameObject($"{tip.name}_target").transform;
            }
            ResetTarget();
            SmoothTarget(1f);
        }

        // try to calculate the inner angle of the root using law of cosines
        public float GetThighAngle()
        {
            float b = Vector3.Distance(root.position, smoothedTarget.position);
            if (legLength <= b)
            {
                return Mathf.NegativeInfinity;
            }

            float angle_A = Mathf.Acos((b * b + c2_sub_a2) / (c_mul_2 * b)) * Mathf.Rad2Deg;
            return angle_A;
        }

        // move the limb to the IK target
        public void ApplyIK()
        {
            // try to calculate the inner angle
            float thighAngle = GetThighAngle();
            if (thighAngle == Mathf.NegativeInfinity)
            {
                return;
            }

            // calculate the normal vector of the plane spanned by hint, target, root
            Vector3 hintDirection = (hint.position - root.position).normalized;
            Vector3 targetDirection = (smoothedTarget.position - root.position).normalized;
            Vector3 axis = Vector3.Cross(targetDirection, hintDirection);

            // rotate the upper limb
            Vector3 currentThighDirection = (mid.position - root.position).normalized;
            Vector3 newThighDirection = Quaternion.AngleAxis(thighAngle, axis) * targetDirection;
            root.rotation = Quaternion.FromToRotation(currentThighDirection, newThighDirection) * root.rotation;

            // rotate the lower limb
            Vector3 currentCalfDirection = (tip.position - mid.position).normalized;
            Vector3 newCalfDirection = (smoothedTarget.position - mid.position).normalized;
            mid.rotation = Quaternion.FromToRotation(currentCalfDirection, newCalfDirection) * mid.rotation;

            // rotate the tip corrected by the offset
            tip.rotation = smoothedTarget.rotation * rotationOffset;
        }

        // lerp smoothedTarget towards target
        public void SmoothTarget(float rate)
        {
            smoothedTarget.SetPositionAndRotation(
                Vector3.Lerp(smoothedTarget.position, target.position, rate),
                Quaternion.Lerp(smoothedTarget.rotation, target.rotation, rate)
            );
        }

        // set target back to tip, rotation aligned with the body space
        public void ResetTarget()
        {
            target.SetPositionAndRotation(tip.position, tip.rotation * Quaternion.Inverse(rotationOffset));
        }
    }
}

public class AdvancedIK : BaseFootIK<TwoBoneConstraint>
{
    [SerializeField] TwoBoneConstraint leftFootConstraint;
    [SerializeField] TwoBoneConstraint rightFootConstraint;

    [Header("Advanced Setting")]
    [SerializeField] bool enableFootLifting = true;
    [SerializeField, Range(0, 1)] float smoothRate = 0.5f;
    [Tooltip("Snap the body to the ground.")]
    [SerializeField] bool adaptiveBodyHeight = true;

    private CharacterController characterController; // can be replaced with CapsuleCollider
    private Vector3 originalColliderCenter;

    void Awake()
    {
        leftFootConstraint.Init(transform.rotation);
        rightFootConstraint.Init(transform.rotation);

        characterController = GetComponent<CharacterController>();
        if (characterController)
        {
            originalColliderCenter = characterController.center;
        }
        else
        {
            adaptiveBodyHeight = false;
            Debug.LogWarning("No CharacterController found. Disabling adaptive collider height.");
        }

#if UNITY_EDITOR
        gizmosCaches.Add(leftFootConstraint, new GizmosCache());
        gizmosCaches.Add(rightFootConstraint, new GizmosCache());
#endif
    }

    private bool animatorUpdated = false;
    void OnAnimatorIK(int layerIndex)
    {
        animatorUpdated = true;
    }

    void LateUpdate()
    {
        if (!animatorUpdated)
        {
            return;
        }
        animatorUpdated = false;

        ResolveIKTarget(leftFootConstraint);
        ResolveIKTarget(rightFootConstraint);

        Placelimb(leftFootConstraint);
        Placelimb(rightFootConstraint);

        if (adaptiveBodyHeight)
        {
            AdjustBodyHeight();
        }
    }

    override protected void ResolveIKTarget(TwoBoneConstraint footConstraint)
    {
        // ground detection using SphereCast
        Vector3 footPosition = footConstraint.tip.position;
#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateRaycast(footPosition);
#endif
        if (!FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal))
        {
            footConstraint.ResetTarget();
            return;
        }
        footConstraint.groundHeight = groundPosition.y;

        // calculate position
        float verticalOffset = (ankleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // calculate rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);
#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, forward);
#endif

        // set the IK target
        footConstraint.target.SetPositionAndRotation(IK_position, IK_rotation);
    }

    void Placelimb(TwoBoneConstraint footConstraint)
    {
        if (enableFootLifting &&
            // foot is above the ground
            footConstraint.target.position.y < footConstraint.tip.position.y)
        {
            footConstraint.ResetTarget();
        }

        footConstraint.SmoothTarget(smoothRate);
        footConstraint.ApplyIK();
    }

    private float smoothHeightOffset = 0f;
    void AdjustBodyHeight()
    {
        float deltaHeight = Mathf.Abs(
            leftFootConstraint.groundHeight
            - rightFootConstraint.groundHeight
        );

        float nextSmoothHeightOffset = Mathf.Lerp(smoothHeightOffset, deltaHeight, Time.deltaTime);
        float deltaHeightOffset = smoothHeightOffset - nextSmoothHeightOffset;
        smoothHeightOffset = nextSmoothHeightOffset;

        characterController.center = originalColliderCenter + new Vector3(0, smoothHeightOffset, 0);
        transform.position += new Vector3(0, deltaHeightOffset, 0);
    }
}
