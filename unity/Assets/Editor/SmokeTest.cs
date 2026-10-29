using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Test de fumée : `Tools > Idle Prototype > Test de fumée`. Lance le Play, un « robot » joue 90 s (ramasse, dépose,
// ramasse les billets), puis vérifie : 0 erreur/avertissement, l'argent a augmenté, et sauvegarde -> relecture = même état.
// Le résultat est écrit dans la console (« Test de fumée : OK » ou « ÉCHEC : … »). L'état survit au rechargement
// de domaine du Play grâce à SessionState.
[InitializeOnLoad]
public static class SmokeTest
{
    const string KeyActive = "Smoke.Active";
    const string KeyStart = "Smoke.Start";
    const string KeyIssues = "Smoke.Issues";
    const string KeyMoney = "Smoke.MaxMoney";
    const double Duration = 90.0;

    static readonly List<string> issues = new List<string>();
    static int maxMoney;
    static PlayerController player;
    static StackCarrier carrier;
    static Wallet wallet;
    static double lastPhaseChange;
    static int phase; // 0 palette, 1 poste, 2 produits, 3 comptoir, 4 billets
    static bool checkedSave;
    static string lastTarget = "?";

    static SmokeTest()
    {
        if (SessionState.GetBool(KeyActive, false))
        {
            issues.AddRange(SessionState.GetString(KeyIssues, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            maxMoney = SessionState.GetInt(KeyMoney, 0);
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
    }

    [MenuItem("Tools/Idle Prototype/Test de fumée")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Test de fumée : quitter le Play d'abord."); return; }
        SaveSystem.EraseEverything();
        SessionState.SetBool(KeyActive, true);
        SessionState.SetString(KeyStart, "0");
        SessionState.SetString(KeyIssues, "");
        SessionState.SetInt(KeyMoney, 0);
        EditorApplication.isPlaying = true;
    }

    static void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Log || !EditorApplication.isPlaying) return;
        if (condition.Contains("MCP-FOR-UNITY") || condition.Contains("Importer(")) return;
        issues.Add((type == LogType.Warning ? "[avert] " : "[erreur] ") + condition.Replace('\n', ' '));
        SessionState.SetString(KeyIssues, string.Join("\n", issues));
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true; // sans cela le jeu se fige quand la fenêtre Unity n'a pas le focus
        if (player == null)
        {
            player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            carrier = UnityEngine.Object.FindObjectOfType<StackCarrier>();
            wallet = UnityEngine.Object.FindObjectOfType<Wallet>();
            if (player == null || wallet == null) return;
            lastPhaseChange = EditorApplication.timeSinceStartup;
            if (SessionState.GetString(KeyStart, "0") == "0") // le décompte démarre quand la scène est prête
                SessionState.SetString(KeyStart, EditorApplication.timeSinceStartup.ToString("R"));
        }
        double start = double.Parse(SessionState.GetString(KeyStart, "0"));
        double elapsed = EditorApplication.timeSinceStartup - start;
        maxMoney = Mathf.Max(maxMoney, wallet.Money);
        SessionState.SetInt(KeyMoney, maxMoney);

        if (elapsed >= Duration) { Finish(); return; }
        Drive();
    }

    // Robot : palette jusqu'à pile pleine, poste jusqu'à pile vide, produits, comptoir, billets ; 12 s maximum par cible.
    static void Drive()
    {
        double now = EditorApplication.timeSinceStartup;
        Transform target = null;
        if (phase == 0) { var p = UnityEngine.Object.FindObjectOfType<ResourcePile>(); target = p != null ? p.transform : null; if (carrier.Count >= carrier.Capacity) Next(); }
        else if (phase == 1) { var st = UnityEngine.Object.FindObjectOfType<InstallStation>(); target = st != null ? st.transform : null; if (carrier.Count == 0) Next(); }
        else if (phase == 2)
        {
            Product prod = UnityEngine.Object.FindObjectOfType<Product>();
            var st = UnityEngine.Object.FindObjectOfType<InstallStation>();
            target = prod != null ? prod.transform : (st != null ? st.transform : null);
            var pc = player.GetComponent<ProductCarrier>();
            if (pc != null && pc.Count > 0) Next();
        }
        else if (phase == 3)
        {
            var c = UnityEngine.Object.FindObjectOfType<ServiceCounter>(); target = c != null ? c.transform : null;
            var pc = player.GetComponent<ProductCarrier>();
            if (pc != null && pc.Count == 0) Next();
        }
        else
        {
            Bill best = null; float bd = float.MaxValue;
            foreach (Bill b in UnityEngine.Object.FindObjectsOfType<Bill>())
            {
                float d = (b.transform.position - player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            target = best != null ? best.transform : null;
            if (best == null) Next();
        }

        if (now - lastPhaseChange > 12.0) Next();
        lastTarget = target != null ? target.name + target.position.ToString("F1") : "aucune";
        if (target == null) { player.SetMoveInput(Vector2.zero); return; }
        Vector3 d3 = target.position - player.transform.position;
        Vector2 dir = new Vector2(d3.x, d3.z);
        player.SetMoveInput(dir.magnitude < 0.4f ? Vector2.zero : dir.normalized);
    }

    static void Next()
    {
        phase = (phase + 1) % 5;
        lastPhaseChange = EditorApplication.timeSinceStartup;
    }

    static void Finish()
    {
        player.SetMoveInput(Vector2.zero);
        var failures = new List<string>(issues);
        if (maxMoney <= 0) failures.Add("l'argent n'a jamais augmenté (phase " + phase + ", caisses " + carrier.Count + "/" + carrier.Capacity + ", joueur " + player.transform.position.ToString("F1") + ", dernière cible " + lastTarget + ")");

        if (!checkedSave)
        {
            checkedSave = true;
            string a = null, b = null;
            try
            {
                var save = UnityEngine.Object.FindObjectOfType<SaveSystem>();
                BindingFlags inst = BindingFlags.Instance | BindingFlags.NonPublic;
                Type t = typeof(SaveSystem);
                a = (string)t.GetMethod("Snapshot", inst).Invoke(save, null);
                t.GetMethod("Save", inst).Invoke(save, null);
                var read = (SaveData)t.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                read.lastSaveUtc = 0;
                b = JsonUtility.ToJson(read);
            }
            catch (Exception e) { failures.Add("sauvegarde : " + e.Message); }
            if (a != null && a != b) failures.Add("sauvegarde relue différente de l'état (" + a.Length + " / " + (b != null ? b.Length : 0) + " caractères)");
        }

        SessionState.SetBool(KeyActive, false);
        Application.logMessageReceived -= OnLog;
        EditorApplication.update -= Tick;
        if (failures.Count == 0) Debug.Log("Test de fumée : OK (90 s, 0 erreur ni avertissement, argent max " + maxMoney + ", sauvegarde relue identique).");
        else Debug.LogError("Test de fumée : ÉCHEC\n" + string.Join("\n", failures));
        SaveSystem.Suspended = true; // l'arrêt ne doit pas réécrire la sauvegarde du test
        EditorApplication.isPlaying = false;
    }
}
