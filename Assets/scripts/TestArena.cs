using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Тестовая арена из главного меню: волн и Стража нет, врагов и оружие выбирает владелец.
// Левая панель — оружие (переключается на лету), правая — враги (клик создаёт выбранное число рядом с героем).
[DefaultExecutionOrder(900)]
public class TestArena : MonoBehaviour
{
    private const int RowsPerPage = 8;
    private static readonly int[] Amounts = { 1, 5, 10, 25 };

    private AutoShoter shooter;
    private WaveManager waves;
    private PlayerHealth health;
    private Canvas canvas;
    private int amountIndex;
    private bool immortal = true;
    private Text amountLabel, immortalLabel, statusLabel;
    private readonly List<Button> weaponButtons = new List<Button>();
    private int weaponPage, enemyPage;
    private List<int> weaponIndexes = new List<int>();
    private RectTransform weaponList, enemyList;
    private int currentWeapon;

    public void Build(int startWeapon)
    {
        shooter = GetComponent<AutoShoter>();
        health = GetComponent<PlayerHealth>();
        waves = FindObjectOfType<WaveManager>();
        currentWeapon = startWeapon;
        canvas = GameUi.Canvas("Test Arena UI", 120);

        Color panel = new Color(0.025f, 0.045f, 0.065f, 0.82f);
        GameUi.Label(canvas.transform, "ТЕСТОВАЯ АРЕНА  •  Esc — пауза и выход в меню", new Vector2(0.2f, 0.93f), new Vector2(0.8f, 0.99f), 20,
            new Color(1f, 0.65f, 0.25f));

        RectTransform left = GameUi.Box(canvas.transform, "Weapons", new Vector2(0.008f, 0.14f), new Vector2(0.2f, 0.92f), panel, true);
        GameUi.Label(left, "ОРУЖИЕ", new Vector2(0f, 0.93f), new Vector2(1f, 1f), 20, GameUi.Accent);
        weaponList = GameUi.Box(left, "List", new Vector2(0f, 0.1f), new Vector2(1f, 0.93f), Color.clear);
        RectTransform right = GameUi.Box(canvas.transform, "Enemies", new Vector2(0.8f, 0.14f), new Vector2(0.992f, 0.92f), panel, true);
        GameUi.Label(right, "ВРАГИ", new Vector2(0f, 0.93f), new Vector2(1f, 1f), 20, new Color(1f, 0.5f, 0.4f));
        enemyList = GameUi.Box(right, "List", new Vector2(0f, 0.1f), new Vector2(1f, 0.93f), Color.clear);

        weaponIndexes.Clear();
        if (shooter != null && shooter.weapons != null)
            for (int i = 0; i < shooter.weapons.Length; i++)
                if (shooter.weapons[i] != null && shooter.weapons[i].IsSelectable) weaponIndexes.Add(i);

        PageButtons(left, () => { weaponPage++; FillWeapons(); }, () => { weaponPage--; FillWeapons(); });
        PageButtons(right, () => { enemyPage++; FillEnemies(); }, () => { enemyPage--; FillEnemies(); });

        RectTransform bottom = GameUi.Box(canvas.transform, "Controls", new Vector2(0.21f, 0.015f), new Vector2(0.79f, 0.115f), panel, true);
        Button amount = GameUi.Button(bottom, "", new Vector2(0.01f, 0.1f), new Vector2(0.26f, 0.9f), () =>
        {
            amountIndex = (amountIndex + 1) % Amounts.Length;
            RefreshLabels();
        });
        amountLabel = amount.GetComponentInChildren<Text>();
        Button clear = GameUi.Button(bottom, "Убрать врагов", new Vector2(0.28f, 0.1f), new Vector2(0.52f, 0.9f), ClearEnemies);
        Button immortalButton = GameUi.Button(bottom, "", new Vector2(0.54f, 0.1f), new Vector2(0.78f, 0.9f), () =>
        {
            immortal = !immortal;
            RefreshLabels();
        });
        immortalLabel = immortalButton.GetComponentInChildren<Text>();
        Button heal = GameUi.Button(bottom, "Вылечить", new Vector2(0.8f, 0.1f), new Vector2(0.99f, 0.9f), () =>
        {
            if (health != null) health.Heal(health.maxHP);
        });
        foreach (Button b in new[] { amount, clear, immortalButton, heal }) UiArtwork.Round(b.GetComponent<Image>());
        statusLabel = GameUi.Label(canvas.transform, "", new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.17f), 16,
            new Color(0.62f, 0.69f, 0.77f));

        FillWeapons();
        FillEnemies();
        RefreshLabels();
    }

    private void PageButtons(RectTransform parent, UnityEngine.Events.UnityAction next, UnityEngine.Events.UnityAction previous)
    {
        Button prev = GameUi.Button(parent, "◄", new Vector2(0.05f, 0.01f), new Vector2(0.45f, 0.09f), previous);
        Button nxt = GameUi.Button(parent, "►", new Vector2(0.55f, 0.01f), new Vector2(0.95f, 0.09f), next);
        UiArtwork.Round(prev.GetComponent<Image>());
        UiArtwork.Round(nxt.GetComponent<Image>());
    }

    private void Clear(RectTransform list)
    {
        for (int i = list.childCount - 1; i >= 0; i--) Destroy(list.GetChild(i).gameObject);
    }

    private static int Wrap(int page, int count)
    {
        int pages = Mathf.Max(1, Mathf.CeilToInt(count / (float)RowsPerPage));
        return ((page % pages) + pages) % pages;
    }

    private void FillWeapons()
    {
        weaponPage = Wrap(weaponPage, weaponIndexes.Count);
        Clear(weaponList);
        weaponButtons.Clear();
        float pitch = 1f / RowsPerPage;
        for (int row = 0; row < RowsPerPage; row++)
        {
            int n = weaponPage * RowsPerPage + row;
            if (n >= weaponIndexes.Count) break;
            int index = weaponIndexes[n];
            WeaponData weapon = shooter.weapons[index];
            string title = !string.IsNullOrEmpty(weapon.title) ? weapon.title : weapon.weaponName;
            float top = 1f - row * pitch;
            Button button = GameUi.Button(weaponList, title, new Vector2(0.04f, top - pitch + 0.008f), new Vector2(0.96f, top - 0.008f),
                () => PickWeapon(index));
            button.GetComponent<Image>().color = index == currentWeapon ? new Color(0.12f, 0.45f, 0.4f) : new Color(0.08f, 0.27f, 0.31f);
            button.GetComponentInChildren<Text>().fontSize = 17;
            UiArtwork.Round(button.GetComponent<Image>());
            weaponButtons.Add(button);
        }
    }

    private void FillEnemies()
    {
        EnemyType[] types = waves != null ? waves.enemies : null;
        int count = types == null ? 0 : types.Length;
        enemyPage = Wrap(enemyPage, count);
        Clear(enemyList);
        float pitch = 1f / RowsPerPage;
        for (int row = 0; row < RowsPerPage; row++)
        {
            int n = enemyPage * RowsPerPage + row;
            if (n >= count) break;
            EnemyType type = types[n];
            if (type == null || type.prefab == null) continue;
            int index = n;
            float top = 1f - row * pitch;
            Button button = GameUi.Button(enemyList, string.IsNullOrEmpty(type.name) ? type.prefab.name : type.name,
                new Vector2(0.04f, top - pitch + 0.008f), new Vector2(0.96f, top - 0.008f), () => SpawnEnemy(index));
            button.GetComponentInChildren<Text>().fontSize = 17;
            button.GetComponent<Image>().color = new Color(0.31f, 0.14f, 0.12f);
            UiArtwork.Round(button.GetComponent<Image>());
        }
    }

    private void PickWeapon(int index)
    {
        if (shooter == null || !shooter.SelectWeapon(index)) return;
        currentWeapon = index;
        WeaponData weapon = shooter.weapons[index];
        statusLabel.text = "Оружие: " + (!string.IsNullOrEmpty(weapon.title) ? weapon.title : weapon.weaponName);
        FillWeapons();
    }

    private void SpawnEnemy(int typeIndex)
    {
        if (waves == null) return;
        int made = waves.SpawnTest(typeIndex, Amounts[amountIndex], transform.position);
        statusLabel.text = "Создано: " + made + " × " + waves.enemies[typeIndex].name + "   •   врагов на арене: " + EnemyAI.ActiveCount;
    }

    private void ClearEnemies()
    {
        foreach (EnemyAI enemy in FindObjectsOfType<EnemyAI>()) Destroy(enemy.gameObject);
        statusLabel.text = "Арена очищена";
    }

    private void RefreshLabels()
    {
        amountLabel.text = "За клик: " + Amounts[amountIndex];
        immortalLabel.text = immortal ? "Бессмертие: вкл" : "Бессмертие: выкл";
    }

    private void Update()
    {
        if (immortal && health != null && !health.IsDead) health.GrantInvulnerability(0.5f);
        if (canvas != null)
        {
            GameSession session = GameSession.Instance;
            canvas.enabled = session != null && session.State == GameRunState.Playing;
        }
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
