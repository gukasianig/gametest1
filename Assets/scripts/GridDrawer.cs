using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridDrawer : MonoBehaviour
{
    public int gridSize = 20;      // размер сетки
    public float cellSize = 1f;    // размер клетки

    [Min(40f)] public float groundSize = 400f;
    private Material groundMaterial;
    private Texture2D groundTexture;

    void Awake()
    {
        // The old Gizmos grid is editor-only; this is a real rendered floor.
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("Arena ground requires the URP/Lit shader.", this);
            return;
        }
        groundTexture = new Texture2D(512, 512, TextureFormat.RGB24, true);
        groundTexture.name = "Arena floor grid";
        groundTexture.wrapMode = TextureWrapMode.Repeat;
        groundTexture.filterMode = FilterMode.Trilinear;
        groundTexture.anisoLevel = 8;
        Color[] pixels = new Color[512 * 512];
        for (int y = 0; y < 512; y++)
        {
            for (int x = 0; x < 512; x++)
            {
                int tileX = x * 5 / 512;
                int tileY = y * 5 / 512;
                float grain = ((x * 17 + y * 31) % 23 - 11) * 0.001f;
                float variation = ((tileX + tileY) % 2) * 0.018f;
                Color color = new Color(0.23f + grain + variation, 0.28f + grain + variation, 0.26f + grain + variation);
                bool seam = x * 5 % 512 < 5 || y * 5 % 512 < 5;
                bool major = x < 3 || y < 3;
                bool marker = (Mathf.Abs(x - 256) < 2 && Mathf.Abs(y - 256) < 12) ||
                    (Mathf.Abs(y - 256) < 2 && Mathf.Abs(x - 256) < 12);
                if (seam) color *= 0.78f;
                if (major) color = new Color(0.34f, 0.39f, 0.35f);
                if (marker) color = new Color(0.55f, 0.52f, 0.36f);
                pixels[y * 512 + x] = color;
            }
        }
        groundTexture.SetPixels(pixels);
        groundTexture.Apply(true, true);
        groundMaterial = new Material(shader);
        groundMaterial.name = "Arena ground";
        groundMaterial.SetTexture("_BaseMap", groundTexture);
        groundMaterial.SetColor("_BaseColor", Color.white);
        groundMaterial.SetFloat("_Smoothness", 0.05f);
        float size = Mathf.Max(40f, groundSize);
        groundMaterial.SetTextureScale("_BaseMap", Vector2.one * (size / 10f));

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Arena Ground";
        floor.transform.SetParent(transform, false);
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        floor.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    void OnDestroy()
    {
        if (groundMaterial != null) Destroy(groundMaterial);
        if (groundTexture != null) Destroy(groundTexture);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.gray;

        for (int x = -gridSize; x <= gridSize; x++)
        {
            Vector3 start = new Vector3(x * cellSize, 0, -gridSize * cellSize);
            Vector3 end = new Vector3(x * cellSize, 0, gridSize * cellSize);

            Gizmos.DrawLine(start, end);
        }

        for (int z = -gridSize; z <= gridSize; z++)
        {
            Vector3 start = new Vector3(-gridSize * cellSize, 0, z * cellSize);
            Vector3 end = new Vector3(gridSize * cellSize, 0, z * cellSize);

            Gizmos.DrawLine(start, end);
        }
    }
}
