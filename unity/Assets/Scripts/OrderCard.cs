using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Une carte du panneau des commandes : portrait (formes simples), contenu, valeur, boutons Refuser / Accepter / x2.
public class OrderCard : MonoBehaviour
{
    [SerializeField] Image bg;
    [SerializeField] Image skin;
    [SerializeField] Image earLeft;
    [SerializeField] Image earRight;
    [SerializeField] Image nose;
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
        bg.color = c.backdrop;
        skin.color = c.skin; hair.color = c.hair; cloth.color = c.cloth;
        earLeft.color = earRight.color = c.skin;
        nose.color = Color.Lerp(c.skin, Color.black, 0.18f);
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
            sb.Append(o.counts[i]).Append(' ').Append(Plural(board.Types[i].DisplayName, o.counts[i]));
        }
        return sb.ToString();
    }

    // « Caméra dôme » -> « caméras dôme », « Badgeuse » -> « badgeuses » : minuscule, « s » sur le premier mot si plusieurs.
    public static string Plural(string name, int count)
    {
        if (string.IsNullOrEmpty(name)) return name;
        string lower = char.ToLower(name[0]) + name.Substring(1);
        if (count <= 1) return lower;
        int space = lower.IndexOf(' ');
        return space < 0 ? lower + "s" : lower.Substring(0, space) + "s" + lower.Substring(space);
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
