using System.Collections.Generic;
using UnityEngine;

// Анимация смерти. Health.Die вызывает Spawn до уничтожения врага: модель-скин отцепляется от врага
// и доигрывает смерть сама (без логики, коллайдеров и тега Enemy), затем удаляется.
//  Shatter — лёгкие: части разлетаются, брызги и лужа;  Burst — подрывник: то же сильнее и обугленное;
//  Topple  — тяжёлые и стрелки: валится на бок, поджимает лапы, уходит в землю;
//  Fall    — летучая мышь: складывает крылья, падает, крутясь;  Split — разделяющийся: половины разъезжаются к осколкам.
public sealed class EnemyCorpse : MonoBehaviour
{
    public enum Style { Shatter, Burst, Topple, Fall, Split }

    sealed class Piece
    {
        public Transform t;
        public Vector3 velocity, spinAxis;
        public float spin;
        public Vector3 scale;
    }

    sealed class Limb
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 axis;
        public float angle;
    }

    Style style;
    bool bomber;
    float age, groundY, side, height, width;
    Vector3 forward, right, pivot, startPos, startScale;
    float rolled;
    bool sinking;
    Vector3 sinkFrom;
    readonly List<Piece> pieces = new List<Piece>();
    readonly List<Limb> limbs = new List<Limb>();
    Transform halfL, halfR, seam;
    Vector3 halfLStart, halfRStart, seamScale;
    Color goo;

    public static void Spawn(Health health)
    {
        if (health == null) return;
        Transform skin = health.transform.Find("SkinModel");
        if (skin == null || !skin.gameObject.activeInHierarchy) return;

        EnemyAbility ability = health.GetComponent<EnemyAbility>();
        ModelWalkAnimator animator = skin.GetComponent<ModelWalkAnimator>();
        if (animator != null) { animator.ClearTint(); animator.enabled = false; Destroy(animator); }

        Renderer[] renderers = skin.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

        Style style = Choose(health, ability, skin, bounds);
        Color goo = ability != null ? EnemyAbility.RoleColor(ability.role) : new Color(0.55f, 0.3f, 0.65f);
        Vector3 forward = health.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        skin.SetParent(null, true);
        skin.name = "Corpse";
        EnemyCorpse corpse = skin.gameObject.AddComponent<EnemyCorpse>();
        corpse.bomber = ability != null && ability.role == EnemyRole.Bomber;
        corpse.Init(style, forward.normalized, bounds, goo);
    }

    static Style Choose(Health health, EnemyAbility ability, Transform skin, Bounds bounds)
    {
        if (ability != null)
        {
            switch (ability.role)
            {
                case EnemyRole.Bomber: return ability.Detonated ? Style.Burst : Style.Shatter;
                case EnemyRole.Splitter: return skin.Find("Half_L") != null && !health.name.Contains("fragment") ? Style.Split : Style.Shatter;
                case EnemyRole.Teleporter: return Style.Fall;
            }
        }
        if (skin.Find("Wing_L") != null && skin.Find("Leg_FL") == null && skin.Find("Leg_L") == null) return Style.Fall;
        if (health.name.Contains("minion") || health.name.Contains("fragment")) return Style.Shatter;
        // Крупные и роли с «оружием» падают целиком, мелочь разлетается.
        if (ability != null || Mathf.Max(bounds.size.x, bounds.size.z) > 1.7f) return Style.Topple;
        return Style.Shatter;
    }

    void Init(Style value, Vector3 dir, Bounds bounds, Color color)
    {
        style = value;
        forward = dir;
        right = Vector3.Cross(Vector3.up, forward).normalized;
        groundY = bounds.min.y;
        height = Mathf.Max(0.2f, bounds.size.y);
        width = Mathf.Max(0.3f, Mathf.Min(bounds.size.x, bounds.size.z));
        goo = color;
        startPos = transform.position;
        startScale = transform.localScale;
        side = Random.value < 0.5f ? -1f : 1f;
        Vector3 center = bounds.center;

        switch (style)
        {
            case Style.Shatter:
            case Style.Burst:
            {
                bool burst = style == Style.Burst;
                // Все части — прямые дети корня, чтобы каждая летела сама по себе.
                var meshes = new List<Transform>();
                foreach (Renderer r in GetComponentsInChildren<Renderer>()) meshes.Add(r.transform);
                foreach (Transform t in meshes) t.SetParent(transform, true);
                foreach (Transform t in meshes)
                {
                    Vector3 away = t.position - center;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0004f) away = Random.insideUnitSphere;
                    away.y = 0f;
                    float power = burst ? Random.Range(6f, 10f) : Random.Range(2.2f, 4.5f);
                    pieces.Add(new Piece
                    {
                        t = t,
                        velocity = away.normalized * power + Vector3.up * (burst ? Random.Range(6f, 10f) : Random.Range(3f, 5.5f)),
                        spinAxis = Random.onUnitSphere,
                        spin = Random.Range(300f, 800f) * (burst ? 1.6f : 1f),
                        scale = t.localScale,
                    });
                    if (burst)
                    {
                        var block = new MaterialPropertyBlock();
                        block.SetColor("_BaseColor", new Color(0.35f, 0.3f, 0.3f));   // обуглено
                        Renderer r = t.GetComponent<Renderer>();
                        if (r != null) r.SetPropertyBlock(block);
                    }
                }
                Vector3 floor = new Vector3(center.x, groundY, center.z);
                EnemyFx.Blobs(center, goo, burst ? 6 : 9, burst ? 7f : 4.5f, 0.14f, 0.7f, groundY);
                EnemyFx.GroundSplat(floor, width * 0.6f, goo * 0.55f, 2.6f);
                if (!burst) EnemyFx.Puff(center, Color.Lerp(goo, Color.white, 0.4f), width * 0.7f, 0.35f, Vector3.up);
                // Убитый подрывник лопается маленьким хлопком без урона и тряски (сам взрыв — только при подрыве).
                if (!burst && bomber) EnemyFx.Explosion(floor, 1.2f, 0f, false);
                break;
            }
            case Style.Topple:
                // Ось опрокидывания — нижнее ребро со стороны падения.
                pivot = new Vector3(center.x, groundY, center.z) + right * side * width * 0.45f;
                CollectLimbs();
                EnemyFx.Puff(new Vector3(center.x, groundY + 0.1f, center.z) + right * side * width * 0.6f,
                    new Color(0.55f, 0.5f, 0.45f), width * 0.5f, 0.5f, Vector3.up * 0.8f);
                break;
            case Style.Fall:
                CollectLimbs();
                break;
            case Style.Split:
                halfL = Find("Half_L");
                halfR = Find("Half_R");
                seam = Find("Seam");
                // Лапы уезжают вместе со своей половиной.
                foreach (Transform t in GetComponentsInChildren<Transform>())
                {
                    if (!t.name.StartsWith("Leg_")) continue;
                    float s = ModelWalkAnimator.Side(t.name);
                    Transform half = s < 0f ? halfL : halfR;
                    if (half != null) t.SetParent(half, true);
                }
                if (halfL != null) halfLStart = halfL.position;
                if (halfR != null) halfRStart = halfR.position;
                if (seam != null) seamScale = seam.localScale;
                EnemyFx.Blobs(center, new Color(0.65f, 0.9f, 0.3f), 10, 4f, 0.12f, 0.6f, groundY);
                break;
        }
    }

    Transform Find(string name)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>()) if (t.name == name) return t;
        return null;
    }

    void CollectLimbs()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name;
            float angle;
            if (n.StartsWith("Leg")) angle = -35f;        // лапы подгибаются к брюху
            else if (n.StartsWith("Shin")) angle = 55f;
            else if (n == "Jaw") angle = 30f;             // пасть раскрыта
            else if (n == "Head") angle = 12f;
            else if (n.StartsWith("Wing")) angle = 0f;
            else if (n.StartsWith("Tail")) angle = 10f;
            else continue;
            Vector3 axis = n.StartsWith("Wing") ? t.InverseTransformDirection(forward) * -ModelWalkAnimator.Side(n) : t.InverseTransformDirection(right);
            if (n.StartsWith("Wing")) angle = 75f;        // крылья складываются вверх
            limbs.Add(new Limb { t = t, rest = t.localRotation, axis = axis, angle = angle });
        }
    }

    void PoseLimbs(float k)
    {
        foreach (Limb l in limbs) l.t.localRotation = l.rest * Quaternion.AngleAxis(l.angle * k, l.axis);
    }

    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        age += dt;
        switch (style)
        {
            case Style.Shatter: UpdatePieces(dt, 0.55f, 0.35f); break;
            case Style.Burst: UpdatePieces(dt, 0.7f, 0.4f); break;
            case Style.Topple: UpdateTopple(dt); break;
            case Style.Fall: UpdateFall(dt); break;
            case Style.Split: UpdateSplit(); break;
        }
    }

    void UpdatePieces(float dt, float hold, float fade)
    {
        if (age >= hold + fade) { Destroy(gameObject); return; }
        float k = age < hold ? 1f : 1f - Smooth((age - hold) / fade);
        foreach (Piece p in pieces)
        {
            if (p.t == null) continue;
            p.velocity += Vector3.down * 20f * dt;
            Vector3 pos = p.t.position + p.velocity * dt;
            if (pos.y < groundY + 0.05f)
            {
                pos.y = groundY + 0.05f;
                p.velocity = new Vector3(p.velocity.x * 0.5f, -p.velocity.y * 0.3f, p.velocity.z * 0.5f);
                p.spin *= 0.5f;
            }
            p.t.position = pos;
            p.t.Rotate(p.spinAxis, p.spin * dt, Space.World);
            p.t.localScale = p.scale * Mathf.Max(0.001f, k);
        }
    }

    void UpdateTopple(float dt)
    {
        const float fall = 0.38f, rest = 0.5f, sink = 0.7f;
        // Падение с небольшим отскоком в конце.
        float t = Mathf.Clamp01(age / fall);
        float target = (t < 1f ? t * t : 1f) * 88f;
        if (age > fall && age < fall + 0.15f) target -= Mathf.Sin((age - fall) / 0.15f * Mathf.PI) * 8f;
        float delta = target - rolled;
        rolled = target;
        transform.RotateAround(pivot, forward, -side * delta);
        PoseLimbs(Smooth(age / (fall + 0.2f)));
        if (age > fall + rest)
        {
            if (!sinking) { sinkFrom = transform.position; sinking = true; }
            float s = Smooth((age - fall - rest) / sink);
            transform.position = sinkFrom + Vector3.down * height * 0.9f * s;   // уходит в землю
            transform.localScale = startScale * (1f - 0.35f * s);
        }
        if (age > fall + rest + sink) Destroy(gameObject);
    }

    void UpdateFall(float dt)
    {
        const float drop = 0.45f, sink = 0.6f;
        PoseLimbs(Smooth(age / 0.2f));
        float t = Mathf.Clamp01(age / drop);
        // Подскок вверх и падение, вращаясь вокруг вертикали.
        float y = Mathf.Sin(t * Mathf.PI) * 0.5f * (1f - t);
        transform.position = new Vector3(startPos.x, startPos.y + y, startPos.z);
        transform.Rotate(Vector3.up, 720f * (1f - t) * dt, Space.World);
        if (age >= drop && age - dt < drop)
        {
            transform.localScale = Vector3.Scale(startScale, new Vector3(1.15f, 1.15f, 1.15f));
            EnemyFx.Puff(new Vector3(startPos.x, groundY + 0.1f, startPos.z), new Color(0.5f, 0.45f, 0.5f), width * 0.8f, 0.45f, Vector3.up * 0.5f);
        }
        if (age > drop)
        {
            float s = Smooth((age - drop) / sink);
            transform.position = new Vector3(startPos.x, startPos.y - height * s, startPos.z);
            transform.localScale = startScale * (1f - 0.5f * s);
        }
        if (age > drop + sink) Destroy(gameObject);
    }

    // Половины разъезжаются к точкам осколков (±1.3 м по X, как в EnemyAbility.OnDeath) и тают, когда осколки «выросли».
    void UpdateSplit()
    {
        const float travel = 0.22f, fade = 0.18f;
        float t = Smooth(age / travel);
        float hop = Mathf.Sin(Mathf.Clamp01(age / travel) * Mathf.PI) * 0.35f;
        if (halfL != null) halfL.position = halfLStart + Vector3.left * 1.3f * t + Vector3.up * hop;
        if (halfR != null) halfR.position = halfRStart + Vector3.right * 1.3f * t + Vector3.up * hop;
        if (seam != null)
        {
            // Перемычка растягивается и рвётся.
            float stretch = Mathf.Clamp01(age / (travel * 0.6f));
            seam.localScale = age < travel * 0.6f ? seamScale * (1f + stretch * 0.8f) : Vector3.one * 0.001f;
            if (age >= travel * 0.6f && age - Time.deltaTime < travel * 0.6f)
                EnemyFx.Blobs(seam.position, new Color(0.65f, 0.9f, 0.3f), 8, 3.5f, 0.1f, 0.5f, groundY);
        }
        if (age > travel)
        {
            float k = 1f - Smooth((age - travel) / fade);
            transform.localScale = startScale * Mathf.Max(0.001f, k);
            // Масштаб корня сжимает и положение половин к его центру — держим их на месте.
            if (halfL != null) halfL.position = halfLStart + Vector3.left * 1.3f;
            if (halfR != null) halfR.position = halfRStart + Vector3.right * 1.3f;
        }
        if (age > travel + fade) Destroy(gameObject);
    }
}
