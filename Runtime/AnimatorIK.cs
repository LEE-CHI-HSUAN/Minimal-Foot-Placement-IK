using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements foot IK using Unity's built-in Animator IK system.
/// </summary>
public class AnimatorIK : BaseFootIK<AvatarIKGoal>
{
    private Animator animator;

    // Access to parameter names of the animator
    private Dictionary<AvatarIKGoal, string> IKGoal2Str = new()
    {
        { AvatarIKGoal.LeftFoot, "LeftIKWeight" },
        { AvatarIKGoal.RightFoot, "RightIKWeight" },
    };

    void Awake()
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
        // Detect ground beneath the foot
        Vector3 footPosition = animator.GetIKPosition(foot);
        FindGround(footPosition, out Vector3 groundPosition, out Vector3 groundNormal);

        // Position target on ground surface, offset by ankle distance along the normal
        Vector3 IK_position = groundPosition + ankleOffset * groundNormal;

        // Orient target to align with ground normal
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Quaternion IK_rotation = Quaternion.LookRotation(forward, groundNormal);

        // Apply IK weights based on animator parameters and set target pose
        // // Branch 1: IK always on
        // animator.SetIKPositionWeight(foot, 1);
        // animator.SetIKRotationWeight(foot, 1);
        // Branch 2: adjust IK weight by animation curve in the fbx import settings
        animator.SetIKPositionWeight(foot, animator.GetFloat(IKGoal2Str[foot]));
        animator.SetIKRotationWeight(foot, animator.GetFloat(IKGoal2Str[foot]));
        animator.SetIKPosition(foot, IK_position);
        animator.SetIKRotation(foot, IK_rotation);

#if UNITY_EDITOR
        gizmosCaches[foot].PopulateRaycast(footPosition);
        gizmosCaches[foot].PopulateHit(groundPosition, groundNormal, transform.forward, forward);
#endif
    }
}
