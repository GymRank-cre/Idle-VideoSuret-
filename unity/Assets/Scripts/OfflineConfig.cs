using UnityEngine;

// Réglages des gains hors ligne. Aucun employé embauché (porteur ET livreur) : aucun gain.
[CreateAssetMenu(fileName = "OfflineConfig", menuName = "Idle/Offline Config")]
public class OfflineConfig : ScriptableObject
{
    [SerializeField, Min(0.1f)] float maxHours = 4f;          // durée d'absence prise en compte au maximum
    [SerializeField, Range(0f, 1f)] float efficiency = 0.2f;  // part de la cadence théorique réellement gagnée
    [SerializeField, Min(0f)] float minSeconds = 60f;         // en dessous, pas d'écran « Bon retour »
    [Header("Cadence mesurée des employés (secondes par aller-retour complet)")]
    [SerializeField, Min(1f)] float haulerCycleSeconds = 9.2f; // tas -> poste -> tas avec sa pleine charge (mesuré en Play en 11a-4 : ~9 s)
    [SerializeField, Min(1f)] float courierCycleSeconds = 14f; // plateau -> comptoir -> plateau avec sa pleine charge

    public float MaxHours => maxHours;
    public float Efficiency => efficiency;
    public float MinSeconds => minSeconds;
    public float HaulerCycleSeconds => haulerCycleSeconds;
    public float CourierCycleSeconds => courierCycleSeconds;
}
