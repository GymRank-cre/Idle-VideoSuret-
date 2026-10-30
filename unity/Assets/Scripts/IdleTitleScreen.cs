using UnityEngine;
using UnityEngine.UI;

// Écran titre (UW-5) : image plein écran (le logo est dedans), bouton JOUER qui bat. Le toucher libère le son du navigateur,
// puis l'écran s'efface en 0,45 s avec un léger zoom. Il s'affiche à chaque lancement.
public class IdleTitleScreen : MonoBehaviour
{
    [SerializeField] RectTransform background;
    [SerializeField] CanvasGroup group;
    [SerializeField] Button playButton;
    [SerializeField] Transform buttonVisual;
    [SerializeField] float imageAspect = 1080f / 1910f;
    [SerializeField, Min(0.05f)] float fadeSeconds = 0.45f;
    [SerializeField, Min(1f)] float closeZoom = 1.06f;
    [SerializeField, Min(1f)] float pulseScale = 1.07f;
    [SerializeField, Min(0.1f)] float pulsePeriod = 1.1f;

    bool closing;
    float elapsed;
    Vector2 fittedArea;

    // « cover » : l'image remplit toute la zone sans se déformer ; ce qui dépasse est coupé (en bas et sur les côtés, le haut reste ancré).
    public static Vector2 CoverSize(Vector2 area, float aspect)
    {
        if (area.x / area.y > aspect) return new Vector2(area.x, area.x / aspect);
        return new Vector2(area.y * aspect, area.y);
    }

    void Start()
    {
        playButton.onClick.AddListener(Play);
    }

    void Play()
    {
        if (closing) return;
        closing = true;
        group.blocksRaycasts = false;
        if (AudioManager.Instance != null) AudioManager.Instance.Unlock();
        AudioManager.Play(SfxId.Unlock);
    }

    public void Fit()
    {
        fittedArea = ((RectTransform)transform).rect.size;
        background.sizeDelta = CoverSize(fittedArea, imageAspect);
    }

    void Update()
    {
        if (((RectTransform)transform).rect.size != fittedArea) Fit();
        bool calm = MotionSettings.ReduceMotion;

        if (!closing)
        {
            float k = calm ? 0f : 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / pulsePeriod);
            buttonVisual.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, k);
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / fadeSeconds);
        group.alpha = 1f - t;
        if (!calm) transform.localScale = Vector3.one * Mathf.Lerp(1f, closeZoom, t);
        if (t >= 1f) gameObject.SetActive(false);
    }
}
