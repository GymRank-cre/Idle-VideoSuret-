using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Q2 : test de rythme des premières minutes (`Tools > Idle Prototype > Test de rythme (7 min)`).
// Un robot joue une PARTIE NEUVE à vitesse réelle, sans triche, comme un joueur normal : il ramasse des caisses, les dépose,
// apporte les produits, ramasse les billets, et va payer une dalle dès qu'il en a les moyens. Il note la minute de chaque jalon
// et le compare aux cibles de QUALITE.md §2. Résultat dans la console (« Test de rythme : ... »).
[InitializeOnLoad]
public static class FtueTest
{
    const string KeyActive = "Ftue.Active";
    const string KeyStart = "Ftue.Start";
    const string KeyMarks = "Ftue.Marks";
    const double Duration = 420.0;

    struct Milestone
    {
        public string key, label;
        public float target; // secondes
    }

    static readonly Milestone[] Milestones =
    {
        new Milestone { key = "crate", label = "première caisse ramassée", target = 10f },
        new Milestone { key = "bill", label = "premier billet (client servi)", target = 20f },
        new Milestone { key = "pad", label = "première dalle payée", target = 30f },
        new Milestone { key = "employee", label = "premier employé", target = 120f },
        new Milestone { key = "courier", label = "livreur (le poste tourne seul)", target = 60f * 3f },
        new Milestone { key = "test", label = "machine Test débloquée", target = 240f },
        new Milestone { key = "pack", label = "machine Emballage débloquée", target = 270f },
        new Milestone { key = "belt", label = "premier tapis (après l'Emballage)", target = 390f },
        new Milestone { key = "offer", label = "première offre de commande", target = 300f },
    };

    static readonly Dictionary<string, float> marks = new Dictionary<string, float>();
    static PlayerController player;
    static StackCarrier carrier;
    static ProductCarrier products;
    static Wallet wallet;
    static double lastPhaseChange;
    static int phase;
    static double padSince;
    static UnlockPad padTarget;
    static float lastLogged;

    static FtueTest()
    {
        EditorApplication.update += Watch;
        if (SessionState.GetBool(KeyActive, false))
        {
            foreach (string kv in SessionState.GetString(KeyMarks, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] p = kv.Split('=');
                if (p.Length == 2) marks[p[0]] = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
            }
            EditorApplication.update += Tick;
        }
    }

    [MenuItem("Tools/Idle Prototype/Test de rythme x5 (35 min)")]
    public static void RunFive()
    {
        SessionState.SetInt("Ftue.Remaining", 4);
        Run();
    }

    // enchaîne les passages restants quand le Play précédent est terminé
    static void Watch()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        int left = SessionState.GetInt("Ftue.Remaining", 0);
        if (left <= 0 || !SessionState.GetBool("Ftue.Again", false)) return;
        SessionState.SetBool("Ftue.Again", false);
        SessionState.SetInt("Ftue.Remaining", left - 1);
        Run();
    }

    [MenuItem("Tools/Idle Prototype/Test de rythme (7 min)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Test de rythme : quitter le Play d'abord."); return; }
        SaveSystem.EraseEverything();
        PlayerPrefs.DeleteAll();
        marks.Clear();
        SessionState.SetBool(KeyActive, true);
        SessionState.SetString(KeyStart, "0");
        SessionState.SetString(KeyMarks, "");
        EditorApplication.isPlaying = true;
    }

    static void Save()
    {
        var sb = new StringBuilder();
        foreach (var kv in marks) sb.Append(kv.Key).Append('=').Append(kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(';');
        SessionState.SetString(KeyMarks, sb.ToString());
    }

    static void Mark(string key, float t)
    {
        if (marks.ContainsKey(key)) return;
        marks[key] = t;
        Save();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) return;
        Application.runInBackground = true;
        if (player == null)
        {
            player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            carrier = UnityEngine.Object.FindFirstObjectByType<StackCarrier>();
            products = UnityEngine.Object.FindFirstObjectByType<ProductCarrier>();
            wallet = UnityEngine.Object.FindFirstObjectByType<Wallet>();
            if (player == null || wallet == null) return;
            SaveSystem.Suspended = true; // l'état du test ne doit jamais écrire la vraie sauvegarde
            if (SessionState.GetString(KeyStart, "0") == "0") SessionState.SetString(KeyStart, EditorApplication.timeSinceStartup.ToString("R"));
            lastPhaseChange = EditorApplication.timeSinceStartup;
        }
        double start = double.Parse(SessionState.GetString(KeyStart, "0"));
        float t = (float)(EditorApplication.timeSinceStartup - start);

        Detect(t);
        if (t - lastLogged >= 30f)
        {
            lastLogged = t;
            Debug.Log("Rythme t=" + Clock(t) + " argent=" + wallet.Money + " phase=" + phase + " dalle=" + (padTarget != null ? padTarget.name + "(" + padTarget.RemainingPrice + ")" : "-") + " pos=" + player.transform.position.ToString("F1") + " caisses=" + (carrier != null ? carrier.Count : -1) + " | monde : billets=" + UnityEngine.Object.FindObjectsByType<Bill>(FindObjectsSortMode.None).Length + " produits=" + UnityEngine.Object.FindObjectsByType<Product>(FindObjectsSortMode.None).Length + " clients=" + UnityEngine.Object.FindObjectsByType<Customer>(FindObjectsSortMode.None).Length + " employes=" + UnityEngine.Object.FindObjectsByType<Employee>(FindObjectsSortMode.None).Length + " produitsPorte=" + (products != null ? products.Count : -1) + " | " + Diagnose());
        }
        if (t >= Duration) { Finish(); return; }
        Drive();
    }

    // ------------------------------------------------------------------ jalons

    static void Detect(float t)
    {
        if (carrier != null && carrier.Count > 0) Mark("crate", t);
        if (wallet.Money > 0) Mark("bill", t);
        foreach (UnlockPad pad in UnityEngine.Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pad.SaveId != null && pad.SaveId.StartsWith("UpPad_")) continue;
            if (pad.IsUnlocked) Mark("pad", t);
        }
        if (Active("Employee")) Mark("employee", t);
        if (Active("Courier")) Mark("courier", t);
        if (RootActive("TestMachine")) Mark("test", t);
        if (RootActive("PackMachine")) Mark("pack", t);
        if (RootActive("Belt1")) Mark("belt", t);
        var board = UnityEngine.Object.FindFirstObjectByType<OrderBoard>();
        if (board != null && (board.Offers.Count > 0 || board.HasActive)) Mark("offer", t);
        var pro = UnityEngine.Object.FindFirstObjectByType<ProPoints>();
        if (pro != null && pro.Points > 0) Mark("pro", t);
        var rep = UnityEngine.Object.FindFirstObjectByType<ReputationState>();
        if (rep != null)
            for (int i = 0; i < rep.Config.Nodes.Length; i++) if (rep.Level(i) > 0) Mark("rep", t);
    }

    // état de la file et des produits : sert à comprendre un blocage (type demandé par le client en tête / types présents)
    static string Diagnose()
    {
        var sb = new StringBuilder();
        var queue = UnityEngine.Object.FindFirstObjectByType<CustomerQueue>();
        Customer head = queue != null ? queue.Head : null;
        sb.Append("tete=").Append(head != null && head.OrderType != null ? head.OrderType.name + " x" + head.Remaining : "-");
        var count = new Dictionary<string, int>();
        foreach (Product p in UnityEngine.Object.FindObjectsByType<Product>(FindObjectsSortMode.None))
        {
            if (!p.gameObject.activeInHierarchy || p.Type == null) continue;
            count.TryGetValue(p.Type.name, out int n);
            count[p.Type.name] = n + 1;
        }
        sb.Append(" produits{");
        foreach (var kv in count) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
        sb.Append('}');
        if (queue != null)
        {
            sb.Append(" file[");
            foreach (Customer c in queue.Line) sb.Append(c.OrderType != null ? c.OrderType.name : "?").Append('x').Append(c.Remaining).Append(' ');
            sb.Append(']');
        }
        return sb.ToString();
    }

    static bool Active(string typeName)
    {
        foreach (var e in UnityEngine.Object.FindObjectsByType<Employee>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (typeName == "Employee") return true;
        if (typeName == "Courier") return UnityEngine.Object.FindFirstObjectByType<Courier>(FindObjectsInactive.Exclude) != null;
        return false;
    }

    static bool RootActive(string name)
    {
        foreach (GameObject g in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (g.name == name && g.transform.parent == null) return true;
        return false;
    }

    // ------------------------------------------------------------------ robot (comme un joueur normal, vitesse réelle)

    static void Drive()
    {
        double now = EditorApplication.timeSinceStartup;

        // 1. une dalle à portée de l'argent : on y va et on y reste jusqu'à ce qu'elle soit payée
        if (padTarget == null || padTarget.IsUnlocked || !padTarget.gameObject.activeInHierarchy || now - padSince > 12.0)
        {
            padTarget = null;
            UnlockPad best = null; float bd = float.MaxValue;
            foreach (UnlockPad pad in UnityEngine.Object.FindObjectsByType<UnlockPad>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (pad.IsUnlocked || (pad.SaveId != null && pad.SaveId.StartsWith("UpPad_"))) continue;
                if (pad.RemainingPrice > wallet.Money) continue;
                float d = (pad.transform.position - player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = pad; }
            }
            if (best != null) { padTarget = best; padSince = now; }
        }
        if (padTarget != null) { MoveTo(padTarget.transform.position); return; }

        // 2. sinon la boucle de jeu : palette, poste, produits, comptoir, billets
        Vector3 target = player.transform.position;
        if (phase == 0) { var pile = UnityEngine.Object.FindFirstObjectByType<ResourcePile>(); if (pile != null) target = pile.transform.position; if (carrier.Count >= carrier.Capacity) Next(); }
        else if (phase == 1) { var st = FindStation(); if (st != null) target = st.position; if (carrier.Count == 0) Next(); }
        else if (phase == 2)
        {
            // comme un joueur : on prend le produit que demande le client en tête (pas n'importe lequel)
            var queue = UnityEngine.Object.FindFirstObjectByType<CustomerQueue>();
            Customer head = queue != null ? queue.Head : null;
            var st = FindStation();
            target = st != null ? st.position : target;
            if (head != null && head.OrderType != null && products != null)
            {
                if (products.Has(head.OrderType)) Next();
                else
                {
                    float bd2 = float.MaxValue;
                    foreach (Product p in UnityEngine.Object.FindObjectsByType<Product>(FindObjectsSortMode.None))
                    {
                        if (!p.gameObject.activeInHierarchy || p.Type != head.OrderType || st == null || (p.transform.position - st.position).sqrMagnitude > 25f) continue;
                        float d = (p.transform.position - player.transform.position).sqrMagnitude;
                        if (d < bd2) { bd2 = d; target = p.transform.position; }
                    }
                }
            }
            else if (products != null && products.Count > 0) Next();
        }
        else if (phase == 3)
        {
            var counter = UnityEngine.Object.FindFirstObjectByType<ServiceCounter>();
            if (counter != null) target = counter.transform.position;
            if (products != null && products.Count == 0) Next();
        }
        else
        {
            Bill best = null; float bd = float.MaxValue;
            foreach (Bill b in UnityEngine.Object.FindObjectsByType<Bill>(FindObjectsSortMode.None))
            {
                float d = (b.transform.position - player.transform.position).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            if (best != null) target = best.transform.position; else Next();
        }
        if (now - lastPhaseChange > 12.0) Next();
        MoveTo(target);
    }

    static Transform FindStation()
    {
        foreach (InstallStation s in UnityEngine.Object.FindObjectsByType<InstallStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (s.name == "InstallStation") return s.transform;
        return null;
    }

    static void MoveTo(Vector3 target)
    {
        Vector3 d3 = target - player.transform.position;
        Vector2 dir = new Vector2(d3.x, d3.z);
        player.SetMoveInput(dir.magnitude < 0.4f ? Vector2.zero : dir.normalized);
    }

    static void Next()
    {
        phase = (phase + 1) % 5;
        lastPhaseChange = EditorApplication.timeSinceStartup;
    }

    // ------------------------------------------------------------------ résultat

    static string Clock(float s) { return ((int)(s / 60f)) + ":" + ((int)(s % 60f)).ToString("00"); }

    static void Finish()
    {
        player.SetMoveInput(Vector2.zero);
        var sb = new StringBuilder("Test de rythme : jalon | cible | mesuré | écart\n");
        int late = 0;
        foreach (Milestone m in Milestones)
        {
            if (marks.TryGetValue(m.key, out float t))
            {
                float gap = (t - m.target) / m.target * 100f;
                bool tooLate = t > m.target * 1.5f;
                if (tooLate) late++;
                sb.Append(m.label).Append(" | ").Append(Clock(m.target)).Append(" | ").Append(Clock(t)).Append(" | ").Append(gap >= 0 ? "+" : "").Append(Mathf.RoundToInt(gap)).Append(" %").Append(tooLate ? "  TROP TARD" : "").Append('\n');
            }
            else
            {
                late++;
                sb.Append(m.label).Append(" | ").Append(Clock(m.target)).Append(" | non atteint en ").Append(Clock((float)Duration)).Append("  TROP TARD\n");
            }
        }
        sb.Append("Argent en fin de test : ").Append(wallet.Money).Append("  (Points Pro et réputation : non évalués, le robot ne livre pas de commande)");
        SessionState.SetBool(KeyActive, false);
        SessionState.SetBool("Ftue.Again", SessionState.GetInt("Ftue.Remaining", 0) > 0);
        EditorApplication.update -= Tick;
        Debug.Log(sb.ToString());
        SaveSystem.Suspended = true;
        EditorApplication.isPlaying = false;
        Debug.Log("Test de rythme terminé : " + late + " jalon(s) hors tolérance (+50 %).");
    }
}
