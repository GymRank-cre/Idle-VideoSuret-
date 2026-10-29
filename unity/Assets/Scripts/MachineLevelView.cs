using UnityEngine;

// Sur une machine : petite pastille « Niv N » qui « pop » à chaque achat, et pièces visibles qui apparaissent aux paliers
// (1er palier : bordure dorée, 2e : deuxième lampe, 3e : antenne). Le niveau affiché = niveaux de vitesse + d'ajout.
public class MachineLevelView : MonoBehaviour
{
    [SerializeField] MachineUpgradeState state;
    [SerializeField] string machineId = "assembly";
    [SerializeField] TextMesh text;
    [SerializeField] TextMesh outline;
    [SerializeField] GameObject[] milestoneParts;   // [0] = 1er palier...
    [SerializeField] float popDuration = 0.3f;
    [SerializeField] float popScale = 1.45f;

    int shown = -1;
    float popTime = -1f;
    Vector3 baseScale;

    void Awake()
    {
        baseScale = transform.localScale;
        state.OnChanged += OnChanged;
    }

    void OnDestroy()
    {
        if (state != null) state.OnChanged -= OnChanged;
    }

    void OnEnable()
    {
        Refresh(false);
    }

    void OnChanged(string id)
    {
        if (id == machineId) Refresh(true);
    }

    void Refresh(bool pop)
    {
        int level = state.TotalLevel(machineId);
        if (level != shown)
        {
            shown = level;
            string s = level > 0 ? Loc.T("machine.level", level) : "";
            if (text != null) text.text = s;
            if (outline != null) outline.text = s;
            if (pop && level > 0) popTime = 0f;
        }
        int m = state.MilestoneCount(machineId);
        for (int i = 0; i < milestoneParts.Length; i++)
            if (milestoneParts[i] != null) milestoneParts[i].SetActive(m > i);
    }

    void Update()
    {
        if (popTime < 0f) return;
        popTime += Time.deltaTime;
        float k = Mathf.Clamp01(popTime / popDuration);
        transform.localScale = baseScale * (1f + (popScale - 1f) * Mathf.Sin(k * Mathf.PI));
        if (k >= 1f) { popTime = -1f; transform.localScale = baseScale; }
    }
}
