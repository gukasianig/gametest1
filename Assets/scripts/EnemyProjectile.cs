using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    // Вид снаряда («чем стреляют»): Ball — прежний шар (Страж), Acid — плевок кислотой (стрелок), Quill — костяная игла (снайпер).
    public enum Style { Ball, Acid, Quill }

    public Transform target;
    public Vector3 direction;
    public int damage = 12;
    public float speed = 8f;
    public Style style;
    private float age;
    private Vector3 baseScale;

    static readonly Color AcidColor = new Color(0.6f, 0.95f, 0.2f);
    static readonly Color AcidDark = new Color(0.25f, 0.55f, 0.1f);
    static readonly Color BoneColor = new Color(0.96f, 0.9f, 0.74f);
    static readonly Color BallColor = new Color(1f, 0.52f, 0.15f);

    public static EnemyProjectile Create(Vector3 position, Vector3 direction, Transform target, int damage, Style style = Style.Ball)
    {
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        Vector3 start = new Vector3(position.x, 0.9f, position.z);
        GameObject root;
        switch (style)
        {
            case Style.Acid:
            {
                // Вытянутый сгусток с тёмным ядром и зелёным следом.
                root = EnemyFx.Prim(PrimitiveType.Sphere, start, AcidColor);
                root.transform.rotation = Quaternion.LookRotation(direction);
                root.transform.localScale = new Vector3(0.34f, 0.3f, 0.52f);
                GameObject core = EnemyFx.Prim(PrimitiveType.Sphere, start, AcidDark);
                core.transform.SetParent(root.transform, false);
                core.transform.localPosition = new Vector3(0f, 0f, 0.22f);
                core.transform.localScale = Vector3.one * 0.55f;
                Trail(root, AcidColor, 0.28f, 0.22f);
                break;
            }
            case Style.Quill:
            {
                // Игла из кости: тонкий цилиндр вдоль полёта, бледный след.
                root = new GameObject("Hostile shot");
                root.transform.position = start;
                root.transform.rotation = Quaternion.LookRotation(direction);
                GameObject shaft = EnemyFx.Prim(PrimitiveType.Cylinder, start, BoneColor);
                shaft.transform.SetParent(root.transform, false);
                shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shaft.transform.localScale = new Vector3(0.09f, 0.42f, 0.09f);
                GameObject tip = EnemyFx.Prim(PrimitiveType.Cube, start, new Color(0.45f, 0.3f, 0.5f));
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = new Vector3(0f, 0f, -0.38f);
                tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                tip.transform.localScale = new Vector3(0.16f, 0.16f, 0.08f);
                Trail(root, new Color(1f, 0.95f, 0.6f), 0.12f, 0.16f);
                break;
            }
            default:
                root = EnemyFx.Prim(PrimitiveType.Sphere, start, BallColor);
                root.transform.localScale = Vector3.one * 0.4f;
                Trail(root, BallColor, 0.3f, 0.2f);
                break;
        }
        root.name = "Hostile shot";
        EnemyProjectile shot = root.AddComponent<EnemyProjectile>();
        shot.direction = direction;
        shot.target = target;
        shot.damage = damage;
        shot.style = style;
        shot.baseScale = root.transform.localScale;
        return shot;
    }

    static void Trail(GameObject root, Color color, float width, float time)
    {
        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = time;
        trail.minVertexDistance = 0.15f;
        trail.widthMultiplier = width;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = EnemyFx.Mat(color);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (target == null || GameSession.Instance == null || GameSession.Instance.IsFinished) { Destroy(gameObject); return; }
        Vector3 start = transform.position;
        Vector3 end = start + direction * speed * Time.deltaTime;
        age += Time.deltaTime;
        if (age > 7f || !ArenaLayout.Instance.IsWalkable(end, 0.15f) ||
            !ArenaLayout.Instance.PathClear(start, end, 0.15f)) { Impact(start); Destroy(gameObject); return; }
        Vector3 segment = end - start;
        float t = Mathf.Clamp01(Vector3.Dot(target.position - start, segment) / Mathf.Max(segment.sqrMagnitude, 0.00001f));
        if (Vector3.Distance(start + segment * t, target.position) < 0.75f)
        {
            target.GetComponent<PlayerHealth>().TakeDamage(damage);
            Impact(start + segment * t);
            Destroy(gameObject);
            return;
        }
        transform.position = end;
        if (style == Style.Acid)
        {
            // Сгусток пульсирует и роняет капли.
            float wobble = Mathf.Sin(age * 28f) * 0.12f;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + wobble, 1f - wobble, 1f + wobble * 0.5f));
            if (Random.value < Time.deltaTime * 14f)
            {
                FxPiece drop = EnemyFx.Piece(PrimitiveType.Sphere, transform.position, 0.1f, 0.4f, EnemyFx.Ramp(AcidColor, AcidDark));
                drop.gravity = 9f;
                drop.groundY = GroundHeight.At(transform.position) + 0.02f;
                drop.grow = 0.05f;
            }
        }
    }

    private void Impact(Vector3 point)
    {
        float ground = GroundHeight.At(point) + 0.05f;
        switch (style)
        {
            case Style.Acid:
                EnemyFx.Blobs(point, AcidColor, 7, 4f, 0.13f, 0.5f, ground);
                EnemyFx.GroundSplat(new Vector3(point.x, ground - 0.05f, point.z), 0.45f, AcidDark, 1.6f);
                break;
            case Style.Quill:
                EnemyFx.Blobs(point, BoneColor, 5, 5f, 0.08f, 0.35f, ground);
                break;
            default:
                EnemyFx.Blobs(point, BallColor, 5, 4f, 0.12f, 0.4f, ground);
                break;
        }
    }
}

// Снаряд миномётчика: спора летит по дуге к кольцу удара и лопается. Только вид — урон считает EnemyAbility по таймеру.
public sealed class MortarShell : MonoBehaviour
{
    Vector3 from, to;
    float duration, age, height;
    static readonly Color SporeColor = new Color(1f, 0.55f, 0.3f);
    static readonly Color SporeGlow = new Color(0.85f, 1f, 0.35f);

    public static void Launch(Vector3 from, Vector3 to, float duration, float radius)
    {
        GameObject go = EnemyFx.Prim(PrimitiveType.Sphere, from, SporeColor);
        go.name = "Mortar spore";
        go.transform.localScale = Vector3.one * 0.5f;
        GameObject spot = EnemyFx.Prim(PrimitiveType.Sphere, from, SporeGlow);
        spot.transform.SetParent(go.transform, false);
        spot.transform.localPosition = new Vector3(0.25f, 0.2f, 0f);
        spot.transform.localScale = Vector3.one * 0.45f;
        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.widthMultiplier = 0.3f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = EnemyFx.Mat(SporeColor);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MortarShell shell = go.AddComponent<MortarShell>();
        shell.from = from;
        shell.to = to;
        shell.duration = Mathf.Max(0.1f, duration);
        shell.height = 3.5f + Vector3.Distance(from, to) * 0.15f;
        EnemyFx.Blobs(from, SporeColor, 4, 2.5f, 0.1f, 0.35f, from.y - 1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        if (GameSession.Instance != null && GameSession.Instance.IsFinished) { Destroy(gameObject); return; }
        age += dt;
        float t = Mathf.Clamp01(age / duration);
        transform.position = Vector3.Lerp(from, to, t) + Vector3.up * height * 4f * t * (1f - t);
        transform.Rotate(new Vector3(1f, 0.3f, 0.2f), 540f * dt, Space.Self);
        float pulse = 0.5f + Mathf.Sin(age * 18f) * 0.05f;
        transform.localScale = Vector3.one * pulse;
        if (t >= 1f)
        {
            Vector3 ground = new Vector3(to.x, to.y, to.z);
            // Взрыв споры (09.11.2026 владелец: «нет эффекта попадания и взрыва»): огненный шар, дым, ударное кольцо,
            // свет и звук — облегчённый EnemyFx.Explosion, сверху споровые брызги цвета мортиры.
            EnemyFx.Explosion(ground, EnemyAbility.MortarRadius * 0.8f, 0.55f, false);
            EnemyFx.LightFlash(ground + Vector3.up * 1f, SporeColor, EnemyAbility.MortarRadius * 3f, 4f, 0.3f);
            GameAudio.Play(Sfx.Explosion, 0.7f);
            EnemyFx.Blobs(ground + Vector3.up * 0.3f, SporeColor, 10, 5f, 0.14f, 0.55f, to.y);
            EnemyFx.Blobs(ground + Vector3.up * 0.3f, SporeGlow, 6, 3.5f, 0.1f, 0.5f, to.y);
            for (int i = 0; i < 4; i++)
                EnemyFx.Puff(ground + Random.insideUnitSphere * 0.8f + Vector3.up * 0.3f, new Color(0.75f, 0.6f, 0.45f), 1.1f, 0.6f, Vector3.up * 1.2f);
            EnemyFx.Ring(ground + Vector3.up * 0.1f, EnemyAbility.MortarRadius, SporeColor, 0.35f, 0.35f);
            EnemyFx.GroundSplat(ground, EnemyAbility.MortarRadius * 0.55f, new Color(0.45f, 0.3f, 0.2f), 2.5f);
            ScreenShake.AddAt(ground, 0.15f);
            Destroy(gameObject);
        }
    }
}
