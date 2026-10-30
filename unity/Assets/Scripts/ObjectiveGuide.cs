using UnityEngine;
using UnityEngine.UI;

// Guide du début de partie sans texte long : une flèche 3D flotte au-dessus du joueur et pointe vers l'objectif,
// avec une courte phrase en haut de l'écran. Chaque étape se valide toute seule d'après l'état du jeu.
// Quand la dernière étape est validée, le guide disparaît pour toujours (PlayerPrefs).
public class ObjectiveGuide : MonoBehaviour
{
    enum Step { CollectCrates, DepositCrates, TakeProduct, DeliverProduct, CollectBills, PayPad, HireEmployee, HireCourier, BuyTest, BuyPack, BuyBelt, OpenUpgrades, Done }

    public static int PhraseCount => (int)Step.Done;   // une phrase par étape : vérifié par les tests

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
    [SerializeField] GameObject hirePadObject;    // la dalle d'embauche du porteur
    [SerializeField] GameObject courierPadObject; // la dalle d'embauche du livreur
    [SerializeField] UnlockPad testPad;           // P1 : les achats de machines (dans l'ordre livreur -> Test -> Emballage -> tapis)
    [SerializeField] UnlockPad packPad;
    [SerializeField] UnlockPad beltPad;
    [SerializeField] RectTransform hand;          // P1 : main qui pointe et tape (facultative)
    [SerializeField] UpgradePanel upgradePanel;   // bouton Améliorations (dernière étape)
    [SerializeField] Transform arrow;             // flèche 3D (enfant de rien : placée par ce script)
    [SerializeField] Text label;
    [SerializeField] GameObject labelRoot;
    [Header("Objectif réel après le tutoriel (11c-bis)")]
    [SerializeField] OrderBoard board;
    [SerializeField] MachineUpgradeState machineUpgrades;
    [SerializeField] MachineUpgradeAdvisor advisor;

    Step step;
    string[] phrases;
    int moneyAtStart;   // argent au lancement : un billet encaissé = argent supérieur
    float checkTimer;
    float playSeconds;  // temps de jeu écoulé pendant le guide (la main s'éteint après config.HandSeconds)

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

    float lateTimer;

    void Update()
    {
        if (step == Step.Done)
        {
            lateTimer += Time.deltaTime;
            if (lateTimer >= 0.5f) { lateTimer = 0f; LateGuide(); }
            return;
        }

        // le joueur est déjà en pleine chaîne (Emballage débloquée) : le tutoriel du début n'a plus lieu d'être
        if (board != null && board.Unlocked && board.gameObject.activeInHierarchy)
        {
            if (step < Step.BuyBelt && beltPad != null && !beltPad.IsUnlocked) { step = Step.BuyBelt; ShowPhrase(); }   // il ne reste que le tapis à montrer
            else if (beltPad == null || beltPad.IsUnlocked) Finish(true);
        }
        playSeconds += Time.deltaTime;

        // Les états coûteux (recherche des produits et billets) ne sont lus que quelques fois par seconde.
        checkTimer += Time.deltaTime;
        if (checkTimer >= 0.2f)
        {
            checkTimer = 0f;
            Advance();
        }
        UpdateArrow();
        UpdateHand();
    }

    public void RefreshHand(float aspect) { UpdateHand(aspect); }   // utilisé par la capture d'écran (rendu hors fenêtre de jeu)

    // P1 : la main tape sur l'objectif (écran) pendant les premières minutes, puis seulement pour les achats de machines.
    void UpdateHand(float aspect = 0f)
    {
        if (hand == null) return;
        Transform target = Target();
        bool wanted = target != null && target.gameObject.activeInHierarchy && (playSeconds < config.HandSeconds || step >= Step.BuyTest);
        Camera cam = Camera.main;
        Canvas canvas = hand.GetComponentInParent<Canvas>();
        if (!wanted || cam == null || canvas == null) { if (hand.gameObject.activeSelf) hand.gameObject.SetActive(false); return; }

        // coordonnées de viewport (0..1) : indépendantes de la taille de l'écran, de la zone sûre et du mode du canvas
        Vector3 point = target.position + Vector3.up * 0.4f;
        Vector3 vp = cam.WorldToViewportPoint(point);
        if (aspect > 0f)   // rendu dans une texture d'un autre format que la fenêtre : projection recalculée avec cet aspect
        {
            Matrix4x4 m = Matrix4x4.Perspective(cam.fieldOfView, aspect, cam.nearClipPlane, cam.farClipPlane) * cam.worldToCameraMatrix;
            Vector4 clip = m * new Vector4(point.x, point.y, point.z, 1f);
            vp = new Vector3(clip.x / clip.w * 0.5f + 0.5f, clip.y / clip.w * 0.5f + 0.5f, clip.w);
        }
        bool visible = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        if (visible)
        {
            var root = (RectTransform)canvas.rootCanvas.transform;
            Vector3 world = root.TransformPoint(new Vector3(root.rect.xMin + vp.x * root.rect.width, root.rect.yMin + vp.y * root.rect.height, 0f));
            Vector2 local = ((RectTransform)hand.parent).InverseTransformPoint(world);
            float tap = Mathf.Abs(Mathf.Sin(Time.unscaledTime * config.HandTapSpeed));   // 0 = en l'air, 1 = au contact
            hand.anchoredPosition = local + Vector2.down * (config.HandTapDistance * (1f - tap));
            hand.localScale = Vector3.one * (1f - 0.1f * tap);
        }
        if (hand.gameObject.activeSelf != visible) hand.gameObject.SetActive(visible);
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
            case Step.HireCourier:
                return courierPadObject == null || (!courierPadObject.activeSelf && !hirePadObject.activeSelf);
            case Step.BuyTest:
                return testPad == null || testPad.IsUnlocked;
            case Step.BuyPack:
                return packPad == null || packPad.IsUnlocked;
            case Step.BuyBelt:
                return beltPad == null || beltPad.IsUnlocked;
            case Step.OpenUpgrades:
                return upgradePanel == null || upgradePanel.HasBeenOpened;
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
            case Step.HireCourier: return courierPadObject != null ? courierPadObject.transform : null;
            case Step.BuyTest: return testPad != null ? testPad.transform : null;
            case Step.BuyPack: return packPad != null ? packPad.transform : null;
            case Step.BuyBelt: return beltPad != null ? beltPad.transform : null;
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
        arrow.localScale = Vector3.one * 0.75f;   // plus petite : elle ne recouvre plus les textes au sol
        arrow.position = player.position + Vector3.up * (config.ArrowHeight + 0.5f + bob);
        arrow.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    void ShowPhrase()
    {
        if (label != null && (int)step < phrases.Length) label.text = phrases[(int)step];
        if (upgradePanel != null) upgradePanel.SetHighlight(step == Step.OpenUpgrades); // le bouton bat pendant la dernière étape
    }

    // Après le tutoriel : une commande à prendre d'abord, puis la machine à améliorer si on en a les moyens, sinon silence.
    void LateGuide()
    {
        if (label == null || labelRoot == null) return;
        string text = null;
        if (board != null && board.HasActive) text = Loc.T("guide.deliver");   // une commande est en cours : la barre compacte affiche le détail
        else if (board != null && board.Unlocked && board.Offers.Count > 0) text = Loc.T("guide.order");
        else if (machineUpgrades != null && advisor != null && !string.IsNullOrEmpty(advisor.Bottleneck)) // machine goulot abordable
        {
            string id = advisor.Bottleneck;
            int cost = machineUpgrades.IsMax(id, MachineUpgradeState.Speed) ? int.MaxValue : machineUpgrades.NextCost(id, MachineUpgradeState.Speed);
            int i = machineUpgrades.Config.IndexOf(id);
            if (i >= 0 && wallet.Money >= cost) text = Loc.T("guide.upgrade", Loc.Or("machine." + id, machineUpgrades.Config.Machines[i].displayName));
        }
        if (text == null) { if (labelRoot.activeSelf) labelRoot.SetActive(false); return; }
        label.text = text;
        if (!labelRoot.activeSelf) labelRoot.SetActive(true);
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
        if (hand != null) hand.gameObject.SetActive(false);
        if (labelRoot != null) labelRoot.SetActive(false);
        if (upgradePanel != null) upgradePanel.SetHighlight(false);
    }
}
