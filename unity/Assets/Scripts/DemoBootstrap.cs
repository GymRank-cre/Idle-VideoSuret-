#if IDLE_DEMO
using UnityEngine;

// UNIQUEMENT pour les builds de démonstration (symbole IDLE_DEMO, jamais dans le build publié) : débloque la chaîne et
// déclenche une scène choisie par l'adresse (?step=panel|bar|van|pro) pour photographier le jeu dans un navigateur.
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

    void AcceptLater() { FindFirstObjectByType<OrderBoard>().Accept(0); }

    void Update()
    {
        if (done || string.IsNullOrEmpty(step)) return;
        t += Time.deltaTime;
        if (t < 2f) return;
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
        if (step == "van") Invoke(nameof(AcceptLater), 8f); // la caméra doit montrer la camionnette pendant la capture
    }
}
#endif
