using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Health : MonoBehaviour
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

    public void TakeDamage(int damage)
    {
        
        if (isDead) return;
        currentHealth -=damage;
        UpdateHPText();
        Debug.Log("HP:" + currentHealth);
            if (currentHealth <= 0)
            {
                Die();
            }
             
    }
    void Die()
    {
        Debug.Log("Dead");
        Destroy(gameObject);
    }
}
