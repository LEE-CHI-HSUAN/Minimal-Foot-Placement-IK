#if UNITY_EDITOR

using UnityEngine;

/// <summary>
/// Data container for caching gizmo visualization information in the Unity Editor.
/// </summary>
public class GizmosCache
{
    public Vector3 footPosition;
    public Vector3 groundPosition, groundNormal, footForward, forwardProjection;
    public bool hit = false;

    /// <summary> Initializes the cache for a new raycast operation. </summary>
    public void PopulateRaycast(Vector3 footPosition)
    {
        this.footPosition = footPosition;
        hit = false;
    }

    /// <summary> Populates the cache with ground hit information. </summary>
    public void PopulateHit(Vector3 position, Vector3 normal, Vector3 footForward, Vector3 forwardProjection)
    {
        hit = true;
        groundPosition = position;
        groundNormal = normal;
        this.footForward = footForward;
        this.forwardProjection = forwardProjection;
    }
}

#endif