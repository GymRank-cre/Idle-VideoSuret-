// Point d'entrée unique des bonus de Réputation (11d) : les systèmes lisent leurs multiplicateurs ici, et nulle part ailleurs.
// Valeurs neutres tant qu'aucun bonus n'est acheté. Remplies par ReputationState (et par les tests).
public static class Bonuses
{
    public static float Revenue = 1f;            // x paiements
    public static float ChainSpeed = 1f;         // x vitesse des machines
    public static float OfflineGain = 1f;        // x gains hors ligne
    public static float OfflineCapHours = 0f;    // + heures de plafond hors ligne
    public static int StackCapacity = 0;         // + caisses (technicien)
    public static float PlayerSpeed = 1f;        // x vitesse du technicien
    public static float OrderPatience = 1f;      // x patience des clients de commande
    public static int OfferSlots = 0;            // + offres affichées
    public static int ActiveOrders = 0;          // + commandes en parallèle
    public static float PadPrice = 1f;           // x prix des dalles de déblocage

    public static void Reset()
    {
        Revenue = ChainSpeed = OfflineGain = PlayerSpeed = OrderPatience = PadPrice = 1f;
        OfflineCapHours = 0f;
        StackCapacity = OfferSlots = ActiveOrders = 0;
    }
}
