using UnityEngine;

// Assemblage (poste 2) : un bras orange à deux segments qui oscille au-dessus de l'établi, un pupitre dont l'écran clignote et une
// caméra blanche qui apparaît à mi-cycle (formules de `stationDraw` du web). Le bras se déplace dans un plan vertical orienté vers la
// caméra posée sur l'établi ; ses angles sont ceux du web (épaule -1,0 + 0,55 sin, coude +1,5 + 0,5 cos).
public class AssemblyMachine : MonoBehaviour, IMachineAnim
{
    [SerializeField] Transform upper;           // segment épaule -> coude (maillage unité le long de Z)
    [SerializeField] Transform fore;            // segment coude -> pince
    [SerializeField] Transform elbowJoint;
    [SerializeField] Transform handJoint;
    [SerializeField] Vector3 shoulder = new Vector3(0.8f, 0.98f, 0.55f);   // base du bras, dans le repère de la machine
    [SerializeField] Vector2 reachDirection = new Vector2(0.2f, 0.79f);    // direction horizontale vers l'établi (X, Z)
    [SerializeField, Min(0.1f)] float upperLength = 0.62f;
    [SerializeField, Min(0.1f)] float foreLength = 0.5f;
    [SerializeField] GameObject camera;         // la caméra blanche, visible de 45 % à 100 % du cycle
    [SerializeField] Renderer screen;
    [SerializeField] Material screenA;
    [SerializeField] Material screenB;
    [SerializeField, Min(0.5f)] float blinkRate = 2f;

    bool screenState = true;

    public void Animate(float phase, float time)
    {
        float a = phase * Mathf.PI * 2f;
        float ang = -1.0f + 0.55f * Mathf.Sin(a);                       // angle écran de l'épaule (rad, y vers le bas)
        float ang2 = ang + 1.5f + 0.5f * Mathf.Cos(a);                  // angle du coude
        Vector3 flat = new Vector3(reachDirection.x, 0f, reachDirection.y).normalized;
        Vector3 d1 = flat * Mathf.Cos(-ang) + Vector3.up * Mathf.Sin(-ang);
        Vector3 d2 = flat * Mathf.Cos(-ang2) + Vector3.up * Mathf.Sin(-ang2);
        Vector3 elbow = shoulder + d1 * upperLength, hand = elbow + d2 * foreLength;
        Place(upper, shoulder, elbow);
        Place(fore, elbow, hand);
        elbowJoint.localPosition = elbow;
        handJoint.localPosition = hand;

        bool showCamera = phase > 0.45f;
        if (camera.activeSelf != showCamera) camera.SetActive(showCamera);
        bool on = Mathf.FloorToInt(time * blinkRate) % 2 == 0;
        if (on != screenState) { screenState = on; screen.sharedMaterial = on ? screenA : screenB; }
    }

    // Segment unité (le long de Z) étiré entre deux points du repère de la machine.
    static void Place(Transform segment, Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        segment.localPosition = (from + to) * 0.5f;
        segment.localRotation = Quaternion.LookRotation(d.normalized);
        segment.localScale = new Vector3(1f, 1f, d.magnitude);
    }
}
