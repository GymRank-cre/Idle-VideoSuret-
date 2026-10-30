using UnityEngine;

// Commande livrée (U3c) : son, message, puces +$ et +Pro vers les pastilles, confettis au-dessus du poste.
public class IdleOrderFeedback : MonoBehaviour
{
    [SerializeField] IdleToasts toasts;
    [SerializeField] IdleChips chips;
    [SerializeField] RectTransform chipOrigin;
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] IdleStationView[] stations;

    IdleGame subscribed;

    void Start()
    {
        subscribed = IdleHost.Instance.Game;
        subscribed.OnOrderCompleted += Delivered;
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnOrderCompleted -= Delivered;
    }

    void Delivered(IdleGame.Offer order)
    {
        AudioManager.Play(SfxId.OrderPaid);
        string money = "+" + NumberFormat.Short((long)order.reward);
        toasts.Show(Loc.T("orders.done.toast", Loc.T("client." + order.client + ".name"), money, order.pro));
        chips.Fly(chipOrigin.position, money, false);
        chips.Fly(chipOrigin.position, "+" + order.pro + " Pro", true);
        if (!MotionSettings.ReduceMotion) feedback.Confetti(stations[order.station].transform.position);
    }
}
