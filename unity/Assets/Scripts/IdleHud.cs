using UnityEngine;
using UnityEngine.UI;

// Haut de l'écran (U3a) : argent, revenu par seconde, Points Pro, badges et bouton Turbo. Affiche l'état d'IdleGame, ne décide rien.
// Les textes sont rafraîchis 10 fois par seconde et à chaque changement du jeu, pas à chaque image (moins de texte à reconstruire).
public class IdleHud : MonoBehaviour
{
    [SerializeField] Text cashText;
    [SerializeField] Text rateText;
    [SerializeField] GameObject proPill;
    [SerializeField] Text proText;
    [SerializeField] GameObject ordersBadge;
    [SerializeField] Text ordersBadgeText;
    [SerializeField] GameObject reputationBadge;
    [SerializeField] Button turboButton;
    [SerializeField] Image turboFace;
    [SerializeField] Sprite turboIdleFace;
    [SerializeField] Sprite turboActiveFace;
    [SerializeField] Image turboCover;      // voile sur le bouton : sombre pendant la recharge, éclat qui pulse pendant le turbo
    [SerializeField] Text turboText;
    [SerializeField] Color cooldownCover = new Color(0.055f, 0.086f, 0.125f, 0.55f);
    [SerializeField, Min(0.02f)] float refreshEvery = 0.1f;
    [SerializeField, Min(0f)] float glowStrength = 0.18f;
    [SerializeField, Min(0.1f)] float glowSpeed = 3.14f;

    IdleGame subscribed;
    float timer;
    bool dirty = true;

    void Start()
    {
        turboButton.onClick.AddListener(OnTurbo);
    }

    void Subscribe(IdleGame game)
    {
        if (subscribed != null) return;
        subscribed = game;
        game.OnChanged += MarkDirty;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnChanged -= MarkDirty;
    }

    void MarkDirty() { dirty = true; }

    void OnTurbo()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null) return;
        AudioManager.Play(host.Game.TryTurbo() ? SfxId.Unlock : SfxId.Refuse);
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleGame game = host.Game;
        Subscribe(game);

        if (game.BoostActive && !MotionSettings.ReduceMotion)   // l'éclat du turbo actif bouge à chaque image, le reste non
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * glowSpeed);
            turboCover.color = new Color(1f, 1f, 1f, pulse * glowStrength);
        }

        timer -= Time.unscaledDeltaTime;
        if (timer > 0f && !dirty) return;
        timer = refreshEvery;
        dirty = false;
        Refresh(game);
    }

    void Refresh(IdleGame game)
    {
        cashText.text = NumberFormat.Short((long)game.Cash);
        rateText.text = NumberFormat.Rate(game.Income);

        bool showPro = game.ProTotal > 0 || game.OrdersDone > 0;
        if (proPill.activeSelf != showPro) proPill.SetActive(showPro);
        if (showPro) proText.text = NumberFormat.Short(game.Pro);

        int offers = game.Offers.Count > 0 && game.Active.Count < game.ActiveSlots ? game.Offers.Count : 0;
        if (ordersBadge.activeSelf != (offers > 0)) ordersBadge.SetActive(offers > 0);
        if (offers > 0) ordersBadgeText.text = offers.ToString();
        bool repReady = game.AnyRepAffordable;
        if (reputationBadge.activeSelf != repReady) reputationBadge.SetActive(repReady);

        RefreshTurbo(game);
    }

    void RefreshTurbo(IdleGame game)
    {
        bool on = game.BoostActive;
        double wait = game.BoostCooldownRemaining;
        turboFace.sprite = on ? turboActiveFace : turboIdleFace;
        bool busy = on || wait > 0;
        if (turboCover.gameObject.activeSelf != busy) turboCover.gameObject.SetActive(busy);
        if (on)
        {
            turboText.text = NumberFormat.Seconds(game.BoostRemaining);
            if (MotionSettings.ReduceMotion) turboCover.color = Color.clear;
        }
        else if (wait > 0)
        {
            turboCover.color = cooldownCover;
            turboText.text = NumberFormat.Wait(wait);
        }
    }
}
