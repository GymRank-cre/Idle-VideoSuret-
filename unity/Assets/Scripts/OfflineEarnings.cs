using UnityEngine;

// Calcule le gain pendant l'absence du joueur, d'après les employés embauchés et les réglages (aucun chiffre en dur).
// La chaîne la plus lente limite le débit : poste, caisses apportées par le porteur, livraisons du livreur, arrivée des clients.
public static class OfflineEarnings
{
    public static int Compute(long elapsedSeconds, bool hasHauler, bool hasCourier, OfflineConfig offline,
        StationConfig station, EmployeeConfig hauler, EmployeeConfig courier, CustomerConfig customers)
    {
        if (!hasHauler || !hasCourier) return 0;
        if (station.Products == null || station.Products.Length == 0) return 0;

        long seconds = elapsedSeconds < 0 ? 0 : elapsedSeconds; // jamais négatif (horloge reculée)
        if (seconds < offline.MinSeconds) return 0;
        double capped = Mathf.Min(seconds, offline.MaxHours * 3600f);

        double stationRate = station.ProductsPerJob / (double)station.JobDuration;
        double haulerRate = hauler.Capacity / (double)offline.HaulerCycleSeconds / station.CratesPerJob;
        double courierRate = courier.Capacity / (double)offline.CourierCycleSeconds;
        double customerRate = (customers.MinQuantity + customers.MaxQuantity) * 0.5 / customers.SpawnInterval;
        double rate = System.Math.Min(System.Math.Min(stationRate, haulerRate), System.Math.Min(courierRate, customerRate));

        double sum = 0;
        int count = 0;
        foreach (ProductType t in station.Products)
        {
            if (t == null) continue;
            sum += t.Value;
            count++;
        }
        if (count == 0) return 0;

        return (int)(rate * (sum / count) * capped * offline.Efficiency);
    }
}
