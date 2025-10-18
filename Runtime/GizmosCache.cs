#if UNITY_EDITOR

using UnityEngine;

public class GizmosCache
{
    public Vector3 footPosition;
    public Vector3 groundPosition, groundNormal, footForward, forwardProjection;
    public bool hit = false;

    public void PopulateRaycast(Vector3 footPosition)
    {
        this.footPosition = footPosition;
        hit = false;
    }

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