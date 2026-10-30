using UnityEngine;
using UnityEngine.UI;

// Puces qui s'envolent vers les pastilles du HUD (+$ vers l'argent, +Pro vers les Points Pro). Pool fixe ; coupées par « moins d'effets ».
public class IdleChips : MonoBehaviour
{
    [System.Serializable]
    struct Chip
    {
        public RectTransform rect;
        public Text text;
        public Image background;
        public CanvasGroup group;
    }

    public static IdleChips Instance { get; private set; }

    [SerializeField] Chip[] chips;
    [SerializeField] RectTransform cashTarget;
    [SerializeField] RectTransform proTarget;
    [SerializeField] Color moneyColor = new Color32(0x1F, 0x9D, 0x3A, 0xFF);
    [SerializeField] Color proColor = new Color32(0x0F, 0x6E, 0x7A, 0xFF);
    [SerializeField, Min(0.1f)] float duration = 0.7f;

    float[] elapsed;
    Vector3[] from;
    bool[] toPro;

    void Awake()
    {
        Instance = this;
        elapsed = new float[chips.Length];
        from = new Vector3[chips.Length];
        toPro = new bool[chips.Length];
    }

    public void Fly(Vector3 worldFrom, string text, bool pro)
    {
        if (MotionSettings.ReduceMotion) return;
        for (int i = 0; i < chips.Length; i++)
        {
            if (chips[i].rect.gameObject.activeSelf) continue;
            chips[i].text.text = text;
            chips[i].background.color = pro ? proColor : moneyColor;
            chips[i].rect.gameObject.SetActive(true);
            elapsed[i] = 0f;
            from[i] = worldFrom;
            toPro[i] = pro;
            Animate(i);
            return;
        }
    }

    // Argent dépensé : la puce part du bouton acheté vers la pastille d'argent.
    public static void Spent(Vector3 worldFrom, long price)
    {
        if (Instance != null) Instance.Fly(worldFrom, "−" + NumberFormat.Short(price), false);
    }

    void Update()
    {
        for (int i = 0; i < chips.Length; i++)
        {
            if (!chips[i].rect.gameObject.activeSelf) continue;
            elapsed[i] += Time.unscaledDeltaTime;
            if (elapsed[i] >= duration) chips[i].rect.gameObject.SetActive(false);
            else Animate(i);
        }
    }

    void Animate(int i)
    {
        float t = elapsed[i] / duration;
        float eased = t * t;                                    // part doucement, arrive vite
        RectTransform target = toPro[i] && proTarget.gameObject.activeInHierarchy ? proTarget : cashTarget;   // la pastille Pro peut apparaître en route
        chips[i].rect.position = Vector3.Lerp(from[i], target.position, eased);
        float scale = Mathf.Lerp(1f, 0.6f, eased);
        chips[i].rect.localScale = new Vector3(scale, scale, 1f);
        chips[i].group.alpha = Mathf.Lerp(1f, 0.1f, eased);
    }
}
