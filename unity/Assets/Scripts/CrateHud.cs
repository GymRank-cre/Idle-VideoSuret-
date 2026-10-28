using UnityEngine;
using UnityEngine.UI;

// Affiche le stock de caisses du joueur (en haut à droite) : Count/Capacity.
public class CrateHud : MonoBehaviour
{
    [SerializeField] StackCarrier carrier;
    [SerializeField] Text label;

    void OnEnable()
    {
        carrier.OnCountChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        carrier.OnCountChanged -= Refresh;
    }

    void Refresh()
    {
        label.text = carrier.Count + "/" + carrier.Capacity;
    }
}
