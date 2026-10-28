using UnityEngine;
using UnityEngine.UI;

// Compteur « Produits x/4 » du HUD (même style que le compteur de caisses), avec un léger pop à chaque ajout.
public class ProductHud : MonoBehaviour
{
    [SerializeField] ProductCarrier carrier;
    [SerializeField] Text label;
    [SerializeField] FeedbackConfig config; // facultatif : réglages du pop

    int lastCount = -1;

    void OnEnable()
    {
        carrier.OnCountChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        carrier.OnCountChanged -= Refresh;
    }

    void Refresh()
    {
        label.text = carrier.Count + "<size=44><color=#FFFFFFAA>/" + carrier.Capacity + "</color></size>";

        if (config != null && lastCount >= 0 && carrier.Count > lastCount)
            label.rectTransform.localScale = Vector3.one * config.PopScale;
        lastCount = carrier.Count;
    }

    void Update()
    {
        Transform t = label.rectTransform;
        if (t.localScale == Vector3.one) return;

        float recover = config != null ? config.PopRecover : 8f;
        t.localScale = Vector3.Lerp(t.localScale, Vector3.one, 1f - Mathf.Exp(-recover * Time.deltaTime));
        if ((t.localScale - Vector3.one).sqrMagnitude < 0.0001f)
            t.localScale = Vector3.one;
    }
}
