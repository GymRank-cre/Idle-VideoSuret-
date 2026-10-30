using UnityEngine;
using UnityEngine.UI;

// Une ligne de l'arbre Réputation : icône, nom, effet actuel -> suivant, niveau, bouton Acheter (prix en Points Pro).
public class ReputationRow : MonoBehaviour
{
    [SerializeField] Image card;
    [SerializeField] Image iconBack;
    [SerializeField] Text iconText;           // facultatif (ancien système)
    [SerializeField] Image iconImage;         // pictogramme
    [SerializeField] int nodeIndex;           // noeud de la config représenté par cette ligne
    [SerializeField] Text nameText;
    [SerializeField] Text effectText;
    [SerializeField] Text levelText;
    [SerializeField] Button buy;
    [SerializeField] Image buyBack;
    [SerializeField] Text buyText;
    [SerializeField] GameObject star;

    ReputationState state;
    int index;
    float popTime = -1f;
    Vector3 baseScale;

    public Button Buy => buy;
    public int NodeIndex => nodeIndex;

    public void Init(ReputationState s)
    {
        state = s;
        index = nodeIndex;
        int i = index;
        baseScale = transform.localScale;
        buy.onClick.AddListener(() => state.TryBuy(index));
        ReputationConfig.Node n = s.Config.Nodes[i];
        iconBack.color = n.iconColor;
        if (iconText != null) iconText.text = "";
        nameText.text = n.displayName;
        Refresh();
    }

    public void Pop() { popTime = 0f; }

    public void Refresh()
    {
        ReputationConfig.Node n = state.Config.Nodes[index];
        int level = state.Level(index);
        levelText.text = Loc.T("rep.level", level, n.maxLevel);

        float now = state.Config.Total(n, level), next = state.Config.Total(n, Mathf.Min(level + 1, n.maxLevel));
        effectText.text = state.IsMax(index)
            ? string.Format(n.description, now.ToString("0"))
            : string.Format(n.description, now.ToString("0")) + "  >  " + next.ToString("0");

        string missing = state.MissingPrerequisite(index);
        bool locked = missing != null || !n.implemented;
        card.color = locked ? new Color32(0xDD, 0xD3, 0xC2, 0xFF) : new Color32(0xFF, 0xF1, 0xD6, 0xFF);

        if (!n.implemented) { SetBuy(Loc.T("rep.soon"), false, false); return; }
        if (missing != null) { effectText.text = Loc.T("rep.locked", missing); SetBuy(Loc.T("rep.lockedshort"), false, false); return; } // « Prérequis : ... » sous le nom, jamais dans le bouton
        if (state.IsMax(index)) { SetBuy(Loc.T("rep.max"), false, false); return; }
        SetBuy(NumberFormat.Short(state.NextCost(index)), state.CanBuy(index), true);
    }

    void SetBuy(string text, bool canBuy, bool showStar)
    {
        buyText.text = text;
        buy.interactable = canBuy;
        buyBack.color = canBuy ? (Color)new Color32(0x3D, 0xDC, 0x5A, 0xFF) : new Color32(0x9A, 0xA3, 0xAE, 0xFF);
        star.SetActive(showStar);
    }

    void Update()
    {
        if (popTime < 0f) return;
        popTime += Time.deltaTime;
        float k = Mathf.Clamp01(popTime / 0.35f);
        transform.localScale = baseScale * (1f + 0.06f * Mathf.Sin(k * Mathf.PI));
        if (k >= 1f) { popTime = -1f; transform.localScale = baseScale; }
    }
}
