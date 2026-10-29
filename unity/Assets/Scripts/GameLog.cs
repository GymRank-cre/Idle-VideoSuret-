using System.Collections.Generic;
using UnityEngine;

// Journal d'événements local (rien n'est envoyé) : temps de jeu écoulé jusqu'à chaque déblocage, etc.
// Affiché dans le Diagnostic pour mesurer le rythme de progression sur un vrai téléphone.
public static class GameLog
{
    const int Max = 40;
    static readonly List<string> entries = new List<string>();

    public static IReadOnlyList<string> Entries { get { return entries; } }
    public static float SessionSeconds { get { return Time.realtimeSinceStartup - sessionStart; } }
    static float sessionStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        entries.Clear();
        sessionStart = Time.realtimeSinceStartup;
    }

    public static void Event(string what)
    {
        if (entries.Count >= Max) entries.RemoveAt(0);
        entries.Add(Mathf.RoundToInt(SessionSeconds).ToString().PadLeft(4) + " s  " + what);
    }
}
