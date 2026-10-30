using UnityEngine;

// Emballage (poste 4) : une cabine blanche à entête orange dont la presse descend puis remonte entre 25 % et 75 % du cycle
// (hauteur 1,95 - 0,62 sin(...), comme le web), et un colis emballé qui apparaît devant la cabine à mi-cycle.
public class PackMachine : MonoBehaviour, IMachineAnim
{
    [SerializeField] Transform ram;             // piston de la presse
    [SerializeField] Transform plate;           // plateau de la presse (un peu plus bas que le piston)
    [SerializeField] GameObject package;        // colis fini, visible de 50 % à 100 % du cycle
    [SerializeField] float topHeight = 1.95f;   // hauteur du bas du piston en haut de course
    [SerializeField] float stroke = 0.62f;      // course de la presse
    [SerializeField] float plateOffset = 0.1f;  // le plateau est plus bas que le piston

    float ramX, ramZ, plateX, plateZ;
    bool ready;

    void Prepare()
    {
        ramX = ram.localPosition.x; ramZ = ram.localPosition.z;
        plateX = plate.localPosition.x; plateZ = plate.localPosition.z;
        ready = true;
    }

    public void Animate(float phase, float time)
    {
        if (!ready) Prepare();
        float press = Mathf.Sin(Mathf.Clamp01((phase - 0.25f) / 0.5f) * Mathf.PI);
        float pz = topHeight - stroke * press;
        ram.localPosition = new Vector3(ramX, pz, ramZ);
        plate.localPosition = new Vector3(plateX, pz - plateOffset, plateZ);
        bool packed = phase > 0.5f;
        if (package.activeSelf != packed) package.SetActive(packed);
    }
}
