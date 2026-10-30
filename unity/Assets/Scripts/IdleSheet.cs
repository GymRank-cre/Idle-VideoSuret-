using UnityEngine;
using UnityEngine.UI;

// Fenêtre qui monte du bas (U3c) : voile sombre qui ferme au toucher, feuille orange (0,22 s), croix rouge. Le contenu est une liste qui défile ;
// la feuille prend la hauteur de son contenu, 92 % de l'écran au plus. Sa fenêtre (commandes, réputation, réglages) lui donne cette hauteur.
public class IdleSheet : MonoBehaviour
{
    [SerializeField] RectTransform sheet;
    [SerializeField] RectTransform body;              // zone visible de la liste
    [SerializeField] CanvasGroup group;
    [SerializeField] Button dim;
    [SerializeField] Button closeButton;
    [SerializeField, Min(0f)] float headerHeight = 238f;
    [SerializeField, Min(0f)] float bottomPadding = 50f;
    [SerializeField, Min(0f)] float overlap = 83f;    // le bas dépasse de l'écran : coins arrondis cachés
    [SerializeField, Range(0.3f, 1f)] float maxShare = 0.92f;
    [SerializeField, Min(0.01f)] float duration = 0.22f;
    [SerializeField, Min(0f)] float slide = 110f;

    float contentHeight;
    float elapsed;
    float fittedCanvasHeight;

    void Awake()
    {
        dim.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        elapsed = MotionSettings.ReduceMotion ? duration : 0f;
        Animate();
        Fit();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void SetContentHeight(float height)
    {
        contentHeight = height;
        Fit();
    }

    public void Fit()
    {
        float canvasHeight = ((RectTransform)transform).rect.height;
        fittedCanvasHeight = canvasHeight;
        float safe = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * canvasHeight : 0f;
        float height = Mathf.Min(headerHeight + contentHeight + bottomPadding + safe, canvasHeight * maxShare);
        sheet.sizeDelta = new Vector2(sheet.sizeDelta.x, height + overlap);
        body.offsetMin = new Vector2(body.offsetMin.x, overlap + safe + bottomPadding);
    }

    void Update()
    {
        if (!Mathf.Approximately(((RectTransform)transform).rect.height, fittedCanvasHeight)) Fit();   // l'écran a changé de forme
        if (elapsed >= duration) return;
        elapsed += Time.unscaledDeltaTime;
        Animate();
    }

    void Animate()
    {
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t);
        sheet.anchoredPosition = new Vector2(0f, -overlap - slide * (1f - eased));
        group.alpha = eased;
    }
}
