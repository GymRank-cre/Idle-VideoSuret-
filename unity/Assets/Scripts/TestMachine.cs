using UnityEngine;

// Test (poste 3) : un portique bleu dont un faisceau cyan translucide balaie l'objet d'un bout à l'autre (aller-retour en cosinus,
// comme le web), un petit écran qui passe au vert à 55 % du cycle (« test réussi ») et un voyant jaune qui passe au vert en même temps.
public class TestMachine : MonoBehaviour, IMachineAnim
{
    [SerializeField] Transform scanPlane;
    [SerializeField] Renderer screen;
    [SerializeField] Renderer beacon;
    [SerializeField] Material okMaterial;           // vert
    [SerializeField] Material screenWaiting;        // cyan
    [SerializeField] Material beaconWaiting;        // jaune
    [SerializeField] float scanStart = 0.05f;       // début du balayage (axe X de la machine)
    [SerializeField] float scanLength = 1.5f;
    [SerializeField] Vector2 planeCenter = new Vector2(0.56f, 0.8f);   // hauteur du centre du faisceau, position selon Z

    bool state;
    bool known;

    public void Animate(float phase, float time)
    {
        float sy = scanStart + scanLength * (0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f));
        scanPlane.localPosition = new Vector3(sy, planeCenter.x, planeCenter.y);
        bool ok = phase > 0.55f;
        if (!known || ok != state)
        {
            known = true; state = ok;
            screen.sharedMaterial = ok ? okMaterial : screenWaiting;
            beacon.sharedMaterial = ok ? okMaterial : beaconWaiting;
        }
    }
}
