using UnityEngine;

// Réglages d'une dalle de déblocage.
[CreateAssetMenu(fileName = "UnlockConfig", menuName = "Idle/Unlock Config")]
public class UnlockConfig : ScriptableObject
{
    [SerializeField, Min(1)] int price = 100;               // prix total
    [SerializeField, Min(1)] int payStep = 5;               // argent retiré à chaque pas
    [SerializeField, Min(0.01f)] float payInterval = 0.05f; // secondes entre deux pas

    public int Price => price;
    public int PayStep => payStep;
    public float PayInterval => payInterval;
}
