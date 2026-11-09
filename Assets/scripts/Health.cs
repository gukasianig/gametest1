using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Health : MonoBehaviour, IDamagable
{
    public event System.Action<Health> Died;
    public TextMeshPro hpText;

    public int maxHealth = 100;
    [Min(0)] public int experienceReward = 5;
    int currentHealth;

    // Set by the weapon code around a single TakeDamage call so the damage number can show a critical hit.
    public static bool NextHitCrit;
    // Громкость звука попадания для частого урона (огнемёт, горение): оружие ставит её вокруг своих TakeDamage.
    public static float HitSoundScale = 1f;
    public bool isDead = false;
    // Armored enemies set this below 1 to take reduced damage.
    public float DamageTakenFactor = 1f;
    public int CurrentHealth => currentHealth;
    private EnemyHealthDisplay display;
    private ModelWalkAnimator animator;

    void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    void Start()
    {
        if (CompareTag("Enemy"))
        {
            display = EnemyHealthDisplay.GetOrCreate();
            display.Register(this);
        }
        UpdateHPText();
    }
    void UpdateHPText()
    {
        if (hpText != null)
        {
            hpText.text = currentHealth.ToString();
        }
        if (display != null) display.Refresh(this);
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0) return;
        if (DamageTakenFactor < 0.999f) damage = Mathf.Max(1, Mathf.RoundToInt(damage * DamageTakenFactor));

        int receivedDamage = Mathf.Min(currentHealth, damage);
        currentHealth -= receivedDamage;
        if (CompareTag("Enemy") && RunStatistics.Instance != null)
            RunStatistics.Instance.RecordDamage(receivedDamage);
        if (display == null && CompareTag("Enemy"))
        {
            display = EnemyHealthDisplay.GetOrCreate();
            display.Register(this);
        }
        if (display != null) display.ShowDamage(this, receivedDamage, NextHitCrit);
        if (CompareTag("Enemy") && HitSoundScale > 0.01f) GameAudio.Play(Sfx.Hit, 0.4f * HitSoundScale);
        if (CompareTag("Enemy") && currentHealth > 0)
        {
            if (animator == null) animator = ModelWalkAnimator.Of(this);   // модель-скин ставится после Awake
            if (animator != null) animator.Flinch();
        }

        UpdateHPText();
            if (currentHealth <= 0)
            {
                Die();
            }

    }
    public void SetHealth(int value)
{
    maxHealth = Mathf.Max(1, value);
    currentHealth = maxHealth;
    UpdateHPText();
}
    public int Heal(int amount)
    {
        if (isDead || amount <= 0) return 0;
        int restored = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += restored;
        if (restored > 0) UpdateHPText();
        return restored;
    }

    void Die()
    {
        isDead = true;
        if (CompareTag("Enemy")) GameAudio.Play(Sfx.Kill, 0.5f);
        if (CompareTag("Enemy") && RunStatistics.Instance != null)
            RunStatistics.Instance.RecordKill();
        if (CompareTag("Enemy") && CombatStats.VampirismPerKill > 0f && PlayerProgression.Instance != null)
        {
            PlayerHealth hero = PlayerProgression.Instance.GetComponent<PlayerHealth>();
            if (hero != null) hero.OnEnemyKilled();
        }
        if (CompareTag("Enemy") && experienceReward > 0) DropPickup.TryDrop(transform.position, name.StartsWith("Elite"));
        if (CompareTag("Enemy")) ExperiencePickup.Spawn(transform.position, Mathf.RoundToInt(experienceReward * ArenaEvents.ExperienceFactor));
        Died?.Invoke(this);
        if (CompareTag("Enemy")) EnemyCorpse.Spawn(this);   // модель отцепляется и доигрывает смерть
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (display != null) display.Unregister(this);
    }

}
