using UnityEngine;
using UnityEngine.UI;

// « Bon retour ! » (U3c) : ce que l'usine a gagné pendant l'absence. Le gain est déjà compté par IdleGame ; Collecter ferme et fait voler la puce vers l'argent.
public class IdleWelcomeWindow : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Text durationText;
    [SerializeField] Text gainText;
    [SerializeField] Button collectButton;
    [SerializeField] IdleChips chips;

    double gain;
    IdleHost subscribed;

    void Start()
    {
        collectButton.onClick.AddListener(Collect);
        subscribed = IdleHost.Instance;
        subscribed.OnWelcomeBack += Show;
        if (subscribed.LastOffline.showWelcome) Show(subscribed.LastOffline);
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.OnWelcomeBack -= Show;
    }

    public void Show(IdleGame.OfflineResult result)
    {
        gain = result.gain;
        durationText.text = Loc.T(result.capped ? "welcome.worked.capped" : "welcome.worked", NumberFormat.Time(result.seconds));
        gainText.text = "+" + NumberFormat.Short((long)gain);
        root.SetActive(true);
    }

    void Collect()
    {
        root.SetActive(false);
        AudioManager.Play(SfxId.OrderPaid);
        chips.Fly(collectButton.transform.position, "+" + NumberFormat.Short((long)gain), false);
    }
}
