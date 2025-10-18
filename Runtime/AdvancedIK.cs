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

        public Vector3 TipForward
        {
            get
            {
                Quaternion rotation = tip.rotation * Quaternion.Inverse(rotationOffset);
                return rotation * Vector3.forward;
            }
        }

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
        public void ApplyIK(bool controlRotation = true)
        {
            // try to calculate the inner angle
            float thighAngle = GetThighAngle();
            if (thighAngle == Mathf.NegativeInfinity)
            {
                return;
            }

            Quaternion originalTipRotation = tip.rotation;

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

            if (controlRotation)
            {
                // rotate the tip, corrected by the offset
                tip.rotation = smoothedTarget.rotation * rotationOffset;
            }
            else
            {
                // restore original global rotation
                tip.rotation = originalTipRotation;
            }
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
    [Tooltip("The power to snap the body to the ground.")]
    [SerializeField, Range(0, 1.5f)] float adaptiveBodyHeight = 1;

    [Tooltip("If disabled, the rotation of the foot will follow animation clips.")]
    [SerializeField] bool controlRotation = true;
    [SerializeField] float footLength = 0.2f;

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
            adaptiveBodyHeight = 0f;
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

        if (adaptiveBodyHeight > 0.1f)
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

        // Calculate the distance between ankle and ground
        float dynamicAnkleOffset = ankleOffset;
        if (!controlRotation)
        {
            Vector3 footForward = footConstraint.TipForward;
            Vector3 groundForward = Vector3.ProjectOnPlane(footForward, groundNormal);
            Vector3 axis = Vector3.Cross(groundForward, footForward);
            float radius = Mathf.Deg2Rad * Vector3.SignedAngle(groundForward, footForward, axis);

            dynamicAnkleOffset = Mathf.Cos(radius) * ankleOffset + Mathf.Sin(radius) * footLength;
        }

        // calculate position
        float verticalOffset = (dynamicAnkleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // set the IK target
        Vector3 forward = Vector3.zero;
        if (controlRotation)
        {
            // calculate rotation
            forward = Vector3.ProjectOnPlane(footConstraint.TipForward, groundNormal);
            Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

            footConstraint.target.SetPositionAndRotation(IK_position, IK_rotation);
        }
        else
        {
            footConstraint.target.position = IK_position;
        }

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, footConstraint.TipForward, forward);
#endif
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
        footConstraint.ApplyIK(controlRotation);
    }

    private float smoothHeightOffset = 0f;
    void AdjustBodyHeight()
    {
        float deltaHeight = Mathf.Abs(
            leftFootConstraint.groundHeight
            - rightFootConstraint.groundHeight
        ) * adaptiveBodyHeight;

        float nextSmoothHeightOffset = Mathf.Lerp(smoothHeightOffset, deltaHeight, Time.deltaTime);
        float deltaHeightOffset = smoothHeightOffset - nextSmoothHeightOffset;
        smoothHeightOffset = nextSmoothHeightOffset;

        characterController.center = originalColliderCenter + new Vector3(0, smoothHeightOffset, 0);
        transform.position += new Vector3(0, deltaHeightOffset, 0);
    }
}
