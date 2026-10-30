using System;
using System.Collections.Generic;

// Règles pures du jeu idle (U1) : revenus, coûts, paliers, options d'achat et achat conseillé. Aucune dépendance à Unity,
// aucune valeur en dur : tout vient de IdleEconomyConfig. Port à l'identique de usine-web/src/econ.js.
public class IdleEconomy
{
    public class StationLevels
    {
        public bool unlocked;
        public int speed;
        public int add;
        public int slots = 1;      // machines posées
        public StationLevels Copy() { return new StationLevels { unlocked = unlocked, speed = speed, add = add, slots = slots }; }
    }

    public struct Globals
    {
        public double revenue, speed, cost, order, offlineHours, turboLength, turboCooldown;
        public int slots;
    }

    public enum Kind { Upgrade, Unlock, Hire, Slot }

    public struct Option
    {
        public Kind kind;
        public int station;
        public bool speedAxis;     // vrai : Vitesse, faux : Ajouter (inutilisé pour un déblocage)
        public int count;
        public double cost;
        public double delta;       // gain exact de revenu par seconde
        public double payback;     // coût / gain
        public bool milestone;     // l'achat franchit un palier x2
    }

    public const int SerreStation = 0;   // le début de la chaîne

    readonly IdleEconomyConfig config;
    readonly double[] baseSpeedCost, baseAddCost, flowScratch, soldScratch;

    public IdleEconomyConfig Config => config;
    public int StationCount => config.stations.Length;

    public IdleEconomy(IdleEconomyConfig config)
    {
        this.config = config;
        int n = config.stations.Length;
        baseSpeedCost = new double[n];
        baseAddCost = new double[n];
        flowScratch = new double[n];
        soldScratch = new double[n];
        for (int i = 0; i < n; i++)
        {
            double i0 = config.stations[i].value / config.stations[i].cycle;                       // revenu par seconde au niveau 0
            baseSpeedCost[i] = Math.Max(config.costSpeedFloor, i0 * config.costSpeedFactor);
            baseAddCost[i] = Math.Max(config.costAddFloor, i0 * config.costAddFactor);
        }
    }

    // ------------------------------------------------------------------ paliers

    public int MilestoneCount(int totalLevel)
    {
        int n = 0;
        foreach (int m in config.milestones) { if (totalLevel >= m) n++; else break; }
        return n;
    }

    // Aspect de la machine d'un poste : 0 au départ, +1 à chaque niveau de `aspectLevels` atteint.
    public int AspectOf(int totalLevel)
    {
        int aspect = 0;
        foreach (int level in config.aspectLevels) if (totalLevel >= level) aspect++;
        return aspect;
    }

    public int NextMilestone(int totalLevel)
    {
        foreach (int m in config.milestones) if (totalLevel < m) return m;
        return -1;
    }

    // ------------------------------------------------------------------ bonus de Réputation

    public int RepIndex(string id)
    {
        for (int i = 0; i < config.reputation.Length; i++) if (config.reputation[i].id == id) return i;
        return -1;
    }

    public Globals GlobalsFor(int[] rep)
    {
        int Lv(string id) { int i = RepIndex(id); return rep != null && i >= 0 && i < rep.Length ? rep[i] : 0; }
        var g = new Globals
        {
            revenue = 1 + config.repRevenuePerLevel * Lv("rev"),
            speed = 1 + config.repSpeedPerLevel * Lv("spd"),
            cost = Math.Max(config.repCostFloor, 1 - config.repCostPerLevel * Lv("cost")),
            order = 1 + config.repOrderPerLevel * Lv("ordr"),
            offlineHours = config.offlineBaseHours + config.offlineHoursPerLevel * Lv("off"),
            slots = 1 + Lv("slot"),
            turboLength = config.turboBaseSeconds + config.turboSecondsPerLevel * Lv("turbo"),
            turboCooldown = Math.Max(config.turboCooldownFloor, config.turboBaseCooldown - config.turboCooldownPerLevel * Lv("turbo")),
        };
        return g;
    }

    public long RepCost(int node, int level)
    {
        IdleEconomyConfig.RepNode r = config.reputation[node];
        return (long)Math.Ceiling(r.baseCost * Math.Pow(r.mul, level));
    }

    // ------------------------------------------------------------------ revenus

    // Un palier double le débit de la machine, pas la valeur de la pièce : une pièce vaut ainsi toujours plus au poste suivant, et ouvrir un poste ne fait jamais perdre d'argent.
    public double ItemsPerSecond(int i, StationLevels st, Globals g)
    {
        return (1 + st.add) * (1 + config.speedStep * st.speed) * Math.Pow(2, MilestoneCount(st.speed + st.add)) * st.slots * g.speed / config.stations[i].cycle;
    }

    public double MoneyPerItem(int i, Globals g) { return config.stations[i].value * g.revenue; }

    // Ce qui traverse la chaîne en régime établi : chaque poste prend ce que le précédent lui donne, dans la limite de son débit ; ce que le suivant n'absorbe pas est vendu au prix du poste.
    // `flow[i]` : pièces par seconde à la sortie du poste i ; `sold[i]` : pièces vendues par seconde à ce poste.
    public void Flows(IList<StationLevels> st, Globals g, double[] flow, double[] sold)
    {
        int n = st.Count;
        double through = ItemsPerSecond(SerreStation, st[SerreStation], g);
        flow[SerreStation] = through;
        for (int i = 1; i < n; i++)
        {
            through = st[i].unlocked ? Math.Min(through, ItemsPerSecond(i, st[i], g)) : 0;
            flow[i] = through;
        }
        sold[SerreStation] = 0;
        for (int i = 1; i < n; i++) sold[i] = flow[i] - (i + 1 < n ? flow[i + 1] : 0);
    }

    public double TotalIncome(IList<StationLevels> st, Globals g)
    {
        Flows(st, g, flowScratch, soldScratch);
        double t = 0;
        for (int i = 1; i < st.Count; i++) t += soldScratch[i] * MoneyPerItem(i, g);
        return t;
    }

    // Gain de revenu si le poste i passe à `changed`.
    double GainWith(IList<StationLevels> st, Globals g, int i, StationLevels changed)
    {
        StationLevels before = st[i];
        double now = TotalIncome(st, g);
        st[i] = changed;
        double after = TotalIncome(st, g);
        st[i] = before;
        return after - now;
    }

    // Gain de revenu de k niveaux de plus : nul sur un poste qui n'est pas le goulot, sauf s'il franchit un palier.
    public double UpgradeGain(IList<StationLevels> st, Globals g, int i, bool speedAxis, int k) { return GainWith(st, g, i, With(st[i], speedAxis, k)); }

    public double SlotGain(IList<StationLevels> st, Globals g, int i)
    {
        StationLevels more = st[i].Copy();
        more.slots++;
        return GainWith(st, g, i, more);
    }

    public double UnlockGain(IList<StationLevels> st, Globals g, int i)
    {
        StationLevels open = st[i].Copy();
        open.unlocked = true;
        return GainWith(st, g, i, open);
    }

    // ------------------------------------------------------------------ coûts

    double BaseCost(bool speedAxis, int i) { return speedAxis ? baseSpeedCost[i] : baseAddCost[i]; }
    double Growth(bool speedAxis) { return speedAxis ? config.growthSpeed : config.growthAdd; }

    // Coût exact (réel) de k niveaux de plus à partir du niveau L : somme géométrique.
    public double BulkCost(bool speedAxis, int i, int level, int k, Globals g)
    {
        double r = Growth(speedAxis);
        return BaseCost(speedAxis, i) * g.cost * Math.Pow(r, level) * (Math.Pow(r, k) - 1) / (r - 1);
    }

    // Nombre maximal de niveaux achetables avec `cash`.
    public int MaxAffordable(bool speedAxis, int i, int level, double cash, Globals g)
    {
        double r = Growth(speedAxis), c1 = BaseCost(speedAxis, i) * g.cost * Math.Pow(r, level);
        if (cash < c1) return 0;
        int k = (int)Math.Floor(Math.Log(1 + cash * (r - 1) / c1) / Math.Log(r));
        while (k > 0 && BulkCost(speedAxis, i, level, k, g) > cash) k--;
        return Math.Max(0, k);
    }

    // Prix affiché et payé : entier, arrondi au plafond ; au-delà de ce qu'un entier contient, il reste au maximum (jamais négatif, donc jamais gratuit).
    public static long Ceil(double cost) { return cost >= 9.2e18 ? long.MaxValue : (long)Math.Ceiling(cost - 1e-9); }

    public double UnlockCost(int i, Globals g) { return config.stations[i].value / config.stations[i].cycle * config.stations[i].unlockPayback * g.cost; }

    // Prix de la machine suivante d'un poste qui en a déjà `owned`.
    public double SlotCost(int i, int owned, Globals g)
    {
        IdleEconomyConfig.Station last = config.stations[config.stations.Length - 1];
        return last.value / last.cycle * config.slotPayback[owned - 1] * Math.Pow(config.slotStep, i - 1) * g.cost;
    }

    public StationLevels With(StationLevels st, bool speedAxis, int k)
    {
        return new StationLevels { unlocked = st.unlocked, slots = st.slots, speed = st.speed + (speedAxis ? k : 0), add = st.add + (speedAxis ? 0 : k) };
    }

    // ------------------------------------------------------------------ options d'achat et meilleur achat

    public List<Option> Options(IList<StationLevels> st, Globals g, int k)
    {
        var result = new List<Option>();
        int nextLocked = -1;
        for (int i = 0; i < st.Count; i++)
        {
            StationLevels s = st[i];
            if (!s.unlocked) { if (nextLocked < 0) nextLocked = i; continue; }
            for (int axis = 0; axis < 2; axis++)
            {
                bool speedAxis = axis == 0;
                double cost = BulkCost(speedAxis, i, speedAxis ? s.speed : s.add, k, g);
                double delta = UpgradeGain(st, g, i, speedAxis, k);
                result.Add(new Option
                {
                    kind = Kind.Upgrade, station = i, speedAxis = speedAxis, count = k, cost = cost, delta = delta,
                    payback = delta > 0 ? cost / delta : double.PositiveInfinity,
                    milestone = MilestoneCount(s.speed + s.add + k) > MilestoneCount(s.speed + s.add),
                });
            }
            if (s.slots < config.stations[i].slots)
            {
                double cost = SlotCost(i, s.slots, g);
                double delta = SlotGain(st, g, i);
                result.Add(new Option { kind = Kind.Slot, station = i, count = 1, cost = cost, delta = delta, payback = delta > 0 ? cost / delta : double.PositiveInfinity });
            }
        }
        if (nextLocked >= 0)
        {
            double cost = UnlockCost(nextLocked, g);
            double delta = UnlockGain(st, g, nextLocked);
            result.Add(new Option { kind = Kind.Unlock, station = nextLocked, count = 1, cost = cost, delta = delta, payback = delta > 0 ? cost / delta : double.PositiveInfinity });
        }
        return result;
    }

    double Score(Option o) { return Score(o, config.newStationFavor); }
    static double Score(Option o, double favorUnlock) { return o.kind == Kind.Upgrade ? o.payback : o.payback * favorUnlock; }

    // Meilleur délai de remboursement (le nouveau poste est favorisé si `favorUnlock` < 1) ; `found` faux si rien n'est achetable.
    public Option Best(List<Option> options, double favorUnlock, out bool found)
    {
        found = false;
        Option best = default(Option);
        double bestScore = double.PositiveInfinity;
        foreach (Option o in options)
        {
            double score = Score(o, favorUnlock);
            if (double.IsInfinity(score) || double.IsNaN(score)) continue;
            if (!found || score < bestScore) { best = o; bestScore = score; found = true; }
        }
        if (found) return best;
        // plusieurs postes à égalité parfaite : aucun achat seul ne rapporte, le moins cher débloque la situation
        foreach (Option o in options) if (!found || o.cost < best.cost) { best = o; found = true; }
        return best;
    }

    // Achat conseillé : le meilleur ; si son prix est à plus de `goalFarSeconds` de revenu, le meilleur achat abordable
    // maintenant (`goal` contient alors le vrai objectif). `income` = revenu par seconde réel (turbo compris).
    public Option Advice(IList<StationLevels> st, Globals g, double cash, double income, out bool found, out bool hasGoal, out Option goal)
    {
        List<Option> options = Options(st, g, 1);
        Option best = Best(options, config.newStationFavor, out found);
        hasGoal = false;
        goal = default(Option);
        if (!found) return best;
        if (cash < best.cost && (best.cost - cash) / Math.Max(0.01, income) > config.goalFarSeconds)
        {
            bool haveAlt = false;
            Option alt = default(Option);
            double altScore = double.PositiveInfinity;
            foreach (Option o in options)
            {
                double score = Score(o);
                if (o.cost <= cash && !double.IsInfinity(score) && (!haveAlt || score < altScore)) { alt = o; altScore = score; haveAlt = true; }
            }
            if (haveAlt) { goal = best; hasGoal = true; best = alt; }
        }
        return best;
    }
}
