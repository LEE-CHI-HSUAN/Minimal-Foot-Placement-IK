using UnityEngine;
using System;
using Scratch;

namespace Scratch
{
    /// <summary>
    /// A simple two-bone IK constraint implementation.
    /// </summary>
    [Serializable]
    public class TwoBoneConstraint
    {
        public Transform root;
        public Transform mid;
        public Transform tip;
        public Transform target;
        public Transform hint;

        private Quaternion rotationOffset;

        /// <summary> Initializes the constraint with the initial body rotation. </summary>
        public void Init(Quaternion bodyRotation)
        {
            // Store the initial rotation offset to maintain consistent alignment
            rotationOffset = Quaternion.Inverse(bodyRotation) * tip.rotation;
        }

        /// <summary> Calculates the angle of the root (thigh) using the Law of Cosines. </summary>
        public float GetThighAngle()
        {
            // Calculate lengths of triangle sides
            float c = Vector3.Distance(root.position, mid.position); // upper limb length
            float a = Vector3.Distance(mid.position, tip.position); // lower limb length
            float b = Vector3.Distance(root.position, target.position); // distance from root to target

            // If sum of limb lengths is less than distance to target, the target is unreachable
            if (a + c <= b)
            {
                return Mathf.NegativeInfinity;
            }

            // Law of Cosines: c^2 = a^2 + b^2 - 2ab*cos(C)
            // Rearranged to find angle A (inner angle of root):
            // cos(A) = (b^2 + c^2 - a^2) / (2bc)
            float angle_A = Mathf.Acos((b * b + c * c - a * a) / (2 * b * c)) * Mathf.Rad2Deg;
            return angle_A;
        }

        /// <summary> Applies IK to move the limb to the target position and rotation. </summary>
        public void ApplyIK()
        {
            float thighAngle = GetThighAngle();
            if (thighAngle == Mathf.NegativeInfinity) // target is unreachable
            {
                return;
            }

            // Calculate rotation axis perpendicular to the plane formed by root, hint, and target
            Vector3 hintDirection = (hint.position - root.position).normalized;
            Vector3 targetDirection = (target.position - root.position).normalized;
            Vector3 rotationAxis = Vector3.Cross(targetDirection, hintDirection);

            // Rotate upper limb
            Vector3 currentThighDirection = (mid.position - root.position).normalized;
            Vector3 newThighDirection = Quaternion.AngleAxis(thighAngle, rotationAxis) * targetDirection;
            root.rotation = Quaternion.FromToRotation(currentThighDirection, newThighDirection) * root.rotation;

            // Rotate lower limb
            Vector3 currentCalfDirection = (tip.position - mid.position).normalized;
            Vector3 newCalfDirection = (target.position - mid.position).normalized;
            mid.rotation = Quaternion.FromToRotation(currentCalfDirection, newCalfDirection) * mid.rotation;

            // Apply target rotation corrected by the initial offset
            tip.rotation = target.rotation * rotationOffset;
        }
    }
}

/// <summary>
/// Implements foot IK from scratch using a two-bone constraint.
/// </summary>
public class ScratchIK : BaseFootIK<TwoBoneConstraint>
{
    [SerializeField] TwoBoneConstraint leftFootConstraint;
    [SerializeField] TwoBoneConstraint rightFootConstraint;

    void Awake()
    {
        leftFootConstraint.Init(transform.rotation);
        rightFootConstraint.Init(transform.rotation);

#if UNITY_EDITOR
        gizmosCaches.Add(leftFootConstraint, new GizmosCache());
        gizmosCaches.Add(rightFootConstraint, new GizmosCache());
#endif
    }

    private bool animationHasUpdated = false; // flag
    void OnAnimatorIK(int layerIndex)
    {
        animationHasUpdated = true;
    }

    void LateUpdate()
    {
        if (animationHasUpdated)
        {
            animationHasUpdated = false;

            ResolveIKTarget(leftFootConstraint);
            ResolveIKTarget(rightFootConstraint);

            leftFootConstraint.ApplyIK();
            rightFootConstraint.ApplyIK();
        }
    }

    override protected void ResolveIKTarget(TwoBoneConstraint footConstraint)
    {
        // Detect ground beneath the foot
        Vector3 footPosition = footConstraint.tip.position;
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // Calculate IK target position
        float verticalOffset = (ankleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // Calculate IK target rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // Set the IK target transform
        footConstraint.target.SetPositionAndRotation(IK_position, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateRaycast(footPosition);
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, transform.forward, forward);
#endif
    }
}
