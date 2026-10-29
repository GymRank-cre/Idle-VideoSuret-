using System;
using UnityEngine;

// Chiffre qui monte et s'efface (+10). Texte 3D qui fait face à la caméra ; revient à son pool à la fin.
// Si plusieurs gains arrivent presque ensemble, ils s'additionnent dans le même chiffre (AddValue).
public class FloatingText : MonoBehaviour
{
    [SerializeField] TextMesh main;
    [SerializeField] TextMesh shadow; // contour sombre

    string custom; // texte libre (« +5 % vitesse ») ; null = « +valeur »
    Vector3 start;
    Color color;
    int total;
    float duration, height, elapsed;
    Action<GameObject> release;

    public void PlayText(string text, Vector3 position, Color textColor, float lifetime, float rise, Action<GameObject> releaseToPool)
    {
        Play(0, position, textColor, lifetime, rise, releaseToPool);
        custom = text;
        SetText();
    }

    public void Play(int value, Vector3 position, Color textColor, float lifetime, float rise, Action<GameObject> releaseToPool)
    {
        total = value;
        custom = null;
        SetText();
        start = position;
        color = textColor;
        duration = Mathf.Max(0.05f, lifetime);
        height = rise;
        release = releaseToPool;
        elapsed = 0f;
        Apply(0f);
    }

    // Ajoute un gain au chiffre en cours : il grossit et se relance (petit "pop").
    public void AddValue(int value)
    {
        total += value;
        SetText();
        elapsed = Mathf.Min(elapsed, duration * 0.05f);
    }

    void SetText()
    {
        string text = custom ?? "+" + total;
        main.text = text;
        shadow.text = text;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float k = Mathf.Clamp01(elapsed / duration);
        Apply(k);
        if (k >= 1f)
        {
            if (release != null) release(gameObject);
            else Destroy(gameObject);
        }
    }

    void Apply(float k)
    {
        float rise = 1f - (1f - k) * (1f - k); // monte vite puis ralentit
        transform.position = start + Vector3.up * (height * rise);

        float pop = 1f + 0.35f * Mathf.Clamp01(1f - k * 6f); // petit "pop" au début
        transform.localScale = Vector3.one * pop;

        float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, k)); // s'efface sur la 2e moitié
        main.color = new Color(color.r, color.g, color.b, alpha);
        shadow.color = new Color(0.05f, 0.07f, 0.1f, alpha);
    }
}
