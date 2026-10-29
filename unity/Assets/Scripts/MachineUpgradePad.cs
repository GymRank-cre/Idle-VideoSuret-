using UnityEngine;

// Une dalle qui se paie plusieurs fois (UnlockPad en mode « répétable ») : prix du prochain niveau, achat, niveau affiché.
public interface IRepeatablePad
{
    bool IsMaxed { get; }
    int NextPrice();
    void Purchased();      // appelé quand le prix est payé
}

// Source de prix/achat d'une dalle d'amélioration par machine (un axe d'une machine) + affichage du niveau,
// de l'icône (flèche verte « meilleur achat » qui rebondit ou grise « inutile pour l'instant ») et fête de palier.
public class MachineUpgradePad : MonoBehaviour, IRepeatablePad
{
    [SerializeField] MachineUpgradeState state;
    [SerializeField] string machineId = "assembly";
    [SerializeField, Range(0, 1)] int axis;              // 0 vitesse, 1 ajouter
    [SerializeField] TextMesh levelText;                 // « Niv 4 »
    [SerializeField] TextMesh levelOutline;
    [SerializeField] Transform bestBuyArrow;             // flèche verte au-dessus de la dalle
    [SerializeField] Transform uselessArrow;             // flèche grise
    [SerializeField] FeedbackSpawner feedback;
    [SerializeField] CameraFollow cameraFollow;
    [SerializeField] GameObject machineRoot;             // la dalle n'existe que quand cette machine est là
    [SerializeField] float bounceHeight = 0.25f;

    bool best, advisorSet;
    float arrowBaseY, uselessBaseY;
    int shownLevel = -1;

    public string MachineId => machineId;
    public GameObject MachineRoot => machineRoot;
    public int Axis => axis;
    public bool IsMaxed => state.IsMax(machineId, axis);

    public int NextPrice() { return IsMaxed ? 0 : state.NextCost(machineId, axis); }

    public void Purchased()
    {
        bool milestone = state.Purchase(machineId, axis);
        GameLog.Event("amélioration " + machineId + (axis == 0 ? " vitesse " : " ajouter ") + state.Level(machineId, axis));
        AudioManager.Play(SfxId.Upgrade);
        AudioManager.Vibrate();
        Vector3 top = transform.position + Vector3.up * 0.6f;
        if (feedback != null)
        {
            if (milestone) feedback.Confetti(top); else feedback.Sparkle(top);
        }
        if (milestone && cameraFollow != null)
        {
            cameraFollow.Shake(0.09f, 0.25f);
            cameraFollow.ZoomPulse(0.05f, 0.3f);
        }
    }

    // Appelé par le conseiller : true = cette machine est le goulot de la chaîne.
    public void SetBestBuy(bool isBottleneck)
    {
        best = isBottleneck;
        advisorSet = true;
    }

    void Awake()
    {
        if (bestBuyArrow != null) arrowBaseY = bestBuyArrow.localPosition.y;
        if (uselessArrow != null) uselessBaseY = uselessArrow.localPosition.y;
    }

    void Update()
    {
        int level = state.Level(machineId, axis);
        if (level != shownLevel)
        {
            shownLevel = level;
            string s = IsMaxed ? "MAX" : Loc.T("machine.level", level);
            if (levelText != null) levelText.text = s;
            if (levelOutline != null) levelOutline.text = s;
        }

        bool showBest = advisorSet && best && !IsMaxed;
        bool showUseless = advisorSet && !best && !IsMaxed;
        if (bestBuyArrow != null)
        {
            if (bestBuyArrow.gameObject.activeSelf != showBest) bestBuyArrow.gameObject.SetActive(showBest);
            if (showBest)
            {
                Vector3 p = bestBuyArrow.localPosition;
                bestBuyArrow.localPosition = new Vector3(p.x, arrowBaseY + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * bounceHeight, p.z);
            }
        }
        if (uselessArrow != null && uselessArrow.gameObject.activeSelf != showUseless) uselessArrow.gameObject.SetActive(showUseless);
    }
}
