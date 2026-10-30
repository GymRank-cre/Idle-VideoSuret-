using System;

// La matière qui circule au début de la chaîne : le robot découpeur coupe les cristaux, le robot graveur les change en puces, le drone les porte au stock
// et l'Assemblage vide ce stock. Chaque étape attend la précédente et s'arrête quand la suivante est pleine : c'est ce qui rend un goulot visible.
// Sans Unity (testable en simulation). Les quantités sont décimales : les débits dépassent vite 100 pièces par seconde.
public class IdleChain
{
    const double MaxStep = 0.25;   // un long pas de temps est découpé : le drone ne saute jamais un aller-retour

    readonly IdleEconomyConfig cfg;

    public double Cut, Raw, Stock, Load;        // cristaux coupés, puces gravées, puces en stock, puces sur le drone
    public double DronePhase;                   // 0 = posé ; de 0 à 0,5 : aller ; de 0,5 à 1 : retour
    public bool Cutting, Etching, Assembling;   // ce qui a tourné au dernier pas de temps

    public IdleChain(IdleEconomyConfig config)
    {
        cfg = config;
    }

    public void Reset()
    {
        Cut = Raw = Stock = Load = DronePhase = 0;
        Cutting = Etching = Assembling = false;
    }

    // Tas entre deux robots : quelques secondes de production.
    public double BufferSize(double rateIn) { return Math.Max(cfg.chainBufferMin, rateIn * cfg.chainBufferSeconds); }

    public double StockSize(double rateIn, double rateOut) { return Math.Max(cfg.chainStockMin, Math.Max(rateIn, rateOut) * cfg.chainStockSeconds); }

    // Avance de `dt` secondes avec `crew` robots recrutés, dans l'ordre ; renvoie le nombre de pièces sorties de l'Assemblage.
    public double Step(double dt, int crew, double rateIn, double rateOut)
    {
        double made = 0;
        int steps = Math.Max(1, (int)Math.Ceiling(dt / MaxStep));
        for (int i = 0; i < steps; i++) made += Advance(dt / steps, crew, rateIn, rateOut);
        return made;
    }

    double Advance(double dt, int crew, double rateIn, double rateOut)
    {
        double buffer = BufferSize(rateIn);

        double cut = crew >= 1 ? Math.Min(rateIn * dt, Math.Max(0, buffer - Cut)) : 0;
        Cut += cut;
        Cutting = cut > 0;

        double etched = crew >= 2 ? Math.Min(Math.Min(rateIn * dt, Cut), Math.Max(0, buffer - Raw)) : 0;
        Cut -= etched;
        Raw += etched;
        Etching = etched > 0;

        if (crew >= 3) Fly(dt, StockSize(rateIn, rateOut));

        double made = Math.Min(rateOut * dt, Stock);
        Stock -= made;
        Assembling = made > 0;
        return made;
    }

    void Fly(double dt, double stockSize)
    {
        if (DronePhase <= 0)
        {
            double take = Math.Min(Raw, stockSize - Stock);
            if (take < cfg.droneMinLoad) return;
            Raw -= take;
            Load = take;
        }
        double before = DronePhase;
        DronePhase += dt / cfg.droneTrip;
        if (before < 0.5 && DronePhase >= 0.5) { Stock += Load; Load = 0; }
        if (DronePhase >= 1) DronePhase = 0;
    }
}
