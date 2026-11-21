using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 19.11.2026: проверка 18 новых карточек на GreenArenaPlay (batch, без -quit): все карточки дают свои значения,
// тексты и иконки есть, и каждый эффект реально срабатывает в бою (уклонение, снаряды, последний шанс, стихии,
// импульсы, аура, рывок, разделение, заряженный выстрел, трофей, азарт, выпадение в окне выбора).
// Результат: Logs/new-cards-check.txt. Запуск: -executeMethod NewCardsChecks.RunBatch
[InitializeOnLoad]
public static class NewCardsChecks
{
    const string Marker = "Logs/new-cards.request", Report = "Logs/new-cards-check.txt";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static double started, stepAt;
    static int step, errors, failures, chargedSeen;
    static readonly StringBuilder log = new StringBuilder();
    static GameObject hero;
    static PlayerProgression progression;
    static PlayerHealth health;
    static AutoShoter shooter;
    static readonly List<Health> dummies = new List<Health>();
    static readonly List<int> marks = new List<int>();
    static float dashDistance;

    static NewCardsChecks()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && File.Exists(Marker))
            {
                File.Delete(Marker); started = EditorApplication.timeSinceStartup; step = 0;
                Application.logMessageReceived += (m, st, type) => { if (type == LogType.Exception || type == LogType.Error) { errors++; log.AppendLine("ERROR " + m + "\n" + st); } };
                EditorApplication.update += Tick;
            }
        };
    }

    public static void RunBatch()
    {
        File.WriteAllText(Marker, "1");
        EditorSceneManager.OpenScene("Assets/Scenes/GreenArenaPlay.unity");
        EditorApplication.isPlaying = true;
    }

    static void Check(bool ok, string what)
    {
        if (!ok) failures++;
        log.AppendLine((ok ? "PASS " : "FAIL ") + what);
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        bool ok = failures == 0 && errors == 0;
        log.AppendLine("failures=" + failures + " runtime errors=" + errors);
        File.WriteAllText(Report, (ok ? "OK" : "FAIL") + "\n" + log);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static object Field(object target, string name) { return target.GetType().GetField(name, Any).GetValue(target); }
    static void SetField(object target, string name, object value) { target.GetType().GetField(name, Any).SetValue(target, value); }
    static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Any).Invoke(target, args); }
    static HeroStats Stats => progression.Stats;

    static void Ranks(Dictionary<string, int> values)
    {
        var ranks = (Dictionary<string, int>)Field(progression, "ranks");
        ranks.Clear();
        foreach (var pair in values) ranks[pair.Key] = pair.Value;
        Call(progression, "ApplyStats");
    }

    // Чистый урон по герою: без неуязвимости и брони, с полным HP.
    static int Hurt(int damage, HeroDamageCause cause)
    {
        SetField(health, "invulnerableUntil", 0f);
        health.Heal(100000);
        int before = health.CurrentHP;
        health.TakeDamage(damage, cause);
        return before - health.CurrentHP;
    }

    static GameObject template;

    // Волны выключены; манекены — копии первого врага, неподвижные и очень живучие.
    static bool PrepareTemplate()
    {
        if (template != null) return true;
        EnemyAI first = null;
        foreach (EnemyAI e in EnemyAI.All) if (e != null) { first = e; break; }
        if (first == null) return false;
        first.gameObject.SetActive(false);   // копия неактивного объекта не регистрируется среди врагов
        template = Object.Instantiate(first.gameObject);
        template.name = first.name.StartsWith("Elite") ? "Dummy" : first.name;
        foreach (WaveManager waves in Object.FindObjectsOfType<WaveManager>()) waves.enabled = false;
        var alive = new List<EnemyAI>(EnemyAI.All);
        foreach (EnemyAI e in alive) if (e != null) Object.Destroy(e.gameObject);
        Object.Destroy(first.gameObject);
        return true;
    }

    static Health Dummy(Vector3 at, string name = null)
    {
        GameObject copy = Object.Instantiate(template, new Vector3(at.x, template.transform.position.y, at.z), Quaternion.identity);
        copy.SetActive(true);
        if (name != null) copy.name = name;
        EnemyAI ai = copy.GetComponent<EnemyAI>();
        ai.target = hero.transform;
        ai.MovementLocked = true;
        Health h = copy.GetComponent<Health>();
        h.SetHealth(100000);
        dummies.Add(h);
        return h;
    }

    static void ClearDummies()
    {
        foreach (Health h in dummies) if (h != null) Object.Destroy(h.gameObject);
        dummies.Clear();
        marks.Clear();
    }

    static Vector3 Near(float x, float z) { return hero.transform.position + new Vector3(x, 0f, z); }

    static void Tick()
    {
        double t = EditorApplication.timeSinceStartup - started;
        if (t > 120) { log.AppendLine("timeout at step " + step); failures++; Finish(); return; }
        if (hero == null) hero = GameObject.Find("Capsule");
        if (hero == null) return;
        if (GameSession.Instance != null && GameSession.Instance.State == GameRunState.Upgrade)
        {
            GameObject levelUp = GameObject.Find("Level Up");
            if (levelUp != null)
                foreach (UnityEngine.UI.Button card in levelUp.GetComponentsInChildren<UnityEngine.UI.Button>())
                    if (card.interactable) { card.onClick.Invoke(); break; }
        }
        if (step >= 2 && health != null && !health.IsDead && step != 7) { health.GrantInvulnerability(0.5f); }
        if (step >= 2 && shooter != null && step != 12) shooter.enabled = false;
        if (step == 12 && (bool)Field(shooter, "chargedVolley")) chargedSeen++;

        if (step == 0 && t > 1.5)
        {
            MetaProgress.UseMemory();
            hero.GetComponent<StartMenu>().SendMessage("BeginGame", (object)0);
            step = 1; stepAt = t + 3;
        }
        else if (step == 1 && t > stepAt)
        {
            if (!PrepareTemplate()) { if (t > stepAt + 20) { Check(false, "no enemies to copy"); Finish(); } return; }
            progression = hero.GetComponent<PlayerProgression>();
            health = hero.GetComponent<PlayerHealth>();
            shooter = hero.GetComponent<AutoShoter>();
            Check(hero.GetComponent<HeroCardEffects>() != null, "HeroCardEffects added to hero");
            // Все новые карточки на максимальном ранге.
            var all = new Dictionary<string, int>();
            var catalog = (UpgradeCatalog)Field(progression, "catalog");
            int newCards = 0;
            foreach (UpgradeDefinition d in catalog.entries)
                if ((int)d.effect >= (int)UpgradeEffect.Dodge) { all[d.id] = d.maxRanks; newCards++; }
            foreach (UpgradeDefinition d in PlayerProgression.SynergyUpgrades)
                if ((int)d.effect >= (int)UpgradeEffect.Dodge) { all[d.id] = 1; newCards++; }
            Check(newCards == 18, "18 new cards in catalog + synergies (found " + newCards + ")");
            Ranks(all);
            Check(Mathf.Approximately(Stats.Dodge, 0.24f) && Mathf.Approximately(Stats.ProjectileGuard, 0.4f) &&
                Mathf.Approximately(Stats.DashInvulnerabilityBonus, 0.6f) && Stats.TrophyParts == 3, "defence stats: dodge " + Stats.Dodge + " guard " + Stats.ProjectileGuard + " dash " + Stats.DashInvulnerabilityBonus + " trophy " + Stats.TrophyParts);
            Check(Stats.BurnRank == 3 && Stats.PoisonRank == 3 && Stats.FrostRank == 3 && Stats.ShockRank == 3, "element ranks 3/3/3/3");
            Check(Stats.SplitShot == 4 && Stats.Retaliate == 3 && Stats.PulseAura == 4 && Stats.ChargedShot == 3, "split " + Stats.SplitShot + " retaliate " + Stats.Retaliate + " aura " + Stats.PulseAura + " charged " + Stats.ChargedShot);
            Check(Stats.LastChance && Stats.Gamble && Stats.CritPierce && Stats.BurningShot && Stats.BlastDash, "flags last chance/gamble/crit pierce/burning shot/blast dash");
            Check(Stats.DropLuck >= 0.99f && Mathf.Approximately(Stats.EnemySpeedCurse, 1.15f) && Stats.ExperienceGain >= 0.29f, "gamble luck " + Stats.DropLuck + ", time greed speed " + Stats.EnemySpeedCurse + " xp " + Stats.ExperienceGain);
            // Тексты карточек и иконки.
            MethodInfo preview = typeof(PlayerProgression).GetMethod("Preview", Any);
            var defs = new List<UpgradeDefinition>();
            foreach (UpgradeDefinition d in catalog.entries) if ((int)d.effect >= (int)UpgradeEffect.Dodge) defs.Add(d);
            foreach (UpgradeDefinition d in PlayerProgression.SynergyUpgrades) if ((int)d.effect >= (int)UpgradeEffect.Dodge) defs.Add(d);
            int texts = 0, icons = 0;
            foreach (UpgradeDefinition d in defs)
            {
                string text = (string)preview.Invoke(progression, new object[] { d });
                if (!string.IsNullOrEmpty(text) && !text.Contains("+0% → +0%")) texts++; else log.AppendLine("   bad text " + d.id + ": " + text);
                if (UiArtwork.Upgrade(d.effect) != null) icons++; else log.AppendLine("   no icon " + d.effect);
            }
            Check(texts == defs.Count && icons == defs.Count, "card texts " + texts + "/" + defs.Count + ", icons " + icons + "/" + defs.Count);
            // Выпадение: новые карточки встречаются в окнах выбора.
            Ranks(new Dictionary<string, int>());
            typeof(PlayerProgression).GetField("<Level>k__BackingField", Any).SetValue(progression, 12);
            var seen = new HashSet<UpgradeEffect>();
            for (int i = 0; i < 300; i++)
            {
                progression.RollOffers();
                foreach (UpgradeDefinition d in progression.Offered) if ((int)d.effect >= (int)UpgradeEffect.Dodge) seen.Add(d.effect);
            }
            Check(seen.Count >= 12, "new cards offered across 300 rolls: " + seen.Count + " kinds");
            // Улучшения того, чего у героя ещё нет, не выпадают раньше основной карточки.
            var dependent = new[] { UpgradeEffect.GrenadeRadius, UpgradeEffect.TurretDamage, UpgradeEffect.DroneDamage, UpgradeEffect.CritPower,
                UpgradeEffect.OrbitDamage, UpgradeEffect.OrbitSpeed, UpgradeEffect.OrbitCrit, UpgradeEffect.CritPierce, UpgradeEffect.BurningShot,
                UpgradeEffect.BlastDash, UpgradeEffect.SteelWill };
            var early = new HashSet<UpgradeEffect>();
            for (int i = 0; i < 400; i++)
            {
                progression.RollOffers();
                foreach (UpgradeDefinition d in progression.Offered) if (System.Array.IndexOf(dependent, d.effect) >= 0) early.Add(d.effect);
            }
            Check(early.Count == 0, "no dependent cards without their base (offered early: " + string.Join(",", early) + ")");
            Check(!shooter.GrenadeUnlocked, "grenades locked without the Grenades card");
            Ranks(new Dictionary<string, int> { { "grenade", 1 }, { "turret_count", 1 }, { "drone_count", 1 }, { "crit_chance", 1 } });
            var unlocked = new HashSet<UpgradeEffect>();
            for (int i = 0; i < 400; i++)
            {
                progression.RollOffers();
                foreach (UpgradeDefinition d in progression.Offered) unlocked.Add(d.effect);
            }
            Check(unlocked.Contains(UpgradeEffect.GrenadeRadius) && unlocked.Contains(UpgradeEffect.TurretDamage) &&
                unlocked.Contains(UpgradeEffect.DroneDamage) && unlocked.Contains(UpgradeEffect.CritPower) && shooter.GrenadeUnlocked,
                "after base cards their upgrades appear (radius, turret/drone damage, crit power)");
            Ranks(new Dictionary<string, int>());
            step = 2; stepAt = t + 0.5;
        }
        else if (step == 2 && t > stepAt)
        {
            Ranks(new Dictionary<string, int>());
            Stats.Armor = 0f;
            Check(Hurt(20, HeroDamageCause.Contact) == 20, "baseline: contact 20 → lost 20");
            Stats.Dodge = 1f;
            Check(Hurt(20, HeroDamageCause.Contact) == 0, "dodge 100%: no damage");
            Stats.Dodge = 0f;
            Stats.ProjectileGuard = 0.4f;
            int shot = Hurt(50, HeroDamageCause.Projectile), touch = Hurt(50, HeroDamageCause.Contact);
            Check(shot == 30 && touch == 50, "projectile guard 40%: projectile " + shot + ", contact " + touch);
            Stats.ProjectileGuard = 0f;
            Stats.LastChance = true;
            SetField(health, "invulnerableUntil", 0f);
            health.Heal(100000);
            health.TakeDamage(100000, HeroDamageCause.Contact);
            Check(!health.IsDead && health.LastChanceUsed && Mathf.Abs(health.CurrentHP - health.maxHP * 0.3f) <= 1.5f, "last chance: alive with " + health.CurrentHP + "/" + health.maxHP);
            Stats.LastChance = false;
            health.Heal(100000);
            step = 3; stepAt = t + 0.2;
        }
        else if (step == 3 && t > stepAt)
        {
            // Стихии: зажигательные, ядовитые, ледяные и шоковые пули через общий путь попадания.
            Health a = Dummy(Near(3f, 0f)), b = Dummy(Near(4.5f, 0f));
            Stats.BurnRank = Stats.PoisonRank = Stats.FrostRank = Stats.ShockRank = 3;
            int bBefore = b.CurrentHealth;
            shooter.ElementAttack.Hit(a, 200);
            Check(a.GetComponent<ElementStatus>() != null, "burn/poison status on target");
            Check(SlowEffect.MovementFactor(a.GetComponent<EnemyAI>()) < 0.7f, "frost slow " + SlowEffect.MovementFactor(a.GetComponent<EnemyAI>()).ToString("0.00"));
            Check(b.CurrentHealth < bBefore, "shock chain hit neighbour: " + (bBefore - b.CurrentHealth));
            marks.Add(a.CurrentHealth);
            step = 4; stepAt = t + 1.5;
        }
        else if (step == 4 && t > stepAt)
        {
            Health a = dummies[0];
            Check(a.CurrentHealth < marks[0], "damage over time ticks: " + (marks[0] - a.CurrentHealth));
            Stats.BurnRank = Stats.PoisonRank = Stats.FrostRank = Stats.ShockRank = 0;
            ClearDummies();
            // Разделение пули: попадание порождает осколки.
            Health target = Dummy(Near(3f, 0f));
            Stats.SplitShot = 3;
            GameObject prefab = shooter.baseWeapon.bulletPrefab;
            GameObject go = Object.Instantiate(prefab, target.transform.position - Vector3.forward * 0.2f + Vector3.up, Quaternion.LookRotation(Vector3.forward));
            Bullet bullet = go.GetComponent<Bullet>();
            bullet.SetOwner(hero.transform);
            bullet.stats = Stats;
            bullet.elementAttack = shooter.ElementAttack;
            bullet.split = Stats.SplitShot;
            bullet.splitPrefab = prefab;
            bullet.damage = 100;
            bullet.speed = 30f;
            bullet.lifeTime = 1f;
            SetField(bullet, "rb", go.GetComponent<Rigidbody>());
            go.GetComponent<Rigidbody>().velocity = Vector3.forward * 30f;
            int before = Object.FindObjectsOfType<Bullet>().Length;
            typeof(Bullet).GetMethod("OnTriggerEnter", Any).Invoke(bullet, new object[] { target.GetComponentInChildren<Collider>() });
            int after = Object.FindObjectsOfType<Bullet>().Length;
            int fragments = 0;
            foreach (Bullet other in Object.FindObjectsOfType<Bullet>()) if (other != bullet && other.damage == 35) fragments++;
            Check(fragments == 3, "split shot: " + fragments + " fragments at 35% (bullets " + before + " → " + after + ")");
            Stats.SplitShot = 0;
            ClearDummies();
            step = 5; stepAt = t + 0.3;
        }
        else if (step == 5 && t > stepAt)
        {
            // Ответный импульс: удар по герою бьёт и отталкивает соседей.
            Dummy(Near(2f, 0f));
            Stats.Retaliate = 3;
            marks.Add(dummies[0].CurrentHealth);
            Hurt(5, HeroDamageCause.Contact);
            Check(dummies[0].CurrentHealth < marks[0], "retaliate pulse damage " + (marks[0] - dummies[0].CurrentHealth));
            marks[0] = dummies[0].CurrentHealth;
            Hurt(5, HeroDamageCause.Contact);
            Check(dummies[0].CurrentHealth == marks[0], "retaliate cooldown blocks a second pulse");
            Stats.Retaliate = 0;
            // Импульсная аура: урон сам, раз в 2 с.
            Stats.PulseAura = 2;
            step = 6; stepAt = t + 0.6;
        }
        else if (step == 6 && t > stepAt)
        {
            Check(dummies[0].CurrentHealth < marks[0], "pulse aura damage " + (marks[0] - dummies[0].CurrentHealth));
            Stats.PulseAura = 0;
            marks[0] = dummies[0].CurrentHealth;
            // Взрывной рывок и щит рывка.
            Stats.BlastDash = true;
            Stats.DashInvulnerabilityBonus = 0.6f;
            dashDistance = hero.GetComponent<PlayMovement>().dashDistance;
            hero.GetComponent<PlayMovement>().dashDistance = 0.3f;   // взрыв рядом с манекеном
            SetField(health, "invulnerableUntil", 0f);
            hero.GetComponent<PlayMovement>().RequestDash();
            step = 7; stepAt = t + 0.15;
        }
        else if (step == 7 && t > stepAt)
        {
            float until = (float)Field(health, "invulnerableUntil") - Time.time;
            Check(until > 0.55f, "dash shield: invulnerability left " + until.ToString("0.00") + " s (base 0.4 + 0.6)");
            step = 8; stepAt = t + 0.6;
        }
        else if (step == 8 && t > stepAt)
        {
            Check(dummies[0] != null && dummies[0].CurrentHealth < marks[0], "blast dash damage " + (dummies[0] != null ? marks[0] - dummies[0].CurrentHealth : 0));
            Stats.BlastDash = false;
            Stats.DashInvulnerabilityBonus = 0f;
            hero.GetComponent<PlayMovement>().dashDistance = dashDistance;
            ClearDummies();
            // Трофей: элита даёт Детали.
            Stats.TrophyParts = 2;
            CombatStats.RunTrophyParts = 0;
            Health elite = Dummy(Near(3f, 2f), "Elite trophy dummy");
            elite.TakeDamage(1000000);
            Check(CombatStats.RunTrophyParts == 2, "trophy: elite gave " + CombatStats.RunTrophyParts + " parts");
            Stats.TrophyParts = 0;
            dummies.Clear();
            // Азарт: аптечки становятся магнитами.
            Stats.Gamble = true;
            DropPickup.Clear();
            // Предметы, выпавшие раньше (например, с элиты-манекена), удаляются только в конце кадра — не считаем их.
            var earlier = new HashSet<DropPickup>(Object.FindObjectsOfType<DropPickup>());
            for (int i = 0; i < 40; i++) DropPickup.TryDrop(Near(6f, i * 0.3f), true);
            int kits = 0, magnets = 0;
            foreach (DropPickup p in Object.FindObjectsOfType<DropPickup>()) if (earlier.Contains(p)) continue; else if (p.Kind == PickupKind.Health) kits++; else magnets++;
            Check(kits == 0 && magnets > 10, "gamble: kits " + kits + ", magnets " + magnets);
            Stats.Gamble = false;
            DropPickup.Clear();
            step = 9; stepAt = t + 0.3;
        }
        else if (step == 9 && t > stepAt)
        {
            // Заряженный выстрел: стрельба по живой цели, счётчик выстрелов.
            Dummy(Near(5f, 0f));
            Ranks(new Dictionary<string, int> { { "charged_shot", 3 } });
            shooter.enabled = true;
            step = 12; stepAt = t + 5;
        }
        else if (step == 12 && t > stepAt)
        {
            Check(chargedSeen > 0, "charged shot fired (frames with charged volley: " + chargedSeen + ")");
            Stats.ChargedShot = 0;
            ClearDummies();
            log.AppendLine("state=" + GameSession.Instance.State);
            Finish();
        }
    }
}
