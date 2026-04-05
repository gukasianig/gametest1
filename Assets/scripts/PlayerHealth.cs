
using UnityEngine;
using TMPro;
using UnityEngine.UI;




public class PlayerHealth : MonoBehaviour
{
    public Image hpImage;
    public Image hpDamageImage;
    //float damageSmooth = 0f;


    bool isDead = false;
    public int maxHP = 100;
    int currentHP;

   
    public GameObject gameOverUI;
    float smoothHP;
    void Start()
{
    Time.timeScale = 1f;
     currentHP = maxHP;
    smoothHP = maxHP;

    if (hpImage != null)
        hpImage.fillAmount = 1f;

    if (hpDamageImage != null)
        hpDamageImage.fillAmount = 1f;

    if (gameOverUI != null)
        gameOverUI.SetActive(false);
}

    public void TakeDamage(int damage)
{
    if (isDead) return;

    currentHP -= damage;
    currentHP = Mathf.Max(currentHP, 0);

    UpdateUI();
    
    //float t = (float)currentHP / maxHP;
    //hpFill.color = Color.Lerp(Color.red, Color.green, t);

    if (currentHP <= 0)
    {
        Die();
    }
}

    void UpdateUI()
    {
     if (hpImage == null || hpDamageImage == null)
    {
        Debug.LogError("HP UI not assigned!");
        return;
    }

    float target = (float)currentHP / maxHP;

    hpImage.color = Color.Lerp(Color.red, Color.green, target);
    }
    void Update()
    {

       if (hpImage == null || hpDamageImage == null) return;

    float target = (float)currentHP / maxHP;

    hpImage.fillAmount = Mathf.Lerp(
        hpImage.fillAmount,
        target,
        Time.deltaTime * 10f
    );

    hpDamageImage.fillAmount = Mathf.Lerp(
        hpDamageImage.fillAmount,
        target,
        Time.deltaTime * 3f
    );
        
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
