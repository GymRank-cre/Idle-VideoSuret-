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

        // Loc
        Eq("Version : 7", Loc.T("settings.version", 7), "Loc avec argument");
        Eq("clé.inconnue", Loc.T("clé.inconnue"), "Loc clé inconnue renvoyée telle quelle");
        Eq("Niv 2/10", Loc.T("upgrades.level", 2, 10), "Loc niveau");

        // Migration de sauvegarde
        var old = new SaveData { version = 1, money = 50, upgrades = null, settingsSaved = true };
        SaveData m = SaveSystem.Migrate(old);
        Eq(SaveData.CurrentVersion.ToString(), m.version.ToString(), "migration v1 -> version courante");
        Eq("True", (m.upgrades != null).ToString(), "migration : liste d'améliorations créée");
        Eq("False", m.settingsSaved.ToString(), "migration : réglages remis par défaut");
        Eq("50", m.money.ToString(), "migration : l'argent est conservé");
        SaveData cur = SaveSystem.Migrate(new SaveData { money = 9 });
        Eq("9", cur.money.ToString(), "sauvegarde courante inchangée");

        SaveData v3 = SaveSystem.Migrate(new SaveData { version = 3, money = 7, machines = null });
        Eq("6", v3.version.ToString(), "migration v3 -> version courante (6)");
        Eq("True", (v3.machines != null && v3.machines.Count == 0).ToString(), "migration v3 -> v4 : aucune machine");
        Eq("7", v3.money.ToString(), "migration v3 -> v4 : l'argent est conservé");

        // Hors ligne avec la chaîne : le plus lent des maillons limite le débit, la valeur est multipliée
        {
            var offCfg = ScriptableObject.CreateInstance<OfflineConfig>();
            var stCfg = ScriptableObject.CreateInstance<StationConfig>();
            var emp = ScriptableObject.CreateInstance<EmployeeConfig>();
            var cust = ScriptableObject.CreateInstance<CustomerConfig>();
            var pt = ScriptableObject.CreateInstance<ProductType>();
            var sso = new SerializedObject(stCfg);
            SerializedProperty prods = sso.FindProperty("products");
            prods.arraySize = 1;
            prods.GetArrayElementAtIndex(0).objectReferenceValue = pt;
            sso.ApplyModifiedPropertiesWithoutUndo();
            var pso = new SerializedObject(pt);
            pso.FindProperty("value").intValue = 100;
            pso.ApplyModifiedPropertiesWithoutUndo();
            var oso = new SerializedObject(offCfg);
            oso.FindProperty("efficiency").floatValue = 1f;
            oso.FindProperty("minSeconds").floatValue = 0f;
            oso.ApplyModifiedPropertiesWithoutUndo();
            var cso = new SerializedObject(cust);
            cso.FindProperty("spawnInterval").floatValue = 0.5f; // clients très fréquents : jamais le goulot
            cso.ApplyModifiedPropertiesWithoutUndo();
            var eso = new SerializedObject(emp);
            eso.FindProperty("capacity").intValue = 100; // porteur et livreur très rapides : jamais le goulot
            eso.ApplyModifiedPropertiesWithoutUndo();
            int plain = OfflineEarnings.Compute(100, true, true, offCfg, stCfg, emp, emp, cust);
            var chain = new OfflineEarnings.Chain { automated = true, testThroughput = 0.4f, packThroughput = 0.2f, valueMultiplier = 2f };
            int withChain = OfflineEarnings.Compute(100, true, true, offCfg, stCfg, emp, emp, cust, 1f, 1f, 1f, 0, chain);
            // goulot = Emballage (0,2/s) x 100 s x 100 x 2 = 4000 ; sans chaîne le débit du poste (0,5/s par défaut) donne 5000
            Eq("4000", withChain.ToString(), "hors ligne : chaîne limitée par Emballage et valeur x2");
            Eq("True", (plain > 0).ToString(), "hors ligne sans chaîne : inchangé et positif");
            var fast = new OfflineEarnings.Chain { assemblySpeed = 2f };
            int doubled = OfflineEarnings.Compute(100, true, true, offCfg, stCfg, emp, emp, cust, 1f, 1f, 1f, 0, fast);
            Eq(plain.ToString("0"), (doubled / 2).ToString("0"), "hors ligne : assemblage 2x plus rapide = gain doublé");
            Object.DestroyImmediate(offCfg); Object.DestroyImmediate(stCfg); Object.DestroyImmediate(emp);
            Object.DestroyImmediate(cust); Object.DestroyImmediate(pt);
        }

        // 11b : améliorations par machine
        {
            var mu = ScriptableObject.CreateInstance<MachineUpgradeConfig>();
            var muo = new SerializedObject(mu);
            SerializedProperty ml = muo.FindProperty("machines");
            ml.arraySize = 1;
            SerializedProperty m0 = ml.GetArrayElementAtIndex(0);
            m0.FindPropertyRelative("id").stringValue = "test";
            m0.FindPropertyRelative("speedBaseCost").intValue = 120;
            m0.FindPropertyRelative("addBaseCost").intValue = 200;
            m0.FindPropertyRelative("speedMax").intValue = 12;
            m0.FindPropertyRelative("addMax").intValue = 3;
            muo.ApplyModifiedPropertiesWithoutUndo();
            MachineUpgradeConfig.Machine mm = mu.Machines[0];
            Eq("120", mu.SpeedCost(mm, 0).ToString(), "coût vitesse niveau 0");
            Eq("143", mu.SpeedCost(mm, 1).ToString(), "coût vitesse x1,19 par niveau");
            Eq("True", (mu.SpeedCost(mm, 10) > mu.SpeedCost(mm, 9)).ToString(), "coût croissant");
            Eq("1,00", mu.SpeedFactor(0, 0).ToString("0.00"), "vitesse niveau 0");
            Eq("1,75", mu.SpeedFactor(5, 0).ToString("0.00"), "vitesse niveau 5 : +15 % par niveau");
            Eq("5,00", mu.SpeedFactor(10, 0).ToString("0.00"), "palier 10 : x2 de débit");
            Eq("10,00", mu.SpeedFactor(10, 10).ToString("0.00"), "palier 10 des deux axes");
            var go3 = new GameObject("MuState");
            var mst = go3.AddComponent<MachineUpgradeState>();
            var mso = new SerializedObject(mst);
            mso.FindProperty("config").objectReferenceValue = mu;
            mso.ApplyModifiedPropertiesWithoutUndo();
            for (int n = 0; n < 9; n++) mst.Purchase("test", MachineUpgradeState.Speed);
            Eq("True", mst.Purchase("test", MachineUpgradeState.Speed).ToString(), "le 10e achat atteint un palier");
            Eq("10", mst.Level("test", MachineUpgradeState.Speed).ToString(), "niveau vitesse 10");
            mst.Purchase("test", MachineUpgradeState.Speed); mst.Purchase("test", MachineUpgradeState.Speed);
            Eq("True", mst.IsMax("test", MachineUpgradeState.Speed).ToString(), "plafond de vitesse (12)");
            Eq("False", mst.Purchase("test", MachineUpgradeState.Speed).ToString(), "au plafond : plus d'achat");
            Eq("12", mst.Level("test", MachineUpgradeState.Speed).ToString(), "le niveau ne dépasse pas le plafond");
            mst.SetLevel("test", MachineUpgradeState.Add, 99);
            Eq("3", mst.Level("test", MachineUpgradeState.Add).ToString(), "restauration plafonnée");

            // machine : AJOUTER = plus d'objets par cycle ; VITESSE = cycle plus court
            var mcfg = ScriptableObject.CreateInstance<MachineConfig>();
            var mgo = new GameObject("UpMachine");
            var pm = mgo.AddComponent<ProductionMachine>();
            pm.Configure(mcfg);
            var pso2 = new SerializedObject(pm);
            pso2.FindProperty("upgrades").objectReferenceValue = mst;
            pso2.FindProperty("machineId").stringValue = "test";
            pso2.ApplyModifiedPropertiesWithoutUndo();
            Eq("4", pm.OutputPerCycle.ToString(), "ajouter x3 : 1 + 3 objets par cycle");
            Eq("True", (pm.CycleSeconds < 2f / 4f).ToString(), "vitesse niveau 12 : cycle plus de 4 fois plus court");
            Object.DestroyImmediate(mgo); Object.DestroyImmediate(go3);
            Object.DestroyImmediate(mcfg); Object.DestroyImmediate(mu);
        }

        SaveData v4 = SaveSystem.Migrate(new SaveData { version = 4, money = 9, machineLevels = null });
        Eq("6", v4.version.ToString(), "migration v4 -> version courante (6)");
        Eq("True", (v4.machineLevels != null && v4.machineLevels.Count == 0).ToString(), "migration v4 -> v5 : tout à 0");

        // 11c : commandes de clients
        {
            var oc = ScriptableObject.CreateInstance<OrderConfig>();
            var oso = new SerializedObject(oc);
            oso.FindProperty("clients").arraySize = 1;
            SerializedProperty tl = oso.FindProperty("tiers");
            tl.arraySize = 1;
            SerializedProperty t0 = tl.GetArrayElementAtIndex(0);
            t0.FindPropertyRelative("minItems").intValue = 2;
            t0.FindPropertyRelative("maxItems").intValue = 2;
            t0.FindPropertyRelative("patience").floatValue = 100f;
            t0.FindPropertyRelative("weight").floatValue = 1f;
            oso.FindProperty("offerDelayMin").floatValue = 1000f;
            oso.FindProperty("offerDelayMax").floatValue = 1000f;
            oso.FindProperty("maxOffers").intValue = 1;
            oso.ApplyModifiedPropertiesWithoutUndo();
            var ptA = ScriptableObject.CreateInstance<ProductType>();
            var ptB = ScriptableObject.CreateInstance<ProductType>();
            var pa = new SerializedObject(ptA); pa.FindProperty("value").intValue = 25; pa.ApplyModifiedPropertiesWithoutUndo();
            var pb = new SerializedObject(ptB); pb.FindProperty("value").intValue = 40; pb.ApplyModifiedPropertiesWithoutUndo();
            var bgo = new GameObject("BoardTest");
            var board = bgo.AddComponent<OrderBoard>();
            var bso = new SerializedObject(board);
            bso.FindProperty("config").objectReferenceValue = oc;
            SerializedProperty tp = bso.FindProperty("types");
            tp.arraySize = 2;
            tp.GetArrayElementAtIndex(0).objectReferenceValue = ptA;
            tp.GetArrayElementAtIndex(1).objectReferenceValue = ptB;
            bso.ApplyModifiedPropertiesWithoutUndo();

            Eq("0", board.Offers.Count.ToString(), "aucune offre au départ");
            board.Tick(0.1f);
            Eq("1", board.Offers.Count.ToString(), "une offre est générée");
            Eq("1", OrderBoard.TotalOf(board.Offers[0]).ToString(), "la première offre est facile : 1 colis");
            Eq("45", board.Offers[0].reward.ToString(), "récompense = valeur 25 x 1,8");
            Eq("1", board.Offers[0].pro.ToString(), "Points Pro : au moins 1");
            Eq("5", oc.ProPoints(1000).ToString(), "1000 $ / 200 = 5 Points Pro");
            for (int k = 0; k < 181; k++) board.Tick(1f);
            Eq("0", board.Offers.Count.ToString(), "l'offre expire au bout de 180 s");

            OrderBoard.Offer second = board.Generate(false);
            Eq("2", OrderBoard.TotalOf(second).ToString(), "palier : 2 colis");
            bool completed = false, failed = false;
            board.OnCompleted += o => completed = true;
            board.OnFailed += o => failed = true;
            for (int k = 0; k < 900; k++) board.Tick(1f);   // une nouvelle offre arrive (minuteur écoulé)
            Eq("True", board.Accept(0).ToString(), "accepter l'offre");
            Eq("False", board.Accept(0).ToString(), "une seule commande à la fois");
            int needed = board.Active.counts[0] > 0 ? 0 : 1;
            ProductType neededType = board.Types[needed];
            ProductType other = board.Active.counts[1 - needed] == 0 ? board.Types[1 - needed] : null; // un type non commandé, s'il y en a un
            if (other != null) Eq("False", board.Deliver(other).ToString(), "un colis qui n'est pas attendu ne compte pas");
            Eq("True", board.Deliver(neededType).ToString(), "un colis attendu compte");
            foreach (ProductType pt in board.Types) while (board.Needs(pt)) board.Deliver(pt);
            Eq("True", completed.ToString(), "commande finie");
            Eq("False", board.HasActive.ToString(), "plus de commande en cours");

            for (int k = 0; k < 1000; k++) board.Tick(1f);   // encore une offre
            board.Accept(0);
            for (int k = 0; k < 101; k++) board.Tick(1f);    // patience écoulée
            Eq("True", failed.ToString(), "délai écoulé : la camionnette repart");
            Eq("False", board.HasActive.ToString(), "commande abandonnée, sans pénalité");

            Object.DestroyImmediate(bgo); Object.DestroyImmediate(oc);
            Object.DestroyImmediate(ptA); Object.DestroyImmediate(ptB);
        }

        SaveData v5 = SaveSystem.Migrate(new SaveData { version = 5, money = 3 });
        Eq("6", v5.version.ToString(), "migration v5 -> v6 : version");
        Eq("0", v5.proPoints.ToString(), "migration v5 -> v6 : 0 Point Pro");

        // Économie : coût des améliorations strictement positif et croissant
        foreach (UpgradeConfig cfg in Resources.FindObjectsOfTypeAll<UpgradeConfig>())
        {
            foreach (UpgradeConfig.Upgrade u in cfg.Upgrades)
            {
                bool growing = true;
                for (int l = 1; l < u.maxLevel; l++) if (cfg.Cost(u, l) < cfg.Cost(u, l - 1)) growing = false;
                Eq("True", growing.ToString(), "coût croissant : " + u.id);
                Eq("True", (cfg.Cost(u, 0) > 0).ToString(), "coût > 0 : " + u.id);
            }
            break;
        }

        // Machine de production : cycle, sortie pleine = bloquée, rien n'est perdu
        var cfg2 = ScriptableObject.CreateInstance<MachineConfig>();
        var so2 = new SerializedObject(cfg2);
        so2.FindProperty("cycleSeconds").floatValue = 2f;
        so2.FindProperty("inputCapacity").intValue = 3;
        so2.FindProperty("outputCapacity").intValue = 1;
        so2.ApplyModifiedPropertiesWithoutUndo();
        var go2 = new GameObject("MachineTest");
        var machine = go2.AddComponent<ProductionMachine>();
        machine.Configure(cfg2);
        Eq("WaitingInput", machine.State.ToString(), "machine vide : entrée vide");
        machine.TryAddInput(2);
        Eq("False", machine.TryAddInput(2).ToString(), "entrée pleine refusée (3 max)");
        machine.Step(1f);
        Eq("Running", machine.State.ToString(), "machine en cycle");
        machine.Step(1.5f);
        Eq("1", machine.OutputCount.ToString(), "un objet sorti après le cycle");
        machine.Step(2.5f); // 2e cycle : la sortie (1 place) est pleine
        Eq("Blocked", machine.State.ToString(), "sortie pleine : goulot");
        Eq("1", machine.OutputCount.ToString(), "sortie plafonnée");
        machine.TryTakeOutput(1);
        machine.Step(0.1f);
        Eq("1", machine.OutputCount.ToString(), "le cycle bloqué se termine dès qu'une place se libère");
        Eq("1,2K > 3/10", StockLabel.Format(1234, 1250, 3, 10).Replace("/1,2K", "").Replace(" ", " "), "format du stock (entrée tronquée)");
        Object.DestroyImmediate(go2);
        Object.DestroyImmediate(cfg2);

        if (failures.Count == 0)
            Debug.Log("Tests unitaires : " + count + " vérifications, toutes réussies.");
        else
            Debug.LogError("Tests unitaires : " + failures.Count + " échec(s) sur " + count + "\n" + string.Join("\n", failures));
    }
}
