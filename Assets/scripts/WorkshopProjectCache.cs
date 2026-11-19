using UnityEngine;

public sealed class WorkshopProjectCache : MonoBehaviour
{
    private bool collected;
    private Transform visual;
    public static WorkshopProjectCache Spawn(Vector3 position)
    {
        position.y = GroundHeight.At(position);
        var root = new GameObject("Workshop scanner cache");
        root.transform.position = position;
        var cache = root.AddComponent<WorkshopProjectCache>();
        // Reuse the existing chest model; primitive is only its usual renderer fallback/fit bounds.
        var body = EnemyFx.Prim(PrimitiveType.Cube, position + Vector3.up * .45f, new Color(.08f, .55f, .65f));
        body.name = "Scanner chest visual";
        body.transform.SetParent(root.transform, true);
        body.transform.localScale = new Vector3(.9f, .65f, .65f);
        foreach (Collider c in body.GetComponents<Collider>()) Destroy(c);
        ModelSkins.ApplyEnemy(body, "Small chest");
        cache.visual = body.transform;
        var glow = new GameObject("Scanner glow", typeof(Light)).GetComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = Vector3.up * 1.2f;
        glow.color = GameUi.Accent;
        glow.range = 6f;
        glow.intensity = 2f;
        return cache;
    }

    public bool Collect(PlayerProgression hero)
    {
        if (collected || hero == null || hero.Choosing || hero.GetComponent<PlayerHealth>().IsDead || GameSession.TestMode || GameSession.Instance == null || GameSession.Instance.State != GameRunState.Playing) return false;
        collected = true;
        WorkshopProjects.CollectCache(hero);
        GameSession.Instance?.Announce(GameText.Source("projects.signal.collected") + WorkshopProjects.Option("scanner"), 3f);
        GameAudio.Play(Sfx.Chest, .6f);
        Destroy(gameObject);
        return true;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || GameSession.Instance == null || GameSession.Instance.State != GameRunState.Playing) return;
        if (visual != null) visual.Rotate(Vector3.up, 18f * Time.deltaTime, Space.World);
        Transform nearest = CoopMode.NearestAlive(transform.position);
        if (nearest == null && PlayerProgression.Instance != null) nearest = PlayerProgression.Instance.transform;
        if (nearest != null && WeaponTargets.Flat(transform.position, nearest.position) <= 2f) Collect(nearest.GetComponent<PlayerProgression>());
    }
}
