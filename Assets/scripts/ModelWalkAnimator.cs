using UnityEngine;

// Процедурная анимация ходьбы для моделей по именам частей: Leg* (ноги, шарнир вверху; Leg_L1..L4/R1..R4 — шесть/восемь лап),
// Arm* (руки машут в противофазе), Tail* (хвост виляет), Wing* (крылья машут), Body/Carapace (подскок/парение), Antenna (качается).
// Скорость берётся из смещения модели, поэтому врагов, логику и физику не трогает.
public sealed class ModelWalkAnimator : MonoBehaviour
{
    const float StrideLength = 0.9f;   // метров на один полный цикл ноги
    const float LegSwing = 32f;        // градусов вперёд/назад
    const float IdleBreath = 0.012f;

    enum Kind { Leg, Arm, Tail, Wing, Antenna }
    struct Part { public Transform t; public Quaternion rest; public Vector3 axis; public float phase; public float sign; public Kind kind; }

    Part[] parts;
    Transform body;
    Vector3 bodyRest, lastPos;
    Quaternion bodyRestRot;
    Vector3 bodyForwardAxis;
    int legCount;
    float phase, speed, time;

    static bool IsBody(string n) { return n == "Body" || n == "Carapace"; }

    public static bool CanAnimate(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>())
            if (t.name.StartsWith("Leg") || t.name.StartsWith("Wing") || IsBody(t.name)) return true;
        return false;
    }

    // Диагональные пары в противофазе: FL+BR / FR+BL; для Leg_L/R и Leg_L1..R4 — чередование по сторонам и номеру.
    static float LegPhase(string n, int index)
    {
        char last = n[n.Length - 1];
        bool left = n.Contains("_L");
        if (char.IsDigit(last)) return (((last - '0') + (left ? 0 : 1)) % 2 == 0) ? 0f : Mathf.PI;
        if (n.EndsWith("FL") || n.EndsWith("BR") || n.EndsWith("MR")) return 0f;
        if (n.EndsWith("FR") || n.EndsWith("BL") || n.EndsWith("ML")) return Mathf.PI;
        return left ? 0f : Mathf.PI;
    }

    void Start()
    {
        var list = new System.Collections.Generic.List<Part>();
        Vector3 right = transform.right, up = Vector3.up;
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name;
            if (n.StartsWith("Leg")) { list.Add(Make(t, t.InverseTransformDirection(right), LegPhase(n, list.Count), 1f, Kind.Leg)); legCount++; }
            else if (n.StartsWith("Arm")) list.Add(Make(t, t.InverseTransformDirection(right), n.Contains("_L") ? Mathf.PI : 0f, 1f, Kind.Arm));
            else if (n.StartsWith("Tail")) list.Add(Make(t, t.InverseTransformDirection(up), 0f, 1f, Kind.Tail));
            else if (n.StartsWith("Wing")) list.Add(Make(t, t.InverseTransformDirection(transform.forward), 0f, n.Contains("_L") ? 1f : -1f, Kind.Wing));
            else if (n == "Antenna") list.Add(Make(t, t.InverseTransformDirection(right), 0f, 1f, Kind.Antenna));
            else if (IsBody(n)) { body = t; bodyRest = t.localPosition; bodyRestRot = t.localRotation; bodyForwardAxis = t.InverseTransformDirection(transform.forward); }
        }
        parts = list.ToArray();
        lastPos = transform.position;
        phase = Random.value * Mathf.PI * 2f; // враги не шагают синхронно
        time = Random.value * 10f;
    }

    static Part Make(Transform t, Vector3 axis, float phase, float sign, Kind kind)
    {
        return new Part { t = t, rest = t.localRotation, axis = axis, phase = phase, sign = sign, kind = kind };
    }

    void LateUpdate()
    {
        if (parts == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;
        float v = Mathf.Min(delta.magnitude / dt, 12f);
        speed = Mathf.Lerp(speed, v, 12f * dt);
        phase += speed * dt / StrideLength * Mathf.PI * 2f;
        time += dt;

        float move = Mathf.Clamp01(speed / 1.2f);    // 0 стоит, 1 идёт
        bool flier = legCount == 0;                  // без ног — парит и машет крыльями

        foreach (Part p in parts)
        {
            float angle = 0f;
            switch (p.kind)
            {
                case Kind.Leg: angle = Mathf.Sin(phase + p.phase) * LegSwing * move; break;
                case Kind.Arm: angle = Mathf.Sin(phase + p.phase) * 22f * move + Mathf.Sin(time * 1.9f + p.phase) * 3f; break;
                case Kind.Tail: angle = Mathf.Sin(time * 4.5f + phase * 0.5f) * (6f + 12f * move); break;
                case Kind.Wing: angle = p.sign * Mathf.Sin(time * (flier ? 16f : 5f)) * (flier ? 28f : 6f); break;
                case Kind.Antenna: angle = Mathf.Sin(time * 7f) * 6f * move + Mathf.Sin(time * 1.7f) * 3f; break;
            }
            p.t.localRotation = p.rest * Quaternion.AngleAxis(angle, p.axis);
        }
        if (body != null)
        {
            float hover = flier ? 0.1f + Mathf.Sin(time * 2.6f) * 0.08f : 0f;
            float bob = hover + Mathf.Abs(Mathf.Sin(phase)) * 0.05f * move + Mathf.Sin(time * 2.2f) * IdleBreath * (1f - move);
            // Смещение вверх в мировых осях: у моделей из FBX корень повёрнут (270° по X), локальная Y — не «вверх».
            Vector3 up = Vector3.up * bob;
            body.localPosition = bodyRest + (body.parent != null ? body.parent.InverseTransformVector(up) : up);
            body.localRotation = bodyRestRot * Quaternion.AngleAxis(Mathf.Sin(phase) * 4f * move, bodyForwardAxis);
        }
    }
}
