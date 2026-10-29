using UnityEngine;
using UnityEngine.UI;

// Bouton rond « Améliorations » (avec un point quand un achat est possible) et panneau des achats.
// Le jeu continue derrière le panneau. Retour d'information à l'achat : pop de la ligne, étincelles, chiffre flottant.
public class UpgradePanel : MonoBehaviour
{
    [SerializeField] UpgradeState state;
    [SerializeField] Wallet wallet;
    [SerializeField] GameObject panel;
    [SerializeField] Button openButton;
    [SerializeField] Button closeButton;
    [SerializeField] RectTransform openButtonRect;
    [SerializeField] GameObject dot;
    [SerializeField] UpgradeRow[] rows;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] Transform player;

    bool highlight;

    public bool IsOpen => panel.activeSelf;
    public bool HasBeenOpened { get; private set; }

    void Awake()
    {
        panel.SetActive(false);
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
    }

    void Start()
    {
        for (int i = 0; i < rows.Length; i++) rows[i].Init(state, i);
    }

    void OnEnable()
    {
        state.OnChanged += RefreshRows;
        state.OnPurchased += Purchased;
        wallet.OnChanged += RefreshRowsWithMoney;
    }

    void OnDisable()
    {
        state.OnChanged -= RefreshRows;
        state.OnPurchased -= Purchased;
        wallet.OnChanged -= RefreshRowsWithMoney;
    }

    void RefreshRowsWithMoney(int money)
    {
        RefreshRows();
    }

    void RefreshRows()
    {
        if (!panel.activeSelf) return;
        foreach (UpgradeRow row in rows) row.Refresh();
    }

    void Toggle()
    {
        if (panel.activeSelf) Close();
        else Open();
    }

    void Open()
    {
        panel.SetActive(true);
        HasBeenOpened = true;
        foreach (UpgradeRow row in rows) row.Refresh();
    }

    void Close()
    {
        panel.SetActive(false);
    }

    // Le guide fait battre le bouton pour le montrer.
    public void SetHighlight(bool on)
    {
        highlight = on;
        if (!on) openButtonRect.localScale = Vector3.one;
    }

    void Purchased(int index)
    {
        rows[index].Pop();
        RefreshRows();
        if (feedback == null || player == null) return;
        feedback.Sparkle(player.position + Vector3.up * 1.3f);
        feedback.Message(player.position + Vector3.up * 2.4f, UpgradeRow.Short(state.Config.Upgrades[index]));
    }

    void Update()
    {
        dot.SetActive(!panel.activeSelf && state.AnyAffordable);
        if (highlight && !panel.activeSelf)
            openButtonRect.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f));
    }
}
