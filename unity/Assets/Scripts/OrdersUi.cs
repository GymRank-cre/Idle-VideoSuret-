using UnityEngine;
using UnityEngine.UI;

// Interface des commandes (11c) : bouton carré orange avec pastille du nombre d'offres, panneau COMMANDES, barre compacte
// « Commande de Karim 2/5 » sous la consigne, pastille Points Pro (cachée tant que 0).
public class OrdersUi : MonoBehaviour
{
    [SerializeField] OrderBoard board;
    [SerializeField] ProPoints pro;
    [SerializeField] Button openButton;
    [SerializeField] GameObject badge;
    [SerializeField] Text badgeText;
    [SerializeField] GameObject panel;
    [SerializeField] Button closeButton;
    [SerializeField] OrderCard[] cards;
    [SerializeField] Text emptyText;
    [SerializeField] GameObject bar;
    [SerializeField] Text barText;
    [SerializeField] Image barFill;
    [SerializeField] GameObject proPill;
    [SerializeField] Text proText;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField] Transform dockPoint;
    [SerializeField] Button backdrop;                 // fond sombre cliquable : toucher en dehors = fermer
    [SerializeField] string[] hideNames;              // consigne et boutons carrés (enfants de la zone sûre), masqués quand le panneau est ouvert

    bool hintShown;
    float proPop = -1f;
    Vector3 proBase;

    void Awake()
    {
        panel.SetActive(false);
        openButton.gameObject.SetActive(false);
        bar.SetActive(false);
        proPill.SetActive(false);
        proBase = proPill.transform.localScale;
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
        if (backdrop != null) { backdrop.onClick.AddListener(Close); backdrop.gameObject.SetActive(false); }
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].Accept.onClick.AddListener(() => OnAccept(index));
            cards[i].Refuse.onClick.AddListener(() => { AudioManager.Play(SfxId.Click); board.Refuse(index); });
            cards[i].Later.onClick.AddListener(() => cards[index].ShowLater());
        }
    }

    void OnEnable()
    {
        board.OnChanged += Refresh;
        board.OnFirstOffer += OnFirstOffer;
        pro.OnChanged += OnPro;
    }

    void OnDisable()
    {
        board.OnChanged -= Refresh;
        board.OnFirstOffer -= OnFirstOffer;
        pro.OnChanged -= OnPro;
    }

    void Start() { Refresh(); }

    void OnFirstOffer()
    {
        hintShown = true;
        if (cameraFollow != null && dockPoint != null) cameraFollow.Peek(dockPoint, 1.2f);
    }

    void OnAccept(int index)
    {
        if (!board.Accept(index)) { AudioManager.Play(SfxId.Refuse); return; }
        AudioManager.Play(SfxId.Hire);
        Close();
    }

    void OnPro(int points)
    {
        proText.text = NumberFormat.Short(points);
        if (!proPill.activeSelf) proPill.SetActive(true);
        proPop = 0f;
    }

    void Toggle()
    {
        AudioManager.Play(SfxId.Click);
        if (panel.activeSelf) Close(); else Open();
    }

    public void Open()
    {
        if (backdrop != null) { backdrop.gameObject.SetActive(true); backdrop.transform.SetAsLastSibling(); }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling(); // au-dessus de tout, fond sombre compris
        SetHidden(true);
        hintShown = false;
        Refresh();
    }

    // Consigne et boutons carrés : invisibles et non cliquables tant que le panneau est ouvert.
    void SetHidden(bool hidden)
    {
        if (hideNames == null) return;
        foreach (string n in hideNames)
        {
            Transform t = transform.parent != null ? transform.parent.Find(n) : null; // construits après ce script : résolus à l'ouverture
            if (t == null) continue;
            GameObject g = t.gameObject;
            CanvasGroup cg = g.GetComponent<CanvasGroup>();
            if (cg == null) cg = g.AddComponent<CanvasGroup>();
            cg.alpha = hidden ? 0f : 1f;
            cg.blocksRaycasts = !hidden;
        }
    }

    void Close()
    {
        SetHidden(false);
        if (backdrop != null) backdrop.gameObject.SetActive(false);
        panel.SetActive(false);
        RefreshBar();
    }

    void Refresh()
    {
        // le bouton n'existe qu'une fois l'Emballage débloqué
        if (board.Unlocked && !openButton.gameObject.activeSelf) openButton.gameObject.SetActive(true);

        int n = board.Offers.Count;
        badge.SetActive(n > 0);
        badgeText.text = n.ToString();

        if (panel.activeSelf)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                bool has = i < n;
                cards[i].gameObject.SetActive(has);
                if (has) cards[i].Bind(board, i);
            }
            emptyText.gameObject.SetActive(n == 0);
        }

        RefreshBar();
    }

    void RefreshBar()
    {
        if (panel.activeSelf)
        {
            bar.SetActive(false); // le panneau est ouvert : la barre compacte attend
        }
        else if (board.HasActive)
        {
            bar.SetActive(true);
            OrderConfig.Client c = board.Config.Clients[board.Active.clientIndex];
            barText.text = Loc.T("orders.bar", c.displayName, board.ActiveDelivered, board.ActiveTotal);
        }
        else if (hintShown && board.Offers.Count > 0)
        {
            bar.SetActive(true);
            barText.text = Loc.T("orders.hint");
            barFill.fillAmount = 1f;
        }
        else
        {
            bar.SetActive(false);
        }
    }

    void Update()
    {
        if (board.HasActive && bar.activeSelf) barFill.fillAmount = board.PatienceFraction;
        if (proPop >= 0f)
        {
            proPop += Time.deltaTime;
            float k = Mathf.Clamp01(proPop / 0.35f);
            proPill.transform.localScale = proBase * (1f + 0.3f * Mathf.Sin(k * Mathf.PI));
            if (k >= 1f) { proPop = -1f; proPill.transform.localScale = proBase; }
        }
    }
}
