using UnityEngine;
using UnityEngine.UI;

// Sélecteur ×1 / ×10 / MAX du tiroir (U3b) : change IdleGame.BuyMode, lu aussi par les plaques au sol ; le choix est sauvegardé avec la partie.
public class IdleBuyModes : MonoBehaviour
{
    [System.Serializable]
    struct Choice
    {
        public int mode;           // 1, 10 ou 0 (maximum)
        public Button button;
        public Image background;
        public GameObject ring;    // liseré jaune du mode choisi
    }

    [SerializeField] Choice[] choices;
    [SerializeField] Color selectedColor = new Color32(0x0E, 0x16, 0x20, 0xFF);
    [SerializeField] Color idleColor = new Color(0.055f, 0.086f, 0.125f, 0.35f);

    int shown = -1;

    void Start()
    {
        foreach (Choice c in choices)
        {
            int mode = c.mode;
            c.button.onClick.AddListener(() => IdleHost.Instance.Game.BuyMode = mode);
        }
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null || host.Game.BuyMode == shown) return;
        shown = host.Game.BuyMode;
        foreach (Choice c in choices)
        {
            bool on = c.mode == shown;
            c.background.color = on ? selectedColor : idleColor;
            c.ring.SetActive(on);
        }
    }
}
