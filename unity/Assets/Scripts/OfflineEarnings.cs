using UnityEngine;

// Calcule le gain pendant l'absence du joueur, d'après les employés embauchés et les réglages (aucun chiffre en dur).
// La chaîne la plus lente limite le débit : poste, caisses apportées par le porteur, livraisons du livreur, arrivée des clients.
public static class OfflineEarnings
{
    // Chaîne Test -> Emballage entièrement automatique (tapis achetés) : débits des deux machines et valeur du produit final.
    public struct Chain
    {
        public bool automated;
        public float testThroughput;   // produits par seconde
        public float packThroughput;
        public float valueMultiplier;  // x2,4 : multiplicateur du dernier maillon
        public float assemblySpeed;    // améliorations de l'assemblage (11b) : 0 = pas d'amélioration, sinon facteur de vitesse
        public int assemblyExtraProducts; // produits en plus par travail
    }

    public static int Compute(long elapsedSeconds, bool hasHauler, bool hasCourier, OfflineConfig offline,
        StationConfig station, EmployeeConfig hauler, EmployeeConfig courier, CustomerConfig customers,
        float jobDurationMultiplier = 1f, float employeeSpeedMultiplier = 1f, float payMultiplier = 1f, int employeeCapacityBonus = 0, Chain chain = default(Chain))
    {
        if (!hasHauler || !hasCourier) return 0;
        if (station.Products == null || station.Products.Length == 0) return 0;

        long seconds = elapsedSeconds < 0 ? 0 : elapsedSeconds; // jamais négatif (horloge reculée)
        if (seconds < offline.MinSeconds) return 0;
        double capped = Mathf.Min(seconds, (offline.MaxHours + Bonuses.OfflineCapHours) * 3600f);

        double assemblySpeed = (chain.assemblySpeed > 0f ? chain.assemblySpeed : 1.0) * Bonuses.ChainSpeed;
        int productsPerJob = Mathf.Min(station.ProductsPerJob + chain.assemblyExtraProducts, station.OutputCapacity);
        double stationRate = productsPerJob / ((double)station.JobDuration * jobDurationMultiplier / assemblySpeed);
        double haulerRate = (hauler.Capacity + employeeCapacityBonus) / ((double)offline.HaulerCycleSeconds / employeeSpeedMultiplier) * station.ProductsPerJob / station.CratesPerJob;
        double courierRate = (courier.Capacity + employeeCapacityBonus) / ((double)offline.CourierCycleSeconds / employeeSpeedMultiplier);
        double customerRate = (customers.MinQuantity + customers.MaxQuantity) * 0.5 / customers.SpawnInterval;
        double rate = System.Math.Min(System.Math.Min(stationRate, haulerRate), System.Math.Min(courierRate, customerRate));
        double chainMultiplier = 1.0;
        if (chain.automated)
        {
            rate = System.Math.Min(rate, System.Math.Min(chain.testThroughput, chain.packThroughput)); // le plus lent des maillons
            chainMultiplier = chain.valueMultiplier;
        }

        double sum = 0;
        int count = 0;
        foreach (ProductType t in station.Products)
        {
            if (t == null) continue;
            sum += t.Value;
            count++;
        }
        if (count == 0) return 0;

        return (int)(rate * (sum / count) * chainMultiplier * payMultiplier * Bonuses.Revenue * Bonuses.OfflineGain * capped * offline.Efficiency);
    }
}
