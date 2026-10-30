using UnityEngine;
using UnityEngine.UI;

// Bandeau sous le HUD (U3c) : une ligne par commande en cours (client, pièces, barre). Toucher une ligne ouvre la fenêtre Commandes.
public class IdleOrderStrip : MonoBehaviour
{
    [System.Serializable]
    struct Line
    {
        public GameObject root;
        public Button button;
        public Text title;
        public Text count;
        public RectTransform fill;
    }

    [SerializeField] Line[] lines;
    [SerializeField] IdleSheet ordersSheet;
    [SerializeField, Min(0.02f)] float refreshEvery = 0.2f;

    float timer;

    void Start()
    {
        foreach (Line line in lines) line.button.onClick.AddListener(ordersSheet.Open);
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = refreshEvery;

        IdleGame game = host.Game;
        for (int i = 0; i < lines.Length; i++)
        {
            bool show = i < game.Active.Count;
            if (lines[i].root.activeSelf != show) lines[i].root.SetActive(show);
            if (show) Fill(game, lines[i], game.Active[i]);
        }
    }

    static void Fill(IdleGame game, Line line, IdleGame.Offer o)
    {
        line.title.text = Loc.T("strip.title", Loc.T("client." + o.client + ".name"), Loc.T("items." + game.Eco.Config.stations[o.station].id));
        line.count.text = Loc.T("orders.count", NumberFormat.Short((long)o.progress), NumberFormat.Short(o.qty));
        Vector2 max = line.fill.anchorMax;
        max.x = Mathf.Clamp01((float)(o.progress / o.qty));
        line.fill.anchorMax = max;
    }
}
