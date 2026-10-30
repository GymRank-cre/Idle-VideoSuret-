using System;

// La matière qui circule dans toute la chaîne : le robot découpeur coupe les cristaux, le robot graveur les change en puces, le drone les porte au premier stock ;
// chaque poste vide son stock, et un chariot porte ce qui en sort au stock du poste suivant. Chaque étape attend la précédente. Ce qui ne trouve plus de place devant
// le poste suivant est vendu au prix du poste : un goulot ne bloque pas l'usine, il coûte de l'argent, et ça se voit. Le dernier poste ouvert vend tout.
// Sans Unity (testable en simulation). Les quantités sont décimales : les débits dépassent vite 100 pièces par seconde.
public class IdleChain
{
    const double MaxStep = 0.25;   // un long pas de temps est découpé : un porteur ne saute jamais un aller-retour

    // Le drone (indice 0) ou un chariot : il prend ce qu'il peut, fait l'aller, dépose, revient.
    public class Carrier
    {
        public double Load, Phase;   // Phase : 0 = à l'arrêt au départ ; de 0 à 0,5 : aller ; de 0,5 à 1 : retour
    }

    readonly IdleEconomyConfig cfg;
    readonly int count;

    public double Cut, Raw;              // cristaux coupés, puces gravées
    public readonly double[] Stock;      // pièces qui attendent devant chaque poste
    public readonly double[] Output;     // pièces sorties de chaque poste, que le chariot n'a pas encore prises
    public readonly double[] Sold;       // pièces vendues à chaque poste pendant le dernier Step
    public readonly Carrier[] Carriers;  // [i] porte ce qui sort du poste i
    public readonly bool[] Working;      // ce qui a tourné au dernier pas de temps
    public bool Etching;

    public IdleChain(IdleEconomyConfig config)
    {
        cfg = config;
        count = config.stations.Length;
        Stock = new double[count];
        Output = new double[count];
        Sold = new double[count];
        Working = new bool[count];
        Carriers = new Carrier[count];
        for (int i = 0; i < count; i++) Carriers[i] = new Carrier();
    }

    public void Reset()
    {
        Cut = Raw = 0;
        Etching = false;
        for (int i = 0; i < count; i++)
        {
            Stock[i] = Output[i] = Sold[i] = 0;
            Carriers[i].Load = Carriers[i].Phase = 0;
            Working[i] = false;
        }
    }

    // Tas entre deux robots : quelques secondes de production.
    public double BufferSize(double rateIn) { return Math.Max(cfg.chainBufferMin, rateIn * cfg.chainBufferSeconds); }

    // Sortie d'un poste : peu de place, pour que le surplus se vende tout de suite.
    public double OutputSize(double rate) { return Math.Max(cfg.chainBufferMin, rate * cfg.chainOutputSeconds); }

    // Stock devant un poste : il garde quelques secondes de ce que ce poste consomme.
    public double StockSize(double rate) { return Math.Max(cfg.chainStockMin, rate * cfg.chainStockSeconds); }

    // Avance de `dt` secondes : `crew` robots recrutés, `rates` le débit de chaque poste, `open` le nombre de postes ouverts depuis le début de la chaîne. `Sold` dit ce qui a été vendu.
    public void Step(double dt, int crew, double[] rates, int open)
    {
        for (int i = 0; i < count; i++) Sold[i] = 0;
        int steps = Math.Max(1, (int)Math.Ceiling(dt / MaxStep));
        for (int i = 0; i < steps; i++) Advance(dt / steps, crew, rates, open);
    }

    void Advance(double dt, int crew, double[] rates, int open)
    {
        double buffer = BufferSize(rates[0]);

        double cut = crew >= 1 ? Math.Min(rates[0] * dt, Math.Max(0, buffer - Cut)) : 0;
        Cut += cut;
        Working[0] = cut > 0;

        double etched = crew >= 2 ? Math.Min(Math.Min(rates[0] * dt, Cut), Math.Max(0, buffer - Raw)) : 0;
        Cut -= etched;
        Raw += etched;
        Etching = etched > 0;

        if (crew >= 3 && open > 1) Carry(Carriers[0], ref Raw, ref Stock[1], StockSize(rates[1]), dt, cfg.droneTrip);

        for (int i = 1; i < open; i++)
        {
            double made = Math.Min(rates[i] * dt, Stock[i]);
            Stock[i] -= made;
            Working[i] = made > 0;
            if (i + 1 >= open) { Sold[i] += made; continue; }

            Output[i] += made;
            double size = OutputSize(rates[i]);
            if (Output[i] > size) { Sold[i] += Output[i] - size; Output[i] = size; }
            Carry(Carriers[i], ref Output[i], ref Stock[i + 1], StockSize(rates[i + 1]), dt, cfg.cartTrip);
        }
    }

    void Carry(Carrier c, ref double from, ref double to, double size, double dt, double trip)
    {
        if (c.Phase <= 0)
        {
            double take = Math.Min(from, size - to);
            if (take < cfg.carrierMinLoad) return;
            from -= take;
            c.Load = take;
        }
        double before = c.Phase;
        c.Phase += dt / trip;
        if (before < 0.5 && c.Phase >= 0.5) { to += c.Load; c.Load = 0; }
        if (c.Phase >= 1) c.Phase = 0;
    }
}
