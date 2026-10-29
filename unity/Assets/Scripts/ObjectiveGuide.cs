using UnityEngine;
using UnityEngine.UI;

// Guide du début de partie sans texte long : une flèche 3D flotte au-dessus du joueur et pointe vers l'objectif,
// avec une courte phrase en haut de l'écran. Chaque étape se valide toute seule d'après l'état du jeu.
// Quand la dernière étape est validée, le guide disparaît pour toujours (PlayerPrefs).
public class ObjectiveGuide : MonoBehaviour
{
    enum Step { CollectCrates, DepositCrates, TakeProduct, DeliverProduct, CollectBills, PayPad, HireEmployee, Done }

    [SerializeField] GuideConfig config;
    [SerializeField] Transform player;
    [SerializeField] StackCarrier crates;
    [SerializeField] ProductCarrier products;
    [SerializeField] Wallet wallet;
    [SerializeField] Transform pile;
    [SerializeField] Transform station;
    [SerializeField] Transform tray;
    [SerializeField] Transform counter;
    [SerializeField] GameObject payPadObject;     // la dalle du 2e poste (se désactive quand elle est payée)
    [SerializeField] GameObject hirePadObject;    // la dalle d'embauche
    [SerializeField] Transform arrow;             // flèche 3D (enfant de rien : placée par ce script)
    [SerializeField] Text label;
    [SerializeField] GameObject labelRoot;

    Step step;
    string[] phrases;
    int moneyAtStart;   // argent au lancement : un billet encaissé = argent supérieur
    float checkTimer;

    void Start()
    {
        phrases = config.Phrases;
        moneyAtStart = wallet.Money;
        if (PlayerPrefs.GetInt(config.DoneKey, 0) == 1)
        {
            Finish(false);
            return;
        }
        step = Step.CollectCrates;
        // partie rechargée au milieu du guide : on reprend après la dalle déjà payée
        if (payPadObject != null && !payPadObject.activeSelf) step = Step.PayPad;
        ShowPhrase();
    }

    void Update()
    {
        if (step == Step.Done) return;

        // Les états coûteux (recherche des produits et billets) ne sont lus que quelques fois par seconde.
        checkTimer += Time.deltaTime;
        if (checkTimer >= 0.2f)
        {
            checkTimer = 0f;
            Advance();
        }
        UpdateArrow();
    }

    // Passe aux étapes suivantes tant que la condition de l'étape courante est remplie.
    void Advance()
    {
        int guard = 0;
        while (step != Step.Done && IsDone(step) && guard++ < 8)
        {
            step++;
            if (step == Step.Done) { Finish(true); return; }
            ShowPhrase();
        }
    }

    bool IsDone(Step s)
    {
        switch (s)
        {
            case Step.CollectCrates:
                return crates.Count >= config.CratesToCollect;
            case Step.DepositCrates:
                // un produit est apparu (sur le plateau ou déjà dans les mains)
                return products.Count > 0 || AnyProductOnTray();
            case Step.TakeProduct:
                return products.Count > 0;
            case Step.DeliverProduct:
                // le client est servi : des billets sont sur le comptoir, ou l'argent a déjà augmenté
                return wallet.Money > moneyAtStart || AnyBillOnCounter();
            case Step.CollectBills:
                return wallet.Money > moneyAtStart;
            case Step.PayPad:
                return payPadObject == null || !payPadObject.activeSelf;
            case Step.HireEmployee:
                // la dalle d'embauche est cachée tant que la dalle du poste 2 n'est pas payée
                return hirePadObject == null || (!hirePadObject.activeSelf && (payPadObject == null || !payPadObject.activeSelf));
        }
        return false;
    }

    bool AnyProductOnTray()
    {
        foreach (Product p in FindObjectsByType<Product>(FindObjectsSortMode.None))
            if (p.gameObject.activeInHierarchy && !p.IsCollecting) return true;
        return false;
    }

    bool AnyBillOnCounter()
    {
        foreach (Bill b in FindObjectsByType<Bill>(FindObjectsSortMode.None))
            if (b.gameObject.activeInHierarchy && !b.IsCollecting && b.transform.position.y > 0.5f) return true;
        return false;
    }

    Transform Target()
    {
        switch (step)
        {
            case Step.CollectCrates: return pile;
            case Step.DepositCrates: return station;
            case Step.TakeProduct: return tray;
            case Step.DeliverProduct: return counter;
            case Step.CollectBills: return NearestBill() ?? counter;
            case Step.PayPad: return payPadObject != null ? payPadObject.transform : null;
            case Step.HireEmployee: return hirePadObject != null ? hirePadObject.transform : null;
        }
        return null;
    }

    Transform NearestBill()
    {
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Bill b in FindObjectsByType<Bill>(FindObjectsSortMode.None))
        {
            if (!b.gameObject.activeInHierarchy || b.IsCollecting) continue;
            float d = (b.transform.position - player.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = b.transform; }
        }
        return best;
    }

    void UpdateArrow()
    {
        Transform target = Target();
        if (target == null) { arrow.gameObject.SetActive(false); return; }

        Vector3 flat = target.position - player.position;
        flat.y = 0f;
        bool arrived = flat.magnitude < config.HideDistance;
        arrow.gameObject.SetActive(!arrived);
        if (arrived) return;

        float bob = Mathf.Sin(Time.time * config.ArrowBobSpeed) * config.ArrowBob;
        arrow.position = player.position + Vector3.up * (config.ArrowHeight + bob);
        arrow.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    void ShowPhrase()
    {
        if (label != null && (int)step < phrases.Length) label.text = phrases[(int)step];
    }

    void Finish(bool remember)
    {
        step = Step.Done;
        if (remember)
        {
            PlayerPrefs.SetInt(config.DoneKey, 1);
            PlayerPrefs.Save();
        }
        if (arrow != null) arrow.gameObject.SetActive(false);
        if (labelRoot != null) labelRoot.SetActive(false);
    }
}
