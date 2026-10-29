using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tests unitaires lancés par le menu `Tools > Idle Prototype > Tests unitaires` (le projet n'a pas d'assembly de test :
// le code du jeu est dans l'assembly par défaut, que les assemblies de test ne peuvent pas référencer).
public static class UnitTests
{
    [MenuItem("Tools/Idle Prototype/Tests unitaires")]
    public static void Run()
    {
        var failures = new List<string>();
        int count = 0;

        void Eq(string expected, string actual, string what)
        {
            count++;
            if (expected != actual) failures.Add(what + " : attendu « " + expected + " », obtenu « " + actual + " »");
        }

        // NumberFormat.Short
        Eq("0", NumberFormat.Short(0), "0");
        Eq("7", NumberFormat.Short(7), "7");
        Eq("999", NumberFormat.Short(999), "999");
        Eq("1K", NumberFormat.Short(1000), "1000");
        Eq("1,2K", NumberFormat.Short(1234), "1234");
        Eq("1,2K", NumberFormat.Short(1299), "1299 (tronqué, pas arrondi)");
        Eq("9,9K", NumberFormat.Short(9999), "9999");
        Eq("12K", NumberFormat.Short(12000), "12000");
        Eq("12,3K", NumberFormat.Short(12345), "12345");
        Eq("123K", NumberFormat.Short(123456), "123456");
        Eq("999K", NumberFormat.Short(999999), "999999 (jamais 1000K)");
        Eq("1M", NumberFormat.Short(1000000), "1 million");
        Eq("1,2M", NumberFormat.Short(1200000), "1,2 million");
        Eq("3,4B", NumberFormat.Short(3400000000L), "3,4 milliards");
        Eq("2T", NumberFormat.Short(2000000000000L), "2 billions");
        Eq("1,5aa", NumberFormat.Short(1500000000000000L), "1,5aa");
        Eq("-1,2K", NumberFormat.Short(-1234), "négatif");
        Eq("9,2ab", NumberFormat.Short(9200000000000000000L), "9,2ab (1e18 = groupe 6)");

        if (failures.Count == 0)
            Debug.Log("Tests unitaires : " + count + " vérifications, toutes réussies.");
        else
            Debug.LogError("Tests unitaires : " + failures.Count + " échec(s) sur " + count + "\n" + string.Join("\n", failures));
    }
}
