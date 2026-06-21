using UnityEngine;
using System;
using Advanced;

namespace Advanced
{
    /// <summary>
    /// Advanced two-bone IK constraint with target smoothing and rotation control.
    /// </summary>
    [Serializable]
    public class TwoBoneConstraint
    {
        public Transform root;
        public Transform mid;
        public Transform tip;
        public Transform target;
        public Transform hint;

        private float legLength; // Sum of thigh and calf lengths
        private float c2_sub_a2, c_mul_2; // Pre-computed constants for Law of Cosines
        private Quaternion rotationOffset;
        private Transform smoothedTarget; // Target position after smoothing

        [HideInInspector] public float groundHeight; // Current target ground height

        /// <summary>
        /// Gets the forward direction of the tip in world space, aligned with the body's orientation.
        /// </summary>
        public Vector3 TipForward
        {
            get
            {
                Quaternion rotation = tip.rotation * Quaternion.Inverse(rotationOffset);
                return rotation * Vector3.forward;
            }
        }

        /// <summary> Initializes constraint parameters and target transforms. </summary>
        public void Init(Quaternion bodyRotation)
        {
            // Pre-compute constants for performance
            float thighLength = Vector3.Distance(root.position, mid.position);
            float calfLength = Vector3.Distance(mid.position, tip.position);
            legLength = thighLength + calfLength;
            c2_sub_a2 = thighLength * thighLength - calfLength * calfLength;
            c_mul_2 = thighLength * 2;

            // Fix tip/body rotation misalignment
            rotationOffset = Quaternion.Inverse(bodyRotation) * tip.rotation;

            // Initialize target transforms
            smoothedTarget = new GameObject($"{tip.name}_smoothedTarget").transform;
            if (target == null)
            {
                target = new GameObject($"{tip.name}_target").transform;
            }
            ResetTarget();
            SmoothTarget(1f);
        }

        /// <summary> Calculates the angle of the root (thigh) using the Law of Cosines. </summary>
        public float GetThighAngle()
        {
            float b = Vector3.Distance(root.position, smoothedTarget.position);
            // Target unreachable if distance exceeds total limb length
            if (legLength <= b)
            {
                return Mathf.NegativeInfinity;
            }

            // Law of Cosines to calculate the inner angle at the root
            float angle_A = Mathf.Acos((b * b + c2_sub_a2) / (c_mul_2 * b)) * Mathf.Rad2Deg;
            return angle_A;
        }

        /// <summary> Applies IK to move the limb to the target position and rotation. </summary>
        public void ApplyIK(bool controlRotation = true)
        {
            float thighAngle = GetThighAngle();
            if (thighAngle == Mathf.NegativeInfinity)
            {
                return;
            }

            Quaternion originalTipRotation = tip.rotation;

            // Calculate rotation axis perpendicular to the plane formed by root, hint, and target
            Vector3 hintDirection = (hint.position - root.position).normalized;
            Vector3 targetDirection = (smoothedTarget.position - root.position).normalized;
            Vector3 axis = Vector3.Cross(targetDirection, hintDirection);

            // Rotate upper limb
            Vector3 currentThighDirection = (mid.position - root.position).normalized;
            Vector3 newThighDirection = Quaternion.AngleAxis(thighAngle, axis) * targetDirection;
            root.rotation = Quaternion.FromToRotation(currentThighDirection, newThighDirection) * root.rotation;

            // Rotate lower limb
            Vector3 currentCalfDirection = (tip.position - mid.position).normalized;
            Vector3 newCalfDirection = (smoothedTarget.position - mid.position).normalized;
            mid.rotation = Quaternion.FromToRotation(currentCalfDirection, newCalfDirection) * mid.rotation;

            if (controlRotation)
            {
                // Align tip with smoothed target rotation, corrected by initial offset
                tip.rotation = smoothedTarget.rotation * rotationOffset;
            }
            else
            {
                // Restore original global rotation
                tip.rotation = originalTipRotation;
            }
        }

        /// <summary> Smoothly interpolates smoothedTarget towards the main target. </summary>
        public void SmoothTarget(float rate)
        {
            smoothedTarget.SetPositionAndRotation(
                Vector3.Lerp(smoothedTarget.position, target.position, rate),
                Quaternion.Lerp(smoothedTarget.rotation, target.rotation, rate)
            );
        }

        /// <summary> Resets target position to current tip position, aligned with body space. </summary>
        public void ResetTarget()
        {
            target.SetPositionAndRotation(tip.position, tip.rotation * Quaternion.Inverse(rotationOffset));
        }
    }
}

/// <summary>
/// Advanced foot IK implementation featuring foot lifting, target smoothing,
/// and adaptive body height adjustment.
/// </summary>
public class AdvancedIK : BaseFootIK<TwoBoneConstraint>
{
    [SerializeField] TwoBoneConstraint leftFootConstraint;
    [SerializeField] TwoBoneConstraint rightFootConstraint;

    [Header("Advanced Setting")]
    public bool enableFootLifting = true;
    [SerializeField, Range(0, 1)] float smoothRate = 0.5f;
    [Tooltip("The power to snap the body to the ground.")]
    [SerializeField, Range(0, 1.5f)] float adaptiveBodyHeight = 1;

    [Tooltip("If disabled, the rotation of the foot will follow animation clips.")]
    public bool controlRotation = true;
    [SerializeField] float footLength = 0.2f;

    private CharacterController characterController; // Hint: CapsuleCollider is an alternative
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
        // Detect ground beneath the foot
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

        // Calculate ankle offset based on ground normal and foot rotation
        float dynamicAnkleOffset = ankleOffset;
        if (!controlRotation)
        {
            Vector3 footForward = footConstraint.TipForward;
            Vector3 groundForward = Vector3.ProjectOnPlane(footForward, groundNormal);
            Vector3 axis = Vector3.Cross(groundForward, footForward);
            
            // Calculate rotation angle relative to ground plane to adjust offset based on foot length
            float radius = Mathf.Deg2Rad * Vector3.SignedAngle(groundForward, footForward, axis);
            dynamicAnkleOffset = Mathf.Cos(radius) * ankleOffset + Mathf.Sin(radius) * footLength;
        }

        // Calculate IK target position
        float verticalOffset = (dynamicAnkleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        Vector3 forward = Vector3.zero;
        if (controlRotation)
        {
            // Calculate IK target rotation
            forward = Vector3.ProjectOnPlane(footConstraint.TipForward, groundNormal);
            Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

            // Update position and rotation
            footConstraint.target.SetPositionAndRotation(IK_position, IK_rotation);
        }
        else
        {
            // Upate position only
            footConstraint.target.position = IK_position;
        }

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, footConstraint.TipForward, forward);
#endif
    }

    /// <summary> Smooths target movement and applies IK, optionally lifting foot if current position is above target. </summary>
    void Placelimb(TwoBoneConstraint footConstraint)
    {
        if (enableFootLifting &&
            footConstraint.target.position.y < footConstraint.tip.position.y)
        {
            footConstraint.ResetTarget();
        }

        footConstraint.SmoothTarget(smoothRate);
        footConstraint.ApplyIK(controlRotation);
    }

    private float smoothHeightOffset = 0f;
    /// <summary> Adjusts the character's collider to allow the foot to touch the ground. </summary>
    void AdjustBodyHeight()
    {
        float deltaHeight = Mathf.Abs(
            leftFootConstraint.groundHeight
            - rightFootConstraint.groundHeight
        ) * adaptiveBodyHeight;

        // Smoothly transition height adjustment
        float nextSmoothHeightOffset = Mathf.Lerp(smoothHeightOffset, deltaHeight, Time.deltaTime);
        float deltaHeightOffset = smoothHeightOffset - nextSmoothHeightOffset;
        smoothHeightOffset = nextSmoothHeightOffset;

        // Apply height adjustment to collider center and character transform
        characterController.center = originalColliderCenter + new Vector3(0, smoothHeightOffset, 0);
        transform.position += new Vector3(0, deltaHeightOffset, 0);
    }
}
