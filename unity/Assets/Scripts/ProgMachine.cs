using UnityEngine;

// Programmation : une baie de serveurs dont l'écran clignote pendant que la caméra posée sur la table reçoit son programme. La caméra apparaît à 30 % du cycle.
public class ProgMachine : MonoBehaviour, IMachineAnim
{
    [SerializeField] Renderer screen;
    [SerializeField] Material screenA;
    [SerializeField] Material screenB;
    [SerializeField] GameObject product;
    [SerializeField, Min(0.5f)] float blinkRate = 3f;

    bool screenState = true;

    public void Animate(float phase, float time)
    {
        bool showProduct = phase > 0.3f;
        if (product.activeSelf != showProduct) product.SetActive(showProduct);
        bool on = Mathf.FloorToInt(time * blinkRate) % 2 == 0;
        if (on != screenState) { screenState = on; screen.sharedMaterial = on ? screenA : screenB; }
    }
}
