using System;
using System.Collections.Generic;

// Modèle de jeu idle (U1) : état de la partie et toute sa logique (revenus, achats, commandes, Réputation, turbo, hors ligne),
// sans Unity (testable en simulation). Le temps de jeu `Now` est en secondes (UTC Unix en jeu, libre dans les tests).
public class IdleGame
{
    public class Offer
    {
        public int id, client, station, qty, pro, say;
        public double reward, progress;
    }

    public enum PurchaseResult { Ok, Milestone, TooExpensive, Invalid }
    public enum PadKind { Speed, Add, Unlock, Hire }   // Hire : `station` désigne le robot à recruter (0 à 2)

    public struct OfflineResult
    {
        public double gain, seconds;
        public bool capped, showWelcome;
    }

    public readonly IdleEconomy Eco;
    readonly IdleEconomyConfig cfg;
    readonly Random rng;

    public double Now;                                  // horloge de jeu, en secondes
    public double Cash, Earned;
    public long Buys;
    public int Pro, ProTotal;
    public readonly List<IdleEconomy.StationLevels> Stations = new List<IdleEconomy.StationLevels>();
    public int Crew;                                    // robots recrutés, dans l'ordre ; rien ne tourne tant que l'équipe n'est pas complète
    public readonly IdleChain Chain;
    public int[] Rep;
    public readonly List<Offer> Offers = new List<Offer>();
    public readonly List<Offer> Active = new List<Offer>();
    public int OrderSeq = 1, OrdersDone;
    public double NextOfferAt;                          // horloge de jeu
    public double BoostUntil, BoostReadyAt;             // horloge de jeu

    IdleEconomy.Globals g;
    double incomeCache, otherIncome, chainValue;

    public event Action OnChanged;
    public event Action<int> OnPurchased;               // poste acheté ou amélioré
    public event Action<int> OnMilestone;               // palier x2 franchi sur ce poste
    public event Action<int> OnUnlocked;                // poste débloqué
    public event Action<int> OnHired;                   // robot recruté
    public event Action<Offer> OnOrderCompleted;

    public IdleEconomy.Globals Globals => g;
    public double BaseIncome => incomeCache;
    public bool CrewReady => Crew >= cfg.hirePrices.Length;
    public bool BoostActive => Now < BoostUntil;
    public double BoostMultiplier => BoostActive ? cfg.turboMultiplier : 1;
    public double Income => incomeCache * BoostMultiplier;     // revenu réel par seconde (turbo compris)
    public int BuyMode = 1;                             // 1, 10 ou 0 (= maximum abordable) : sélecteur du tiroir et plaques au sol
    public double BoostRemaining => Math.Max(0, BoostUntil - Now);
    public double BoostCooldownRemaining => Math.Max(0, BoostReadyAt - Now);

    public IdleGame(IdleEconomyConfig config, int seed = 0, double now = 0)
    {
        cfg = config;
        Eco = new IdleEconomy(config);
        Chain = new IdleChain(config);
        rng = seed == 0 ? new Random() : new Random(seed);
        Now = now;
        NewGame();
    }

    public void NewGame()
    {
        Stations.Clear();
        for (int i = 0; i < cfg.stations.Length; i++) Stations.Add(new IdleEconomy.StationLevels { unlocked = cfg.stations[i].unlockPayback <= 0 });
        Rep = new int[cfg.reputation.Length];
        Cash = cfg.startCash; Earned = 0; Buys = 0; Pro = 0; ProTotal = 0;
        Offers.Clear(); Active.Clear(); OrderSeq = 1; OrdersDone = 0; NextOfferAt = 0;
        BoostUntil = 0; BoostReadyAt = 0;
        Crew = 0;
        Chain.Reset();
        Recalc();
    }

    public void Recalc()
    {
        g = Eco.GlobalsFor(Rep);
        chainValue = Eco.ChainValue(Stations, g);
        incomeCache = CrewReady ? Eco.TotalIncome(Stations, g) : 0;
        otherIncome = CrewReady ? incomeCache - Eco.ChainRate(Stations, g) * chainValue : 0;
    }

    void Changed() { OnChanged?.Invoke(); }

    // ------------------------------------------------------------------ temps

    public void Tick(double dt)
    {
        Now += dt;
        int serre = IdleEconomy.SerreStation, assembly = IdleEconomy.AssemblyStation;
        double rateOut = Stations[assembly].unlocked ? Eco.ItemsPerSecond(assembly, Stations[assembly], g) : 0;
        double made = Chain.Step(dt, Crew, Eco.ItemsPerSecond(serre, Stations[serre], g), rateOut);
        double gain = (made * chainValue + otherIncome * dt) * BoostMultiplier;   // l'argent de la chaîne vient de ce qui sort vraiment de l'Assemblage
        Cash += gain; Earned += gain;
        if (!CrewReady) return;
        OrderProgress(dt, 1);
        RefreshOffers();
    }

    // Le poste tourne-t-il en ce moment ? Le début de la chaîne s'arrête quand la matière manque ou que la suite est pleine.
    public bool StationWorking(int station)
    {
        if (station == IdleEconomy.SerreStation) return Chain.Cutting;
        return station != IdleEconomy.AssemblyStation || Chain.Assembling;
    }

    // ------------------------------------------------------------------ recrutements

    public long HirePrice(int role) { return IdleEconomy.Ceil(cfg.hirePrices[role]); }

    // Les robots se recrutent dans l'ordre : découpeur, graveur, drone.
    public PurchaseResult BuyHire(int role)
    {
        if (CrewReady || role != Crew) return PurchaseResult.Invalid;
        long cost = HirePrice(role);
        if (Cash < cost) return PurchaseResult.TooExpensive;
        Cash -= cost;
        Crew++;
        Buys++;
        Recalc();
        OnHired?.Invoke(role);
        RefreshOffers();
        Changed();
        return PurchaseResult.Ok;
    }

    // Ce qu'il reste à payer pour lancer la chaîne ; aucune autre source d'argent n'existe avant.
    double RemainingHireCost()
    {
        double total = 0;
        for (int i = Crew; i < cfg.hirePrices.Length; i++) total += cfg.hirePrices[i];
        return total;
    }

    // ------------------------------------------------------------------ achats

    // k = 0 : maximum abordable (au moins 1). Renvoie le prix entier réellement demandé via `price`.
    public int ResolveCount(int station, bool speedAxis, int k)
    {
        IdleEconomy.StationLevels st = Stations[station];
        int level = speedAxis ? st.speed : st.add;
        return k > 0 ? k : Math.Max(1, Eco.MaxAffordable(speedAxis, station, level, Cash, g));
    }

    public long UpgradePrice(int station, bool speedAxis, int k)
    {
        IdleEconomy.StationLevels st = Stations[station];
        return IdleEconomy.Ceil(Eco.BulkCost(speedAxis, station, speedAxis ? st.speed : st.add, ResolveCount(station, speedAxis, k), g));
    }

    public PurchaseResult BuyUpgrade(int station, bool speedAxis, int k)
    {
        if (!CrewReady || station < 0 || station >= Stations.Count || !Stations[station].unlocked) return PurchaseResult.Invalid;
        IdleEconomy.StationLevels st = Stations[station];
        int count = ResolveCount(station, speedAxis, k);
        long cost = IdleEconomy.Ceil(Eco.BulkCost(speedAxis, station, speedAxis ? st.speed : st.add, count, g));
        if (Cash < cost) return PurchaseResult.TooExpensive;
        int before = Eco.MilestoneCount(st.speed + st.add);
        Cash -= cost;
        if (speedAxis) st.speed += count; else st.add += count;
        Buys++;
        Recalc();
        bool milestone = Eco.MilestoneCount(st.speed + st.add) > before;
        OnPurchased?.Invoke(station);
        if (milestone) OnMilestone?.Invoke(station);
        RefreshOffers();
        Changed();
        return milestone ? PurchaseResult.Milestone : PurchaseResult.Ok;
    }

    // Seul le prochain poste verrouillé peut être acheté.
    public int NextLockedStation()
    {
        for (int i = 0; i < Stations.Count; i++) if (!Stations[i].unlocked) return i;
        return -1;
    }

    public long UnlockPrice(int station) { return IdleEconomy.Ceil(Eco.UnlockCost(station, g)); }

    public PurchaseResult BuyUnlock(int station)
    {
        if (!CrewReady || station < 0 || station != NextLockedStation()) return PurchaseResult.Invalid;
        long cost = UnlockPrice(station);
        if (Cash < cost) return PurchaseResult.TooExpensive;
        Cash -= cost;
        Stations[station].unlocked = true;
        Buys++;
        Recalc();
        OnUnlocked?.Invoke(station);
        OnPurchased?.Invoke(station);
        RefreshOffers();
        Changed();
        return PurchaseResult.Ok;
    }

    public enum PlateState { Hidden, Unlock, Open }

    // Cycles visuels par seconde d'un poste : 1 / durée du cycle (vitesse et Réputation comprises, sans « Ajouter »), plafonné.
    public float VisualFrequency(int station)
    {
        IdleEconomy.StationLevels st = Stations[station];
        double cycle = cfg.stations[station].cycle / (1 + cfg.speedStep * st.speed) / g.speed;
        float cap = station < cfg.visualCap.Length ? cfg.visualCap[station] : 1f;
        return (float)Math.Min(1.0 / cycle, cap);
    }

    // Ce que rapporte un poste quand il tourne, sans turbo.
    public double StationIncome(int station)
    {
        if (station == IdleEconomy.AssemblyStation) return Eco.ItemsPerSecond(station, Stations[station], g) * chainValue;
        return Eco.Income(station, Stations[station], g);
    }

    public double UpgradeGain(int station, bool speedAxis, int k) { return Eco.UpgradeGain(Stations, g, station, speedAxis, k); }

    // Gain d'argent qu'un cycle VISUEL d'un poste représente (revenu / fréquence visuelle, turbo compris) : chiffre « +x » qui monte.
    public double CycleGain(int station)
    {
        float f = VisualFrequency(station);
        return f > 0f ? StationIncome(station) * BoostMultiplier / f : 0.0;
    }

    // Ce que le joueur VOIT de ses achats : « Vitesse » accélère le tapis du poste, « Ajouter » y met plus de caisses et grossit la pile.
    public float BeltSpeedFactor(int station)
    {
        return (float)Math.Min(cfg.beltSpeedCap, (1 + cfg.speedStep * Stations[station].speed) * g.speed);
    }

    public int BeltItemCount(int station, int max)
    {
        return Math.Min(max, cfg.beltBaseItems + Stations[station].add / cfg.addPerBeltItem);
    }

    public int StackSize(int station)
    {
        return Math.Min(cfg.stackMax, cfg.stackBase + Stations[station].add / cfg.addPerStackCrate);
    }

    // Un poste ouvert a ses plaques Vitesse et Ajouter, le prochain verrouillé sa plaque Débloquer, les suivants rien.
    public PlateState PlateStateOf(int station)
    {
        if (Stations[station].unlocked) return PlateState.Open;
        return CrewReady && station == NextLockedStation() ? PlateState.Unlock : PlateState.Hidden;
    }

    // Tant que l'équipe n'est pas complète, seule la plaque du prochain recrutement existe : l'argent de départ ne peut pas partir ailleurs.
    public bool PadVisible(int station, PadKind kind)
    {
        if (kind == PadKind.Hire) return station == Crew;
        if (!CrewReady) return false;
        PlateState state = PlateStateOf(station);
        return kind == PadKind.Unlock ? state == PlateState.Unlock : state == PlateState.Open;
    }

    // Prix affiché sur une plaque (0 si elle est cachée) : suit le mode ×1/×10/MAX.
    public long PadPrice(int station, PadKind kind)
    {
        if (!PadVisible(station, kind)) return 0;
        if (kind == PadKind.Hire) return HirePrice(station);
        return kind == PadKind.Unlock ? UnlockPrice(station) : UpgradePrice(station, kind == PadKind.Speed, BuyMode);
    }

    // Touche sur une plaque : achète ce qu'elle propose.
    public PurchaseResult BuyPad(int station, PadKind kind)
    {
        if (!PadVisible(station, kind)) return PurchaseResult.Invalid;
        if (kind == PadKind.Hire) return BuyHire(station);
        return kind == PadKind.Unlock ? BuyUnlock(station) : BuyUpgrade(station, kind == PadKind.Speed, BuyMode);
    }

    // Ce conseil correspond-il à cette plaque ?
    public static bool IsAdvised(IdleEconomy.Option best, int station, PadKind kind)
    {
        if (best.station != station) return false;
        if (best.kind == IdleEconomy.Kind.Hire) return kind == PadKind.Hire;
        if (best.kind == IdleEconomy.Kind.Unlock) return kind == PadKind.Unlock;
        return kind == (best.speedAxis ? PadKind.Speed : PadKind.Add);
    }

    // Achat conseillé du moment (et son objectif lointain éventuel).
    public IdleEconomy.Option Advice(out bool found, out bool hasGoal, out IdleEconomy.Option goal)
    {
        if (!CrewReady)
        {
            found = true; hasGoal = false; goal = default(IdleEconomy.Option);
            return new IdleEconomy.Option { kind = IdleEconomy.Kind.Hire, station = Crew, count = 1, cost = HirePrice(Crew) };
        }
        return Eco.Advice(Stations, g, Cash, Income, out found, out hasGoal, out goal);
    }

    public PurchaseResult BuyAdvised()
    {
        bool found, hasGoal;
        IdleEconomy.Option goal;
        IdleEconomy.Option o = Advice(out found, out hasGoal, out goal);
        if (!found) return PurchaseResult.Invalid;
        if (o.kind == IdleEconomy.Kind.Hire) return BuyHire(o.station);
        return o.kind == IdleEconomy.Kind.Unlock ? BuyUnlock(o.station) : BuyUpgrade(o.station, o.speedAxis, 1);
    }

    // ------------------------------------------------------------------ commandes

    int UnlockedCount { get { int n = 0; foreach (var s in Stations) if (s.unlocked) n++; return n; } }

    public int ActiveSlots => g.slots;

    double Range(double a, double b) { return a + rng.NextDouble() * (b - a); }

    public Offer GenerateOffer()
    {
        var unlocked = new List<int>();
        for (int i = 0; i < Stations.Count; i++) if (Stations[i].unlocked) unlocked.Add(i);
        int station = rng.NextDouble() < cfg.lastStationChance ? unlocked[unlocked.Count - 1] : unlocked[rng.Next(unlocked.Count)];
        IdleEconomy.StationLevels st = Stations[station];
        double rate = Eco.ItemsPerSecond(station, st, g);
        int qty = Math.Max(cfg.minQuantity, (int)Math.Round(rate * Range(cfg.fillSecondsMin, cfg.fillSecondsMax)));
        if (qty > 40) qty = (int)Math.Round(qty / 10.0) * 10; else if (qty > 12) qty = (int)Math.Round(qty / 5.0) * 5;
        double reward = qty * Eco.MoneyPerItem(station, st, g) * cfg.rewardFactor * g.order;
        int pro = cfg.proBase + (station >= cfg.proFromStation ? 1 : 0) + (qty >= cfg.proBigQuantity ? 1 : 0);
        return new Offer { id = OrderSeq++, client = rng.Next(cfg.clientCount), station = station, qty = qty, reward = reward, pro = pro, say = rng.Next(cfg.sayCount) };
    }

    public void RefreshOffers()
    {
        if (!CrewReady) return;
        while (Offers.Count < cfg.offerCount && Now >= NextOfferAt) Offers.Add(GenerateOffer());
    }

    public bool Accept(int id)
    {
        int ix = Offers.FindIndex(o => o.id == id);
        if (ix < 0 || Active.Count >= ActiveSlots) return false;
        Offer o = Offers[ix];
        Offers.RemoveAt(ix);
        o.progress = 0;
        Active.Add(o);
        NextOfferAt = Now + cfg.nextOfferDelay;
        Offers.Add(GenerateOffer());
        Changed();
        return true;
    }

    public bool Decline(int id)
    {
        int ix = Offers.FindIndex(o => o.id == id);
        if (ix < 0) return false;
        Offers[ix] = GenerateOffer();
        Changed();
        return true;
    }

    public void OrderProgress(double dt, double mult)
    {
        List<Offer> done = null;
        foreach (Offer o in Active)
        {
            o.progress += Eco.ItemsPerSecond(o.station, Stations[o.station], g) * dt * mult;
            if (o.progress >= o.qty) { if (done == null) done = new List<Offer>(); done.Add(o); }
        }
        if (done != null) foreach (Offer o in done) Complete(o);
    }

    void Complete(Offer o)
    {
        Active.Remove(o);
        OrdersDone++;
        Cash += o.reward; Earned += o.reward;
        Pro += o.pro; ProTotal += o.pro;
        OnOrderCompleted?.Invoke(o);
        RefreshOffers();
        Changed();
    }

    // ------------------------------------------------------------------ Réputation

    public int RepLevel(int node) { return Rep[node]; }
    public bool RepMax(int node) { return Rep[node] >= cfg.reputation[node].maxLevel; }
    public long RepCost(int node) { return Eco.RepCost(node, Rep[node]); }
    public bool CanBuyRep(int node) { return !RepMax(node) && Pro >= RepCost(node); }

    public bool AnyRepAffordable
    {
        get { for (int i = 0; i < Rep.Length; i++) if (CanBuyRep(i)) return true; return false; }
    }

    public bool BuyRep(int node)
    {
        if (!CanBuyRep(node)) return false;
        Pro -= (int)RepCost(node);
        Rep[node]++;
        Recalc();
        RefreshOffers();
        Changed();
        return true;
    }

    // ------------------------------------------------------------------ turbo

    public bool TurboReady => Now >= BoostReadyAt;

    public bool TryTurbo()
    {
        if (!TurboReady) return false;
        BoostUntil = Now + g.turboLength;
        BoostReadyAt = BoostUntil + g.turboCooldown;
        Changed();
        return true;
    }

    // ------------------------------------------------------------------ hors ligne

    // Gain pendant une absence de `gapSeconds` : revenu de base (sans turbo), plafonné ; fait aussi avancer les commandes.
    public OfflineResult Offline(double gapSeconds)
    {
        var r = new OfflineResult();
        if (gapSeconds < cfg.offlineMinSeconds) return r;
        double cap = g.offlineHours * 3600;
        double t = Math.Min(gapSeconds, cap);
        r.seconds = t;
        r.gain = incomeCache * t;
        r.capped = gapSeconds > cap;
        r.showWelcome = gapSeconds >= cfg.welcomeMinSeconds;
        Cash += r.gain; Earned += r.gain;
        OrderProgress(t, 1);
        Changed();
        return r;
    }

    // ------------------------------------------------------------------ sauvegarde (v9)

    public SaveData ToSave(long utcSeconds)
    {
        var d = new SaveData
        {
            version = SaveData.CurrentVersion, cash = Cash, earned = Earned, buys = Buys, proPoints = Pro, proTotal = ProTotal,
            lastSaveUtc = utcSeconds, orderSeq = OrderSeq, ordersDone = OrdersDone,
            buyMode = BuyMode, nextOfferIn = Math.Max(0, NextOfferAt - Now), boostRemaining = BoostRemaining, boostCooldown = BoostCooldownRemaining,
            crew = Crew, chainCut = Chain.Cut, chainRaw = Chain.Raw, chainStock = Chain.Stock, chainLoad = Chain.Load, dronePhase = Chain.DronePhase,
        };
        foreach (var s in Stations) d.stations.Add(new SaveData.StationSave { unlocked = s.unlocked, speed = s.speed, add = s.add });
        for (int i = 0; i < Rep.Length; i++) d.reputation.Add(new SaveData.ReputationLevel { id = cfg.reputation[i].id, level = Rep[i] });
        foreach (Offer o in Offers) d.offers.Add(ToSave(o));
        foreach (Offer o in Active) d.active.Add(ToSave(o));
        return d;
    }

    static SaveData.OfferSave ToSave(Offer o)
    {
        return new SaveData.OfferSave { id = o.id, client = o.client, station = o.station, qty = o.qty, pro = o.pro, say = o.say, reward = o.reward, progress = o.progress };
    }

    static Offer FromSave(SaveData.OfferSave s)
    {
        return new Offer { id = s.id, client = s.client, station = s.station, qty = s.qty, pro = s.pro, say = s.say, reward = s.reward, progress = s.progress };
    }

    // Charge une sauvegarde déjà migrée en v9 ; tolère les valeurs absurdes (négatives, trop grandes, NaN).
    public void Load(SaveData d)
    {
        NewGame();
        Cash = Sane(d.cash, cfg.startCash); Earned = Sane(d.earned, 0); Buys = Math.Max(0, d.buys);
        Pro = Math.Max(0, d.proPoints); ProTotal = Math.Max(Pro, d.proTotal);
        for (int i = 0; i < Stations.Count && i < d.stations.Count; i++)
        {
            Stations[i].unlocked = cfg.stations[i].unlockPayback <= 0 || d.stations[i].unlocked;
            Stations[i].speed = Math.Max(0, Math.Min(d.stations[i].speed, 100000));
            Stations[i].add = Math.Max(0, Math.Min(d.stations[i].add, 100000));
        }
        foreach (SaveData.ReputationLevel r in d.reputation)
        {
            int ix = Eco.RepIndex(r.id);
            if (ix >= 0) Rep[ix] = Math.Max(0, Math.Min(r.level, cfg.reputation[ix].maxLevel));
        }
        Crew = Math.Max(0, Math.Min(d.crew, cfg.hirePrices.Length));
        Cash = Math.Max(Cash, RemainingHireCost());      // une sauvegarde abîmée ne bloque jamais le démarrage
        Chain.Cut = Sane(d.chainCut, 0); Chain.Raw = Sane(d.chainRaw, 0); Chain.Stock = Sane(d.chainStock, 0); Chain.Load = Sane(d.chainLoad, 0);
        Chain.DronePhase = Math.Min(Sane(d.dronePhase, 0), 0.999);
        BuyMode = d.buyMode == 10 || d.buyMode == 0 ? d.buyMode : 1;
        OrderSeq = Math.Max(1, d.orderSeq); OrdersDone = Math.Max(0, d.ordersDone);
        NextOfferAt = Now + Math.Max(0, d.nextOfferIn);
        BoostUntil = Now + Math.Max(0, d.boostRemaining);
        BoostReadyAt = Now + Math.Max(0, d.boostCooldown);
        Recalc();
        foreach (SaveData.OfferSave s in d.offers) if (ValidOffer(s)) Offers.Add(FromSave(s));
        foreach (SaveData.OfferSave s in d.active) if (ValidOffer(s) && Active.Count < ActiveSlots) Active.Add(FromSave(s));
        RefreshOffers();
        Changed();
    }

    bool ValidOffer(SaveData.OfferSave s)
    {
        return s.station >= 0 && s.station < Stations.Count && Stations[s.station].unlocked && s.qty > 0 && s.client >= 0 && s.client < cfg.clientCount;
    }

    static double Sane(double v, double fallback)
    {
        return double.IsNaN(v) || double.IsInfinity(v) || v < 0 ? fallback : v;
    }
}
