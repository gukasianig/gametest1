using UnityEngine;

// Объёмное поле арены «Завод» (модель из Blender: плиты, борта, башни, скалы). Висит на корне модели в сцене.
// Включается только на теме «Завод»: прячет плоский пол и серые кубы бортов (коллайдеры остаются).
public class ArenaTerrain : MonoBehaviour
{
    public static bool Exists { get { return Object.FindObjectOfType<ArenaTerrain>(true) != null; } }

    // Вызывается из ArenaLayout.Build (factory=true для темы Завод).
    public static ArenaTerrain Apply(bool factory)
    {
        ArenaTerrain terrain = Object.FindObjectOfType<ArenaTerrain>(true);
        if (terrain == null) return null;
        terrain.gameObject.SetActive(factory);
        HideFlatFloor(factory);
        return terrain;
    }

    public static void HideFlatFloor(bool hide)
    {
        GridDrawer grid = Object.FindObjectOfType<GridDrawer>();
        if (grid == null) return;
        Transform floor = grid.transform.Find("Arena Ground");
        if (floor == null) return;
        MeshRenderer renderer = floor.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = !hide;
    }

    private void Start() { HideFlatFloor(true); }
}
