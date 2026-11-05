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

    public bool isDead = false;
    public int CurrentHealth => currentHealth;
    private EnemyHealthDisplay display;

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

        int receivedDamage = Mathf.Min(currentHealth, damage);
        currentHealth -= receivedDamage;
        if (CompareTag("Enemy") && RunStatistics.Instance != null)
            RunStatistics.Instance.RecordDamage(receivedDamage);
        if (display == null && CompareTag("Enemy"))
        {
            display = EnemyHealthDisplay.GetOrCreate();
            display.Register(this);
        }
        if (display != null) display.ShowDamage(this, receivedDamage);

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
        if (CompareTag("Enemy") && RunStatistics.Instance != null)
            RunStatistics.Instance.RecordKill();
        if (CompareTag("Enemy")) ExperiencePickup.Spawn(transform.position, experienceReward);
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (display != null) display.Unregister(this);
    }

}
