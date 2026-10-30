using UnityEngine;
using UnityEngine.UI;

// Une carte de la fenêtre Commandes (U3c) : client, réplique, pièces demandées, gains. Une offre se refuse ou s'accepte ; une commande en cours montre sa barre.
public class IdleOrderCard : MonoBehaviour
{
    [SerializeField] Image portrait;
    [SerializeField] Text nameText;
    [SerializeField] Text sayText;
    [SerializeField] Text needText;
    [SerializeField] Text moneyText;
    [SerializeField] Text proText;
    [SerializeField] RectTransform needBox;
    [SerializeField] float offerBoxHeight = 133f;
    [SerializeField] float activeBoxHeight = 172f;
    [SerializeField] GameObject progressRoot;
    [SerializeField] GameObject progressBar;
    [SerializeField] RectTransform progressFill;
    [SerializeField] Text progressText;
    [SerializeField] GameObject actions;
    [SerializeField] Button refuseButton;
    [SerializeField] IdleBuyButton acceptButton;

    int offerId;

    void Start()
    {
        refuseButton.onClick.AddListener(Refuse);
        acceptButton.Button.onClick.AddListener(Accept);
    }

    void Refuse()
    {
        IdleHost.Instance.Game.Decline(offerId);
    }

    void Accept()
    {
        AudioManager.Play(IdleHost.Instance.Game.Accept(offerId) ? SfxId.Upgrade : SfxId.Refuse);
    }

    public void Show(IdleGame game, IdleGame.Offer order, Sprite face, bool active, bool full)
    {
        offerId = order.id;
        IdleEconomyConfig cfg = game.Eco.Config;
        portrait.sprite = face;
        nameText.text = Loc.T("client." + order.client + ".name");
        sayText.text = Loc.T("orders.quote", Loc.T("order.say." + order.say));
        needText.text = Loc.T("orders.need", NumberFormat.Short(order.qty), Loc.T("items." + cfg.stations[order.station].id));
        moneyText.text = NumberFormat.Short((long)order.reward);
        proText.text = Loc.T("orders.pro", order.pro);

        needBox.sizeDelta = new Vector2(needBox.sizeDelta.x, active ? activeBoxHeight : offerBoxHeight);
        progressRoot.SetActive(active);
        progressBar.SetActive(active);
        actions.SetActive(!active);
        if (active) Progress(order);
        else
        {
            acceptButton.SetLabel(Loc.T(full ? "orders.full" : "orders.accept"));
            acceptButton.SetState(!full, false, false);
        }
    }

    // Avance en direct, sans reconstruire la liste.
    public void Progress(IdleGame.Offer order)
    {
        float share = Mathf.Clamp01((float)(order.progress / order.qty));
        Vector2 max = progressFill.anchorMax;
        max.x = share;
        progressFill.anchorMax = max;
        progressText.text = Loc.T("orders.progress", NumberFormat.Short((long)order.progress), NumberFormat.Short(order.qty));
    }
}
