using UnityEngine;
using UnityEngine.UI;

// Bandeau « Meilleur achat » (U3b) : le conseil d'IdleGame (même calcul que le web), son gain, et le bouton qui l'achète, grisé « Dans N s » si trop cher.
public class IdleBestBar : MonoBehaviour
{
    [SerializeField] Text titleText;
    [SerializeField] Text detailText;
    [SerializeField] IdleBuyButton buyButton;
    [SerializeField] RectTransform waitFill;

    void Start()
    {
        buyButton.Button.onClick.AddListener(Buy);
    }

    void Buy()
    {
        IdleGame game = IdleHost.Instance.Game;
        bool found, hasGoal;
        IdleEconomy.Option goal;
        IdleEconomy.Option o = game.Advice(out found, out hasGoal, out goal);
        if (found) IdlePurchaseSound.Play(game.BuyAdvised(), o.kind == IdleEconomy.Kind.Unlock);
    }

    public void Refresh(IdleGame game, IdleEconomy.Option o, bool found, bool hasGoal, IdleEconomy.Option goal)
    {
        if (!found)
        {
            titleText.text = Loc.T("drawer.best.none");
            detailText.text = "";
            return;
        }
        IdleEconomyConfig cfg = game.Eco.Config;
        string name = Loc.T(cfg.stations[o.station].nameKey);
        if (o.kind == IdleEconomy.Kind.Unlock) titleText.text = Loc.T("drawer.best.unlock", name);
        else
        {
            IdleEconomy.StationLevels st = game.Stations[o.station];
            titleText.text = Loc.T("drawer.best.line", name, Loc.T(o.speedAxis ? "drawer.speed" : "drawer.add"), (o.speedAxis ? st.speed : st.add) + 1);
        }

        long price = IdleEconomy.Ceil(o.cost);
        double payback = o.delta > 0 ? price / o.delta : 0;
        string detail = Loc.T("drawer.best.detail", NumberFormat.Rate(o.delta), NumberFormat.Time(payback));
        if (hasGoal) detail += Loc.T("drawer.best.goal", GoalName(cfg, goal));
        detailText.text = detail;

        bool affordable = game.Cash >= price;
        buyButton.SetPrice(price);
        buyButton.SetLabel(affordable ? Loc.T("drawer.buy") : Loc.T("drawer.in", NumberFormat.Time(System.Math.Max(1, (price - game.Cash) / System.Math.Max(0.01, game.Income)))));
        buyButton.SetState(affordable, false, true);

        Vector2 max = waitFill.anchorMax;
        max.x = affordable ? 0f : Mathf.Clamp01((float)(game.Cash / price));
        waitFill.anchorMax = max;
    }

    static string GoalName(IdleEconomyConfig cfg, IdleEconomy.Option goal)
    {
        string name = Loc.T(cfg.stations[goal.station].nameKey);
        if (goal.kind == IdleEconomy.Kind.Unlock) return name;
        return Loc.T(goal.speedAxis ? "drawer.goal.speed" : "drawer.goal.add", name);
    }
}
