using UnityEngine;

// Tiroir du bas (U3b) : sa hauteur (47 % de l'écran, bornée, comme le web), la marge de la zone sûre, et le rafraîchissement du bandeau et des lignes.
// Tout est mis à jour 5 fois par seconde et à chaque changement du jeu ; seules les barres de cycle bougent à chaque image.
public class IdleDrawer : MonoBehaviour
{
    [SerializeField] RectTransform panel;
    [SerializeField] RectTransform list;                // zone de la liste : son bas suit la zone sûre
    [SerializeField] IdleBestBar bestBar;
    [SerializeField] IdleStationRow[] rows;
    [SerializeField, Range(0.1f, 0.9f)] float heightShare = 0.47f;
    [SerializeField, Min(0f)] float minHeight = 831f;   // 300 px du web
    [SerializeField, Min(0f)] float maxHeight = 1191f;  // 430 px du web
    [SerializeField, Min(0f)] float overlap = 83f;      // le bas du panneau dépasse de l'écran : ses coins arrondis restent cachés
    [SerializeField, Min(0.02f)] float refreshEvery = 0.2f;

    IdleGame subscribed;
    float timer;
    bool dirty = true;
    float shownHeight = -1f, shownSafe = -1f;

    void Subscribe(IdleGame game)
    {
        if (subscribed != null) return;
        subscribed = game;
        game.OnChanged += MarkDirty;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnChanged -= MarkDirty;
    }

    void MarkDirty() { dirty = true; }

    void Update()
    {
        Fit();
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        Subscribe(game);

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f && !dirty) return;
        timer = refreshEvery;
        dirty = false;

        bool found, hasGoal;
        IdleEconomy.Option goal;
        IdleEconomy.Option best = game.Advice(out found, out hasGoal, out goal);
        bestBar.Refresh(game, best, found, hasGoal, goal);
        for (int i = 0; i < rows.Length; i++) rows[i].Refresh(game, best, found);
    }

    public void Fit()
    {
        float canvasHeight = ((RectTransform)transform).rect.height;
        float height = Mathf.Clamp(canvasHeight * heightShare, minHeight, maxHeight);
        float safe = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * canvasHeight : 0f;
        if (Mathf.Approximately(height, shownHeight) && Mathf.Approximately(safe, shownSafe)) return;
        shownHeight = height;
        shownSafe = safe;
        panel.offsetMin = new Vector2(panel.offsetMin.x, -overlap);
        panel.offsetMax = new Vector2(panel.offsetMax.x, height);
        list.offsetMin = new Vector2(list.offsetMin.x, overlap + safe);
    }
}
