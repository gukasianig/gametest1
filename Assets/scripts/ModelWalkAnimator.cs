using System.Collections.Generic;
using UnityEngine;

// Процедурная анимация существ D3 по именам частей модели (скелета нет, части — отдельные меши со своими шарнирами):
// Leg* (бедро, шарнир вверху) и дочерний Shin* (голень, шарнир в колене), Arm*, Tail*, Wing*, Ear*, Head и дочерний Jaw,
// Body/Carapace, Throat, Frill, SporeSac, SignalFins, Brood*, Shield*.
// Ходьба берётся из смещения модели; позы атак включают способности через Hold/Fire/Flinch/PopIn.
// Логику, коллайдеры и физику врага не трогает: двигаются только части модели-скина.
public sealed class ModelWalkAnimator : MonoBehaviour
{
    public enum Pose { None, Aim, Windup, Dash, Swell, Cast, Blink }

    const float StrideLength = 0.9f;   // метров на один полный цикл ноги
    const float LegSwing = 32f;        // градусов вперёд/назад
    const float KneeBend = 40f;
    const float IdleBreath = 0.012f;
    const int PoseCount = 7;

    enum Kind { Leg, Shin, Arm, Tail, Wing, Antenna, Ear, Head, Jaw, Throat, Frill, Sac, Fins, Brood, Shield }

    sealed class Part
    {
        public Transform t;
        public Quaternion rest;
        public Vector3 restScale;
        public Vector3 pitch, yaw, roll;   // оси «вправо», «вверх», «вперёд» существа в локальных осях части
        public float phase, side;          // фаза шага; сторона −1 левая, +1 правая
        public bool front;
        public Kind kind;
    }

    Part[] parts;
    Transform body, owner;
    Vector3 bodyRest, lastPos;
    Quaternion bodyRestRot, rootRestRot;
    Vector3 bodyPitch, bodyRoll, rootRestPos, rootRestScale;
    Vector3 scaleUp, scaleForward, scaleRight;   // модули осей существа в осях корня модели (для сплющивания)
    int legCount;
    bool hasWings, started;
    float phase, speed, time, clock, seed;

    Pose pose;
    float poseUntil;
    readonly float[] weights = new float[PoseCount];
    float fireAt = -10f, flinchAt = -10f;
    float popStart = -1f, popDuration;
    float popDelayRequest = -1f, popDurationRequest;
    Vector3 preStartScale;

    Renderer[] renderers;
    MaterialPropertyBlock block;
    Color flashColor;
    float flashUntil;
    bool tinted;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    static bool IsBody(string n) { return n == "Body" || n == "Carapace"; }

    public static bool CanAnimate(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("Leg") || t.name.StartsWith("Wing") || IsBody(t.name)) return true;
        return false;
    }

    public static ModelWalkAnimator Of(Component actor)
    {
        // Настоящий null вместо «пустышки» Unity, чтобы вызовы через ?. были безопасны.
        ModelWalkAnimator found = actor != null ? actor.GetComponentInChildren<ModelWalkAnimator>() : null;
        return found != null && found.enabled ? found : null;
    }

    // ---- Управление из способностей ----

    // Держать позу до Release или указанное время (страховка, если корутина прервана).
    public void Hold(Pose value, float seconds = 3f) { pose = value; poseUntil = clock + seconds; }
    public void Release() { pose = Pose.None; }
    // Короткий импульс выстрела/плевка/удара: рывок головы, челюсть, отдача.
    public void Fire() { fireAt = clock; }
    // Попадание по врагу: вздрагивание и белая вспышка.
    public void Flinch()
    {
        if (clock - flinchAt < 0.12f) return;   // частый урон (огонь, кислота) не превращает вспышку в мерцание
        flinchAt = clock;
        Flash(new Color(2.2f, 2.2f, 2.2f), 0.07f);
    }
    public void Flash(Color color, float seconds) { flashColor = color; flashUntil = clock + seconds; }

    // Появление «из ничего» с перелётом масштаба (осколки, миньоны, выход из телепорта).
    public void PopIn(float delay, float duration)
    {
        if (!started)
        {
            // До Start: запомнить настоящий масштаб и спрятать модель, чтобы она не мелькнула в полный размер.
            if (popDelayRequest < 0f) preStartScale = transform.localScale;
            popDelayRequest = delay;
            popDurationRequest = duration;
            transform.localScale = preStartScale * 0.001f;
            return;
        }
        popStart = clock + delay;
        popDuration = Mathf.Max(0.05f, duration);
    }

    // Снять вспышку перед тем, как модель станет трупом.
    public void ClearTint()
    {
        if (!tinted || renderers == null) return;
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(null);
        tinted = false;
    }

    // ---- Подготовка ----

    static float LegPhase(string n)
    {
        char last = n[n.Length - 1];
        bool left = n.Contains("_L");
        if (char.IsDigit(last)) return (((last - '0') + (left ? 0 : 1)) % 2 == 0) ? 0f : Mathf.PI;
        if (n.EndsWith("FL") || n.EndsWith("BR") || n.EndsWith("MR")) return 0f;
        if (n.EndsWith("FR") || n.EndsWith("BL") || n.EndsWith("ML")) return Mathf.PI;
        return left ? 0f : Mathf.PI;
    }

    // Сторона по последней букве имени без номера: Leg_FL, Shin_L3, Wing_L → −1; …R → +1; Tail, Head → 0.
    public static float Side(string n)
    {
        int i = n.Length - 1;
        while (i > 0 && char.IsDigit(n[i])) i--;
        if (i <= 0 || n[i - 1] != '_' && n[i - 2 < 0 ? 0 : i - 2] != '_') return 0f;
        return n[i] == 'L' ? -1f : n[i] == 'R' ? 1f : 0f;
    }

    void Start()
    {
        owner = transform.parent != null ? transform.parent : transform;
        Vector3 right = owner.right, up = owner.up, forward = owner.forward;
        var list = new List<Part>();
        var legPhases = new Dictionary<string, float>();
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name;
            Kind kind;
            if (n.StartsWith("Leg")) kind = Kind.Leg;
            else if (n.StartsWith("Shin")) kind = Kind.Shin;
            else if (n.StartsWith("Arm")) kind = Kind.Arm;
            else if (n.StartsWith("Tail")) kind = Kind.Tail;
            else if (n.StartsWith("Wing")) kind = Kind.Wing;
            else if (n.StartsWith("Ear")) kind = Kind.Ear;
            else if (n == "Antenna") kind = Kind.Antenna;
            else if (n == "Head") kind = Kind.Head;
            else if (n == "Jaw") kind = Kind.Jaw;
            else if (n == "Throat") kind = Kind.Throat;
            else if (n == "Frill") kind = Kind.Frill;
            else if (n == "SporeSac") kind = Kind.Sac;
            else if (n == "SignalFins") kind = Kind.Fins;
            else if (n.StartsWith("Brood")) kind = Kind.Brood;
            else if (n.StartsWith("Shield")) kind = Kind.Shield;
            else
            {
                if (IsBody(n))
                {
                    body = t; bodyRest = t.localPosition; bodyRestRot = t.localRotation;
                    bodyPitch = t.InverseTransformDirection(right);
                    bodyRoll = t.InverseTransformDirection(forward);
                }
                continue;
            }
            var p = new Part
            {
                t = t, rest = t.localRotation, restScale = t.localScale, kind = kind, side = Side(n),
                pitch = t.InverseTransformDirection(right), yaw = t.InverseTransformDirection(up), roll = t.InverseTransformDirection(forward),
                front = n.Contains("_F") || (n.Length > 0 && char.IsDigit(n[n.Length - 1]) && n[n.Length - 1] <= '2'),
            };
            if (kind == Kind.Leg) { p.phase = LegPhase(n); legPhases[n.Substring(3)] = p.phase; legCount++; }
            if (kind == Kind.Wing) hasWings = true;
            list.Add(p);
        }
        // Голень наследует фазу своего бедра (Shin_FL ↔ Leg_FL).
        foreach (Part p in list)
            if (p.kind == Kind.Shin && legPhases.TryGetValue(p.t.name.Substring(4), out float legPhase)) p.phase = legPhase;
        parts = list.ToArray();

        rootRestPos = transform.localPosition;
        rootRestRot = transform.localRotation;
        rootRestScale = popDelayRequest >= 0f ? preStartScale : transform.localScale;
        scaleUp = Abs(transform.InverseTransformDirection(up));
        scaleForward = Abs(transform.InverseTransformDirection(forward));
        scaleRight = Abs(transform.InverseTransformDirection(right));
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();

        lastPos = transform.position;
        phase = Random.value * Mathf.PI * 2f; // враги не шагают синхронно
        seed = Random.value * 10f;
        time = seed;
        started = true;
        if (popDelayRequest >= 0f) PopIn(popDelayRequest, popDurationRequest);
    }

    static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

    // Пик быстро, затем спад: 0 → 1 за rise секунд, 1 → 0 за fall.
    static float Impulse(float t, float rise, float fall)
    {
        if (t < 0f) return 0f;
        if (t < rise) return t / rise;
        float k = 1f - (t - rise) / fall;
        return k > 0f ? k * k : 0f;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.9f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---- Кадр ----

    void LateUpdate()
    {
        if (parts == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        clock += dt;
        time += dt;

        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;
        float v = Mathf.Min(delta.magnitude / dt, 14f);
        speed = Mathf.Lerp(speed, v, 12f * dt);
        phase += Mathf.Min(speed, 6.5f) * dt / StrideLength * Mathf.PI * 2f;   // на рывке ноги часто, но не мельтешат

        if (pose != Pose.None && clock > poseUntil) pose = Pose.None;
        for (int i = 0; i < PoseCount; i++)
        {
            float target = (int)pose == i && i != 0 ? 1f : 0f;
            float rate = i == (int)Pose.Dash || i == (int)Pose.Blink ? 12f : 6f;
            weights[i] = Mathf.MoveTowards(weights[i], target, rate * dt);
        }
        float aim = weights[(int)Pose.Aim], wind = weights[(int)Pose.Windup], dash = weights[(int)Pose.Dash];
        float swell = weights[(int)Pose.Swell], cast = weights[(int)Pose.Cast], blink = weights[(int)Pose.Blink];
        float fire = Impulse(clock - fireAt, 0.05f, 0.3f);
        float flinch = Impulse(clock - flinchAt, 0.03f, 0.16f);
        float busy = Mathf.Clamp01(aim + wind + dash + swell + cast + blink);

        float move = Mathf.Clamp01(speed / 1.2f) * (1f - wind);   // 0 стоит, 1 идёт
        bool flier = legCount == 0 && hasWings;
        bool gallop = dash > 0.5f;

        foreach (Part p in parts)
        {
            float pitch = 0f, yaw = 0f, roll = 0f, scale = 1f;
            switch (p.kind)
            {
                case Kind.Leg:
                {
                    float legPhase = gallop ? (p.front ? 0f : Mathf.PI) : p.phase;   // на рывке — галоп: передние вместе, задние вместе
                    pitch = Mathf.Sin(phase + legPhase) * LegSwing * move * (1f + 0.45f * dash);
                    if (p.front && p.side < 0f) pitch += Mathf.Sin(time * 15f) * 26f * wind;   // бьёт копытом перед тараном
                    else pitch -= 8f * wind;                                                    // остальные упираются
                    roll = p.side * (12f * swell + 6f * flinch);                                // раздувшись, расставляет лапы
                    pitch += 10f * flinch;
                    break;
                }
                case Kind.Shin:
                {
                    float legPhase = gallop ? (p.front ? 0f : Mathf.PI) : p.phase;
                    // Колено сгибается, когда нога идёт вперёд (поднята), и выпрямляется в опоре.
                    pitch = Mathf.Max(0f, -Mathf.Cos(phase + legPhase)) * KneeBend * move * (1f + 0.3f * dash);
                    if (p.front && p.side < 0f) pitch += Mathf.Max(0f, Mathf.Cos(time * 15f)) * 35f * wind;
                    pitch += 14f * wind + 10f * aim + 10f * flinch;   // присед перед атакой
                    break;
                }
                case Kind.Arm:
                    pitch = Mathf.Sin(phase + (p.side < 0f ? Mathf.PI : 0f)) * 22f * move + Mathf.Sin(time * 1.9f + p.side) * 3f;
                    pitch += -55f * cast - 20f * fire + 15f * flinch;
                    roll = p.side * 18f * cast;
                    break;
                case Kind.Tail:
                    yaw = Mathf.Sin(time * 4.5f + phase * 0.5f) * (6f + 12f * move) * (1f - dash * 0.7f);
                    yaw += Mathf.Sin(time * 16f) * 22f * wind;      // хлещет хвостом перед атакой
                    pitch = -12f * swell - 10f * aim + 8f * dash - 14f * cast;
                    break;
                case Kind.Wing:
                {
                    float flap = flier ? Mathf.Sin(time * (16f + 10f * busy)) * 28f : Mathf.Sin(time * 5f) * 6f;
                    roll = -p.side * (flap * (1f - blink) + 70f * blink);   // при телепорте складывает крылья
                    break;
                }
                case Kind.Antenna:
                    pitch = Mathf.Sin(time * 7f) * 6f * move + Mathf.Sin(time * 1.7f) * 3f;
                    break;
                case Kind.Ear:
                    pitch = Mathf.Sin(phase * 2f) * 8f * move - 25f * flinch - 15f * dash;
                    roll = p.side * Mathf.Sin(time * 2.3f + p.side) * 5f;
                    break;
                case Kind.Head:
                    pitch = -Mathf.Sin(phase * 2f) * 3f * move;                       // голова гасит подскок шага
                    pitch += -7f * aim + 16f * fire + 18f * wind + 10f * dash - 20f * cast - 14f * flinch;
                    pitch += Mathf.Sin(time * 31f) * 4f * swell;
                    yaw = Mathf.Sin(time * 0.6f + seed) * 10f * (1f - move) * (1f - busy);   // стоя оглядывается
                    break;
                case Kind.Jaw:
                {
                    float chew = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.1f + seed)), 10f) * 12f;   // иногда приоткрывает пасть
                    pitch = chew * (1f - busy) + 34f * fire + 10f * aim + (12f + Mathf.Sin(time * 22f) * 7f) * wind
                          + 20f * dash + 24f * swell + 16f * cast + 22f * flinch;
                    break;
                }
                case Kind.Throat:
                    scale = 1f + aim * (0.5f + 0.12f * Mathf.Sin(time * 14f)) - 0.3f * fire;   // набирает кислоту
                    break;
                case Kind.Frill:
                    scale = 1f + 0.28f * aim + 0.12f * fire + 0.15f * flinch;
                    break;
                case Kind.Sac:
                    scale = 1f + aim * (0.16f + 0.08f * Mathf.Sin(time * 11f)) - 0.3f * fire;
                    pitch = -12f * aim + 10f * fire;
                    break;
                case Kind.Fins:
                    scale = 1f + 0.3f * cast + 0.04f * Mathf.Sin(time * 3f);
                    break;
                case Kind.Brood:
                    scale = 1f + cast * 0.25f * (0.5f + 0.5f * Mathf.Sin(time * 12f + p.side)) + 0.03f * Mathf.Sin(time * 2f + p.side);
                    break;
                case Kind.Shield:
                    yaw = p.side * (Mathf.Sin(phase) * 4f * move - 12f * flinch);
                    break;
            }
            Quaternion r = Quaternion.identity;
            if (pitch != 0f) r *= Quaternion.AngleAxis(pitch, p.pitch);
            if (yaw != 0f) r *= Quaternion.AngleAxis(yaw, p.yaw);
            if (roll != 0f) r *= Quaternion.AngleAxis(roll, p.roll);
            p.t.localRotation = p.rest * r;
            p.t.localScale = p.restScale * scale;
        }

        if (body != null)
        {
            float hover = flier ? 0.1f + Mathf.Sin(time * 2.6f) * 0.08f : 0f;
            float bob = hover + Mathf.Abs(Mathf.Sin(phase)) * 0.05f * move * (1f + dash) + Mathf.Sin(time * 2.2f) * IdleBreath * (1f - move);
            bob -= 0.04f * wind;
            // Смещение вверх в мировых осях: у моделей из FBX корень повёрнут (270° по X), локальная Y — не «вверх».
            Vector3 up = Vector3.up * bob;
            body.localPosition = bodyRest + (body.parent != null ? body.parent.InverseTransformVector(up) : up);
            float bodyPitchAngle = 10f * dash - 6f * wind - 6f * fire - 8f * cast - 5f * flinch + Mathf.Sin(phase * 2f) * 3f * dash;
            float bodyRollAngle = Mathf.Sin(phase) * 4f * move + Mathf.Sin(time * 37f) * 3f * (wind + swell);
            body.localRotation = bodyRestRot * Quaternion.AngleAxis(bodyPitchAngle, bodyPitch) * Quaternion.AngleAxis(bodyRollAngle, bodyRoll);
        }

        AnimateRoot(aim, wind, dash, swell, cast, blink, fire, flinch);
        UpdateTint(swell);
    }

    void AnimateRoot(float aim, float wind, float dash, float swell, float cast, float blink, float fire, float flinch)
    {
        // Сплющивание/вытягивание: вниз при замахе и попадании, вперёд на рывке, раздувание у подрывника.
        float sy = 1f - 0.12f * wind - 0.06f * aim + 0.07f * cast - 0.12f * flinch - 0.05f * fire - 0.08f * dash;
        float sx = 1f + 0.07f * wind + 0.05f * flinch;
        float sz = 1f + 0.05f * wind + 0.18f * dash + 0.08f * fire;
        float swellPulse = swell * (0.28f + 0.07f * Mathf.Sin(time * (14f + 26f * swell)));
        float uniform = (1f + swellPulse) * (1f - blink * 0.97f);
        if (popStart >= 0f)
        {
            float t = (clock - popStart) / popDuration;
            uniform *= t <= 0f ? 0.001f : t >= 1f ? 1f : Mathf.Max(0.001f, EaseOutBack(t));
            if (t >= 1f) popStart = -1f;
        }
        Vector3 axes = scaleUp * sy + scaleRight * sx + scaleForward * sz;
        transform.localScale = Vector3.Scale(rootRestScale, axes) * uniform;

        // Отдача назад при выстреле и дрожь перед рывком/взрывом.
        Vector3 offset = -owner.forward * 0.14f * fire;
        float shake = 0.03f * wind + 0.05f * swell;
        if (shake > 0f) offset += new Vector3(Mathf.Sin(time * 53f), 0f, Mathf.Cos(time * 47f)) * shake;
        Transform parent = transform.parent;
        transform.localPosition = rootRestPos + (parent != null ? parent.InverseTransformVector(offset) : offset);

        // Вращение волчком при телепорте.
        if (blink > 0f && parent != null)
            transform.localRotation = Quaternion.AngleAxis(blink * blink * 540f, parent.InverseTransformDirection(Vector3.up)) * rootRestRot;
        else transform.localRotation = rootRestRot;
    }

    void UpdateTint(float swell)
    {
        if (renderers == null) return;
        Color color = Color.white;
        bool active = false;
        if (clock < flashUntil) { color = flashColor; active = true; }
        else if (swell > 0.05f && Mathf.Sin(time * (9f + 30f * swell)) > 0f) { color = new Color(2f, 0.55f, 0.45f); active = true; }   // мигает красным перед взрывом
        if (!active) { ClearTint(); return; }
        block.SetColor(BaseColorId, color);
        foreach (Renderer r in renderers) if (r != null) r.SetPropertyBlock(block);
        tinted = true;
    }
}
