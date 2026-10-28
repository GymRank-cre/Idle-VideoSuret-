using UnityEngine;

// Fait flotter un objet de haut en bas (billets au sol). L'amplitude est en mètres,
// même si le parent est écrasé (billet plat).
public class Bobbing : MonoBehaviour
{
    [SerializeField] float amplitude = 0.05f;
    [SerializeField] float speed = 3f;

    float phaseOffset;
    float baseY;

    void Awake()
    {
        baseY = transform.localPosition.y;
    }

    void OnEnable()
    {
        phaseOffset = Random.value * Mathf.PI * 2f; // les billets ne bougent pas tous ensemble
    }

    void Update()
    {
        float parentScaleY = transform.parent != null ? Mathf.Max(0.0001f, transform.parent.lossyScale.y) : 1f;
        Vector3 pos = transform.localPosition;
        pos.y = baseY + (Mathf.Sin(Time.time * speed + phaseOffset) * 0.5f + 0.5f) * amplitude / parentScaleY;
        transform.localPosition = pos;
    }
}
