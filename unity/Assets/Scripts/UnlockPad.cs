using UnityEngine;

// Dalle de déblocage : tant que le joueur reste dessus, son argent la paie
// petit à petit et le prix descend. À 0, la dalle disparaît, `target` apparaît
// et les dalles suivantes (`revealOnUnlock`) se dévoilent une à une.
[RequireComponent(typeof(Collider))]
public class UnlockPad : MonoBehaviour
{
    [SerializeField] UnlockConfig config;
    [SerializeField] Wallet wallet;
    [SerializeField] GameObject target;   // objet débloqué, désactivé au départ
    [SerializeField] TextMesh priceLabel; // prix restant, affiché au-dessus de la dalle
    [SerializeField] GameObject[] revealOnUnlock; // dalles suivantes, cachées tant que celle-ci n'est pas payée
    [SerializeField] FeedbackSpawner feedback;    // facultatif : étincelle quand une dalle suivante apparaît
    [SerializeField] CameraFollow cameraFollow;   // facultatif : montre brièvement la dalle qui apparaît
    [SerializeField] Transform fillBar;           // barre qui se remplit au fil du paiement (bord de la dalle)
    [SerializeField] float fillWidth = 3f;
    [SerializeField] bool isHire;                 // dalle d'embauche : fanfare au lieu de l'arpège
    [SerializeField] string saveId;               // identifiant stable pour la sauvegarde

    int remainingPrice;
    bool initialized;
    float timer;
    float sparkTimer;

    public string SaveId => saveId;
    public int RemainingPrice { get { Init(); return remainingPrice; } } // une dalle encore cachée n'a pas eu de Awake
    public bool IsUnlocked => RemainingPrice <= 0;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        Init();
    }

    void Init()
    {
        if (initialized) return;
        initialized = true;
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
        sparkTimer += config.PayInterval;
        if (feedback != null && sparkTimer >= 0.12f && remainingPrice > 0)
        {
            sparkTimer = 0f; // étincelles pendant le paiement
            feedback.Sparkle(transform.position + new Vector3(Random.Range(-1.2f, 1.2f), 0.3f, Random.Range(-1.2f, 1.2f)));
        }

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
            priceLabel.text = NumberFormat.Short(remainingPrice); // l'icône de billet est à gauche du nombre
        if (fillBar != null && config.Price > 0)
        {
            float k = 1f - (float)remainingPrice / config.Price;
            fillBar.localScale = new Vector3(Mathf.Max(0.0001f, fillWidth * k), fillBar.localScale.y, fillBar.localScale.z);
            Vector3 lp = fillBar.localPosition;
            fillBar.localPosition = new Vector3(-fillWidth * 0.5f + fillWidth * k * 0.5f, lp.y, lp.z);
        }
    }

    // Rechargement d'une partie : remet le prix restant ; à 0 la dalle est déjà payée.
    public void Restore(int remaining)
    {
        initialized = true;
        remainingPrice = Mathf.Clamp(remaining, 0, config.Price);
        UpdateLabel();
        if (remainingPrice <= 0)
            Unlock(true);
    }

    void Unlock(bool silent = false)
    {
        if (!silent) GameLog.Event("déblocage " + (string.IsNullOrEmpty(saveId) ? name : saveId));
        if (!silent)
        {
            AudioManager.Play(isHire ? SfxId.Hire : SfxId.Unlock);
            AudioManager.Vibrate();
        }
        if (!silent && feedback != null)
        {
            feedback.Confetti(transform.position);            // vraie fête : confettis,
            if (cameraFollow != null)
            {
                cameraFollow.Shake(0.09f, 0.25f);             // secousse légère
                cameraFollow.ZoomPulse(0.05f, 0.3f);          // et pulsation de zoom
            }
        }
        target.SetActive(true); // PopIn sur la cible fait le gonflement
        foreach (GameObject next in revealOnUnlock)
        {
            if (next == null || next.activeSelf) continue;
            UnlockPad nextPad = next.GetComponent<UnlockPad>();
            if (nextPad != null && nextPad.IsUnlocked) continue; // déjà payée (partie rechargée)
            next.SetActive(true); // PopIn sur la dalle fait le gonflement
            if (silent) continue;
            if (feedback != null) feedback.ProductReady(next.transform.position + Vector3.up * 0.3f);
            if (cameraFollow != null && nextPad != null) cameraFollow.Peek(next.transform, 1.2f);
        }
        gameObject.SetActive(false);
    }
}
