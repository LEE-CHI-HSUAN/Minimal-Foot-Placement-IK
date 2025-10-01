using UnityEngine;
using System;
using Scratch;

namespace Scratch
{
    [Serializable]
    public class TwoBoneConstraint
    {
        public Transform root;
        public Transform mid;
        public Transform tip;
        public Transform target;
        public Transform hint;

        private Quaternion rotationOffset;

        public void Init(Quaternion bodyRotation)
        {
            // used to fix the miss alignment of tip and body rotation
            rotationOffset = Quaternion.RotateTowards(bodyRotation, tip.rotation, Mathf.Infinity);
        }

        // try to calculate the inner angle of the root using law of cosines
        public float GetThighAngle()
        {
            // variable definition
            float c = Vector3.Distance(root.position, mid.position); // upper limb
            float a = Vector3.Distance(mid.position, tip.position); // lower limb
            float b = Vector3.Distance(root.position, target.position); // root to target
            if (a + c <= b) // target is unreachable
            {
                return Mathf.NegativeInfinity;
            }

            // law of cosines
            float angle_A = Mathf.Acos((b * b + c * c - a * a) / (2 * b * c)) * Mathf.Rad2Deg;
            return angle_A;
        }

        // move the limb to the IK target
        public void ApplyIK()
        {
            // try to calculate the inner angle
            float thighAngle = GetThighAngle();
            if (thighAngle == Mathf.NegativeInfinity) // target is unreachable
            {
                return;
            }

            // calculate the normal vector of the plane spanned by hint, target, root
            Vector3 hintDirection = (hint.position - root.position).normalized;
            Vector3 targetDirection = (target.position - root.position).normalized;
            Vector3 rotationAxis = Vector3.Cross(targetDirection, hintDirection);

            // rotate the upper limb
            Vector3 currentThighDirection = (mid.position - root.position).normalized;
            Vector3 newThighDirection = Quaternion.AngleAxis(thighAngle, rotationAxis) * targetDirection;
            root.rotation = Quaternion.FromToRotation(currentThighDirection, newThighDirection) * root.rotation;

            // rotate the lower limb
            Vector3 currentCalfDirection = (tip.position - mid.position).normalized;
            Vector3 newCalfDirection = (target.position - mid.position).normalized;
            mid.rotation = Quaternion.FromToRotation(currentCalfDirection, newCalfDirection) * mid.rotation;

            // rotate the tip corrected by the offset
            tip.rotation = target.rotation * rotationOffset;
        }
    }
}

public class ScratchIK : BaseFootIK<TwoBoneConstraint>
{
    [SerializeField] TwoBoneConstraint leftFootConstraint;
    [SerializeField] TwoBoneConstraint rightFootConstraint;

    void Start()
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
        // ground detection using SphereCast
        Vector3 footPosition = footConstraint.tip.position;
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // calculate position
        float verticalOffset = (ankleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // calculate rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // set the IK target
        footConstraint.target.SetPositionAndRotation(IK_position, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateRaycast(footPosition);
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, forward);
#endif
    }
}
