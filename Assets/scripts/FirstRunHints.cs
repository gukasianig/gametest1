using UnityEngine;
using UnityEngine.UI;

// Quiet control hints for the very first run (B07, 21.11.2026). No pause, no clicks blocked:
// a small plate low on the screen follows the run clock, so level-up and pause screens freeze it.
public class FirstRunHints : MonoBehaviour
{
    private struct Hint
    {
        public float start, end;
        public string id;
        public bool dashHint;
    }

    private static readonly Hint[] Hints =
    {
        new Hint { start = 0f, end = 6f, id = "hints.move" },
        new Hint { start = 10f, end = 16f, id = "hints.dash", dashHint = true },
        new Hint { start = 20f, end = 26f, id = "hints.goals" },
    };

    private const float Fade = 0.35f;

    private GameSession session;
    private PlayMovement player;
    private CanvasGroup group;
    private Text label;
    private int shown = -1;
    private bool dashed;

    // Called from GameSession.Begin; only an ordinary run of a player who has never finished one.
    public static void TryShow(GameSession session)
    {
        if (session == null || GameSession.TestMode || MetaProgress.Data.runs > 0) return;
        FirstRunHints hints = session.GetComponent<FirstRunHints>();
        if (hints == null) hints = session.gameObject.AddComponent<FirstRunHints>();
        hints.session = session;
    }

    private void Start()
    {
        if (session == null) session = GetComponent<GameSession>();
        Canvas canvas = GameUi.Canvas("First Run Hints", 76);
        canvas.transform.SetParent(transform, false);
        Destroy(canvas.GetComponent<GraphicRaycaster>());
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;
        RectTransform plate = GameUi.Box(canvas.transform, "Hint Plate", new Vector2(0.27f, 0.205f), new Vector2(0.73f, 0.255f),
            new Color(0.02f, 0.03f, 0.045f, 0.72f));
        GameUi.Box(plate, "Accent", new Vector2(0f, 0f), new Vector2(1f, 0.06f),
            new Color(GameUi.Accent.r, GameUi.Accent.g, GameUi.Accent.b, 0.7f));
        label = GameUi.Readable(GameUi.Label(plate, "", Vector2.zero, Vector2.one, 18, new Color(0.92f, 0.95f, 0.97f)), 1f);
    }

    private void Update()
    {
        if (session == null || label == null) return;
        if (session.IsFinished || session.Elapsed > Hints[Hints.Length - 1].end + Fade)
        {
            Destroy(group.gameObject);
            Destroy(this);
            return;
        }

        if (player == null) player = FindObjectOfType<PlayMovement>();
        if (player != null && player.Dashing) dashed = true;

        float t = session.Elapsed;
        int index = -1;
        for (int i = 0; i < Hints.Length; i++)
            if (t >= Hints[i].start && t < Hints[i].end && !(Hints[i].dashHint && dashed)) { index = i; break; }

        if (index >= 0 && index != shown)
        {
            shown = index;
            label.text = GameText.Source(Hints[index].id);
        }

        float target = 0f;
        PlayerProgression progression = PlayerProgression.Instance;
        bool toast = progression != null && progression.ToastVisible;
        if (index >= 0 && !toast && session.State == GameRunState.Playing)
        {
            Hint hint = Hints[index];
            target = Mathf.Clamp01(Mathf.Min((t - hint.start) / Fade, (hint.end - t) / Fade));
        }
        // Unscaled: an early dash hides the plate quickly, a level-up screen hides it at once.
        group.alpha = session.State == GameRunState.Playing
            ? (toast ? 0f : Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / Fade)) : 0f;
    }
}
