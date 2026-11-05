using UnityEngine;

public static class OffscreenSpawn
{
    // Reject the whole enemy bounds, not just its centre. A failed search skips spawning.
    public static bool TryFindPosition(Camera camera, Transform player, GameObject prefab,
        float groundHeight, out Vector3 position)
    {
        position = Vector3.zero;
        if (camera == null || player == null || prefab == null) return false;
        float clearance = 3f;
        float bottom = float.PositiveInfinity;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
        {
            clearance = Mathf.Max(clearance, renderer.bounds.extents.magnitude +
                Vector3.Distance(renderer.bounds.center, prefab.transform.position));
            bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        }
        float height = groundHeight + (float.IsPositiveInfinity(bottom) ? 0.5f : prefab.transform.position.y - bottom);

        return TryFindPosition(camera, player, clearance, height, out position);
    }

    public static bool TryFindPosition(Camera camera, Transform player, float clearance,
        float height, out Vector3 position)
    {
        position = Vector3.zero;
        if (camera == null || player == null) return false;

        Plane ground = new Plane(Vector3.up, new Vector3(0f, height, 0f));
        Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float margin = 0.12f + (attempt / 8) * 0.15f;
            float alongEdge = Random.Range(0f, 1f);
            Vector3 viewport;
            switch (Random.Range(0, 4))
            {
                case 0: viewport = new Vector3(-margin, alongEdge, 0f); break;
                case 1: viewport = new Vector3(1f + margin, alongEdge, 0f); break;
                case 2: viewport = new Vector3(alongEdge, -margin, 0f); break;
                default: viewport = new Vector3(alongEdge, 1f + margin, 0f); break;
            }
            Ray ray = camera.ViewportPointToRay(viewport);
            if (!ground.Raycast(ray, out float distance)) continue;
            Vector3 candidate = ray.GetPoint(distance);
            if (ArenaLayout.Instance != null && !ArenaLayout.Instance.IsWalkable(candidate, clearance)) continue;
            Vector3 fromPlayer = candidate - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 100f) continue;
            Bounds bounds = new Bounds(candidate, Vector3.one * clearance * 2f);
            if (GeometryUtility.TestPlanesAABB(frustum, bounds)) continue;
            position = candidate;
            return true;
        }
        return false;
    }
}
