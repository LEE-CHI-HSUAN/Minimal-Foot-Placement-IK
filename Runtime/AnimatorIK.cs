using UnityEngine;

public class AnimatorIK : BaseFootIK<AvatarIKGoal>
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();

#if UNITY_EDITOR
        gizmosCaches.Add(AvatarIKGoal.LeftFoot, new GizmosCache());
        gizmosCaches.Add(AvatarIKGoal.RightFoot, new GizmosCache());
#endif
    }

    void OnAnimatorIK(int layerIndex)
    {
        ResolveIKTarget(AvatarIKGoal.LeftFoot);
        ResolveIKTarget(AvatarIKGoal.RightFoot);
    }

    override protected void ResolveIKTarget(AvatarIKGoal foot)
    {
        // ground detection using SphereCast
        Vector3 footPosition = animator.GetIKPosition(foot);
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // calculate position
        Vector3 IK_position = groundPosition + ankleOffset * groundNormal;

        // calculate rotation
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // call APIs to set the IK target
        animator.SetIKPositionWeight(foot, 1);
        animator.SetIKRotationWeight(foot, 1);
        animator.SetIKPosition(foot, IK_position);
        animator.SetIKRotation(foot, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[foot].PopulateRaycast(footPosition);
        gizmosCaches[foot].PopulateHit(groundPosition, groundNormal, forward);
#endif
    }
}
