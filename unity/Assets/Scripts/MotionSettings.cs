using UnityEngine;

// P4 : option d'accessibilité « Moins de secousses ». Quand elle est active, la caméra ne tremble plus et ne glisse plus
// vers un point d'intérêt (déblocage, quai) : les repères restent donnés par la flèche, la main et les textes.
// Mémorisée avec PlayerPrefs (réglage d'appareil, indépendant de la partie).
public static class MotionSettings
{
    const string Key = "reduce_motion";

    static int cached = -1;   // lu une seule fois dans PlayerPrefs (cette propriété est interrogée à chaque secousse ou pulsation de caméra)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() { cached = -1; }   // Play sans rechargement de domaine : on relit la valeur enregistrée

    public static bool ReduceMotion
    {
        get
        {
            if (cached < 0) cached = PlayerPrefs.GetInt(Key, 0) == 1 ? 1 : 0;
            return cached == 1;
        }
        set
        {
            cached = value ? 1 : 0;
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
        }
    }

    // Amplitude réellement appliquée à la secousse de caméra.
    public static float ShakeAmplitude(float requested)
    {
        return ReduceMotion ? 0f : requested;
    }

    // La caméra peut-elle glisser vers un point d'intérêt (Peek) ?
    public static bool AllowCameraGlide => !ReduceMotion;
}
