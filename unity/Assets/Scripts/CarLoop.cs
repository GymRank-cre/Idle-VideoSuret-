using UnityEngine;

// Une voiture passe sur la route derrière l'atelier toutes les `interval` secondes (décor vivant, hors de la zone de jeu).
public class CarLoop : MonoBehaviour
{
    [SerializeField] Vector3 from = new Vector3(45f, 0f, -18.5f);
    [SerializeField] Vector3 to = new Vector3(-12f, 0f, -18.5f);
    [SerializeField, Min(1f)] float interval = 25f;
    [SerializeField, Min(0.5f)] float travelTime = 7f;
    [SerializeField] float firstDelay = 6f;

    float timer;

    void Start()
    {
        timer = interval - firstDelay; // la première voiture passe `firstDelay` secondes après le départ
        transform.position = from;
    }

    void Update()
    {
        timer += Time.deltaTime;
        float phase = Mathf.Repeat(timer, interval);   // la voiture roule `travelTime` s au début de chaque période
        transform.position = phase < travelTime ? Vector3.Lerp(from, to, phase / travelTime) : from;
    }
}
