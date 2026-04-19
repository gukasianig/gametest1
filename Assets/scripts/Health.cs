using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Health : MonoBehaviour, IDamagable
{
    public TextMeshPro hpText;

    public int maxHealth = 100;
    int currentHealth;

    public bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHPText();
    }
    void UpdateHPText()
    {
        if (hpText != null)
        {
            hpText.text = currentHealth.ToString();
        }
    }

    public void TakeDamage(Damage dmg)
    {
        
        if (isDead) return;
        
        currentHealth -= dmg.amount;

        UpdateHPText();
        Debug.Log("HP:" + currentHealth);
            if (currentHealth <= 0)
            {
                Die();
            }
             
    }
    public void SetHealth(int value)
{
    maxHealth = value;
    currentHealth = value;
    UpdateHPText();
}
    void Die()
    {
        Debug.Log("Dead");
        Destroy(gameObject);
    }
    
}
