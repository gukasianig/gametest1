using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A local holographic base; no lines or particles between unrelated equipment groups.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WorkshopPlatform : MaskableGraphic
{
    public Color Tint = GameUi.Accent;
    public bool Active;
    private float purchaseUntil;
    public void Celebrate() { purchaseUntil = Time.unscaledTime + .8f; }
    private void Update() { SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 1.8f + rectTransform.anchoredPosition.x * .03f);
        float celebration = Mathf.Clamp01((purchaseUntil - Time.unscaledTime) / .8f);
        Vector2 center = rect.center;
        float radius = rect.width * .43f;
        Color light = Tint; light.a = (Active ? .45f : .2f) + .09f * pulse;
        WorkshopTreeLines.Dot(mesh, center, rect.width * .16f, new Color(Tint.r, Tint.g, Tint.b, .16f + celebration * .25f));
        for (int ring = 0; ring < 2; ring++)
        {
            var points = new List<Vector2>(40);
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2f / 40f;
                points.Add(center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .2f) * (1f + ring * .15f));
            }
            WorkshopTreeLines.Strip(mesh, points, ring == 0 ? 1.8f : 1f, 1.4f, light, true);
        }
        if (celebration > 0f)
        {
            var wave = new List<Vector2>(40);
            float spread = 1f + (1f - celebration) * .8f;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2f / 40f;
                wave.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .22f) * radius * spread);
            }
            light.a = celebration * .8f;
            WorkshopTreeLines.Strip(mesh, wave, 2.5f, 1.4f, light, true);
        }
    }
}
