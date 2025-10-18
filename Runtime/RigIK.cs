using UnityEngine;
using UnityEngine.Animations.Rigging;

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
        // ground detection using SphereCast
        Vector3 footPosition = footConstraint.data.tip.position;
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // calculate position
        float verticalOffset = (ankleOffset - sphereRadius) / groundNormal.y;
        Vector3 SphereCenter = groundPosition + sphereRadius * groundNormal;
        Vector3 IK_position = SphereCenter + new Vector3(0, verticalOffset, 0);

        // calculate rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // set the IK target
        // footConstraint.weight = animator.GetFloat("your parameter");
        footConstraint.data.target.SetPositionAndRotation(IK_position, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[footConstraint].PopulateRaycast(footPosition);
        gizmosCaches[footConstraint].PopulateHit(groundPosition, groundNormal, transform.forward, forward);
#endif
    }
}
