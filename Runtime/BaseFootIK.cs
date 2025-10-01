using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

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

    // calculate and set the IK target for a limb
    abstract protected void ResolveIKTarget(T foot);

    // check if there is ground beneath a position
    protected bool FindGround(Vector3 footPosition, out Vector3 point, out Vector3 normal)
    {
        Vector3 rayStart = footPosition + rayOffset * Vector3.up;
        if (Physics.SphereCast(rayStart, sphereRadius, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        // default values if nothing is hit
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

            Vector3 sphereCenter = cache.groundPosition + sphereRadius * cache.groundNormal;
            Gizmos.DrawWireSphere(sphereCenter, sphereRadius);

            // forward vector projection
            Handles.color = Color.deepSkyBlue;
            Handles.DrawLine(cache.groundPosition, cache.groundPosition + cache.forward / 3, 0.2f);
            Handles.color = Color.blue;
            Handles.DrawLine(cache.groundPosition, cache.groundPosition + transform.forward / 3, 0.2f);
            Handles.DrawDottedLine(cache.groundPosition + transform.forward / 3, cache.groundPosition + cache.forward / 3, 5f);

            // tangent plane
            Handles.color = Color.yellow;
            Handles.DrawLine(cache.groundPosition, cache.groundPosition + cache.groundNormal / 3, 0.2f);
            Handles.DrawWireDisc(cache.groundPosition, cache.groundNormal, 0.3f, 0.2f);
        }
    }
#endif
}
