using UnityEngine;

// Fait tourner un objet en continu (pales de ventilateur, etc.) : décor vivant, aucun coût de physique.
public class Spinner : MonoBehaviour
{
    [SerializeField] Vector3 axis = Vector3.up;
    [SerializeField] float degreesPerSecond = 180f;

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
