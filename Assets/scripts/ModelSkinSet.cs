using System;
using UnityEngine;

// Необязательный набор внешних 3D-моделей (Quaternius). Лежит в Assets/Resources/ModelSkinSet.asset.
// Отключить: снять галочку enabled или удалить asset. Полное удаление описано в docs/CHANGELOG.md.
public sealed class ModelSkinSet : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string key;            // имя префаба врага (Enemy_Fast) или Weapon0..Weapon3
        public GameObject model;
        public Vector3 eulerOffset;   // поворот модели, чтобы перед смотрел в +Z
        public float sizeMultiplier = 1f;
        public Vector3 localPosition; // только для оружия: сдвиг относительно сокета
        public bool stretch;          // только для декораций арены: растянуть модель по осям до габарита исходного куба
        public bool centerOnHand;    // только для оружия: выровнять модель по длине относительно кисти
        public Vector3 muzzleOffset; // только для оружия: поправка точки выстрела (в системе героя, метры) к автоматически найденному дулу
    }

    // Декор арены: набор одинаковых объектов, разбросанных по карте (без коллайдеров, не мешает ходьбе).
    [Serializable]
    public sealed class Decor
    {
        public string label;
        public GameObject model;
        public float targetSize = 1.5f;   // длина наибольшей стороны, метры
        public int count = 10;
        public float minRadius = 14f;     // от центра арены
        public float maxRadius = 100f;
        public int cluster = 1;           // сколько штук ставить кучкой в одной точке
    }

    public bool enabled = true;
    public Entry[] entries = new Entry[0];
    public Decor[] decor = new Decor[0];

    public Entry Find(string key)
    {
        if (entries == null) return null;
        foreach (Entry e in entries)
            if (e != null && e.model != null && e.key == key) return e;
        return null;
    }
}
