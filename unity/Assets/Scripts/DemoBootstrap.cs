#if IDLE_DEMO
using System.Collections.Generic;
using UnityEngine;

// UNIQUEMENT pour les builds de démonstration (symbole IDLE_DEMO, jamais dans le build publié) : débloque la chaîne et
// déclenche une scène choisie par l'adresse (?step=panel|bar|van|pro|hand|done) pour photographier le jeu dans un navigateur.
// hand : partie neuve (la main du tutoriel) ; done : une commande est livrée après le bouton Jouer, au ralenti (bandeau et puces).
public class DemoBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create() { new GameObject("DemoBootstrap", typeof(DemoBootstrap)); }

    string step = "";
    float t;
    bool done;

    void Start()
    {
        SaveSystem.Suspended = true;
        string url = Application.absoluteURL ?? "";
        int i = url.IndexOf("step=");
        if (i >= 0) step = url.Substring(i + 5).Split('&')[0];
    }

    // L'écran « Touche pour jouer » est-il parti ?
    static bool GameStarted()
    {
        var tap = FindFirstObjectByType<TapToPlayScreen>(FindObjectsInactive.Include);
        if (tap == null) return true;
        var root = typeof(TapToPlayScreen).GetField("root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var go = root != null ? root.GetValue(tap) as GameObject : null;
        return go == null || !go.activeSelf;
    }

    // Livre une commande entière, puis ralentit le temps pour laisser le temps de photographier l'animation.
    static void DeliverOne(OrderBoard board)
    {
        board.Accept(0);
        var needed = new List<ProductType>();
        int guard = 0;
        while (board.HasActive && guard++ < 200)
        {
            board.GetNeeded(needed);
            if (needed.Count == 0) break;
            board.Deliver(needed[0]);
        }
        Time.timeScale = 0.3f;
    }

    void AcceptLater() { FindFirstObjectByType<OrderBoard>().Accept(0); }

    void Update()
    {
        if (done || string.IsNullOrEmpty(step)) return;
        if (step == "hand") { done = true; return; }   // partie neuve : rien à tricher, la main du tutoriel apparaît seule
        t += Time.deltaTime;
        if (t < 2f) return;
        if (step == "done" && !GameStarted()) return;   // attendre le bouton Jouer pour que le bandeau soit visible
        done = true;

        foreach (UnlockPad pad in FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (pad.SaveId != null && !pad.SaveId.StartsWith("UpPad_")) pad.Restore(0);
        FindFirstObjectByType<Wallet>().Add(5000);
        var board = FindFirstObjectByType<OrderBoard>();
        var ui = FindFirstObjectByType<OrdersUi>();
        for (int k = 0; k < 3; k++) board.AddOffer(board.Generate(k == 0 ? false : false));
        if (step == "pro") FindFirstObjectByType<ProPoints>().Add(7);
        if (step == "panel") ui.Open();
        if (step == "bar") board.Accept(0);
        if (step == "done") DeliverOne(board);
        if (step == "van") Invoke(nameof(AcceptLater), 8f); // la caméra doit montrer la camionnette pendant la capture
    }
}
#endif
