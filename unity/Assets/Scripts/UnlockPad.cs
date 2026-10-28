using UnityEngine;

// Dalle de déblocage : tant que le joueur reste dessus, son argent la paie
// petit à petit et le prix descend. À 0, la dalle disparaît et `target` apparaît.
[RequireComponent(typeof(Collider))]
public class UnlockPad : MonoBehaviour
{
    [SerializeField] UnlockConfig config;
    [SerializeField] Wallet wallet;
    [SerializeField] GameObject target;   // objet débloqué, désactivé au départ
    [SerializeField] TextMesh priceLabel; // prix restant, affiché au-dessus de la dalle

    int remainingPrice;
    float timer;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        remainingPrice = config.Price;
        UpdateLabel();
    }

    void OnTriggerStay(Collider other)
    {
        if (remainingPrice <= 0 || other.GetComponent<PlayerController>() == null) return;

        timer += Time.deltaTime;
        if (timer < config.PayInterval) return;
        timer = 0f;

        // Sans argent le prix reste où il est : rien n'est perdu.
        int amount = Mathf.Min(config.PayStep, remainingPrice, wallet.Money);
        if (amount <= 0 || !wallet.TrySpend(amount)) return;

        remainingPrice -= amount;
        UpdateLabel();

        if (remainingPrice <= 0)
            Unlock();
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null)
            timer = 0f;
    }

    void UpdateLabel()
    {
        if (priceLabel != null)
            priceLabel.text = "$ " + remainingPrice;
    }

    void Unlock()
    {
        target.SetActive(true); // PopIn sur la cible fait le gonflement
        gameObject.SetActive(false);
    }
}
