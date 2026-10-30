using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Test de fumée : `Tools > Idle Prototype > Test de fumée`. Lance le Play, un « robot » joue 90 s (ramasse, dépose,
// ramasse les billets), puis vérifie : 0 erreur/avertissement, l'argent a augmenté, et sauvegarde -> relecture = même état.
// Le résultat est écrit dans la console (« Test de fumée : OK » ou « ÉCHEC : … »). L'état survit au rechargement
// de domaine du Play grâce à SessionState.
// Version étendue (P6) : `Test de fumée étendu (5 min simulées)` : même robot, temps accéléré x4 (300 s de jeu en ~75 s), et des
// actions scriptées : achats d'améliorations (joueur et machines), deux commandes livrées, achats de Réputation ; puis les mêmes
// vérifications (0 erreur, sauvegarde -> relecture identique, avec cette fois un état riche : niveaux, Réputation, dalles).
[InitializeOnLoad]
public static class SmokeTest
{
    const string KeyActive = "Smoke.Active";
    const string KeyStart = "Smoke.Start";
    const string KeyIssues = "Smoke.Issues";
    const string KeyMoney = "Smoke.MaxMoney";
    const string KeyExtended = "Smoke.Extended";
    const double Duration = 90.0;
    const float ExtendedSeconds = 300f;   // secondes de jeu simulées
    const float ExtendedScale = 4f;

    static readonly List<string> issues = new List<string>();
    static int maxMoney;
    static PlayerController player;
    static StackCarrier carrier;
    static Wallet wallet;
    static double lastPhaseChange;
    static int phase; // 0 palette, 1 poste, 2 produits, 3 comptoir, 4 billets
    static bool checkedSave;
    static string lastTarget = "?";
    static bool extended;
    static float simTime;
    static int stage, upgradesBought, machineBought, ordersDelivered, repBought;

    static SmokeTest()
    {
        if (SessionState.GetBool(KeyActive, false))
        {
            issues.AddRange(SessionState.GetString(KeyIssues, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            maxMoney = SessionState.GetInt(KeyMoney, 0);
            extended = SessionState.GetBool(KeyExtended, false);
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }
    }

    [MenuItem("Tools/Idle Prototype/Test de fumée étendu (5 min simulées)")]
    public static void RunExtended()
    {
        Start(true);
    }

    [MenuItem("Tools/Idle Prototype/Test de fumée")]
    public static void Run()
    {
        Start(false);
    }

    static void Start(bool extendedRun)
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Test de fumée : quitter le Play d'abord."); return; }
        SaveSystem.EraseEverything();
        SessionState.SetBool(KeyExtended, extendedRun);
        extended = extendedRun;
        simTime = 0f; stage = 0; upgradesBought = machineBought = ordersDelivered = repBought = 0;
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
        Issue((type == LogType.Warning ? "[avert] " : "[erreur] ") + condition.Replace('\n', ' '));
    }

    static void Issue(string text)
    {
        issues.Add(text);
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
            if (extended) Time.timeScale = ExtendedScale;
        }
        if (extended) { simTime += Time.deltaTime; Scripted(); }
        double start = double.Parse(SessionState.GetString(KeyStart, "0"));
        double elapsed = EditorApplication.timeSinceStartup - start;
        maxMoney = Mathf.Max(maxMoney, wallet.Money);
        SessionState.SetInt(KeyMoney, maxMoney);

        if (extended ? simTime >= ExtendedSeconds : elapsed >= Duration) { Finish(); return; }
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

    // Actions scriptées de la version étendue, à des instants de jeu fixes.
    static void Scripted()
    {
        var board = UnityEngine.Object.FindFirstObjectByType<OrderBoard>();
        var pro = UnityEngine.Object.FindFirstObjectByType<ProPoints>();
        if (stage == 0 && simTime >= 5f) { stage = 1; DevCheats.UnlockChain(); wallet.Add(200000); }
        else if (stage == 1 && simTime >= 20f) { stage = 2; BuyUpgrades(); }
        else if (stage == 2 && simTime >= 60f) { stage = 3; DeliverOrder(board, pro); }
        else if (stage == 3 && simTime >= 100f) { stage = 4; BuyReputation(pro); }
        else if (stage == 4 && simTime >= 150f) { stage = 5; DeliverOrder(board, pro); }
    }

    static void BuyUpgrades()
    {
        var up = UnityEngine.Object.FindFirstObjectByType<UpgradeState>();
        if (up != null)
            for (int i = 0; i < up.Config.Upgrades.Length; i++)
                for (int k = 0; k < 3; k++) if (up.TryBuy(i)) upgradesBought++;
        var mu = UnityEngine.Object.FindFirstObjectByType<MachineUpgradeState>();
        if (mu != null)
            foreach (MachineUpgradeConfig.Machine m in mu.Config.Machines)
                for (int axis = 0; axis < 2; axis++)
                    for (int k = 0; k < 3; k++)
                    {
                        if (mu.IsMax(m.id, axis) || !wallet.TrySpend(mu.NextCost(m.id, axis))) continue;
                        mu.Purchase(m.id, axis);
                        machineBought++;
                    }
    }

    static void DeliverOrder(OrderBoard board, ProPoints pro)
    {
        if (board == null || pro == null) { Issue("[test] OrderBoard ou ProPoints introuvable"); return; }
        int money = wallet.Money, proBefore = pro.Points;
        if (!board.HasActive)
        {
            board.AddOffer(board.Generate(ordersDelivered == 0));
            if (!board.Accept(0)) { Issue("[test] commande refusée"); return; }
        }
        var needed = new List<ProductType>();
        int guard = 0;
        while (board.HasActive && guard++ < 200)
        {
            board.GetNeeded(needed);
            if (needed.Count == 0) break;
            board.Deliver(needed[0]);
        }
        if (board.HasActive) { Issue("[test] commande non terminée après les livraisons"); return; }
        ordersDelivered++;
        if (wallet.Money <= money) Issue("[test] aucun argent reçu à la fin de la commande");
        if (pro.Points <= proBefore) Issue("[test] aucun Point Pro reçu à la fin de la commande");
    }

    static void BuyReputation(ProPoints pro)
    {
        var rep = UnityEngine.Object.FindFirstObjectByType<ReputationState>();
        if (rep == null || pro == null) { Issue("[test] ReputationState ou ProPoints introuvable"); return; }
        pro.Add(300);
        for (int pass = 0; pass < 10; pass++)
            for (int i = 0; i < rep.Config.Nodes.Length; i++) if (rep.TryBuy(i)) repBought++;
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
        if (extended)
        {
            if (upgradesBought == 0) failures.Add("aucune amélioration du joueur achetée");
            if (machineBought == 0) failures.Add("aucune amélioration de machine achetée");
            if (ordersDelivered < 2) failures.Add("commandes livrées : " + ordersDelivered + " sur 2");
            if (repBought == 0) failures.Add("aucun achat de Réputation");
            if (wallet.Money < 0) failures.Add("argent négatif : " + wallet.Money);
            Time.timeScale = 1f;
            SaveSystem.Suspended = false;   // l'état riche doit pouvoir s'écrire pour vérifier la relecture (effacé juste après)
        }
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
        string label = extended ? "300 s simulées (x" + ExtendedScale + "), " + upgradesBought + " améliorations joueur, " + machineBought + " de machines, " + ordersDelivered + " commandes, " + repBought + " achats de Réputation" : "90 s";
        if (failures.Count == 0) Debug.Log("Test de fumée : OK (" + label + ", 0 erreur ni avertissement, argent max " + maxMoney + ", sauvegarde relue identique).");
        else Debug.LogError("Test de fumée : ÉCHEC\n" + string.Join("\n", failures));
        SaveSystem.Suspended = true; // l'arrêt ne doit pas réécrire la sauvegarde du test
        if (extended) SaveSystem.EraseEverything();   // pas d'état « triché » laissé sur le disque
        EditorApplication.isPlaying = false;
    }
}
