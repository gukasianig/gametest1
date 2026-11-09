using UnityEngine;
using UnityEngine.UI;

// Resolution-independent UI symbols. No texture files or external shader required.
[RequireComponent(typeof(CanvasRenderer))]
public class UpgradeIcon : MaskableGraphic
{
    protected UpgradeIcon() { useLegacyMeshGeneration = false; }
    [SerializeField] private UpgradeEffect effect;
    public UpgradeEffect Effect
    {
        get => effect;
        set { effect = value; SetVerticesDirty(); }
    }
    private VertexHelper vertices;
    private Vector2 centre;
    private float scale;

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        vertices = helper;
        Rect rect = rectTransform.rect;
        centre = rect.center;
        scale = Mathf.Min(rect.width, rect.height) * 0.46f;
        switch (effect)
        {
            case UpgradeEffect.Damage:
                Fill(new Vector2(-0.27f, -0.62f), new Vector2(0.27f, -0.62f), new Vector2(0.27f, 0.27f),
                    new Vector2(0, 0.86f), new Vector2(-0.27f, 0.27f));
                Line(new Vector2(-0.4f, -0.75f), new Vector2(0.4f, -0.75f), 0.12f);
                Line(new Vector2(-0.17f, 0.22f), new Vector2(0.17f, 0.22f), 0.08f, new Color(0.03f, 0.08f, 0.1f));
                break;
            case UpgradeEffect.AttackSpeed:
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.75f + i * 0.5f;
                    Line(new Vector2(x, 0.6f), new Vector2(x + 0.4f, 0), 0.15f);
                    Line(new Vector2(x + 0.4f, 0), new Vector2(x, -0.6f), 0.15f);
                }
                break;
            case UpgradeEffect.Movement:
                Fill(new Vector2(-0.45f, 0.75f), new Vector2(0.16f, 0.75f), new Vector2(0.1f, -0.1f),
                    new Vector2(0.72f, -0.4f), new Vector2(0.72f, -0.68f), new Vector2(-0.48f, -0.68f));
                Line(new Vector2(-0.9f, 0.2f), new Vector2(-0.55f, 0.2f), 0.1f);
                Line(new Vector2(-0.9f, -0.1f), new Vector2(-0.6f, -0.1f), 0.1f);
                break;
            case UpgradeEffect.Pickup:
                Arc(Vector2.zero, 0.53f, 180, 360, 0.17f);
                Line(new Vector2(-0.53f, 0), new Vector2(-0.53f, 0.62f), 0.17f);
                Line(new Vector2(0.53f, 0), new Vector2(0.53f, 0.62f), 0.17f);
                Line(new Vector2(-0.68f, 0.75f), new Vector2(-0.38f, 0.75f), 0.12f);
                Line(new Vector2(0.38f, 0.75f), new Vector2(0.68f, 0.75f), 0.12f);
                Fill(new Vector2(0, 0.75f), new Vector2(0.13f, 0.53f), new Vector2(0, 0.3f), new Vector2(-0.13f, 0.53f));
                break;
            case UpgradeEffect.GrenadeDamage:
                Arc(new Vector2(0, -0.2f), 0.49f, 0, 360, 0.15f);
                Line(new Vector2(-0.23f, 0.38f), new Vector2(0.23f, 0.38f), 0.19f);
                Line(new Vector2(0, 0.42f), new Vector2(0.34f, 0.76f), 0.12f);
                Line(new Vector2(0.34f, 0.76f), new Vector2(0.63f, 0.6f), 0.12f);
                Line(new Vector2(0, -0.48f), new Vector2(0, 0.05f), 0.1f);
                break;
            case UpgradeEffect.GrenadeRadius:
                for (int i = 0; i < 8; i++)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4));
                    Line(direction * 0.52f, direction * 0.85f, 0.11f);
                }
                Arc(Vector2.zero, 0.31f, 0, 360, 0.13f);
                break;
            case UpgradeEffect.MaxHealth:
                Rect(-0.2f, -0.8f, 0.2f, 0.8f);
                Rect(-0.8f, -0.2f, 0.8f, 0.2f);
                break;
            case UpgradeEffect.OrbitDamage:
                Arc(Vector2.zero, 0.7f, 0, 360, 0.07f);
                Fill(new Vector2(0, 0.55f), new Vector2(0.15f, 0.15f), new Vector2(0.55f, 0),
                    new Vector2(0.15f, -0.15f), new Vector2(0, -0.55f), new Vector2(-0.15f, -0.15f),
                    new Vector2(-0.55f, 0), new Vector2(-0.15f, 0.15f));
                Dot(new Vector2(0.52f, 0.48f), 0.17f);
                break;
            case UpgradeEffect.CritChance:
                Arc(Vector2.zero, 0.55f, 0, 360, 0.07f);
                Line(new Vector2(-0.95f, 0f), new Vector2(-0.3f, 0f), 0.08f);
                Line(new Vector2(0.3f, 0f), new Vector2(0.95f, 0f), 0.08f);
                Line(new Vector2(0f, -0.95f), new Vector2(0f, -0.3f), 0.08f);
                Line(new Vector2(0f, 0.3f), new Vector2(0f, 0.95f), 0.08f);
                Dot(Vector2.zero, 0.13f);
                break;
            case UpgradeEffect.CritPower:
                Fill(new Vector2(0.15f, 0.9f), new Vector2(-0.4f, -0.1f), new Vector2(0.12f, -0.1f));
                Fill(new Vector2(-0.12f, 0.1f), new Vector2(0.4f, 0.1f), new Vector2(-0.15f, -0.9f));
                break;
            case UpgradeEffect.Ricochet:
                Line(new Vector2(-0.9f, -0.6f), new Vector2(-0.2f, 0.55f), 0.1f);
                Line(new Vector2(-0.2f, 0.55f), new Vector2(0.3f, -0.45f), 0.1f);
                Line(new Vector2(0.3f, -0.45f), new Vector2(0.85f, 0.55f), 0.1f);
                Dot(new Vector2(-0.2f, 0.55f), 0.14f);
                Dot(new Vector2(0.3f, -0.45f), 0.14f);
                break;
            case UpgradeEffect.DroneCount:
            case UpgradeEffect.DroneDamage:
                Dot(new Vector2(-0.55f, 0.35f), 0.22f);
                Dot(new Vector2(0.55f, 0.35f), 0.22f);
                Line(new Vector2(-0.8f, 0.6f), new Vector2(-0.3f, 0.6f), 0.07f);
                Line(new Vector2(0.3f, 0.6f), new Vector2(0.8f, 0.6f), 0.07f);
                if (effect == UpgradeEffect.DroneCount) Dot(new Vector2(0f, -0.4f), 0.22f);
                else Fill(new Vector2(0f, 0.1f), new Vector2(-0.45f, -0.9f), new Vector2(0.45f, -0.9f));
                break;
            case UpgradeEffect.TurretCount:
            case UpgradeEffect.TurretDamage:
                Rect(-0.55f, -0.8f, 0.55f, -0.35f);
                Dot(new Vector2(0f, -0.1f), 0.3f);
                Line(new Vector2(0f, -0.1f), new Vector2(0.8f, 0.55f), 0.2f);
                if (effect == UpgradeEffect.TurretDamage) Fill(new Vector2(-0.75f, 0.5f), new Vector2(-0.3f, 0.25f), new Vector2(-0.4f, 0.8f));
                else Dot(new Vector2(-0.65f, 0.55f), 0.2f);
                break;
            case UpgradeEffect.MineCount:
                Rect(-0.7f, -0.55f, 0.7f, -0.3f);
                Dot(new Vector2(0f, -0.2f), 0.55f);
                Line(new Vector2(0f, 0.2f), new Vector2(0f, 0.8f), 0.14f);
                Line(new Vector2(-0.5f, 0.1f), new Vector2(-0.8f, 0.45f), 0.14f);
                Line(new Vector2(0.5f, 0.1f), new Vector2(0.8f, 0.45f), 0.14f);
                break;
            case UpgradeEffect.Armor:
                Fill(new Vector2(-0.6f, 0.75f), new Vector2(0.6f, 0.75f), new Vector2(0.6f, -0.05f), new Vector2(0f, -0.9f), new Vector2(-0.6f, -0.05f));
                Line(new Vector2(0f, 0.5f), new Vector2(0f, -0.45f), 0.08f, new Color(0.03f, 0.08f, 0.1f));
                break;
            case UpgradeEffect.Regeneration:
                Rect(-0.17f, -0.75f, 0.17f, 0.75f);
                Rect(-0.75f, -0.17f, 0.75f, 0.17f);
                break;
            case UpgradeEffect.Vampirism:
                Dot(new Vector2(0f, -0.25f), 0.5f);
                Fill(new Vector2(-0.46f, -0.05f), new Vector2(0.46f, -0.05f), new Vector2(0f, 0.9f));
                break;
            case UpgradeEffect.DashCooldown:
                Arc(Vector2.zero, 0.65f, 0, 360, 0.08f);
                Line(Vector2.zero, new Vector2(0f, 0.45f), 0.08f);
                Line(Vector2.zero, new Vector2(0.3f, -0.2f), 0.08f);
                break;
            case UpgradeEffect.DashDistance:
                Line(new Vector2(-0.9f, 0f), new Vector2(0.2f, 0f), 0.1f);
                Fill(new Vector2(0.1f, 0.5f), new Vector2(0.7f, 0f), new Vector2(0.1f, -0.5f));
                Fill(new Vector2(0.5f, 0.5f), new Vector2(1.0f, 0f), new Vector2(0.5f, -0.5f));
                break;
            case UpgradeEffect.ExtraPellets:
                Dot(new Vector2(-0.55f, 0.5f), 0.16f);
                Dot(new Vector2(0f, 0.6f), 0.16f);
                Dot(new Vector2(0.55f, 0.5f), 0.16f);
                Line(new Vector2(0f, -0.8f), new Vector2(-0.45f, 0.3f), 0.06f);
                Line(new Vector2(0f, -0.8f), new Vector2(0f, 0.4f), 0.06f);
                Line(new Vector2(0f, -0.8f), new Vector2(0.45f, 0.3f), 0.06f);
                break;
            case UpgradeEffect.ExplosiveBullets:
                Dot(Vector2.zero, 0.3f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Line(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f, 0.1f);
                }
                break;
            case UpgradeEffect.EliteDamage:
                Rect(-0.7f, -0.7f, 0.7f, -0.25f);
                Fill(new Vector2(-0.7f, -0.25f), new Vector2(-0.25f, -0.25f), new Vector2(-0.7f, 0.5f));
                Fill(new Vector2(-0.3f, -0.25f), new Vector2(0.3f, -0.25f), new Vector2(0f, 0.8f));
                Fill(new Vector2(0.25f, -0.25f), new Vector2(0.7f, -0.25f), new Vector2(0.7f, 0.5f));
                break;
            case UpgradeEffect.WeaponRange:
                Arc(Vector2.zero, 0.35f, 0, 360, 0.07f);
                Arc(Vector2.zero, 0.7f, 0, 360, 0.07f);
                Dot(Vector2.zero, 0.12f);
                break;
            case UpgradeEffect.OrbitSpeed:
                Arc(Vector2.zero, 0.65f, 0, 270, 0.09f);
                Fill(new Vector2(0.45f, 0.1f), new Vector2(0.95f, 0.1f), new Vector2(0.7f, -0.35f));
                break;
            case UpgradeEffect.OrbitCrit:
                Arc(Vector2.zero, 0.65f, 0, 360, 0.07f);
                Dot(new Vector2(0.65f, 0f), 0.16f);
                Line(new Vector2(-0.3f, 0f), new Vector2(0.3f, 0f), 0.08f);
                Line(new Vector2(0f, -0.3f), new Vector2(0f, 0.3f), 0.08f);
                break;
            case UpgradeEffect.PickupSpeed:
                Dot(new Vector2(-0.7f, 0.6f), 0.15f);
                Dot(new Vector2(0.7f, 0.6f), 0.15f);
                Dot(new Vector2(0f, -0.6f), 0.25f);
                Line(new Vector2(-0.7f, 0.6f), new Vector2(-0.1f, -0.4f), 0.06f);
                Line(new Vector2(0.7f, 0.6f), new Vector2(0.1f, -0.4f), 0.06f);
                break;
            case UpgradeEffect.Luck:
                Dot(new Vector2(-0.3f, 0.3f), 0.3f);
                Dot(new Vector2(0.3f, 0.3f), 0.3f);
                Dot(new Vector2(-0.3f, -0.3f), 0.3f);
                Dot(new Vector2(0.3f, -0.3f), 0.3f);
                Line(new Vector2(0f, -0.3f), new Vector2(0.15f, -0.95f), 0.08f);
                break;
            case UpgradeEffect.ExperienceGain:
            case UpgradeEffect.ChestXp:
                Fill(new Vector2(0f, 0.9f), new Vector2(0.55f, 0f), new Vector2(0f, -0.9f), new Vector2(-0.55f, 0f));
                break;
            case UpgradeEffect.CursedGlass:
            case UpgradeEffect.CursedHaste:
            case UpgradeEffect.CursedGreed:
                Dot(new Vector2(0f, 0.2f), 0.6f);
                Rect(-0.32f, -0.85f, 0.32f, -0.3f);
                Dot(new Vector2(-0.22f, 0.25f), 0.14f);
                Dot(new Vector2(0.22f, 0.25f), 0.14f);
                break;
            case UpgradeEffect.Pierce:
                Line(new Vector2(-0.95f, 0f), new Vector2(0.45f, 0f), 0.1f);
                Fill(new Vector2(0.45f, 0.35f), new Vector2(0.95f, 0f), new Vector2(0.45f, -0.35f));
                Line(new Vector2(-0.25f, 0.6f), new Vector2(-0.25f, -0.6f), 0.08f);
                Line(new Vector2(0.15f, 0.6f), new Vector2(0.15f, -0.6f), 0.08f);
                break;
            case UpgradeEffect.OrbitCount:
                Arc(Vector2.zero, 0.65f, 0, 360, 0.07f);
                Dot(Vector2.zero, 0.21f);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2 / 3;
                    Dot(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.65f, 0.18f);
                }
                break;
        }
    }

    private void Rect(float left, float bottom, float right, float top)
    {
        Fill(new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top));
    }

    private void Dot(Vector2 point, float radius)
    {
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2 / 24, b = (i + 1) * Mathf.PI * 2 / 24;
            Triangle(point, point + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                point + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
        }
    }

    private void Fill(params Vector2[] points)
    {
        // Icons use star-shaped contours; a centre fan preserves their concave notches.
        Vector2 middle = Vector2.zero;
        foreach (Vector2 point in points) middle += point;
        middle /= points.Length;
        for (int i = 0; i < points.Length; i++) Triangle(middle, points[i], points[(i + 1) % points.Length], color);
    }

    private void Arc(Vector2 origin, float radius, float start, float end, float width)
    {
        for (int i = 0; i < 40; i++)
        {
            float a = Mathf.Lerp(start, end, i / 40f) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(start, end, (i + 1) / 40f) * Mathf.Deg2Rad;
            Line(origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                origin + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width);
        }
    }

    private void Line(Vector2 a, Vector2 b, float width, Color? tint = null)
    {
        Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        Color c = tint ?? color;
        Triangle(a - normal, a + normal, b + normal, c);
        Triangle(a - normal, b + normal, b - normal, c);
    }

    private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int index = vertices.currentVertCount;
        vertices.AddVert(centre + a * scale, tint, Vector2.zero);
        vertices.AddVert(centre + b * scale, tint, Vector2.zero);
        vertices.AddVert(centre + c * scale, tint, Vector2.zero);
        vertices.AddTriangle(index, index + 1, index + 2);
    }
}
