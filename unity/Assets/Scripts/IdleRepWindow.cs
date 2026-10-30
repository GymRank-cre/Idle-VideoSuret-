using UnityEngine;
using UnityEngine.UI;

// Fenêtre Réputation (U3c) : les 7 bonus permanents, payés en Points Pro ; bouton vert si on peut payer, gris sinon, « MAX » au dernier niveau.
public class IdleRepWindow : MonoBehaviour
{
    [System.Serializable]
    struct Card
    {
        public Text level;
        public IdleBuyButton buy;
        public GameObject starIcon;
    }

    [SerializeField] IdleSheet sheet;
    [SerializeField] Text pointsText;
    [SerializeField] Card[] cards;
    [SerializeField] float contentHeight;

    bool wired;

    void OnEnable()
    {
        Wire();
        sheet.SetContentHeight(contentHeight);
        IdleHost.Instance.Game.OnChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        IdleHost host = IdleHost.Instance;
        if (host != null && host.Game != null) host.Game.OnChanged -= Refresh;
    }

    void Wire()
    {
        if (wired) return;
        wired = true;
        for (int i = 0; i < cards.Length; i++)
        {
            int node = i;
            cards[i].buy.Button.onClick.AddListener(() => Buy(node));
        }
    }

    static void Buy(int node)
    {
        AudioManager.Play(IdleHost.Instance.Game.BuyRep(node) ? SfxId.Unlock : SfxId.Refuse);
    }

    void Refresh()
    {
        IdleGame game = IdleHost.Instance.Game;
        pointsText.text = Loc.T("rep.points", game.Pro);
        IdleEconomyConfig cfg = game.Eco.Config;
        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            bool max = game.RepMax(i);
            card.level.text = Loc.T("rep.levelof", game.RepLevel(i), cfg.reputation[i].maxLevel);
            card.starIcon.SetActive(!max);
            if (max) card.buy.SetPriceText(Loc.T("rep.max"));
            else card.buy.SetPrice(game.RepCost(i));
            card.buy.SetState(game.CanBuyRep(i), false, false);
        }
    }
}
