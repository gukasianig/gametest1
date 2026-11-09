using UnityEngine;

// Струя огнемёта из частиц (вместо прежней растянутой сферы): яркое ядро у сопла, языки пламени расходятся конусом
// оружия и за время полёта меняют цвет жёлтый → оранжевый → красный, сверху — дым, искры и мерцающий свет у сопла.
// Материалы — Resources/Fx (FlameFxSetup). Длина струи = дальность оружия.
public sealed class FlameJet : MonoBehaviour
{
    const float Speed = 9f;
    ParticleSystem fire, smoke, sparks;
    Light glow;
    bool emitting;

    public static FlameJet Create()
    {
        var go = new GameObject("Flame jet");
        FlameJet jet = go.AddComponent<FlameJet>();
        jet.Build();
        return jet;
    }

    public bool Emitting => emitting;

    void Build()
    {
        Material add = Resources.Load<Material>("Fx/FlameAdd");
        Material smokeMaterial = Resources.Load<Material>("Fx/FlameSmoke");
        fire = System("Fire", add, 520);
        smoke = System("Smoke", smokeMaterial != null ? smokeMaterial : add, 200);
        sparks = System("Sparks", add, 120);
        var light = new GameObject("Flame light");
        light.transform.SetParent(transform, false);
        light.transform.localPosition = new Vector3(0f, 0.3f, 1.2f);
        glow = light.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.55f, 0.2f);
        glow.range = 6f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
    }

    ParticleSystem System(string name, Material material, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.06f;
        var emission = ps.emission;
        emission.enabled = true;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material != null ? material : EnemyFx.Mat(new Color(1f, 0.5f, 0.1f));
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    // Настроить под оружие: длина струи (м) и полный угол конуса (градусы).
    public void Configure(float range, float coneAngle)
    {
        float life = Mathf.Clamp(range / Speed, 0.25f, 1.2f);
        float half = Mathf.Clamp(coneAngle * 0.5f * 0.75f, 5f, 40f);

        var main = fire.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * 0.85f, Speed * 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        var shape = fire.shape; shape.angle = half;
        var emission = fire.emission; emission.rateOverTime = 170f;
        var size = fire.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 2.2f), new Keyframe(1f, 3.2f)));
        var color = fire.colorOverLifetime; color.enabled = true;
        color.color = Gradient(
            new[] { new GradientColorKey(new Color(1f, 0.97f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.78f, 0.25f), 0.2f),
                new GradientColorKey(new Color(1f, 0.42f, 0.08f), 0.55f), new GradientColorKey(new Color(0.65f, 0.12f, 0.04f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        var rotation = fire.rotationOverLifetime; rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var noise = fire.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 1.4f; noise.scrollSpeed = 1.5f;
        var lift = fire.velocityOverLifetime; lift.enabled = true;
        lift.space = ParticleSystemSimulationSpace.World;
        lift.x = new ParticleSystem.MinMaxCurve(0f); lift.y = new ParticleSystem.MinMaxCurve(0.8f); lift.z = new ParticleSystem.MinMaxCurve(0f);

        main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 1.2f, life * 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * 0.55f, Speed * 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        shape = smoke.shape; shape.angle = half * 1.1f;
        emission = smoke.emission; emission.rateOverTime = 35f;
        size = smoke.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 3.5f)));
        color = smoke.colorOverLifetime; color.enabled = true;
        color.color = Gradient(
            new[] { new GradientColorKey(new Color(0.3f, 0.26f, 0.24f), 0f), new GradientColorKey(new Color(0.42f, 0.4f, 0.4f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.45f), new GradientAlphaKey(0f, 1f) });
        lift = smoke.velocityOverLifetime; lift.enabled = true;
        lift.space = ParticleSystemSimulationSpace.World;
        lift.x = new ParticleSystem.MinMaxCurve(0f); lift.y = new ParticleSystem.MinMaxCurve(1.6f); lift.z = new ParticleSystem.MinMaxCurve(0f);

        main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life * 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(Speed * 0.9f, Speed * 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.gravityModifier = 0.6f;
        shape = sparks.shape; shape.angle = half * 1.2f;
        emission = sparks.emission; emission.rateOverTime = 25f;
        color = sparks.colorOverLifetime; color.enabled = true;
        color.color = Gradient(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
    }

    static Gradient Gradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var g = new Gradient();
        g.SetKeys(colors, alphas);
        return g;
    }

    // Каждый тик огнемёта: струя из сопла в направлении стрельбы.
    public void Aim(Vector3 origin, Vector3 direction)
    {
        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        if (emitting) return;
        emitting = true;
        fire.Play(); smoke.Play(); sparks.Play();
    }

    public void Stop()
    {
        if (!emitting) return;
        emitting = false;
        fire.Stop(); smoke.Stop(); sparks.Stop();   // уже летящие языки догорают сами
    }

    void Update()
    {
        float target = emitting ? 2.2f + Mathf.PerlinNoise(Time.time * 12f, 0.3f) * 1.6f : 0f;
        glow.intensity = Mathf.MoveTowards(glow.intensity, target, Time.deltaTime * 25f);
    }
}
