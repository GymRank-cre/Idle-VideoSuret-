using UnityEngine;

// Réception (poste 1) : un quai avec une porte à lamelles, un gyrophare qui clignote et une palette sur laquelle les caisses
// s'empilent (1 à 4) au rythme du cycle, la dernière « pousse » en grandissant.
public class ReceptionMachine : MonoBehaviour, IMachineAnim, IMachineLoad
{
    [SerializeField] Transform[] crates;        // caisses, de la première à la dernière posée ; « Ajouter » en montre davantage
    [SerializeField] Renderer beacon;
    [SerializeField] Material beaconOn;
    [SerializeField] Material beaconOff;
    [SerializeField, Min(0.5f)] float beaconRate = 3f;   // clignotements par seconde

    bool beaconState = true;
    int capacity = 4;

    public void SetLoad(int count) { capacity = Mathf.Clamp(count, 1, crates.Length); }

    public void Animate(float phase, float time)
    {
        int count = Mathf.Min(capacity, 1 + Mathf.FloorToInt(phase * capacity));
        for (int q = 0; q < crates.Length; q++)
        {
            bool shown = q < count;
            if (crates[q].gameObject.activeSelf != shown) crates[q].gameObject.SetActive(shown);
            if (!shown) continue;
            float pop = q == count - 1 ? Mathf.Clamp01((phase * capacity - q) * 3f) : 1f;
            crates[q].localScale = new Vector3(1f, Mathf.Max(0.01f, pop), 1f);
        }
        bool on = Mathf.FloorToInt(time * beaconRate) % 2 == 0;
        if (on != beaconState) { beaconState = on; beacon.sharedMaterial = on ? beaconOn : beaconOff; }
    }
}
