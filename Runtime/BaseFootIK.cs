using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Abstract base class for foot IK systems. Provides ground detection via SphereCast
/// and editor visualization tools for IK targets and raycasts.
/// </summary>
/// <typeparam name="T">The type of constraint used for the IK target.</typeparam>
public abstract class BaseFootIK<T> : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField, Tooltip("Determine the start of SphereCast.")]
    protected float rayOffset = 0.5f;
    [SerializeField]
    protected float sphereRadius = 0.07f;
    [SerializeField]
    protected float rayDistance = 1f;
    [SerializeField] LayerMask groundLayer = Physics.AllLayers;
    [SerializeField, Tooltip("The distance between the ankle and the sole of foot.")]
    protected float ankleOffset = 0.1f;

    /// <summary>
    /// Calculates and applies the IK target configuration for the specified limb.
    /// </summary>
    /// <param name="foot">The IK constraint representing the foot/limb.</param>
    abstract protected void ResolveIKTarget(T foot);

    /// <summary>
    /// Detects ground beneath the given position using a SphereCast.
    /// </summary>
    /// <param name="footPosition">The current position of the foot.</param>
    /// <param name="point">The world position of the ground hit point.</param>
    /// <param name="normal">The surface normal of the ground hit point.</param>
    /// <returns>True if ground is detected, otherwise false.</returns>
    protected bool FindGround(Vector3 footPosition, out Vector3 point, out Vector3 normal)
    {
        Vector3 rayStart = footPosition + rayOffset * Vector3.up;
        if (Physics.SphereCast(rayStart, sphereRadius, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        // Return default values if nothing is hit to prevent invalid IK calculations
        point = Vector3.negativeInfinity;
        normal = Vector3.negativeInfinity;
        return false;
    }

#if UNITY_EDITOR
    protected Dictionary<T, GizmosCache> gizmosCaches = new();

    void OnDrawGizmos()
    {
        foreach (GizmosCache cache in gizmosCaches.Values)
        {
            // SphereCase
            Gizmos.color = Color.green;
            Vector3 rayStart = cache.footPosition + rayOffset * Vector3.up;
            Vector3 rayEnd = rayStart + rayDistance * Vector3.down;
            Gizmos.DrawWireSphere(rayStart, sphereRadius);
            Gizmos.DrawLine(rayStart, rayEnd);

            // RaycastHit
            if (!cache.hit)
            {
                return;
            }

            // hit position
            Vector3 sphereCenter = cache.groundPosition + sphereRadius * cache.groundNormal;
            Gizmos.DrawWireSphere(sphereCenter, sphereRadius);

            // tangent plane
            Handles.color = Color.yellow;
            Handles.DrawLine(cache.groundPosition, cache.groundPosition + cache.groundNormal / 3, 0.2f);
            Handles.DrawWireDisc(cache.groundPosition, cache.groundNormal, 0.3f, 0.2f);

            // forward vector projection
            Vector3 projectionEnd = cache.groundPosition + cache.forwardProjection / 3;
            Vector3 footTip = cache.groundPosition + cache.footForward / 3;
            Handles.color = Color.red;
            Handles.DrawLine(cache.groundPosition, projectionEnd, 0.2f);
            Handles.color = Color.blue;
            Handles.DrawLine(cache.groundPosition, footTip, 0.2f);
            Handles.DrawDottedLine(footTip, projectionEnd, 5f);
        }
    }
#endif
}
