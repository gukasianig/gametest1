using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draw only the perimeter: UI.Outline duplicates a translucent panel's entire fill and muddies it.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class WorkshopCardBorder : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float radius = Mathf.Min(12f, Mathf.Min(rect.width, rect.height) * .15f);
        var points = new List<Vector2>(36);
        Vector2[] centers = {
            new Vector2(rect.xMax - radius - 1f, rect.yMax - radius - 1f),
            new Vector2(rect.xMin + radius + 1f, rect.yMax - radius - 1f),
            new Vector2(rect.xMin + radius + 1f, rect.yMin + radius + 1f),
            new Vector2(rect.xMax - radius - 1f, rect.yMin + radius + 1f)
        };
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step <= 8; step++)
            {
                float angle = (corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
                points.Add(centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        WorkshopTreeLines.Strip(mesh, points, 1.4f, .8f, color, true);
    }
}
