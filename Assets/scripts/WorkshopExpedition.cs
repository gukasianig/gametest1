using UnityEngine;
using UnityEngine.UI;

// One scanner/goal HUD per shared run; the closest living hero can collect its signal.
public sealed class WorkshopExpedition : MonoBehaviour
{
    private Canvas canvas;
    private Text label;
    private WorkshopProjectCache cache;
    private int signals;
    private float refreshAt;

    public void BeginRun()
    {
        signals = 0;
        if (cache != null) Destroy(cache.gameObject);
        if (canvas == null)
        {
            canvas = GameUi.Canvas("Workshop run objective", 73);
            var plate = GameUi.Box(canvas.transform, "Project goal", new Vector2(.015f, .82f), new Vector2(.30f, .91f), new Color(.025f, .065f, .085f, .88f));
            UiArtwork.Round(plate.GetComponent<Image>());
            label = GameUi.Label(plate, "", Vector2.zero, Vector2.one, 13, new Color(.75f, .9f, .94f));
        }
        canvas.enabled = false;
    }

    private void Update()
    {
        var session = GameSession.Instance;
        bool primary = PlayerProgression.Instance != null && PlayerProgression.Instance.gameObject == gameObject;
        bool visible = primary && session != null && !GameSession.TestMode && session.State == GameRunState.Playing;
        if (canvas == null) return;
        canvas.enabled = visible && (cache != null || WorkshopProjects.Target >= 0);
        if (!visible) return;
        if (cache == null && !session.BossWarning && session.Boss == null && signals < WorkshopProjects.Rank("scanner") && session.Elapsed >= 90f * (signals + 1))
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 at = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 12f;
            cache = WorkshopProjectCache.Spawn(at);
            signals++;
            session.Announce(GameText.Source("projects.signal.found"), 3f);
        }
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .2f;
        string text = WorkshopProjects.Target >= 0 ? WorkshopProjects.Goal : "";
        if (cache != null)
        {
            Vector3 direction = cache.transform.position - transform.position;
            Vector3 screen = Camera.main != null ? Camera.main.WorldToViewportPoint(cache.transform.position) - Camera.main.WorldToViewportPoint(transform.position) : new Vector3(direction.x, direction.z);
            string arrow = Mathf.Abs(screen.x) > Mathf.Abs(screen.y) ? screen.x > 0 ? "→" : "←" : screen.y > 0 ? "↑" : "↓";
            text += (text.Length > 0 ? "\n" : "") + GameText.Source("projects.signal.distance") + Mathf.RoundToInt(direction.magnitude) + GameText.Source("arenaevents.040") + " " + arrow;
        }
        label.text = text;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (cache != null) Destroy(cache.gameObject);
    }
}
