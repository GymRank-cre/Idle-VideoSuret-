using UnityEngine;
using UnityEngine.UI;

// WebGL : les navigateurs bloquent le son avant un geste de l'utilisateur. Cet écran « Touche pour jouer » sert de premier
// geste : au toucher il disparaît et le son démarre. Hors WebGL il ne s'affiche jamais (sauf `forceInEditor`, pour les tests).
public class TapToPlayScreen : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Button tapButton;
    [SerializeField] RectTransform pulse;   // le texte bat doucement
    [SerializeField] bool forceInEditor;

    void Awake()
    {
        bool show = Application.platform == RuntimePlatform.WebGLPlayer || forceInEditor;
        root.SetActive(show);
        tapButton.onClick.AddListener(Tap);
    }

    void Tap()
    {
        root.SetActive(false);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Unlock();
            AudioManager.Play(SfxId.Click);
        }
    }

    void Update()
    {
        if (root.activeSelf)
            pulse.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f));
    }
}
