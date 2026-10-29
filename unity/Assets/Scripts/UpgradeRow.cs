using UnityEngine;
using UnityEngine.UI;

// Une ligne du panneau d'améliorations : nom, niveau, effet du prochain niveau, bouton d'achat.
public class UpgradeRow : MonoBehaviour
{
    [SerializeField] Text nameText;
    [SerializeField] Text levelText;
    [SerializeField] Text effectText;
    [SerializeField] Button buyButton;
    [SerializeField] Text buyLabel;
    [SerializeField] Image buyImage;
    [SerializeField] Color affordableColor = new Color(0.2f, 0.7f, 0.3f);
    [SerializeField] Color lockedColor = new Color(0.3f, 0.33f, 0.38f);
    [SerializeField] float popDuration = 0.25f;

    UpgradeState state;
    int index;
    float popTimer = -1f;

    public void Init(UpgradeState upgradeState, int upgradeIndex)
    {
        state = upgradeState;
        index = upgradeIndex;
        buyButton.onClick.AddListener(() => state.TryBuy(index));
        Refresh();
    }

    // Courte description de l'effet d'un niveau (ligne du panneau et chiffre flottant à l'achat).
    public static string Short(UpgradeConfig.Upgrade u)
    {
        switch (u.kind)
        {
            case UpgradeKind.StackCapacity: return "+" + Mathf.RoundToInt(u.effectPerLevel) + " caisse";
            case UpgradeKind.PlayerSpeed: return "+" + Mathf.RoundToInt(u.effectPerLevel * 100f) + " % vitesse";
            case UpgradeKind.JobSpeed: return "-" + Mathf.RoundToInt(u.effectPerLevel * 100f) + " % durée";
            case UpgradeKind.CustomerPay: return "+" + Mathf.RoundToInt(u.effectPerLevel * 100f) + " % paiement";
            default: return "+" + Mathf.RoundToInt(u.effectPerLevel * 100f) + " % employés";
        }
    }

    public void Refresh()
    {
        UpgradeConfig.Upgrade u = state.Config.Upgrades[index];
        int level = state.Level(index);
        nameText.text = u.displayName;
        levelText.text = "Niv " + level + "/" + u.maxLevel;

        if (state.IsMax(index))
        {
            effectText.text = "Niveau maximum";
            buyLabel.text = "MAX";
            buyButton.interactable = false;
            buyImage.color = lockedColor;
            return;
        }

        effectText.text = "Prochain : " + Short(u);
        buyLabel.text = "$ " + state.NextCost(index);
        bool ok = state.CanAfford(index);
        buyButton.interactable = ok;
        buyImage.color = ok ? affordableColor : lockedColor;
    }

    public void Pop()
    {
        popTimer = 0f;
    }

    void Update()
    {
        if (popTimer < 0f) return;
        popTimer += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(popTimer / popDuration);
        transform.localScale = Vector3.one * (1f + 0.07f * Mathf.Sin(t * Mathf.PI));
        if (t >= 1f)
        {
            transform.localScale = Vector3.one;
            popTimer = -1f;
        }
    }
}
