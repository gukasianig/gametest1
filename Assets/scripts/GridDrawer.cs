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
        groundTexture = ArenaThemes.MakeFloor(ArenaThemes.Current);
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
        // Плоский MeshCollider тонкий: быстрые физические объекты могли проваливаться. Ставим толстый «пол» (верх на y=0).
        MeshCollider thin = floor.GetComponent<MeshCollider>();
        if (thin != null) Destroy(thin);
        BoxCollider solidFloor = floor.AddComponent<BoxCollider>();
        solidFloor.size = new Vector3(10f, 4f, 10f);
        solidFloor.center = new Vector3(0f, -2f, 0f);
        ArenaThemes.ApplyEnvironment(ArenaThemes.Current);
        if (ArenaThemes.Current == ArenaTheme.Factory && ArenaTerrain.Exists) floor.GetComponent<MeshRenderer>().enabled = false;
        if (ArenaThemes.Current == ArenaTheme.Glacier) groundMaterial.SetFloat("_Smoothness", 0.45f);
    }

    // Смена темы: новая текстура пола, материал остаётся тем же.
    public void Apply(ArenaTheme theme)
    {
        if (groundMaterial == null) return;
        Texture2D old = groundTexture;
        groundTexture = ArenaThemes.MakeFloor(theme);
        groundMaterial.SetTexture("_BaseMap", groundTexture);
        groundMaterial.SetFloat("_Smoothness", theme == ArenaTheme.Glacier ? 0.45f : 0.05f);
        if (old != null) Destroy(old);
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
