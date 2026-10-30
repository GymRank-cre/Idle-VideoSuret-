using UnityEngine;

// Fait pivoter un objet d'un côté à l'autre (caméra de surveillance) : ±`angle` degrés autour de l'axe Y, en douceur.
public class Sweeper : MonoBehaviour
{
    [SerializeField] float angle = 30f;
    [SerializeField] float period = 6f;

    Quaternion rest;

    void Awake() { rest = transform.localRotation; }

    void Update()
    {
        float a = Mathf.Sin(Time.time * Mathf.PI * 2f / period) * angle;
        transform.localRotation = rest * Quaternion.Euler(0f, a, 0f);
    }
}
