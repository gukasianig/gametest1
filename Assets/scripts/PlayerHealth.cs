
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    bool isDead = false;
    public int maxHP = 100;
    int currentHP;

    public TextMeshProUGUI hpText;
    public GameObject gameOverUI;

    void Start()
    {
        currentHP = maxHP;
        UpdateUI();

        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }

    public void TakeDamage(int damage)
{
    Debug.Log("DAMAGE CALLED");

    currentHP -= damage;
    currentHP = Mathf.Max(currentHP, 0);

    UpdateUI();

    if (currentHP <= 0)
    {
        Die();
    }
}

    void UpdateUI()
    {
        if (hpText != null)
            hpText.text = "HP: " + currentHP;
            Debug.Log("Updating UI");
    }

    void Die()
{
    if (isDead) return;
    isDead = true;

    Debug.Log("PLAYER DEAD");

    if (gameOverUI != null)
        gameOverUI.SetActive(true);

    Time.timeScale = 0f;
}
   


}
