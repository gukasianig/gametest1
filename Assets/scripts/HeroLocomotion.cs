using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class HeroLocomotion : MonoBehaviour
{
    private const float MinMoveSpeedSquared = 0.04f;
    private const float BlendTime = 0.19f;
    public const float GrenadeReleasePhase = 0.47f;

    [SerializeField] private Animation heroAnimation;

    private Rigidbody body;
    private PlayMovement movement;
    private PlayerHealth health;
    private Transform throwingHand;
    private Transform weaponForearm;
    private Transform leftUpperArm, leftForearm, leftHand, rightUpperArm, rightHand, chestBone;
    private Transform primaryGrip;
    public bool OffhandAvailable => pose != "Throw" && !dead;
    private Quaternion modelRestRotation;
    private GameObject heldGrenade;
    private Action<Vector3> releaseGrenade;
    private string pose;
    public string CurrentPose => pose;
    private Vector3 lastVisualForward;
    private Vector3 rightVisualAim, leftVisualAim;
    private float gaitSpeed = 1f, torsoYaw, torsoPitch, torsoRoll;
    private float recoilUntil;
    private float leftRecoilUntil;
    private AutoShoter shooter;
    private Transform rightTarget, leftTarget;
    private Vector3 combatFacing;
    public bool CanAimWeapons => isActiveAndEnabled && heroAnimation != null && rightUpperArm != null && primaryGrip != null;

    public void SetCombatAim(Transform right, Transform left, Vector3 facing)
    {
        rightTarget = right;
        leftTarget = left;
        combatFacing = facing;
    }
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

    public void Recoil(bool offhand = false)
    {
        if (offhand) leftRecoilUntil = Time.time + .12f;
        else recoilUntil = Time.time + .12f;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayMovement>();
        health = GetComponent<PlayerHealth>();
        shooter = GetComponent<AutoShoter>();
        if (heroAnimation == null)
            return;

        modelRestRotation = heroAnimation.transform.localRotation;
        lastVisualForward = transform.forward;
        Transform chest = heroAnimation.transform.Find("Hips/Spine/Chest")
            ?? heroAnimation.transform.Find("OperatorRig/Hips/Spine/Chest");
        if (chest != null)
        {
            chestBone = chest;
            throwingHand = chest.Find("UpperArmL/ForearmL/HandL/LeftHandGrip");
            weaponForearm = chest.Find("UpperArmR/ForearmR");
            leftUpperArm = chest.Find("UpperArmL");
            leftForearm = chest.Find("UpperArmL/ForearmL");
            leftHand = chest.Find("UpperArmL/ForearmL/HandL");
            rightUpperArm = chest.Find("UpperArmR");
            rightHand = chest.Find("UpperArmR/ForearmR/HandR");
            primaryGrip = chest.Find("UpperArmR/ForearmR/HandR/WeaponSocket");
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
        if (dead)
        {
            // Кооператив: героя подняли — снова обычные позы.
            dead = false;
            deathTime = 0f;
            pose = "";
        }
        if (Time.timeScale == 0f)
            return;

        bool dash = movement != null && movement.Dashing && heroAnimation["Dash"] != null;
        Quaternion modelFacing = modelRestRotation;
        if (dash)
            modelFacing = Quaternion.LookRotation(transform.InverseTransformDirection(movement.DashDirection)) * modelRestRotation;
        else if (rightTarget != null && combatFacing.sqrMagnitude > .01f && pose != "Throw")
            modelFacing = Quaternion.LookRotation(transform.InverseTransformDirection(combatFacing)) * modelRestRotation;
        Transform model = heroAnimation.transform;
        model.localRotation = Quaternion.Slerp(model.localRotation, modelFacing, 1f - Mathf.Exp(-(dash ? 34f : 16f) * Time.deltaTime));

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
            // Keep the dash pose; a due volley waits until its arms are available again.
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
                if (shooter != null) shooter.FlushAimedShot();
                return;
            }
        }

        Vector3 velocity = body.velocity;
        bool moving = velocity.x * velocity.x + velocity.z * velocity.z > MinMoveSpeedSquared;
        Vector3 visualForward = heroAnimation.transform.rotation * Quaternion.Inverse(modelRestRotation) * Vector3.forward;
        float turnRate = lastVisualForward.sqrMagnitude > .01f ? Vector3.SignedAngle(lastVisualForward,visualForward,Vector3.up)/Mathf.Max(.001f,Time.deltaTime) : 0;
        lastVisualForward = visualForward;
        string locomotion = "Idle";
        if (moving)
        {
            Vector3 flat = Vector3.ProjectOnPlane(velocity,Vector3.up).normalized;
            float forward = Vector3.Dot(flat,visualForward);
            float sideways = Vector3.Dot(flat,Vector3.Cross(Vector3.up,visualForward));
            locomotion = forward < -.5f ? "RunBack" : Mathf.Abs(sideways) > .72f ? (sideways > 0 ? "StrafeRight" : "StrafeLeft") : "Run";
            // Enter/exit thresholds differ, avoiding repeated crossfades near a diagonal.
            if (pose == "RunBack" && forward < -.35f) locomotion = "RunBack";
            else if (pose == "StrafeRight" && sideways > .60f && forward > -.6f) locomotion = "StrafeRight";
            else if (pose == "StrafeLeft" && sideways < -.60f && forward > -.6f) locomotion = "StrafeLeft";
            if (heroAnimation[locomotion] == null) locomotion = "Run";
        }
        else if (Mathf.Abs(turnRate)>35)
        {
            string turning = turnRate>0 ? "TurnRight" : "TurnLeft";
            if (heroAnimation[turning] != null) locomotion = turning;
        }
        string next = Time.time < hitUntil && heroAnimation["Hit"] != null ? "Hit" : locomotion;
        PlayPose(next, next == "Hit" ? .025f : BlendTime);
        if (moving && next == locomotion && heroAnimation[next] != null)
        {
            // Match the operator's normal 5 m/s pace; boosts must not turn the short cycle into frantic steps.
            float speed = Mathf.Clamp(new Vector2(velocity.x, velocity.z).magnitude / 5f, .60f, 1.35f);
            gaitSpeed = Mathf.Lerp(gaitSpeed,speed,1f-Mathf.Exp(-12f*Time.deltaTime));
            heroAnimation[next].speed = gaitSpeed;
        }
        ApplyRecoil();
        if (shooter != null) shooter.FlushAimedShot();
    }

    private void PlayPose(string next, float blend)
    {
        if (pose == next || heroAnimation[next] == null)
            return;
        bool keepStep = IsTravelPose(pose) && IsTravelPose(next) && heroAnimation[pose] != null;
        float phase = keepStep ? Mathf.Repeat(heroAnimation[pose].normalizedTime,1f) : 0;
        pose = next;
        heroAnimation[next].time = phase * heroAnimation[next].length;
        heroAnimation.CrossFade(next, blend);
    }

    private static bool IsTravelPose(string value)
    {
        return value == "Run" || value == "RunBack" || value == "StrafeLeft" || value == "StrafeRight";
    }

    private void ApplyRecoil()
    {
        PoseOffhand();
        if (rightTarget != null && combatFacing.sqrMagnitude > .01f && chestBone != null && pose != "Throw")
        {
            Vector3 hipsForward = heroAnimation.transform.rotation * Quaternion.Inverse(modelRestRotation) * Vector3.forward;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(hipsForward, combatFacing, Vector3.up), -45f, 45f);
            float blend = 1f-Mathf.Exp(-12f*Time.deltaTime);
            torsoYaw = Mathf.LerpAngle(torsoYaw,yaw,blend);
            chestBone.rotation = Quaternion.AngleAxis(torsoYaw, Vector3.up) * chestBone.rotation;
            Vector3 velocity = transform.InverseTransformDirection(body.velocity);
            float breath = Mathf.Sin(Time.time * 2.4f) * .6f;
            torsoPitch = Mathf.Lerp(torsoPitch,Mathf.Clamp(velocity.z*1.2f,-5f,5f),blend);
            torsoRoll = Mathf.Lerp(torsoRoll,Mathf.Clamp(-velocity.x,-4f,4f),blend);
            chestBone.localRotation *= Quaternion.Euler(torsoPitch + breath, 0, torsoRoll);
        }
        AlignWeapons(rightTarget, leftTarget);
        if (weaponForearm != null && Time.time < recoilUntil)
        {
            float amount = Mathf.Clamp01((recoilUntil - Time.time) / .12f);
            weaponForearm.localRotation *= Quaternion.Euler(-11f * amount * amount, 0f, 0f);
        }
        if (leftForearm != null && Time.time < leftRecoilUntil && OffhandAvailable)
        {
            float amount = Mathf.Clamp01((leftRecoilUntil - Time.time) / .12f);
            leftForearm.localRotation *= Quaternion.Euler(-11f * amount * amount, 0f, 0f);
        }
    }

    private static Vector3 AimPoint(Transform target)
    {
        Collider collider = target.GetComponentInChildren<Collider>();
        return collider != null ? collider.bounds.center : target.position + Vector3.up * .6f;
    }

    public void AlignWeapons(Transform right, Transform left, bool firing = false)
    {
        if (!CanAimWeapons || (movement != null && movement.Dashing)) return;
        if (right != null) AimArm(rightUpperArm, weaponForearm, rightHand, primaryGrip, ModelSkins.FindMuzzle(transform), VisualAim(right,rightUpperArm.position,ref rightVisualAim,firing), 1f);
        else rightVisualAim = Vector3.zero;
        Transform socket = throwingHand != null ? throwingHand.Find(ModelSkins.OffhandSocketName) : null;
        if (left != null && OffhandAvailable && socket != null)
            AimArm(leftUpperArm, leftForearm, leftHand, socket, ModelSkins.FindOffhandMuzzle(transform), VisualAim(left,leftUpperArm.position,ref leftVisualAim,firing), -1f);
        else if (right != null && OffhandAvailable && shooter != null && shooter.TwoHandedHold)
            SupportWeapon();
    }

    private Vector3 VisualAim(Transform target, Vector3 shoulder, ref Vector3 direction, bool firing)
    {
        Vector3 point = AimPoint(target);
        if (shooter != null && shooter.LevelAim)
        {
            // Не ниже 12° под горизонт и не выше 6°: ракетница на уровне плеча, а не «в землю» у ближнего врага.
            Vector3 flat = point - shoulder; float run = new Vector2(flat.x, flat.z).magnitude;
            if (run > .01f) point.y = shoulder.y + Mathf.Clamp(flat.y, -run * .21f, run * .1f);
        }
        Vector3 intended = (point-shoulder).normalized;
        // Follow motion softly between shots; the actual volley still aligns exactly before spawning bullets.
        if (firing || direction.sqrMagnitude < .01f) direction = intended;
        else
        {
            float blend = 1f-Mathf.Exp(-22f*Mathf.Min(Time.deltaTime,.05f));
            float yaw = Mathf.LerpAngle(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,Mathf.Atan2(intended.x,intended.z)*Mathf.Rad2Deg,blend);
            float pitch = Mathf.Lerp(Mathf.Asin(Mathf.Clamp(direction.y,-1,1)),Mathf.Asin(Mathf.Clamp(intended.y,-1,1)),blend)*Mathf.Rad2Deg;
            direction = Quaternion.Euler(-pitch,yaw,0)*Vector3.forward;
        }
        return shoulder + direction*Vector3.Distance(point,shoulder);
    }

    private void SupportWeapon()
    {
        if (leftUpperArm == null || leftForearm == null || leftHand == null || throwingHand == null) return;
        Transform muzzle = ModelSkins.FindMuzzle(transform);
        if (muzzle == null) return;
        var fx = muzzle.GetComponentInParent<WeaponFireAnimation>();
        Vector3 target = muzzle.position - (fx != null ? fx.BarrelForward : primaryGrip.forward) * .30f - primaryGrip.up * .05f;
        for (int i=0;i<2;i++)
        {
            leftHand.rotation = primaryGrip.rotation * Quaternion.Inverse(throwingHand.rotation) * leftHand.rotation;
            SolveArm(leftUpperArm,leftForearm,leftHand,target-(throwingHand.position-leftHand.position),-1);
        }
        leftHand.rotation = primaryGrip.rotation * Quaternion.Inverse(throwingHand.rotation) * leftHand.rotation;
    }

    private void SolveArm(Transform upper, Transform forearm, Transform hand, Vector3 wristTarget, float side)
    {
        Vector3 shoulder = upper.position;
        float a = Vector3.Distance(shoulder, forearm.position), b = Vector3.Distance(forearm.position, hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 direction = (wristTarget - shoulder).normalized;
        float reach = Mathf.Clamp(Vector3.Distance(wristTarget, shoulder), Mathf.Abs(a-b)+.001f, a+b-.001f);
        Vector3 pole = Vector3.ProjectOnPlane(-transform.up + transform.right * side * .55f, direction).normalized;
        if (pole.sqrMagnitude < .01f) pole = Vector3.ProjectOnPlane(transform.forward,direction).normalized;
        float along = (a*a-b*b+reach*reach)/(2*reach);
        Vector3 elbow = shoulder + direction*along + pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation = Quaternion.FromToRotation(forearm.position-shoulder,elbow-shoulder)*upper.rotation;
        forearm.rotation = Quaternion.FromToRotation(hand.position-forearm.position,shoulder+direction*reach-forearm.position)*forearm.rotation;
    }

    private void AimArm(Transform upper, Transform forearm, Transform hand, Transform grip, Transform muzzle, Vector3 target, float side)
    {
        if (upper == null || forearm == null || hand == null || grip == null || muzzle == null) return;
        Vector3 shoulder = upper.position;
        float a = Vector3.Distance(shoulder, forearm.position), b = Vector3.Distance(forearm.position, hand.position);
        if (a < .001f || b < .001f) return;
        Vector3 direction = (target - shoulder).normalized;
        // Analytic two-bone IK: preserve limb lengths and keep the elbow below/outside the shoulder.
        float extension = side > 0 && shooter != null && shooter.TwoHandedHold ? .60f : .90f;
        float reach = Mathf.Clamp((a + b) * extension, Mathf.Abs(a - b) + .001f, a + b - .001f);
        Vector3 wrist = shoulder + direction * reach;
        Vector3 pole = Vector3.ProjectOnPlane(-transform.up + transform.right * side * .55f, direction).normalized;
        if (pole.sqrMagnitude < .01f) pole = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
        float along = (a * a - b * b + reach * reach) / (2f * reach);
        Vector3 elbow = shoulder + direction * along + pole * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
        upper.rotation = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder) * upper.rotation;
        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, wrist - forearm.position) * forearm.rotation;
        // Recalculate from the actual muzzle, including model offsets, rather than assuming a mesh axis.
        WeaponFireAnimation fx = muzzle.GetComponentInParent<WeaponFireAnimation>();
        for (int i = 0; i < 3; i++)
            hand.rotation = Quaternion.FromToRotation(fx != null ? fx.BarrelForward : grip.forward, target - muzzle.position) * hand.rotation;
    }

    private Vector3 MirrorArmPoint(Vector3 world)
    {
        Vector3 point = chestBone.InverseTransformPoint(world);
        point.x = -point.x;
        return chestBone.TransformPoint(point);
    }

    private void PoseOffhand()
    {
        Transform socket = throwingHand != null ? throwingHand.Find(ModelSkins.OffhandSocketName) : null;
        if (socket == null) return;
        socket.gameObject.SetActive(OffhandAvailable);
        if (!OffhandAvailable || leftUpperArm == null || leftForearm == null || leftHand == null ||
            weaponForearm == null || rightUpperArm == null || rightHand == null || primaryGrip == null) return;
        // Mirror the aiming arm's elbow and wrist; keep the left pistol pointed along the right grip.
        Vector3 elbow = MirrorArmPoint(weaponForearm.position);
        Vector3 wrist = MirrorArmPoint(rightHand.position);
        leftUpperArm.rotation = Quaternion.FromToRotation(leftForearm.position - leftUpperArm.position,
            elbow - leftUpperArm.position) * leftUpperArm.rotation;
        leftForearm.rotation = Quaternion.FromToRotation(leftHand.position - leftForearm.position,
            wrist - leftForearm.position) * leftForearm.rotation;
        leftHand.rotation = primaryGrip.rotation * Quaternion.Inverse(throwingHand.rotation) * leftHand.rotation;
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
