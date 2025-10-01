#if UNITY_EDITOR

using UnityEngine;

public class GizmosCache
{
    public Vector3 footPosition;
    public Vector3 groundPosition, groundNormal, forward;
    public bool hit = false;

    public void PopulateRaycast(Vector3 footPosition)
    {
        this.footPosition = footPosition;
        hit = false;
    }

    public void PopulateHit(Vector3 position, Vector3 normal, Vector3 forward)
    {
        hit = true;
        groundPosition = position;
        groundNormal = normal;
        this.forward = forward;
    }
}

#endif