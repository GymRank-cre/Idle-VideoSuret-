using UnityEngine;

// P1 : placement de la main de tutoriel. Une cible visible reçoit la main dessus ; une cible hors écran (fréquent en portrait,
// où la palette est au bord au départ) la fait rester dans une marge de l'écran, le doigt tourné vers la cible.
public static class HandMath
{
    public static readonly Vector2 Min = new Vector2(0.12f, 0.14f);   // marges en coordonnées de viewport (HUD en haut, joystick en bas)
    public static readonly Vector2 Max = new Vector2(0.88f, 0.80f);

    // `target` en viewport (0..1 = écran). Renvoie le point à utiliser et l'angle (degrés, 0 = doigt vers le haut) du doigt.
    public static Vector2 Clamp(Vector2 target, out float angleDegrees)
    {
        Vector2 clamped = new Vector2(Mathf.Clamp(target.x, Min.x, Max.x), Mathf.Clamp(target.y, Min.y, Max.y));
        Vector2 d = target - clamped;
        angleDegrees = d.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
        return clamped;
    }
}
