using UnityEngine;
using UnityEngine.UI;

// Messages qui apparaissent juste au-dessus du tiroir (2 au plus, 2,6 s) : ils gonflent, restent, puis montent en s'effaçant.
public class IdleToasts : MonoBehaviour
{
    [System.Serializable]
    struct Slot
    {
        public RectTransform rect;
        public Text text;
        public CanvasGroup group;
    }

    [SerializeField] Slot[] slots;
    [SerializeField] RectTransform drawerPanel;
    [SerializeField, Min(0.1f)] float lifetime = 2.6f;
    [SerializeField] float gapAboveDrawer = 72f;
    [SerializeField] float slotHeight = 215f;
    [SerializeField] float rise = 94f;

    float[] elapsed;

    void Awake()
    {
        elapsed = new float[slots.Length];
    }

    public void Show(string text)
    {
        int chosen = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].rect.gameObject.activeSelf) { chosen = i; break; }
            if (elapsed[i] > elapsed[chosen]) chosen = i;     // tout est pris : on remplace le plus ancien
        }
        slots[chosen].text.text = text;
        slots[chosen].rect.gameObject.SetActive(true);
        elapsed[chosen] = 0f;
        Animate(chosen);
    }

    void Update()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].rect.gameObject.activeSelf) continue;
            elapsed[i] += Time.unscaledDeltaTime;
            if (elapsed[i] >= lifetime) slots[i].rect.gameObject.SetActive(false);
            else Animate(i);
        }
    }

    // Le tiroir a changé de hauteur : les messages visibles se replacent au-dessus de lui.
    public void Reposition()
    {
        for (int i = 0; i < slots.Length; i++) if (slots[i].rect.gameObject.activeSelf) Animate(i);
    }

    void Animate(int i)
    {
        float t = elapsed[i] / lifetime;
        bool calm = MotionSettings.ReduceMotion;
        float scale = calm ? 1f : t < 0.1f ? Mathf.Lerp(0.4f, 1.12f, t / 0.1f) : t < 0.16f ? Mathf.Lerp(1.12f, 1f, (t - 0.1f) / 0.06f) : 1f;
        float leaving = Mathf.InverseLerp(0.85f, 1f, t);
        slots[i].group.alpha = Mathf.Min(Mathf.Clamp01(t / 0.1f), 1f - leaving);
        slots[i].rect.localScale = new Vector3(scale, scale, 1f);
        float baseY = drawerPanel.offsetMax.y + gapAboveDrawer + i * slotHeight;
        slots[i].rect.anchoredPosition = new Vector2(0f, baseY + (calm ? 0f : leaving * rise));
    }
}
