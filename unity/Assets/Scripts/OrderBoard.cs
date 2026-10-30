using System;
using System.Collections.Generic;
using UnityEngine;

// Tableau des commandes (11c) : génère les offres (seulement quand l'Emballage existe), les fait expirer, gère la commande
// en cours (compteur par type), la patience du client et la récompense ($ + Points Pro). Une seule responsabilité :
// l'état des commandes ; l'interface, le quai et la camionnette l'écoutent par ses événements.
public class OrderBoard : MonoBehaviour
{
    [Serializable]
    public class Offer
    {
        public int clientIndex;
        public int tier;
        public int[] counts;     // par type de produit
        public int reward;
        public int pro;
        public float lifetime;   // secondes restantes avant expiration
    }

    [SerializeField] OrderConfig config;
    [SerializeField] ProductType[] types;     // dôme, badgeuse, alarme
    [SerializeField] GameObject packMachine;  // l'Emballage : pas de commande tant qu'elle n'existe pas
    [SerializeField] Wallet wallet;
    [SerializeField] ProPoints pro;
    [SerializeField] GameObject dockObject;   // le quai apparaît avec l'Emballage

    readonly List<Offer> offers = new List<Offer>();
    Offer active;
    int[] delivered;
    float patienceLeft;
    float patienceTotal;
    float offerTimer;
    bool firstOffered;

    public OrderConfig Config => config;
    public ProductType[] Types => types;
    public IReadOnlyList<Offer> Offers => offers;
    public Offer Active => active;
    public bool HasActive => active != null;
    public bool Unlocked => packMachine == null || packMachine.activeInHierarchy;
    public int Delivered(int typeIndex) { return delivered != null && typeIndex < delivered.Length ? delivered[typeIndex] : 0; }
    public int ActiveTotal { get { return TotalOf(active); } }
    public int ActiveDelivered { get { int n = 0; if (delivered != null) foreach (int d in delivered) n += d; return n; } }
    public float PatienceFraction => active == null || patienceTotal <= 0f ? 1f : Mathf.Clamp01(patienceLeft / patienceTotal);
    // 0 = content, 1 = neutre, 2 = en colère
    public int Mood => PatienceFraction > 0.5f ? 0 : PatienceFraction > 0.2f ? 1 : 2;

    public event Action OnChanged;
    public event Action<Offer> OnAccepted;
    public event Action<Offer> OnCompleted;
    public event Action<Offer> OnFailed;
    public event Action OnFirstOffer;

    public static int TotalOf(Offer o)
    {
        if (o == null || o.counts == null) return 0;
        int n = 0;
        foreach (int c in o.counts) n += c;
        return n;
    }

    void Awake()
    {
        offerTimer = config.FirstOfferDelay;
    }

    void Update() { Tick(Time.deltaTime); }

    // Avance les minuteurs de `dt` secondes (public pour les tests).
    public void Tick(float dt)
    {
        bool changed = false;
        if (dockObject != null && dockObject.activeSelf != Unlocked) dockObject.SetActive(Unlocked);
        if (Unlocked)
        {
            offerTimer -= dt;
            if (offerTimer <= 0f && offers.Count < config.MaxOffers + Bonuses.OfferSlots)
            {
                offers.Add(Generate(!firstOffered));
                if (!firstOffered) { firstOffered = true; OnFirstOffer?.Invoke(); }
                offerTimer = UnityEngine.Random.Range(config.OfferDelayMin, config.OfferDelayMax);
                changed = true;
            }
            else if (offerTimer <= 0f)
            {
                offerTimer = 1f; // pleine : on réessaie bientôt
            }
        }

        for (int i = offers.Count - 1; i >= 0; i--)
        {
            offers[i].lifetime -= dt;
            if (offers[i].lifetime <= 0f) { offers.RemoveAt(i); changed = true; }
        }

        if (active != null)
        {
            patienceLeft -= dt;
            if (patienceLeft <= 0f)
            {
                Offer failed = active;
                active = null;
                OnFailed?.Invoke(failed); // aucune pénalité d'argent : seulement pas de récompense
                changed = true;
            }
        }
        if (changed) OnChanged?.Invoke();
    }

    // Offre : types et quantités tirés selon le palier ; la toute première est facile (1 colis).
    public Offer Generate(bool easy)
    {
        OrderConfig.Tier[] tiers = config.Tiers;
        int tierIndex = 0;
        var o = new Offer { counts = new int[types.Length], lifetime = config.OfferLifetime };
        o.clientIndex = UnityEngine.Random.Range(0, config.Clients.Length);

        if (easy)
        {
            o.counts[0] = 1;
        }
        else
        {
            float total = 0f;
            foreach (OrderConfig.Tier t in tiers) total += t.weight;
            float r = UnityEngine.Random.value * total;
            for (int i = 0; i < tiers.Length; i++) { r -= tiers[i].weight; if (r <= 0f) { tierIndex = i; break; } }
            int n = UnityEngine.Random.Range(tiers[tierIndex].minItems, tiers[tierIndex].maxItems + 1);
            for (int k = 0; k < n; k++) o.counts[UnityEngine.Random.Range(0, types.Length)]++;
        }
        o.tier = tierIndex;

        int baseValue = 0;
        for (int i = 0; i < types.Length; i++) baseValue += types[i].Value * o.counts[i];
        o.reward = Mathf.RoundToInt(config.Reward(baseValue) * Bonuses.Revenue);
        o.pro = config.ProPoints(o.reward);
        return o;
    }

    // Ajoute une offre à la liste (démonstration, tests).
    public void AddOffer(Offer offer)
    {
        offers.Add(offer);
        OnChanged?.Invoke();
    }

    public bool Accept(int index)
    {
        if (index < 0 || index >= offers.Count || active != null) return false; // une commande en cours à la fois
        active = offers[index];
        offers.RemoveAt(index);
        delivered = new int[types.Length];
        patienceTotal = patienceLeft = config.Tiers[active.tier].patience * Bonuses.OrderPatience;
        OnAccepted?.Invoke(active);
        OnChanged?.Invoke();
        return true;
    }

    public void Refuse(int index)
    {
        if (index < 0 || index >= offers.Count) return;
        offers.RemoveAt(index);
        OnChanged?.Invoke();
    }

    // Reste-t-il à livrer un produit de ce type ?
    public bool Needs(ProductType type)
    {
        int i = IndexOf(type);
        return active != null && i >= 0 && delivered[i] < active.counts[i];
    }

    public int Remaining(int typeIndex)
    {
        return active == null ? 0 : Mathf.Max(0, active.counts[typeIndex] - delivered[typeIndex]);
    }

    public int IndexOf(ProductType type)
    {
        for (int i = 0; i < types.Length; i++) if (types[i] == type) return i;
        return -1;
    }

    // Un colis livré au quai ; renvoie vrai s'il comptait. Commande finie : $ et Points Pro crédités.
    public bool Deliver(ProductType type)
    {
        if (!Needs(type)) return false;
        delivered[IndexOf(type)]++;
        if (ActiveDelivered >= ActiveTotal)
        {
            Offer done = active;
            active = null;
            if (wallet != null) wallet.Add(done.reward);
            if (pro != null) pro.Add(done.pro);
            GameLog.Event("commande livrée +" + done.reward + " $ +" + done.pro + " Pro");
            OnCompleted?.Invoke(done);
        }
        OnChanged?.Invoke();
        return true;
    }

    // Types encore attendus (le livreur décide d'aller au quai).
    public void GetNeeded(List<ProductType> into)
    {
        into.Clear();
        if (active == null) return;
        for (int i = 0; i < types.Length; i++) if (delivered[i] < active.counts[i]) into.Add(types[i]);
    }
}
