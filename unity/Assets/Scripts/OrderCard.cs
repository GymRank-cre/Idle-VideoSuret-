using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Une carte du panneau des commandes : portrait (formes simples), contenu, valeur, boutons Refuser / Accepter / x2.
public class OrderCard : MonoBehaviour
{
    [SerializeField] Image skin;
    [SerializeField] Image hair;
    [SerializeField] Image cloth;
    [SerializeField] Text nameText;
    [SerializeField] Text contentText;
    [SerializeField] Text rewardText;
    [SerializeField] Button accept;
    [SerializeField] Button refuse;
    [SerializeField] Button later;     // x2 (factice : « Bientôt »)
    [SerializeField] Text laterLabel;

    float laterTimer;

    public Button Accept => accept;
    public Button Refuse => refuse;
    public Button Later => later;

    public void Bind(OrderBoard board, int index)
    {
        OrderBoard.Offer o = board.Offers[index];
        OrderConfig.Client c = board.Config.Clients[o.clientIndex];
        skin.color = c.skin; hair.color = c.hair; cloth.color = c.cloth;
        nameText.text = c.displayName + " - " + c.job;
        contentText.text = Describe(board, o);
        rewardText.text = "$ " + NumberFormat.Short(o.reward) + "   " + Loc.T("orders.reward", "", o.pro).Trim();
        accept.interactable = !board.HasActive;
    }

    // « 3 caméras dôme + 2 badgeuses »
    public static string Describe(OrderBoard board, OrderBoard.Offer o)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < o.counts.Length; i++)
        {
            if (o.counts[i] <= 0) continue;
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(o.counts[i]).Append(' ').Append(board.Types[i].DisplayName);
        }
        return sb.ToString();
    }

    public void ShowLater()
    {
        laterTimer = 1.2f;
        laterLabel.text = Loc.T("orders.soon");
        AudioManager.Play(SfxId.Refuse);
    }

    void Update()
    {
        if (laterTimer <= 0f) return;
        laterTimer -= Time.unscaledDeltaTime;
        if (laterTimer <= 0f) laterLabel.text = Loc.T("orders.x2");
    }
}
