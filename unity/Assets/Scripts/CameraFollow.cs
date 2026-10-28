using UnityEngine;

// Caméra fixe en hauteur qui suit le joueur en douceur.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] float smooth = 8f;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }
}
