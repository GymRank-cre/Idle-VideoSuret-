using UnityEngine;
using UnityEngine.UI;

// Bas de l'écran (UL) : bandeau « Meilleur achat », sélecteur ×1/×10/MAX, loupe, et la fiche du poste sélectionné. Le reste de l'écran est le monde.
// Le bandeau et les lignes visibles sont mis à jour 5 fois par seconde et à chaque changement du jeu ; seules les barres de cycle bougent à chaque image.
public class IdleDock : MonoBehaviour
{
    [SerializeField] RectTransform area;                // zone sûre qui contient tout le bas de l'écran
    [SerializeField] RectTransform fiche;
    [SerializeField] IdleBestBar bestBar;
    [SerializeField] IdleStationRow[] rows;
    [SerializeField] IdleTutorialHand tutorial;
    [SerializeField] IdleSelection selection;
    [SerializeField] IdleCameraRig rig;
    [SerializeField] Button viewButton;
    [SerializeField] float barsTop = 360f;              // haut du bandeau et de la rangée du dessus, dans la zone sûre
    [SerializeField] float ficheTop = 571f;             // haut de la fiche ouverte
    [SerializeField] float ficheBottom = 360f;
    [SerializeField] float ficheSlide = 110f;
    [SerializeField, Min(0.01f)] float ficheSeconds = 0.18f;
    [SerializeField, Min(0.02f)] float refreshEvery = 0.2f;

    IdleGame subscribed;
    float timer, ficheTime;
    bool dirty = true;

    // Hauteur occupée en bas de l'écran, en unités du Canvas : les messages se posent juste au-dessus.
    public float TopHeight
    {
        get
        {
            float canvasHeight = ((RectTransform)area.parent).rect.height;
            return area.anchorMin.y * canvasHeight + (fiche.gameObject.activeSelf ? ficheTop : barsTop);
        }
    }

    void Start()
    {
        viewButton.onClick.AddListener(Overview);
    }

    void Overview()
    {
        selection.Deselect();
        rig.ToggleOverview();
    }

    public void ShowFiche(bool show)
    {
        fiche.gameObject.SetActive(show);
        ficheTime = MotionSettings.ReduceMotion ? ficheSeconds : 0f;
        dirty = true;
        SlideFiche();
    }

    void SlideFiche()
    {
        float t = Mathf.Clamp01(ficheTime / ficheSeconds);
        float eased = 1f - (1f - t) * (1f - t);
        fiche.anchoredPosition = new Vector2(fiche.anchoredPosition.x, ficheBottom - ficheSlide * (1f - eased));
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnChanged -= MarkDirty;
    }

    void MarkDirty() { dirty = true; }

    void Update()
    {
        if (ficheTime < ficheSeconds && fiche.gameObject.activeSelf)
        {
            ficheTime += Time.unscaledDeltaTime;
            SlideFiche();
        }
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        if (subscribed == null) { subscribed = game; game.OnChanged += MarkDirty; }

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f && !dirty) return;
        timer = refreshEvery;
        dirty = false;

        bool found, hasGoal;
        IdleEconomy.Option goal;
        IdleEconomy.Option best = game.Advice(out found, out hasGoal, out goal);
        bestBar.Refresh(game, best, found, hasGoal, goal);
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].gameObject.activeInHierarchy) rows[i].Refresh(game, best, found);
        tutorial.Refresh(game, best, found);
    }
}
