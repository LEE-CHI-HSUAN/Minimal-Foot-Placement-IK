using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Implements foot IK using the Animation Rigging package's TwoBoneIKConstraint.
/// </summary>
public class RigIK : BaseFootIK<TwoBoneIKConstraint>
{
    [SerializeField] TwoBoneIKConstraint leftFootConstraint;
    [SerializeField] TwoBoneIKConstraint rightFootConstraint;

#if UNITY_EDITOR
    void Awake()
    {
        gizmosCaches.Add(leftFootConstraint, new GizmosCache());
        gizmosCaches.Add(rightFootConstraint, new GizmosCache());
    }
#endif

    void Update()
    {
        ResolveIKTarget(leftFootConstraint);
        ResolveIKTarget(rightFootConstraint);
    }

    override protected void ResolveIKTarget(TwoBoneIKConstraint footConstraint)
    {
        // Detect ground beneath the foot
        Vector3 footPosition = footConstraint.data.tip.position;
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // Calculate IK target position
        float verticalOffset = (ankleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // Calculate IK target rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // Set the IK target transform
        footConstraint.data.target.SetPositionAndRotation(IK_position, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateRaycast(footPosition);
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, transform.forward, forward);
#endif
    }
}
