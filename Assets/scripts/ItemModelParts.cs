using UnityEngine;

// Подвижные части моделей предметов из Blender (tools/blender/make_items.py, 11.11.2026): поиск по имени внутри SkinModel,
// поворот «головы» к цели вокруг вертикали и короткое появление модели. Без модели (примитивы) всё молча пропускается.
public static class ItemModelParts
{
    public static Transform Find(Transform root, string name)
    {
        Transform skin = root != null ? root.Find("SkinModel") : null;
        return skin != null ? FindDeep(skin, name) : null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    // Поворачивает pivot вокруг вертикали так, чтобы направление pivot → front смотрело на target (не больше maxDegrees).
    public static void AimYaw(Transform pivot, Vector3 front, Vector3 target, float maxDegrees)
    {
        Vector3 current = front - pivot.position, wanted = target - pivot.position;
        current.y = 0f;
        wanted.y = 0f;
        if (current.sqrMagnitude < 1e-6f || wanted.sqrMagnitude < 1e-6f) return;
        float angle = Mathf.Clamp(Vector3.SignedAngle(current, wanted, Vector3.up), -maxDegrees, maxDegrees);
        pivot.Rotate(Vector3.up, angle, Space.World);
    }

    // Медленное вращение вокруг вертикали (корона элиты).
    public sealed class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 60f;
        private void Update() { if (Time.deltaTime > 0f) transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World); }
    }

    // Падение с неба (ящик сброса припасов): ускоряется вниз, у земли — пыль, кольцо и лёгкая тряска.
    public sealed class Fall : MonoBehaviour
    {
        public Vector3 ground;
        public float height = 18f, duration = 0.8f;
        private float age;

        private void Start() { transform.position = ground + Vector3.up * height; }

        private void Update()
        {
            if (Time.deltaTime <= 0f) return;
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            transform.position = ground + Vector3.up * (height * (1f - t * t));
            if (t < 1f) return;
            EnemyFx.Ring(ground + Vector3.up * 0.05f, 2.2f, new Color(0.75f, 0.68f, 0.5f), 0.45f, 0.3f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                EnemyFx.Puff(ground + new Vector3(Mathf.Cos(a) * 0.8f, 0.2f, Mathf.Sin(a) * 0.8f), new Color(0.7f, 0.64f, 0.5f), 0.6f, 0.6f,
                    new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)) * 1.6f);
            }
            ScreenShake.AddAt(ground, 0.12f);
            Destroy(this);
        }
    }

    // Появление: модель «выпрыгивает» из 20% размера с небольшим перелётом за 0,25 с.
    public sealed class Pop
    {
        private readonly Transform skin;
        private readonly Vector3 size;
        private bool done;

        public Pop(Transform root)
        {
            skin = root != null ? root.Find("SkinModel") : null;
            if (skin == null) { done = true; return; }
            size = skin.localScale;
            skin.localScale = size * 0.2f;
        }

        public void Tick(float age)
        {
            if (done) return;
            float t = Mathf.Clamp01(age / 0.25f);
            float back = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f);
            skin.localScale = size * Mathf.LerpUnclamped(0.2f, 1f, back);
            if (t >= 1f) { skin.localScale = size; done = true; }
        }
    }
}
