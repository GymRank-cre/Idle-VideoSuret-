using UnityEngine;
using UnityEngine.UI;

// Une ligne du tiroir (U3b) pour un poste : ouverte (revenu, cycle, Vitesse, Ajouter), prochaine à débloquer, ou encore inconnue (« ??? »).
public class IdleStationRow : MonoBehaviour
{
    public enum RowState { Open, Next, Far }

    [SerializeField] int station;
    [SerializeField] IdleStationView view;
    [SerializeField] Image background;
    [SerializeField] Color openColor = new Color32(0xF6, 0xEF, 0xE2, 0xFF);
    [SerializeField] Color lockedColor = new Color32(0xE3, 0xD7, 0xC0, 0xFF);
    [SerializeField] GameObject openRoot;
    [SerializeField] GameObject nextRoot;
    [SerializeField] GameObject farRoot;
    [SerializeField] Text incomeText;
    [SerializeField] RectTransform cycleFill;
    [SerializeField] Text milestoneText;
    [SerializeField] IdleBuyButton speedButton;
    [SerializeField] IdleBuyButton addButton;
    [SerializeField] Text nextHint;
    [SerializeField] IdleBuyButton unlockButton;
    [SerializeField] Text farHint;

    RowState shown = (RowState)(-1);
    int shownLevel = -1;

    public static RowState StateOf(IdleGame game, int station)
    {
        if (game.Stations[station].unlocked) return RowState.Open;
        return station == 0 || game.Stations[station - 1].unlocked ? RowState.Next : RowState.Far;
    }

    void Start()
    {
        speedButton.Button.onClick.AddListener(() => BuyUpgrade(true));
        addButton.Button.onClick.AddListener(() => BuyUpgrade(false));
        unlockButton.Button.onClick.AddListener(BuyUnlock);
    }

    void BuyUpgrade(bool speedAxis)
    {
        IdleGame game = IdleHost.Instance.Game;
        IdlePurchaseSound.Play(game.BuyUpgrade(station, speedAxis, game.BuyMode), false);
    }

    void BuyUnlock()
    {
        IdlePurchaseSound.Play(IdleHost.Instance.Game.BuyUnlock(station), true);
    }

    public void Refresh(IdleGame game, IdleEconomy.Option best, bool hasBest)
    {
        RowState state = StateOf(game, station);
        if (state != shown) Show(game, state);
        if (state == RowState.Open) RefreshOpen(game, best, hasBest);
        else if (state == RowState.Next) RefreshNext(game, best, hasBest);
    }

    void Show(IdleGame game, RowState state)
    {
        shown = state;
        shownLevel = -1;
        openRoot.SetActive(state == RowState.Open);
        nextRoot.SetActive(state == RowState.Next);
        farRoot.SetActive(state == RowState.Far);
        background.color = state == RowState.Open ? openColor : lockedColor;
        if (station == 0) return;
        IdleEconomyConfig cfg = game.Eco.Config;
        if (state == RowState.Next)
            nextHint.text = Loc.T("drawer.next", NumberFormat.Short((long)System.Math.Round(cfg.stations[station].value / cfg.stations[station - 1].value)));
        else if (state == RowState.Far)
            farHint.text = Loc.T("drawer.far", Loc.T(cfg.stations[station - 1].nameKey));
    }

    void RefreshOpen(IdleGame game, IdleEconomy.Option best, bool hasBest)
    {
        IdleEconomy.StationLevels st = game.Stations[station];
        incomeText.text = NumberFormat.Rate(game.Eco.Income(station, st, game.Globals) * game.BoostMultiplier);
        int level = st.speed + st.add;
        if (level != shownLevel)
        {
            shownLevel = level;
            int next = game.Eco.NextMilestone(level);
            milestoneText.text = next > 0 ? Loc.T("drawer.milestone", level, next) : Loc.T("drawer.milestone.max", level);
        }
        RefreshAxis(game, st, speedButton, true, best, hasBest);
        RefreshAxis(game, st, addButton, false, best, hasBest);
    }

    void RefreshAxis(IdleGame game, IdleEconomy.StationLevels st, IdleBuyButton button, bool speedAxis, IdleEconomy.Option best, bool hasBest)
    {
        int level = speedAxis ? st.speed : st.add;
        int count = game.ResolveCount(station, speedAxis, game.BuyMode);
        long price = game.UpgradePrice(station, speedAxis, game.BuyMode);
        double delta = game.Eco.Income(station, game.Eco.With(st, speedAxis, count), game.Globals) - game.Eco.Income(station, st, game.Globals);
        bool isBest = hasBest && best.kind == IdleEconomy.Kind.Upgrade && best.station == station && best.speedAxis == speedAxis;
        button.SetPrice(price);
        button.SetDelta(delta > 0 ? "+" + NumberFormat.RateValue(delta * game.BoostMultiplier) : "·");
        button.SetLevel(count > 1 ? Loc.T("drawer.levelplus", level, count) : Loc.T("drawer.level", level));
        button.SetState(game.Cash >= price, isBest, isBest);
    }

    void RefreshNext(IdleGame game, IdleEconomy.Option best, bool hasBest)
    {
        long price = game.UnlockPrice(station);
        bool isBest = hasBest && best.kind == IdleEconomy.Kind.Unlock && best.station == station;
        unlockButton.SetPrice(price);
        unlockButton.SetState(game.Cash >= price, isBest, isBest);
    }

    void Update()
    {
        if (shown != RowState.Open || view == null) return;
        Vector2 max = cycleFill.anchorMax;
        max.x = view.Phase - Mathf.Floor(view.Phase);
        cycleFill.anchorMax = max;
    }
}
