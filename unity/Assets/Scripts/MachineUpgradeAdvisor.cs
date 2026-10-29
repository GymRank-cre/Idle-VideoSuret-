using UnityEngine;

// Débit d'une machine (objets par seconde) : sert à repérer le goulot de la chaîne.
public interface IThroughput
{
    float ThroughputPerSecond { get; }
}

// Repère la machine qui limite la chaîne (la plus lente des machines débloquées) et dit à ses dalles d'amélioration
// d'afficher la flèche verte « meilleur achat » ; les autres affichent une flèche grise « inutile pour l'instant ».
public class MachineUpgradeAdvisor : MonoBehaviour
{
    [SerializeField] MonoBehaviour[] machines;   // IThroughput ; actives seulement une fois débloquées
    [SerializeField] string[] machineIds;        // même ordre que `machines`
    [SerializeField] MachineUpgradePad[] pads;
    [SerializeField, Min(0.1f)] float interval = 0.5f;

    float timer;

    public string Bottleneck { get; private set; }

    void Start() { Refresh(); }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;
        Refresh();
    }

    public void Refresh()
    {
        string slowest = null;
        float rate = float.MaxValue;
        for (int i = 0; i < machines.Length; i++)
        {
            if (machines[i] == null || !machines[i].gameObject.activeInHierarchy) continue;
            var t = machines[i] as IThroughput;
            if (t == null) continue;
            if (t.ThroughputPerSecond < rate) { rate = t.ThroughputPerSecond; slowest = machineIds[i]; }
        }
        Bottleneck = slowest;
        foreach (MachineUpgradePad pad in pads)
        {
            if (pad == null) continue;
            if (!pad.gameObject.activeSelf && (pad.MachineRoot == null || pad.MachineRoot.activeInHierarchy))
                pad.gameObject.SetActive(true); // la machine existe : sa dalle apparaît (gonflement PopIn)
            pad.SetBestBuy(pad.MachineId == slowest);
        }
    }
}
