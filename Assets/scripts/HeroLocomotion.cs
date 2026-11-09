using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class HeroLocomotion : MonoBehaviour
{
    private const float MinMoveSpeedSquared = 0.04f;
    private const float BlendTime = 0.12f;
    public const float GrenadeReleasePhase = 0.47f;

    [SerializeField] private Animation heroAnimation;

    private Rigidbody body;
    private PlayMovement movement;
    private PlayerHealth health;
    private Transform throwingHand;
    private Transform weaponForearm;
    private Quaternion modelRestRotation;
    private GameObject heldGrenade;
    private Action<Vector3> releaseGrenade;
    private string pose;
    private float recoilUntil;
    private float hitUntil;
    private float deathTime;
    private int previousHealth;
    private bool dead;

    public bool HasGrenadeAnimation => isActiveAndEnabled && throwingHand != null && heroAnimation["Throw"] != null;
    public bool GrenadeWindupBusy => HasGrenadeAnimation && (pose == "Throw" || (movement != null && movement.Dashing));

    public bool TryBeginThrow(Action<Vector3> release)
    {
        if (!HasGrenadeAnimation || GrenadeWindupBusy)
            return false;

        releaseGrenade = release;
        heldGrenade = ProjectileModels.Create("grenade", throwingHand.position, throwingHand.rotation, Grenade.VisualLength);
        if (heldGrenade != null)
            heldGrenade.transform.SetParent(throwingHand, true);
        PlayPose("Throw", .035f);
        return true;
    }

    public void Recoil()
    {
        recoilUntil = Time.time + .12f;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayMovement>();
        health = GetComponent<PlayerHealth>();
        if (heroAnimation == null)
            return;

        modelRestRotation = heroAnimation.transform.localRotation;
        Transform chest = heroAnimation.transform.Find("Hips/Spine/Chest")
            ?? heroAnimation.transform.Find("OperatorRig/Hips/Spine/Chest");
        if (chest != null)
        {
            throwingHand = chest.Find("UpperArmL/ForearmL/HandL/LeftHandGrip");
            weaponForearm = chest.Find("UpperArmR/ForearmR");
        }
        pose = "Idle";
    }

    private void LateUpdate()
    {
        if (heroAnimation == null)
            return;

        if (health != null && health.IsDead)
        {
            SampleDeath();
            return;
        }
        if (Time.timeScale == 0f)
            return;

        bool dash = movement != null && movement.Dashing && heroAnimation["Dash"] != null;
        Quaternion modelFacing = modelRestRotation;
        if (dash)
            modelFacing = Quaternion.LookRotation(transform.InverseTransformDirection(movement.DashDirection)) * modelRestRotation;
        Transform model = heroAnimation.transform;
        model.localRotation = Quaternion.Slerp(model.localRotation, modelFacing, 1f - Mathf.Exp(-34f * Time.deltaTime));

        if (health != null)
        {
            if (health.CurrentHP < previousHealth)
                hitUntil = Time.time + .24f;
            previousHealth = health.CurrentHP;
        }

        if (dash)
        {
            ClearThrow();
            PlayPose("Dash", .025f);
            heroAnimation["Dash"].speed = heroAnimation["Dash"].length / Mathf.Max(.05f, movement.dashDuration);
            ApplyRecoil();
            return;
        }

        if (pose == "Throw")
        {
            float phase = heroAnimation["Throw"].normalizedTime;
            if (releaseGrenade != null && phase >= GrenadeReleasePhase)
            {
                Action<Vector3> release = releaseGrenade;
                ClearThrow();
                release(throwingHand.position);
            }
            if (phase < 1f)
            {
                ApplyRecoil();
                return;
            }
        }

        Vector3 velocity = body.velocity;
        bool moving = velocity.x * velocity.x + velocity.z * velocity.z > MinMoveSpeedSquared;
        string next = Time.time < hitUntil && heroAnimation["Hit"] != null ? "Hit" : moving ? "Run" : "Idle";
        PlayPose(next, next == "Hit" ? .025f : BlendTime);
        if (next == "Run" && heroAnimation["Run"] != null)
            heroAnimation["Run"].speed = Mathf.Clamp(new Vector2(velocity.x, velocity.z).magnitude / 5f, .55f, 1.65f);
        ApplyRecoil();
    }

    private void PlayPose(string next, float blend)
    {
        if (pose == next || heroAnimation[next] == null)
            return;
        pose = next;
        heroAnimation[next].time = 0f;
        heroAnimation.CrossFade(next, blend);
    }

    private void ApplyRecoil()
    {
        if (weaponForearm != null && Time.time < recoilUntil)
        {
            float amount = Mathf.Clamp01((recoilUntil - Time.time) / .12f);
            weaponForearm.localRotation *= Quaternion.Euler(-11f * amount * amount, 0f, 0f);
        }
    }

    private void SampleDeath()
    {
        AnimationClip clip = heroAnimation.GetClip("Death");
        if (clip == null || deathTime >= clip.length)
            return;
        if (!dead)
        {
            ClearThrow();
            heroAnimation.Stop();
            dead = true;
        }
        deathTime = Mathf.Min(clip.length, deathTime + Time.unscaledDeltaTime);
        clip.SampleAnimation(heroAnimation.gameObject, deathTime);
    }

    private void ClearThrow()
    {
        releaseGrenade = null;
        if (heldGrenade != null)
        {
            heldGrenade.SetActive(false);
            Destroy(heldGrenade);
            heldGrenade = null;
        }
    }

    private void OnDisable() => ClearThrow();
}
