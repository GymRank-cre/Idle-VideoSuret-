using UnityEngine;

// Réglages du retour d'information visuel (chiffres flottants, éclats, "pop" du compteur).
[CreateAssetMenu(fileName = "FeedbackConfig", menuName = "Idle/Feedback Config")]
public class FeedbackConfig : ScriptableObject
{
    [Header("Chiffre flottant (+10)")]
    [SerializeField, Min(0.1f)] float floatDuration = 0.9f;  // secondes
    [SerializeField, Min(0f)] float floatHeight = 1.3f;      // mètres montés pendant la durée
    [SerializeField] Color floatColor = new Color(0.55f, 1f, 0.45f);

    [Header("Gain d'un poste (+x)")]
    [SerializeField, Min(0.1f)] float messageDuration = 1.1f;
    [SerializeField, Min(0f)] float messageRise = 1.1f;
    [SerializeField] Color messageColor = new Color32(0x8C, 0xFF, 0x9E, 0xFF);

    [Header("Éclats de particules")]
    [SerializeField, Min(1)] int burstCount = 10;
    [SerializeField, Min(0.1f)] float burstLifetime = 0.5f;
    [SerializeField, Min(0f)] float burstSpeed = 2.5f;
    [SerializeField, Min(0.01f)] float burstSize = 0.17f;
    [SerializeField] Color crateBurstColor = new Color(0.95f, 0.75f, 0.45f); // caisse arrivée au poste
    [SerializeField] Color billBurstColor = new Color(0.5f, 1f, 0.5f);       // billet ramassé

    [Header("Pop du compteur de caisses")]
    [SerializeField, Min(1f)] float popScale = 1.3f;
    [SerializeField, Min(0.1f)] float popRecover = 8f;

    public float FloatDuration => floatDuration;
    public float FloatHeight => floatHeight;
    public Color FloatColor => floatColor;
    public float MessageDuration => messageDuration;
    public float MessageRise => messageRise;
    public Color MessageColor => messageColor;
    public int BurstCount => burstCount;
    public float BurstLifetime => burstLifetime;
    public float BurstSpeed => burstSpeed;
    public float BurstSize => burstSize;
    public Color CrateBurstColor => crateBurstColor;
    public Color BillBurstColor => billBurstColor;
    public float PopScale => popScale;
    public float PopRecover => popRecover;
}
