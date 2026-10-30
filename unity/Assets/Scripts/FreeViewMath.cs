using UnityEngine;

// Calculs purs de la vue libre (11e), testables : limites de l'atelier, zoom, valeurs invalides rejetées.
public static class FreeViewMath
{
    public const float MinDistanceMul = 0.6f;   // 0,6 = vue rapprochée
    public const float MaxDistanceMul = 2.0f;   // 2 = vue d'ensemble
    public static readonly Vector2 MinXZ = new Vector2(-8f, -13f);
    public static readonly Vector2 MaxXZ = new Vector2(14f, 11f);

    static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

    // Ramène le centre de la vue dans l'atelier ; une valeur invalide (NaN, infini) renvoie `fallback`.
    public static Vector3 ClampCenter(Vector3 c, Vector3 fallback)
    {
        if (!Finite(c.x) || !Finite(c.y) || !Finite(c.z)) return fallback;
        return new Vector3(Mathf.Clamp(c.x, MinXZ.x, MaxXZ.x), 0f, Mathf.Clamp(c.z, MinXZ.y, MaxXZ.y));
    }

    // Zoom au pincement : doigts qui s'écartent (ratio > 1) = on se rapproche (le multiplicateur de distance baisse).
    public static float PinchZoom(float currentMul, float previousDistance, float newDistance)
    {
        if (!Finite(currentMul) || !Finite(previousDistance) || !Finite(newDistance) || previousDistance < 1f || newDistance < 1f) return Mathf.Clamp(Finite(currentMul) ? currentMul : 1f, MinDistanceMul, MaxDistanceMul);
        return Mathf.Clamp(currentMul * previousDistance / newDistance, MinDistanceMul, MaxDistanceMul);
    }
}
