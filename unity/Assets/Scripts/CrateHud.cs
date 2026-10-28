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
        // la capacité est plus petite et plus discrète que le nombre de caisses
        label.text = carrier.Count + "<size=44><color=#FFFFFFAA>/" + carrier.Capacity + "</color></size>";
    }
}
