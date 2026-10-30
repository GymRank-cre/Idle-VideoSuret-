using UnityEngine;
using UnityEngine.UI;

// Interface de la Réputation (11d) : bouton carré orange à étoile (visible dès le premier Point Pro, pastille rouge quand un achat est
// possible), panneau RÉPUTATION (même kit que COMMANDES), fête à l'achat.
public class ReputationUi : MonoBehaviour
{
    [SerializeField] ReputationState state;
    [SerializeField] ProPoints pro;
    [SerializeField] Button openButton;
    [SerializeField] GameObject badge;
    [SerializeField] GameObject panel;
    [SerializeField] Button closeButton;
    [SerializeField] Button backdrop;
    [SerializeField] Text balanceText;
    [SerializeField] ReputationRow[] rows;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] Transform player;
    [SerializeField] string[] hideNames;

    bool everHadPoints;

    void Awake()
    {
        panel.SetActive(false);
        openButton.gameObject.SetActive(false);
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
        if (backdrop != null) { backdrop.onClick.AddListener(Close); backdrop.gameObject.SetActive(false); }
    }

    void OnEnable()
    {
        state.OnChanged += Refresh;
        state.OnPurchased += OnPurchased;
    }

    void OnDisable()
    {
        state.OnChanged -= Refresh;
        state.OnPurchased -= OnPurchased;
    }

    void Start()
    {
        for (int i = 0; i < rows.Length; i++) rows[i].Init(state);
        Refresh();
    }

    void OnPurchased(int index)
    {
        AudioManager.Play(SfxId.Upgrade);
        AudioManager.Vibrate();
        foreach (ReputationRow r in rows) if (r.NodeIndex == index) r.Pop();
        if (feedback != null && player != null) feedback.Confetti(player.position + Vector3.up * 1.2f); // étoiles qui jaillissent
    }

    void Toggle()
    {
        if (panel.activeSelf) Close(); else Open();
    }

    public void Open()
    {
        if (backdrop != null) { backdrop.gameObject.SetActive(true); backdrop.transform.SetAsLastSibling(); }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
        SetHidden(true);
        Refresh();
    }

    void Close()
    {
        SetHidden(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
        panel.SetActive(false);
    }

    void SetHidden(bool hidden)
    {
        if (hideNames == null) return;
        foreach (string n in hideNames)
        {
            Transform t = transform.parent != null ? transform.parent.Find(n) : null;
            if (t == null) continue;
            CanvasGroup cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = hidden ? 0f : 1f;
            cg.blocksRaycasts = !hidden;
        }
    }

    void Refresh()
    {
        if (!everHadPoints && (pro.Points > 0 || AnyLevel())) everHadPoints = true;
        if (everHadPoints && !openButton.gameObject.activeSelf) openButton.gameObject.SetActive(true); // apparaît avec un pop au premier gain
        badge.SetActive(state.AnyAffordable);
        if (!panel.activeSelf) return;
        balanceText.text = NumberFormat.Short(pro.Points);
        foreach (ReputationRow r in rows) r.Refresh();
    }

    bool AnyLevel()
    {
        for (int i = 0; i < state.Config.Nodes.Length; i++) if (state.Level(i) > 0) return true;
        return false;
    }
}
