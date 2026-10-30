using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Test de fumée du jeu idle (`Tools > Idle Prototype > Test de fumée`) : un robot touche « Meilleur achat » pendant 300 s de jeu
// simulées (sans Play : le cœur du jeu n'a pas besoin de scène), accepte les commandes, achète de la Réputation et lance le turbo.
// Vérifications : 0 erreur ni avertissement dans la console, argent jamais négatif, progression, sauvegarde -> relecture identique.
public static class SmokeTest
{
    const double SimSeconds = 300;
    const double Step = 0.1;

    [MenuItem("Tools/Idle Prototype/Test de fumée")]
    public static void Run()
    {
        var issues = new List<string>();
        Application.LogCallback onLog = (condition, stack, type) =>
        {
            if (type == LogType.Log || condition.Contains("MCP-FOR-UNITY") || condition.Contains("Importer(")) return;
            issues.Add((type == LogType.Warning ? "[avert] " : "[erreur] ") + condition.Replace('\n', ' '));
        };
        Application.logMessageReceived += onLog;
        var config = ScriptableObject.CreateInstance<IdleEconomyConfig>();
        string summary = "";
        try
        {
            var game = new IdleGame(config, 11, 0);
            int unlocks = 0, upgrades = 0, ordersAccepted = 0, repBought = 0, turbos = 0;
            double minCash = double.MaxValue;
            game.OnUnlocked += s => unlocks++;
            game.OnPurchased += s => upgrades++;

            for (double t = 0; t < SimSeconds; t += Step)
            {
                game.Tick(Step);
                minCash = Math.Min(minCash, game.Cash);
                if (Math.Abs(t - Math.Round(t)) < 1e-6)   // le robot « touche l'écran » une fois par seconde
                {
                    for (int guard = 0; guard < 20 && game.BuyAdvised() != IdleGame.PurchaseResult.TooExpensive; guard++) { if (game.Buys > 100000) break; }
                    while (game.Offers.Count > 0 && game.Active.Count < game.ActiveSlots && game.Accept(game.Offers[0].id)) ordersAccepted++;
                    for (int r = 0; r < game.Rep.Length; r++) if (game.BuyRep(r)) repBought++;
                    if (game.TryTurbo()) turbos++;
                }
            }

            if (minCash < 0) issues.Add("argent négatif : " + minCash);
            if (double.IsNaN(game.Cash) || double.IsInfinity(game.Cash)) issues.Add("argent invalide : " + game.Cash);
            if (game.Income <= 0) issues.Add("revenu nul à la fin");
            int open = 0; foreach (var s in game.Stations) if (s.unlocked) open++;
            if (open < 2) issues.Add("moins de 2 postes ouverts après 300 s (" + open + ")");
            if (game.OrdersDone < 1) issues.Add("aucune commande livrée en 300 s");
            if (turbos < 1) issues.Add("le turbo n'a jamais été lancé");

            // sauvegarde -> JSON -> chargement -> sauvegarde identique
            string json = JsonUtility.ToJson(game.ToSave(1000));
            var again = new IdleGame(config, 11, 0);
            again.Load(SaveSystem.TryParse(json, config));
            string json2 = JsonUtility.ToJson(again.ToSave(1000));
            // JsonUtility ne relit pas un décimal au bit près (écart au 16e chiffre) : on compare à 12 chiffres significatifs
            string a = Regex.Replace(json, @"-?\d+\.\d+(?:[eE][+-]?\d+)?", m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("G12", System.Globalization.CultureInfo.InvariantCulture));
            string b = Regex.Replace(json2, @"-?\d+\.\d+(?:[eE][+-]?\d+)?", m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("G12", System.Globalization.CultureInfo.InvariantCulture));
            if (a != b)
            {
                int at = 0;
                while (at < a.Length && at < b.Length && a[at] == b[at]) at++;
                issues.Add("sauvegarde relue différente de l'état, à la position " + at + " : avant « " + a.Substring(Math.Max(0, at - 40), Math.Min(120, a.Length - Math.Max(0, at - 40))) + " » / après « " + b.Substring(Math.Max(0, at - 40), Math.Min(120, b.Length - Math.Max(0, at - 40))) + " »");
            }

            // retour après 2 h d'absence : gain = revenu de base x durée, jamais négatif
            double before = again.Cash, baseIncome = again.BaseIncome;
            IdleGame.OfflineResult off = again.Offline(7200);
            if (Math.Abs(off.gain - baseIncome * 7200) > 1e-3 || again.Cash < before) issues.Add("gain hors ligne incohérent");

            summary = open + " postes, " + upgrades + " achats, " + ordersAccepted + " commandes acceptées (" + game.OrdersDone + " livrées), " + repBought + " achats de Réputation, "
                      + turbos + " turbo(s), revenu final " + NumberFormat.Short((long)Math.Min(long.MaxValue, game.Income)) + "/s";
        }
        catch (Exception e)
        {
            issues.Add("exception : " + e);
        }
        finally
        {
            Application.logMessageReceived -= onLog;
            UnityEngine.Object.DestroyImmediate(config);
        }

        if (issues.Count == 0) Debug.Log("Test de fumée : OK (300 s simulées, " + summary + ", 0 erreur ni avertissement, sauvegarde relue identique).");
        else Debug.LogError("Test de fumée : ÉCHEC\n" + string.Join("\n", issues));
    }
}
