using System.Collections.Generic;
using UnityEngine;

// Reworks the existing operator's material palette at runtime; source FBX/materials stay intact.
public sealed class HeroPresentation : MonoBehaviour
{
    private PlayerHealth health;
    private PlayMovement movement;
    private HeroAbilities abilities;
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<bool> optics = new List<bool>();
    private float flashUntil, stepDistance, nextDust;
    private bool wasDashing, wasDead;
    private readonly Color cyan = new Color(.05f,.8f,1f);

    private void Start()
    {
        health = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayMovement>();
        abilities = GetComponent<HeroAbilities>();
        health.Damaged += OnDamaged;
        foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                var mat = new Material(slots[i]);
                string name = slots[i].name;
                Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                if (name.Contains("Graphite")) color = new Color(.075f,.11f,.15f);
                if (name.Contains("Titanium")) color = new Color(.66f,.76f,.80f);
                if (name.Contains("Blue Steel")) color = new Color(.22f,.31f,.39f);
                bool optic = name.Contains("Optics");
                if (optic) { color = cyan; mat.EnableKeyword("_EMISSION"); }
                mat.SetColor("_BaseColor", color);
                materials.Add(mat); colors.Add(color); optics.Add(optic);
                slots[i] = mat;
            }
            renderer.sharedMaterials = slots;
        }
    }

    private void OnDamaged(int raw, int taken)
    {
        flashUntil = Time.time + .16f;
        GameAudio.Play(Sfx.HeroHurt,.6f);
    }

    private void Update()
    {
        if (health == null) return;
        bool dead = health.IsDead;
        if (dead && !wasDead) GameAudio.Play(Sfx.HeroDown,.6f);
        wasDead = dead;
        bool playing = !dead && GameSession.Instance != null && GameSession.Instance.State == GameRunState.Playing && Time.timeScale > 0;
        bool dash = movement != null && movement.Dashing;
        if (playing && dash && !wasDashing)
        {
            GameAudio.Play(Sfx.HeroDash,.45f);
            EnemyFx.Ring(transform.position,.65f,cyan,.23f,.09f);
        }
        wasDashing = dash;
        if (playing)
        {
            float speed = Vector3.ProjectOnPlane(GetComponent<Rigidbody>().velocity,Vector3.up).magnitude;
            stepDistance += dash ? 0 : speed*Time.deltaTime;
            if (stepDistance > 1.7f) { stepDistance %= 1.7f; GameAudio.Play(Sfx.HeroStep,.22f); }
            if (speed > .8f && Time.time >= nextDust)
            {
                nextDust = Time.time + (dash ? .045f : .28f);
                Vector3 point = transform.position; point.y = GroundHeight.At(point)+.1f;
                EnemyFx.Puff(point,dash ? cyan : new Color(.55f,.52f,.35f),dash ? .11f : .055f,.24f,Vector3.up*.25f);
            }
        }
        Color opticColor = abilities != null && abilities.Overdriving ? new Color(1,.62f,.15f) :
            health.CurrentHP < health.maxHP*.3f ? new Color(1,.25f,.16f) : cyan;
        float flash = dead ? 0 : Mathf.Clamp01((flashUntil-Time.time)/.16f);
        for (int i=0;i<materials.Count;i++)
        {
            if (materials[i] == null) continue;
            materials[i].SetColor("_BaseColor",Color.Lerp(optics[i] ? opticColor : colors[i],Color.white,flash*.65f));
            if (optics[i]) materials[i].SetColor("_EmissionColor",dead ? Color.black : opticColor*(1.2f+Mathf.Sin(Time.time*3)*.15f));
        }
    }

    private void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
