using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tests unitaires lancés par le menu `Tools > Idle Prototype > Tests unitaires` (le projet n'a pas d'assembly de test :
// le code du jeu est dans l'assembly par défaut, que les assemblies de test ne peuvent pas référencer).
// Jeu idle (U1) : économie, achats, commandes, Réputation, turbo, hors ligne, sauvegarde v8, migration depuis la v7.
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
        Eq("0,7", NumberFormat.RateValue(0.7), "gain d'un bouton, sans /s");
        Eq("45 s", NumberFormat.Time(45), "durée en secondes");
        Eq("3 min", NumberFormat.Time(180), "durée en minutes rondes");
        Eq("3 min 20 s", NumberFormat.Time(200), "durée minutes + secondes");
        Eq("2 h 5 min", NumberFormat.Time(7500), "durée en heures");
        Eq("0/s", NumberFormat.Rate(0), "revenu nul");
        Eq("0,6/s", NumberFormat.Rate(0.625), "revenu de départ");
        Eq("6,1/s", NumberFormat.Rate(6.12), "revenu sous 10 : une décimale");
        Eq("10/s", NumberFormat.Rate(9.96), "arrondi qui passe à 10");
        Eq("100/s", NumberFormat.Rate(99.6), "entier arrondi");
        Eq("1,23K/s", NumberFormat.Rate(1234), "revenu abrégé");
        Eq("12 s", NumberFormat.Seconds(11.2), "turbo : secondes arrondies au-dessus");
        Eq("60 s", NumberFormat.Wait(60), "recharge : secondes jusqu'à 60");
        Eq("3 min", NumberFormat.Wait(121), "recharge : minutes au-delà");
        Eq("0", NumberFormat.Gain(0), "gain nul");
        Eq("0,1", NumberFormat.Gain(0.04), "gain minuscule : jamais 0");
        Eq("1", NumberFormat.Gain(1), "gain 1");
        Eq("1,7", NumberFormat.Gain(1.74), "gain 1,7");
        Eq("9,9", NumberFormat.Gain(9.99), "gain sous 10 tronqué");
        Eq("12", NumberFormat.Gain(12.8), "gain >= 10 : entier");
        Eq("1,75K", NumberFormat.Gain(1750), "gain en K");
        Eq("0", NumberFormat.Short(0), "0");
        Eq("7", NumberFormat.Short(7), "7");
        Eq("999", NumberFormat.Short(999), "999");
        Eq("1K", NumberFormat.Short(1000), "1000");
        Eq("1,23K", NumberFormat.Short(1234), "1234");
        Eq("1,29K", NumberFormat.Short(1299), "1299 (tronqué, pas arrondi)");
        Eq("9,99K", NumberFormat.Short(9999), "9999");
        Eq("8,92M", NumberFormat.Short(8922805), "prix du mode MAX de la cible");
        Eq("1,05K", NumberFormat.Short(1050), "zéro gardé au milieu");
        Eq("4,06K", NumberFormat.RateValue(4060), "gains comparables sur les boutons");
        Eq("12K", NumberFormat.Short(12000), "12000");
        Eq("12,3K", NumberFormat.Short(12345), "12345");
        Eq("123K", NumberFormat.Short(123456), "123456");
        Eq("999K", NumberFormat.Short(999999), "999999 (jamais 1000K)");
        Eq("1M", NumberFormat.Short(1000000), "1 million");
        Eq("1,2M", NumberFormat.Short(1200000), "1,2 million");
        Eq("3,4B", NumberFormat.Short(3400000000L), "3,4 milliards");
        Eq("2T", NumberFormat.Short(2000000000000L), "2 billions");
        Eq("1,5aa", NumberFormat.Short(1500000000000000L), "1,5aa");
        Eq("-1,23K", NumberFormat.Short(-1234), "négatif");
        Eq("9,2ab", NumberFormat.Short(9200000000000000000L), "9,2ab (1e18 = groupe 6)");

        // Loc
        Eq("Version : 7", Loc.T("settings.version", 7), "Loc avec argument");
        Eq("clé.inconnue", Loc.T("clé.inconnue"), "Loc clé inconnue renvoyée telle quelle");
        Eq("Niv 2/10", Loc.T("upgrades.level", 2, 10), "Loc niveau");


        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string F(double v, string f = "0.####") { return v.ToString(f, inv); }

        // ---------------------------------------------------------------- économie (port de econ.js)
        var cfg = ScriptableObject.CreateInstance<IdleEconomyConfig>();
        void Ready(IdleGame g) { g.Crew = cfg.hirePrices.Length; g.Recalc(); }   // équipe complète, sans passer par les recrutements
        var eco = new IdleEconomy(cfg);
        var g0 = eco.GlobalsFor(new int[cfg.reputation.Length]);
        Eq("6", cfg.stations.Length.ToString(), "6 postes");
        Eq("6 6", IdleLayout.Slots.Length + " " + IdleLayout.Stocks.Length, "le plan de l'usine a un emplacement et un stock pour chaque poste");
        Eq("1 3 3 3 3 1", string.Join(" ", System.Array.ConvertAll(cfg.stations, s => s.slots.ToString())), "4 machines en 3 exemplaires, entre la serre et l'Expédition");
        for (int st3 = 0; st3 < IdleLayout.Slots.Length; st3++) Eq(cfg.stations[st3].slots.ToString(), IdleLayout.Slots[st3].Length.ToString(), "emplacements sur le plan du poste " + st3);
        Eq("1,12,144,1728,20736,248832", string.Join(",", System.Array.ConvertAll(cfg.stations, s => F(s.value))), "valeurs des postes");
        Eq("1.6,3,3.75,4.5,6,8", string.Join(",", System.Array.ConvertAll(cfg.stations, s => F(s.cycle))), "cycles des postes");
        Eq("True", (cfg.stations[0].unlockPayback == 0 && cfg.stations[1].unlockPayback == 0 && cfg.stations[2].unlockPayback > 0).ToString(), "la serre et l'Assemblage sont ouverts d'emblée, le reste a un prix");
        Eq("1 1 1 4 1 60 240", F(g0.revenue) + " " + F(g0.speed) + " " + F(g0.cost) + " " + F(g0.offlineHours) + " " + g0.slots + " " + F(g0.turboLength) + " " + F(g0.turboCooldown), "bonus à zéro");

        var s0 = new IdleEconomy.StationLevels { unlocked = true };
        Eq("0.625", F(eco.ItemsPerSecond(0, s0, g0)), "débit du 1er poste au niveau 0 (1 / 1,6 s)");
        Eq("1", F(eco.ItemsPerSecond(0, new IdleEconomy.StationLevels { unlocked = true, speed = 5 }, g0)), "vitesse niv 5 : +60 %");
        Eq("1.25", F(eco.ItemsPerSecond(0, new IdleEconomy.StationLevels { unlocked = true, add = 1 }, g0)), "ajouter niv 1 : x2 pièces");
        Eq("0,0,1,1,2", F(eco.MilestoneCount(9), "0") + "," + F(eco.MilestoneCount(0), "0") + "," + F(eco.MilestoneCount(10), "0") + "," + F(eco.MilestoneCount(24), "0") + "," + F(eco.MilestoneCount(25), "0"), "paliers x2 (10, 25...)");
        Eq("10.75", F(eco.ItemsPerSecond(0, new IdleEconomy.StationLevels { unlocked = true, speed = 6, add = 4 }, g0)), "palier 10 : le débit double (5 x 1,72 x 2 / 1,6)");
        Eq("25", eco.NextMilestone(10).ToString(), "palier suivant");
        Eq("10", F(eco.BulkCost(true, 0, 0, 1, g0)), "vitesse 1er poste : base max(10, revenu0 x 12)");
        Eq("7", F(eco.BulkCost(false, 0, 0, 1, g0)), "ajout 1er poste : base max(7, revenu0 x 6)");
        Eq("22", IdleEconomy.Ceil(eco.BulkCost(true, 0, 0, 2, g0)).ToString(), "2 niveaux de vitesse : 10 + 11,9 -> 22");
        Eq("1 1 2", eco.MaxAffordable(true, 0, 0, 10.5, g0) + " " + eco.MaxAffordable(true, 0, 0, 21, g0) + " " + eco.MaxAffordable(true, 0, 0, 22, g0), "achat max");
        Eq("0", eco.MaxAffordable(true, 0, 0, 9, g0).ToString(), "achat max : trop cher");
        Eq("True", (System.Math.Abs(eco.UnlockCost(2, g0) - 38.4 * cfg.stations[2].unlockPayback) < 1e-6).ToString(), "prix de la Programmation : son revenu de départ (38,4 $/s) x le temps de remboursement visé");
        List<IdleEconomy.StationLevels> Line(params IdleEconomy.StationLevels[] open)
        {
            var line = new List<IdleEconomy.StationLevels>(open);
            while (line.Count < cfg.stations.Length) line.Add(new IdleEconomy.StationLevels());
            return line;
        }
        var list = Line(s0.Copy(), s0.Copy());
        var opts = eco.Options(list, g0, 1);
        Eq("6", opts.Count.ToString(), "options : vitesse et ajout des 2 postes ouverts, 2e machine de l'Assemblage, déblocage du suivant");
        Eq("Slot 1", opts[4].kind + " " + opts[4].station, "option d'emplacement : sur le poste qui en a");
        Eq("35.2", F(opts[5].delta), "gain du déblocage de la Programmation : 0,27 pièce/s à 144 $, le surplus de l'Assemblage vendu à 12 $");
        Eq("False", opts[0].milestone.ToString(), "pas de palier au niveau 1");
        bool found;
        IdleEconomy.Option best = eco.Best(opts, cfg.newStationFavor, out found);
        Eq("True", found.ToString(), "un meilleur achat existe");

        // la chaîne : le plus lent décide, ce que le suivant n'absorbe pas se vend au prix du poste
        Eq("4", F(eco.TotalIncome(list, g0)), "revenu de départ : le débit de l'Assemblage x 12 $");
        Eq("0 3.5", F(eco.UpgradeGain(list, g0, 0, false, 1)) + " " + F(eco.UpgradeGain(list, g0, 1, false, 1)), "Ajouter : rien sur la serre, qui n'est pas le goulot ; 3,5 $/s sur l'Assemblage, limité par la serre");
        Eq("4", F(eco.TotalIncome(list, g0)), "le calcul du gain ne touche pas aux niveaux");
        var three = Line(new IdleEconomy.StationLevels { unlocked = true, add = 9 }, new IdleEconomy.StationLevels { unlocked = true, add = 2 }, new IdleEconomy.StationLevels { unlocked = true });
        var flowOut = new double[6]; var soldOut = new double[6];
        eco.Flows(three, g0, flowOut, soldOut);
        Eq("1 0.2667 0.7333 0.2667", F(flowOut[1]) + " " + F(flowOut[2]) + " " + F(soldOut[1]) + " " + F(soldOut[2]), "surplus : l'Assemblage sort 1 pièce/s, la Programmation en prend 0,27, le reste se vend à l'Assemblage");
        double threeIncome = 0.7333333 * 12 + 0.2666667 * 144;
        Eq("True", (System.Math.Abs(eco.TotalIncome(three, g0) - threeIncome) < 1e-3).ToString(), "revenu : chaque pièce au prix du poste où elle se vend");
        for (int st4 = 2; st4 < cfg.stations.Length; st4++) Eq("True", (eco.MoneyPerItem(st4, g0) > eco.MoneyPerItem(st4 - 1, g0)).ToString(), "une pièce vaut toujours plus au poste suivant");
        var tied = Line(new IdleEconomy.StationLevels { unlocked = true, add = 7 }, new IdleEconomy.StationLevels { unlocked = true, add = 14 });
        bool tiedFound; eco.Best(eco.Options(tied, g0, 1), cfg.newStationFavor, out tiedFound);
        Eq("True", tiedFound.ToString(), "postes à égalité parfaite : le conseil trouve quand même un achat");
        Eq("True", (IdleEconomy.Ceil(1e30) == long.MaxValue).ToString(), "prix démesuré : jamais négatif, donc jamais gratuit");

        // contrat de rythme : un robot touche « Meilleur achat » toutes les 3 s, sans turbo ; un humain relâché sera 2 à 4 fois plus lent.
        // Chaque étape tient ces jalons ou change ce tableau en le disant. Tolérance 15 %.
        {
            var rb = new IdleGame(cfg, 7, 0); var openAt = new double[cfg.stations.Length];
            double crewAt = 0, firstSale = 0, lastBuy = 0, longestWait = 0, secondAt = 0, thirdAt = 0; long buys = 0;
            for (double t = 0; t < 2.5 * 3600; t += 3)
            {
                rb.Tick(3);
                rb.BuyAdvised();
                while (rb.Offers.Count > 0 && rb.Active.Count < rb.ActiveSlots && rb.Accept(rb.Offers[0].id)) { }
                for (int r = 0; r < rb.Rep.Length; r++) rb.BuyRep(r);
                if (crewAt == 0 && rb.CrewReady) crewAt = t;
                if (firstSale == 0 && rb.Earned > 0) firstSale = t;
                for (int i = 0; i < openAt.Length; i++) if (openAt[i] == 0 && rb.Stations[i].unlocked) openAt[i] = t;
                int fewest = 9;
                for (int i = 1; i <= 4; i++) fewest = System.Math.Min(fewest, rb.Stations[i].slots);
                if (secondAt == 0 && fewest >= 2) secondAt = t;
                if (thirdAt == 0 && fewest >= 3) thirdAt = t;
                if (rb.Buys != buys) { buys = rb.Buys; if (t <= 3600) longestWait = System.Math.Max(longestWait, t - lastBuy); lastBuy = t; }
            }
            Eq("True", (crewAt > 0 && crewAt <= 40).ToString(), "rythme : les 3 recrutements en moins de 40 s (mesuré " + crewAt + " s)");
            Eq("True", (firstSale > 0 && firstSale <= 70).ToString(), "rythme : 1re pièce vendue en moins de 70 s (mesuré " + firstSale + " s)");
            void Window(string name, int station, double low, double high)
            {
                Eq("True", (openAt[station] >= low * 0.85 && openAt[station] <= high * 1.15).ToString(), "rythme : " + name + " entre " + low + " et " + high + " s (mesuré " + openAt[station] + " s)");
            }
            Window("1re Programmation", 2, 150, 240);
            Window("1er Test", 3, 480, 720);
            Window("1er Emballage", 4, 1080, 1560);
            Eq("True", (secondAt >= 2400 * 0.85 && secondAt <= 3600 * 1.15).ToString(), "rythme : 2e machine de chaque poste entre 40 min et 1 h (mesuré " + (secondAt / 60).ToString("0") + " min)");
            Eq("True", (thirdAt >= 5400 * 0.85 && thirdAt <= 7800 * 1.15).ToString(), "rythme : usine complète entre 1 h 30 et 2 h 10 (mesuré " + (thirdAt / 60).ToString("0") + " min)");
            Eq("True", (longestWait <= 480).ToString(), "rythme : jamais plus de 8 min sans achat pendant la 1re heure (mesuré " + longestWait + " s)");
        }

        // la matière circule d'étape en étape, et chacune attend la précédente
        var ch = new IdleChain(cfg);
        var two = new double[] { 1, 1, 0, 0, 0, 0 };
        ch.Step(10, 0, two, 2);
        Eq("0 0 0", F(ch.Sold[1]) + " " + F(ch.Cut) + " " + F(ch.Stock[1]), "sans robot : rien ne bouge");
        ch.Step(100, 1, two, 2);
        Eq("8 0 False", F(ch.Cut) + " " + F(ch.Raw) + " " + ch.Working[0], "découpeur seul : le tas se remplit (8 s de production), puis il s'arrête");
        ch.Step(100, 2, two, 2);
        Eq("8 8 0", F(ch.Cut) + " " + F(ch.Raw) + " " + F(ch.Stock[1]), "avec le graveur : les deux tas sont pleins, rien n'arrive au stock");
        ch.Step(200, 3, new double[] { 1, 0, 0, 0, 0, 0 }, 2);
        Eq("30 True", F(ch.Stock[1]) + " " + (ch.Carriers[0].Load == 0 && ch.Carriers[0].Phase == 0), "avec le drone : stock plein, il ne repart plus");
        var fl = new IdleChain(cfg);
        var inOut = new double[] { 1, 2, 0, 0, 0, 0 };
        fl.Step(30, 3, inOut, 2);
        fl.Step(300, 3, inOut, 2);
        Eq("True", (System.Math.Abs(fl.Sold[1] - 300) < 5).ToString(), "serre plus lente : l'Assemblage sort le débit de la serre (" + F(fl.Sold[1]) + " pièces en 300 s)");
        var sl = new IdleChain(cfg);
        sl.Step(330, 3, new double[] { 2, 1, 0, 0, 0, 0 }, 2);
        Eq("True", (sl.Sold[1] > 290 && sl.Sold[1] <= 330 && sl.Stock[1] <= sl.StockSize(1)).ToString(), "Assemblage plus lent : il sort son propre débit, le stock reste borné (" + F(sl.Sold[1]) + " pièces, stock " + F(sl.Stock[1]) + ")");
        var sp = new IdleChain(cfg);
        var spill = new double[] { 5, 4, 1, 0, 0, 0 };
        sp.Step(60, 3, spill, 3);
        sp.Step(100, 3, spill, 3);
        Eq("True True", (System.Math.Abs(sp.Sold[1] - 300) < 15) + " " + (System.Math.Abs(sp.Sold[2] - 100) < 5), "goulot au 2e poste : le 1er vend son surplus (" + F(sp.Sold[1]) + " en 100 s), le dernier vend tout ce qu'il sort (" + F(sp.Sold[2]) + ")");

        // ---------------------------------------------------------------- partie (achats, commandes, Réputation, turbo, hors ligne)
        var game = new IdleGame(cfg, 7, 1000);
        double hireTotal = 0; foreach (double price in cfg.hirePrices) hireTotal += price;
        Eq("100 0 True", F(game.Cash) + " " + F(game.Income) + " " + (hireTotal <= cfg.startCash), "au départ : de quoi recruter les 3 robots, rien ne rapporte encore");
        Eq("Invalid Invalid Invalid", game.BuyUpgrade(0, true, 1) + " " + game.BuyUnlock(2) + " " + game.BuyHire(1), "avant l'équipe complète : ni amélioration, ni déblocage, ni robot dans le désordre");
        Eq("Ok Ok", game.BuyHire(0) + " " + game.BuyHire(1), "découpeur puis graveur");
        Eq("0", F(game.Income), "deux robots : toujours rien");
        Eq("Ok True 0", game.BuyHire(2) + " " + game.CrewReady + " " + F(game.Cash), "drone : équipe complète, tout l'argent de départ dépensé");
        Eq("Invalid", game.BuyHire(0).ToString(), "robot déjà recruté");
        Eq("4", F(game.Income), "revenu de départ");
        Eq("Invalid", game.BuyUnlock(1).ToString(), "Assemblage déjà ouvert");
        Eq("Invalid", game.BuyUnlock(3).ToString(), "seul le prochain poste peut être débloqué");
        game.Tick(60);
        Eq("True", (game.Cash > 200 && game.Cash < 240).ToString(), "60 s : l'argent arrive à la sortie, une fois le stock alimenté (" + F(game.Cash) + " $)");
        double beforeRun = game.Earned;
        game.Tick(600);
        Eq("True", (System.Math.Abs(game.Earned - beforeRun - game.Income * 600) < game.Income * 3).ToString(), "gain réel = revenu affiché, en régime établi");
        Eq("Ok", game.BuyUpgrade(0, true, 1).ToString(), "1re vitesse achetée");
        Eq("1", game.Stations[0].speed.ToString(), "niveau de vitesse");
        game.Cash = 1e9; game.Recalc();
        Eq("Ok", game.BuyUnlock(2).ToString(), "déblocage du Test");
        Eq("True", game.Stations[2].unlocked.ToString(), "poste débloqué");
        long maxPrice = game.UpgradePrice(0, true, 0);
        double cashBefore = game.Cash;
        game.BuyUpgrade(0, true, 0);
        Eq("True", (System.Math.Abs(cashBefore - game.Cash - maxPrice) < 0.5 && game.Stations[0].speed > 10).ToString(), "achat MAX : prix payé = prix annoncé, plusieurs niveaux");
        Eq("True", (game.Eco.MilestoneCount(game.Stations[0].speed + game.Stations[0].add) >= 1).ToString(), "achat MAX : palier x2 franchi");

        // commandes
        var og = new IdleGame(cfg, 3, 0); Ready(og); og.Cash = 1e9; og.Recalc();
        og.Tick(0.1);
        Eq("3", og.Offers.Count.ToString(), "3 offres affichées");
        int id0 = og.Offers[0].id;
        Eq("True", og.Accept(id0).ToString(), "offre acceptée");
        Eq("1 3", og.Active.Count + " " + og.Offers.Count, "1 commande en cours, offres reconstituées");
        Eq("False", og.Accept(og.Offers[0].id).ToString(), "1 seule commande à la fois sans « Carnet »");
        double cashOrder = og.Cash; int proOrder = og.Pro;
        var act = og.Active[0];
        og.OrderProgress(100000, 1);
        Eq("0 1", og.Active.Count + " " + og.OrdersDone, "commande terminée");
        Eq("True", (og.Cash >= cashOrder + act.reward - 0.001 && og.Pro == proOrder + act.pro && act.pro >= 1).ToString(), "récompense en $ et en Points Pro");
        Eq("True", (act.reward > 0 && act.qty >= cfg.minQuantity).ToString(), "offre valide");
        og.Pro = 100;
        Eq("2 4", og.RepCost(0) + " " + new IdleEconomy(cfg).RepCost(0, 1), "coût de réputation : ceil(2 x 1,55^niveau)");
        Eq("12 40", og.RepCost(4) + " " + new IdleEconomy(cfg).RepCost(4, 1), "coût du Carnet : x3,3");
        Eq("True", og.BuyRep(4).ToString(), "achat du Carnet de commandes");
        Eq("2", og.ActiveSlots.ToString(), "2 commandes en même temps");
        og.BuyRep(0); og.Recalc();
        Eq("1.1", F(og.Globals.revenue), "Contrats premium : +10 %");
        Eq("True", (og.BuyRep(4) && !og.BuyRep(4)).ToString(), "Carnet : 2 niveaux maximum");

        // turbo
        var tg = new IdleGame(cfg, 1, 500); Ready(tg);
        double baseIncome = tg.Income;
        Eq("True", tg.TryTurbo().ToString(), "turbo lancé");
        Eq("True", (System.Math.Abs(tg.Income - 2 * baseIncome) < 1e-9).ToString(), "turbo : revenu x2");
        Eq("False", tg.TryTurbo().ToString(), "turbo en recharge");
        tg.Tick(61);
        Eq("False", tg.BoostActive.ToString(), "turbo fini après 60 s");
        Eq("False", tg.TurboReady.ToString(), "recharge de 240 s en cours");
        tg.Tick(250);
        Eq("True", tg.TurboReady.ToString(), "turbo prêt de nouveau");

        // hors ligne
        var og2 = new IdleGame(cfg, 1, 0); Ready(og2); double inc2 = og2.Income;
        Eq("0", F(og2.Offline(10).gain), "absence de 10 s : rien");
        var off = og2.Offline(100);
        Eq("True", (System.Math.Abs(off.gain - inc2 * 100) < 1e-6 && !off.capped && off.showWelcome).ToString(), "absence de 100 s : revenu x 100 s");
        var offCap = og2.Offline(100 * 3600);
        Eq("True", (offCap.capped && System.Math.Abs(offCap.seconds - 4 * 3600) < 1e-6).ToString(), "plafond de 4 h");
        og2.Pro = 1000; og2.BuyRep(5); og2.Recalc();
        Eq("6", F(og2.Globals.offlineHours), "Veille automatique : +2 h de plafond");

        // sauvegarde v9 : aller-retour identique
        var sg = new IdleGame(cfg, 5, 100); Ready(sg); sg.Cash = 1e7; sg.BuyUnlock(2); sg.BuyUpgrade(0, true, 5); sg.BuyUpgrade(1, false, 3); sg.Pro = 9; sg.ProTotal = 9; sg.BuyRep(0); sg.Tick(1);
        sg.Accept(sg.Offers[0].id); sg.TryTurbo(); sg.Tick(5);
        string json1 = JsonUtility.ToJson(sg.ToSave(12345));
        var sg2 = new IdleGame(cfg, 5, 100);
        sg2.Load(JsonUtility.FromJson<SaveData>(json1));
        Eq(json1, JsonUtility.ToJson(sg2.ToSave(12345)), "sauvegarde -> chargement -> sauvegarde : identique");
        Eq("True", (System.Math.Abs(sg2.Income - sg.Income) < 1e-9 && sg2.Active.Count == 1).ToString(), "état rechargé : revenu et commande en cours");
        Eq("3 True", sg2.Crew + " " + (System.Math.Abs(sg2.Chain.Stock[1] - sg.Chain.Stock[1]) < 1e-6 && System.Math.Abs(sg2.Chain.Carriers[0].Phase - sg.Chain.Carriers[0].Phase) < 1e-6), "état rechargé : équipe et matière en circulation");
        var half = new SaveData { cash = 0, crew = 2 };
        var hg = new IdleGame(cfg, 1, 0); hg.Load(half);
        Eq("2 45", hg.Crew + " " + F(hg.Cash), "sauvegarde sans argent avant l'équipe complète : de quoi finir de recruter");

        // sauvegarde abîmée ou valeurs absurdes
        var bad = new SaveData { cash = double.NaN, earned = -5, proPoints = -3, version = 8 };
        bad.stations.Add(new SaveData.StationSave { unlocked = false, speed = -9, add = 999999999 });
        bad.reputation.Add(new SaveData.ReputationLevel { id = "rev", level = 99 });
        bad.reputation.Add(new SaveData.ReputationLevel { id = "inconnu", level = 5 });
        var bg = new IdleGame(cfg, 1, 0); bg.Load(bad);
        Eq("100 0 True", F(bg.Cash) + " " + bg.Pro + " " + (bg.Stations[0].unlocked && bg.Stations[0].speed == 0 && bg.Stations[0].add <= 100000), "valeurs absurdes : repli sûr");
        Eq("10", bg.Rep[bg.Eco.RepIndex("rev")].ToString(), "niveau de Réputation borné au maximum");
        Eq("null", SaveSystem.TryParse("pas du json", cfg) == null ? "null" : "objet", "fichier abîmé : nouvelle partie");
        Eq("null", SaveSystem.TryParse("", cfg) == null ? "null" : "objet", "fichier vide : nouvelle partie");

        // migration v7 -> v8 : on garde l'argent, les Points Pro et la Réputation ; postes à zéro
        string v7 = "{\"version\":7,\"money\":500,\"proPoints\":7,\"lastSaveUtc\":42,\"reputation\":[{\"id\":\"revenue\",\"level\":3},{\"id\":\"chain\",\"level\":2},{\"id\":\"discount\",\"level\":1},{\"id\":\"stack\",\"level\":4}],"
                  + "\"pads\":[{\"id\":\"TestPad\",\"remaining\":0}],\"employees\":[\"hauler\"],\"soundOn\":false,\"settingsSaved\":true}";
        SaveData mig = SaveSystem.TryParse(v7, cfg);
        Eq("10 500 7 True", mig.version + " " + F(mig.cash) + " " + mig.proPoints + " " + (mig.stations.Count == 6 && mig.stations[0].unlocked && !mig.stations[1].unlocked), "migration v7 : argent, Points Pro, postes à zéro");
        var mg = new IdleGame(cfg, 1, 0); mg.Load(mig);
        Eq("3 2 1", mg.Rep[mg.Eco.RepIndex("rev")] + " " + mg.Rep[mg.Eco.RepIndex("spd")] + " " + mg.Rep[mg.Eco.RepIndex("cost")], "migration v7 : Réputation (revenus, cadence, remises)");
        Eq("False", mig.soundOn.ToString(), "migration v7 : réglages conservés");
        Eq("True True", mg.CrewReady + " " + mg.Stations[1].unlocked, "migration : pas de recrutement à refaire, Assemblage ouvert");
        string v8 = "{\"version\":8,\"cash\":5000.0,\"stations\":[{\"unlocked\":true,\"speed\":3,\"add\":2},{\"unlocked\":false,\"speed\":0,\"add\":0}]}";
        var m8 = new IdleGame(cfg, 1, 0); m8.Load(SaveSystem.TryParse(v8, cfg));
        Eq("3 5000 3 True", m8.Crew + " " + F(m8.Cash) + " " + m8.Stations[0].speed + " " + m8.Stations[1].unlocked, "migration v8 : la partie continue, la chaîne tourne");
        string v9 = "{\"version\":9,\"cash\":5000.0,\"crew\":3,\"stations\":[{\"unlocked\":true,\"speed\":3,\"add\":2},{\"unlocked\":true,\"speed\":1,\"add\":1},{\"unlocked\":true,\"speed\":7,\"add\":8},{\"unlocked\":false,\"speed\":0,\"add\":0},{\"unlocked\":false,\"speed\":0,\"add\":0}],\"active\":[{\"id\":4,\"client\":1,\"station\":2,\"qty\":9,\"pro\":1,\"say\":0,\"reward\":10.0,\"progress\":1.0}]}";
        var m9 = new IdleGame(cfg, 1, 0); m9.Load(SaveSystem.TryParse(v9, cfg));
        Eq("True 0 True 7 False 1 3", m9.Stations[2].unlocked + " " + m9.Stations[2].speed + " " + m9.Stations[3].unlocked + " " + m9.Stations[3].speed + " " + m9.Stations[4].unlocked + " " + m9.Stations[3].slots + " " + m9.Active[0].station, "migration v9 : la Programmation s'insère, le Test garde ses niveaux, sa commande le suit");
        Eq("True", (SaveSystem.TryParse("{\"version\":99}", cfg) == null).ToString(), "version future refusée");

        // ---------------------------------------------------------------- plaques d'achat au sol (U2b)
        var pg = new IdleGame(cfg, 2, 0);
        var S = IdleGame.PadKind.Speed; var A = IdleGame.PadKind.Add; var U = IdleGame.PadKind.Unlock; var H = IdleGame.PadKind.Hire;
        Eq("True False False False", pg.PadVisible(0, H) + " " + pg.PadVisible(1, H) + " " + pg.PadVisible(0, S) + " " + pg.PadVisible(1, A), "au départ : seule la plaque du premier robot");
        Eq("25 0 0", pg.PadPrice(0, H) + " " + pg.PadPrice(1, H) + " " + pg.PadPrice(0, S), "prix de la plaque de recrutement");
        Eq("Ok False True", pg.BuyPad(0, H) + " " + pg.PadVisible(0, H) + " " + pg.PadVisible(1, H), "recrutement par la plaque : la suivante apparaît");
        pg = new IdleGame(cfg, 2, 0); Ready(pg);
        Eq("Open Open Unlock Hidden Hidden Hidden", pg.PlateStateOf(0) + " " + pg.PlateStateOf(1) + " " + pg.PlateStateOf(2) + " " + pg.PlateStateOf(3) + " " + pg.PlateStateOf(4) + " " + pg.PlateStateOf(5), "plaques : ouverts, prochain à débloquer, le reste caché");
        Eq("True True False", pg.PadVisible(0, S) + " " + pg.PadVisible(0, A) + " " + pg.PadVisible(0, U), "poste ouvert : plaques Vitesse et Ajouter");
        Eq("False False True", pg.PadVisible(2, S) + " " + pg.PadVisible(2, A) + " " + pg.PadVisible(2, U), "prochain poste : plaque Débloquer");
        Eq("False False False", pg.PadVisible(3, S) + " " + pg.PadVisible(3, A) + " " + pg.PadVisible(3, U), "postes suivants : aucune plaque");
        Eq("10 7 True 0", pg.PadPrice(0, S) + " " + pg.PadPrice(0, A) + " " + (pg.PadPrice(2, U) == pg.UnlockPrice(2)) + " " + pg.PadPrice(3, U), "prix des plaques ; plaque cachée : pas de prix");
        pg.Cash = 5;
        Eq("TooExpensive", pg.BuyPad(0, A).ToString(), "toucher la plaque sans argent : refusé");
        Eq("Invalid", pg.BuyPad(3, U).ToString(), "plaque cachée : achat impossible");
        Eq("Invalid", pg.BuyPad(0, U).ToString(), "plaque Débloquer d'un poste déjà ouvert : impossible");
        pg.Cash = 1000;
        double cash0 = pg.Cash;
        Eq("Ok", pg.BuyPad(0, A).ToString(), "plaque Ajouter : achat");
        Eq("True 0 1", (pg.Cash < cash0) + " " + pg.Stations[0].speed + " " + pg.Stations[0].add, "plaque Ajouter : un niveau d'Ajouter, argent dépensé");
        Eq("Ok", pg.BuyPad(0, S).ToString(), "plaque Vitesse : achat");
        Eq("1 1", pg.Stations[0].speed + " " + pg.Stations[0].add, "plaque Vitesse : un niveau de Vitesse");
        pg.Cash = pg.UnlockPrice(2) + 1000;
        Eq("Ok", pg.BuyPad(2, U).ToString(), "plaque de déblocage : poste débloqué");
        Eq("True Open Unlock", pg.Stations[2].unlocked + " " + pg.PlateStateOf(2) + " " + pg.PlateStateOf(3), "après le déblocage : poste ouvert, le suivant à débloquer");
        var P = IdleGame.PadKind.Slot;
        Eq("False True False", pg.PadVisible(0, P) + " " + pg.PadVisible(1, P) + " " + pg.PadVisible(3, P), "plaque Machine : seulement sur un poste ouvert qui a de la place");
        double rate1 = pg.Rate(1);
        pg.Cash = pg.SlotPrice(1) - 1;
        Eq("TooExpensive", pg.BuyPad(1, P).ToString(), "machine trop chère");
        pg.Cash = 1e14;
        Eq("Ok 2 True", pg.BuyPad(1, P) + " " + pg.Stations[1].slots + " " + (System.Math.Abs(pg.Rate(1) - 2 * rate1) < 1e-9), "2e machine : le débit du poste double");
        Eq("(6.3, 1.4) (8.6, 1.4)", Inv(IdleLayout.PadPosition(1, P, 1)) + " " + Inv(IdleLayout.PadPosition(1, P, 2)), "la plaque Machine se pose sur la place suivante");
        Eq("Ok 3 False Invalid", pg.BuySlot(1) + " " + pg.Stations[1].slots + " " + pg.PadVisible(1, P) + " " + pg.BuySlot(1), "3 machines au plus");
        Eq("Invalid", pg.BuySlot(0).ToString(), "une seule serre");
        var whole = new IdleGame(cfg, 3, 0); Ready(whole);
        whole.Cash = 1e15;
        for (int st5 = 2; st5 < cfg.stations.Length; st5++) Eq("Ok", whole.BuyUnlock(st5).ToString(), "ouverture du poste " + st5);
        for (int st5 = 0; st5 < cfg.stations.Length; st5++) { whole.Stations[st5].speed = 20; whole.Stations[st5].add = 20; }
        whole.Recalc();
        whole.Tick(120);
        double wholeBefore = whole.Earned;
        whole.Tick(300);
        double wholeGain = (whole.Earned - wholeBefore) / 300;
        Eq("True", (System.Math.Abs(wholeGain - whole.Income) < whole.Income * 0.03).ToString(), "toute la chaîne : gain réel = revenu affiché (" + F(wholeGain, "0") + " pour " + F(whole.Income, "0") + " $/s), le chariot suit");
        var kept = new IdleGame(cfg, 2, 0); kept.Load(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(pg.ToSave(1))));
        Eq("3", kept.Stations[1].slots.ToString(), "emplacements sauvegardés");
        pg.BuyMode = 10; pg.Cash = 1e9;
        int before10 = pg.Stations[0].add;
        Eq("True", (pg.PadPrice(0, A) > 0).ToString(), "mode x10 : prix sur la plaque");
        pg.BuyPad(0, A);
        Eq("10", (pg.Stations[0].add - before10).ToString(), "mode x10 : 10 niveaux d'un coup");
        pg.BuyMode = 0;
        pg.Cash = 1e5;
        int beforeMax = pg.Stations[0].add;
        pg.BuyPad(0, A);
        Eq("True", (pg.Stations[0].add - beforeMax > 10 && pg.Cash >= 0).ToString(), "mode MAX : plusieurs niveaux, argent jamais négatif");
        bool af, ah; IdleEconomy.Option ag;
        var ap = new IdleGame(cfg, 2, 0);
        IdleEconomy.Option ao = ap.Advice(out af, out ah, out ag);
        Eq("True False", IdleGame.IsAdvised(ao, 0, H) + " " + IdleGame.IsAdvised(ao, 0, S), "avant l'équipe complète, le conseil est le recrutement suivant");
        Eq("Ok 1", ap.BuyAdvised() + " " + ap.Crew, "Meilleur achat : recrute");
        Ready(ap);
        ao = ap.Advice(out af, out ah, out ag);
        Eq("True False False", IdleGame.IsAdvised(ao, 1, A) + " " + IdleGame.IsAdvised(ao, 1, S) + " " + IdleGame.IsAdvised(ao, 0, A), "le conseil désigne une seule plaque, sur le goulot");

        // ---------------------------------------------------------------- caméra libre et gestes (UL)
        Eq("False True", IdleWorldInput.Moved(9f, 1f) + " " + IdleWorldInput.Moved(9.5f, 1f), "seuil du glissement : 9 px");
        Eq("False True", IdleWorldInput.Moved(27f, 3f) + " " + IdleWorldInput.Moved(28f, 3f), "seuil du glissement : suit la densité de l'écran");
        Eq("True False False", IdleWorldInput.IsTap(false, 0.5f) + " " + IdleWorldInput.IsTap(true, 0.1f) + " " + IdleWorldInput.IsTap(false, 0.7f), "toucher : court et sans mouvement");
        Eq("(-13.5, 2.2) (10.0, 25.0) (1.0, 5.0)", Inv(IdleFraming.Clamp(new Vector2(-50f, -50f))) + " " + Inv(IdleFraming.Clamp(new Vector2(50f, 50f))) + " " + Inv(IdleFraming.Clamp(new Vector2(1f, 5f))), "limites de la caméra");
        Eq("0,55 3,00 2,00", Fr(IdleFraming.ClampZoom(0.2f)) + " " + Fr(IdleFraming.ClampZoom(9f)) + " " + Fr(IdleFraming.ClampZoom(2f)), "limites du zoom");
        Vector3 planPoint = IdleLayout.ToWorld(3f, 5f);
        Eq("(-2.0, 8.0)", Inv(IdleFraming.ToPlan(planPoint)), "plan a, b : a = x - y, b = x + y");
        Eq("True", (Vector3.Distance(IdleFraming.ToWorld(IdleFraming.ToPlan(planPoint)), planPoint) < 0.001f).ToString(), "plan a, b : aller-retour");
        Eq("0,41 0,41", Fr(IdleFramingViewX(cfg, 0)) + " " + Fr(IdleFramingViewX(cfg, 1)), "cadrage : la colonne de droite est exclue");
        float fh = 24.5f, fw = fh * 390f / 844f;   // ce que voit la caméra au zoom 1 sur un écran 390 x 844
        var fg = new IdleGame(cfg, 2, 0);
        Vector2 Screen(IdleFraming.View view, Vector2 plan)   // où tombe un point du plan, en part de la largeur de l'écran
        {
            return new Vector2(view.viewportX + (plan.x - plan.y - view.focus.x) / Mathf.Sqrt(2f) * view.zoom / fw, 0f);
        }
        IdleFraming.View vStart = IdleFraming.Default(fg, fw, fh, 55f, 0.18f);
        float startRight = Screen(vStart, IdleLayout.Slots[1][0] + new Vector2(IdleLayout.StationSize, 0f)).x;
        float startLeft = Screen(vStart, IdleLayout.PadHire[0]).x;
        Eq("True True", (startRight < 1f) + " " + (startLeft - 0.9f / Mathf.Sqrt(2f) * vStart.zoom / fw > 0f), "au départ : l'Assemblage entier et la plaque de recrutement à l'écran");
        Ready(fg);
        IdleFraming.View vRun = IdleFraming.Default(fg, fw, fh, 55f, 0.18f);
        for (int si = 2; si < 5; si++) fg.Stations[si].unlocked = true;
        fg.Stations[4].slots = 3;
        IdleFraming.View vFar = IdleFraming.Default(fg, fw, fh, 55f, 0.18f);
        Eq("True True", (vRun.zoom >= IdleFraming.MinZoom) + " " + (vRun.zoom <= IdleFraming.DefaultMaxZoom), "cadrage : zoom dans ses limites");
        Eq("True True", (vFar.focus.y > vRun.focus.y) + " " + (vFar.zoom >= 0.8f), "cadrage : la caméra suit la dernière rangée ouverte, sans jamais perdre les machines de vue (zoom " + Fr(vFar.zoom) + ")");
        Eq("True", (Screen(vFar, IdleLayout.PadAdd[4] + new Vector2(0.45f, -0.45f)).x < 1f).ToString(), "cadrage : la rangée tient dans l'écran");
        Eq("3,10", Fr(IdleFraming.Station(2, 3.1f).zoom), "sélection : on ne dézoome pas");
        Eq("2,00", Fr(IdleFraming.Station(2, 1f).zoom), "sélection : zoom d'au moins 2");
        Eq("0 1 -1", IdleFramingPick(0.1f) + " " + IdleFramingPick(0.8f) + " " + IdleFramingPick(5f), "la plaque la plus proche du doigt gagne ; loin de tout : aucune");
        Eq("3 -1 1", IdleSelection.Next(-1, 3) + " " + IdleSelection.Next(3, 3) + " " + IdleSelection.Next(3, 1), "fiche : ouvrir, refermer sur le même poste, changer de poste");

        var pj = JsonUtility.ToJson(pg.ToSave(1));
        var pg2 = new IdleGame(cfg, 2, 0); pg2.Load(JsonUtility.FromJson<SaveData>(pj));
        Eq("0", pg2.BuyMode.ToString(), "mode d'achat sauvegardé");

        // ---------------------------------------------------------------- animation des machines (U2c)
        var vg = new IdleGame(cfg, 4, 0);
        Eq("0.625", F(vg.VisualFrequency(0)), "fréquence visuelle de la serre au départ : 1 / 1,6 s");
        vg.Stations[0].speed = 50; vg.Recalc();
        Eq("1.6", F(vg.VisualFrequency(0)), "fréquence visuelle plafonnée à 1,6 cycles/s");
        vg.Stations[5].unlocked = true; vg.Recalc();
        Eq("0.125", F(vg.VisualFrequency(5)), "Expédition au départ : 1 / 8 s");
        vg.Stations[5].speed = 1000; vg.Recalc();
        Eq("0.45", F(vg.VisualFrequency(5)), "Expédition plafonnée à 0,45");

        // textes des fenêtres : rien ne manque dans Loc
        var missing = new System.Collections.Generic.List<string>();
        for (int c = 0; c < cfg.clientCount; c++) if (Loc.T("client." + c + ".name").StartsWith("client.")) missing.Add("client " + c);
        for (int c = 0; c < cfg.sayCount; c++) if (Loc.T("order.say." + c).StartsWith("order.")) missing.Add("réplique " + c);
        foreach (var st in cfg.stations) if (Loc.T("items." + st.id).StartsWith("items.")) missing.Add("pièces " + st.id);
        foreach (var node in cfg.reputation) if (Loc.T("repnode." + node.id + ".name").StartsWith("repnode.") || Loc.T("repnode." + node.id + ".desc").StartsWith("repnode.")) missing.Add("réputation " + node.id);
        Eq("", string.Join(", ", missing), "textes des fenêtres");
        Eq("8", cfg.clientCount.ToString(), "8 clients, 8 portraits");

        // écran titre : l'image couvre toujours tout l'écran, sans déformation
        Eq("1000,1769", IdleTitleScreen.CoverSize(new Vector2(1000, 1000), 0.5654f).x.ToString("0") + "," + IdleTitleScreen.CoverSize(new Vector2(1000, 1000), 0.5654f).y.ToString("0"), "écran large : largeur d'abord, le bas est coupé");
        Eq("477,844", IdleTitleScreen.CoverSize(new Vector2(390, 844), 0.5654f).x.ToString("0") + "," + IdleTitleScreen.CoverSize(new Vector2(390, 844), 0.5654f).y.ToString("0"), "téléphone étroit : hauteur d'abord, les côtés sont coupés");

        // l'usine grandit
        Eq("0 0 1 1 2 3 4 4", eco.AspectOf(0) + " " + eco.AspectOf(9) + " " + eco.AspectOf(10) + " " + eco.AspectOf(24) + " " + eco.AspectOf(25) + " " + eco.AspectOf(50) + " " + eco.AspectOf(100) + " " + eco.AspectOf(500), "aspects : un de plus à 10, 25, 50 et 100");
        var ug = new IdleGame(cfg, 6, 0);
        var fr = new System.Globalization.CultureInfo("fr-FR");
        Eq("1,00", ug.BeltSpeedFactor(0).ToString("0.00", fr), "tapis : vitesse de départ");
        ug.Stations[0].speed = 10; ug.Recalc();
        Eq("2,20", ug.BeltSpeedFactor(0).ToString("0.00", fr), "tapis : accéléré par la Vitesse");
        ug.Stations[0].speed = 500; ug.Recalc();
        Eq("3,00", ug.BeltSpeedFactor(0).ToString("0.00", fr), "tapis : vitesse plafonnée");
        Eq("2", ug.BeltItemCount(0, 6).ToString(), "tapis : caisses au départ");
        Eq("4", ug.StackSize(0).ToString(), "pile : caisses au départ");
        ug.Stations[0].add = 12;
        Eq("4 6", ug.BeltItemCount(0, 6) + " " + ug.StackSize(0), "Ajouter : plus de caisses sur le tapis et dans la pile");
        ug.Stations[0].add = 600;
        Eq("6 8", ug.BeltItemCount(0, 6) + " " + ug.StackSize(0), "Ajouter : plafonné");

        // étiquettes : aucune ne recouvre une autre étiquette ni une plaque, usine complète comprise (les 5 postes en sont une partie)
        Eq("0", IdleLabelPlacement.Collisions().ToString(), "étiquettes : aucune collision");
        var boxA = new IdleLabelPlacement.Box { position = Vector3.zero, width = 1f, height = 0.34f };
        var boxB = new IdleLabelPlacement.Box { position = Vector3.zero, width = 1f, height = 0.34f };
        Eq("True", IdleLabelPlacement.Touch(boxA, boxB).ToString(), "étiquettes : deux au même endroit se touchent");
        float[] moved = IdleLabelPlacement.Resolve(new[] { boxA, boxB }, new IdleLabelPlacement.Box[0]);
        Eq("True 0", (moved[1] > moved[0]) + " " + F(moved[0]), "étiquettes : la seconde monte, la première ne bouge pas");
        Eq("0", F((moved[1] / IdleLabelPlacement.Step) % 1), "étiquettes : par pas de 0,3 case");

        // prix des plaques : il tient dans la pastille, sans jamais devenir illisible
        Eq("130 117 104 104", IdlePadView.FitSize(130, 0.8f, 0.9f, 0.8f) + " " + IdlePadView.FitSize(130, 1f, 0.9f, 0.8f) + " " + IdlePadView.FitSize(130, 1.125f, 0.9f, 0.8f) + " " + IdlePadView.FitSize(130, 3f, 0.9f, 0.8f), "prix : réduit seulement s'il dépasse, jamais sous 80 %");

        // le jus (UW-4)
        float padGap = float.MaxValue;
        for (int st2 = 0; st2 < 6; st2++) padGap = Mathf.Min(padGap, Vector2.Distance(IdleLayout.PadSpeed[st2], IdleLayout.PadAdd[st2]) - IdleLayout.PadSize);
        Eq("True", (padGap >= 0.06f).ToString(), "plaques d'une paire : au moins 0,06 case de vide");
        Eq("1 2 8 8 0", IdlePurchaseSound.NextSteps(0, 0.5f) + " " + IdlePurchaseSound.NextSteps(1, 1.1f) + " " + IdlePurchaseSound.NextSteps(7, 0.1f) + " " + IdlePurchaseSound.NextSteps(8, 0.1f) + " " + IdlePurchaseSound.NextSteps(5, 1.3f), "son d'achat : +1 demi-ton, plafond 8, retour à 0 après 1,2 s");
        Eq("1,00 2,00", Fr(IdlePurchaseSound.Pitch(0)) + " " + Fr(IdlePurchaseSound.Pitch(12)), "hauteur du son : 12 demi-tons = une octave");
        Eq("0,00 0,08 0,00 0,00", Fr(IdleSlowMo.PushAt(0f, 0.6f, 0.08f)) + " " + Fr(IdleSlowMo.PushAt(0.3f, 0.6f, 0.08f)) + " " + Fr(IdleSlowMo.PushAt(0.6f, 0.6f, 0.08f)) + " " + Fr(IdleSlowMo.PushAt(1f, 0.6f, 0.08f)), "ralenti : la caméra pousse puis revient");
        Vector2 cvA = Vector2.zero, cvC = new Vector2(1f, 0f), cvB = new Vector2(1f, 1f);
        Eq("(0.0, 0.0) (0.8, 0.3) (1.0, 1.0)", Inv(IdleBills.Curve(cvA, cvC, cvB, 0f)) + " " + Inv(IdleBills.Curve(cvA, cvC, cvB, 0.5f)) + " " + Inv(IdleBills.Curve(cvA, cvC, cvB, 1f)), "billets : la courbe part du poste et arrive à la pastille");

        // main de tutoriel
        // main de tutoriel
        Eq("True False False False", IdleTutorialHand.Wanted(0, false, true) + " " + IdleTutorialHand.Wanted(0, true, true) + " " + IdleTutorialHand.Wanted(0, false, false) + " " + IdleTutorialHand.Wanted(12, false, true), "main : abordable, sans fenêtre, 12 premiers achats");
        Eq("True False", IdleTutorialHand.Wanted(11, false, true) + " " + IdleTutorialHand.Wanted(13, false, true), "main : jusqu'au 12e achat");
        Eq("True False False", IdleTutorialHand.PadOnScreen(0.5f, 0.5f) + " " + IdleTutorialHand.PadOnScreen(0.5f, 0.1f) + " " + IdleTutorialHand.PadOnScreen(0.98f, 0.5f), "main : sur la plaque si elle est visible, sinon sur le bandeau");

        // états des lignes du tiroir
        var rg = new IdleGame(cfg, 6, 0);
        Eq("Open Open Next Far Far", IdleStationRow.StateOf(rg, 0) + " " + IdleStationRow.StateOf(rg, 1) + " " + IdleStationRow.StateOf(rg, 2) + " " + IdleStationRow.StateOf(rg, 3) + " " + IdleStationRow.StateOf(rg, 4), "lignes au départ");
        Ready(rg); rg.Cash = 1e9; rg.BuyUnlock(2);
        Eq("Open Open Open Next", IdleStationRow.StateOf(rg, 0) + " " + IdleStationRow.StateOf(rg, 1) + " " + IdleStationRow.StateOf(rg, 2) + " " + IdleStationRow.StateOf(rg, 3), "lignes après un déblocage");

        // gain affiché à chaque cycle visuel
        var cg = new IdleGame(cfg, 6, 0);
        Eq("0", F(cg.CycleGain(0)), "la serre ne vend rien");
        Eq("12", F(cg.CycleGain(1)), "Assemblage au départ : 12 $ par cycle");
        cg.Stations[1].speed = 100; cg.Recalc();
        Eq("True", (System.Math.Abs(cg.CycleGain(1) * cg.VisualFrequency(1) - cg.StationIncome(1) * cg.BoostMultiplier) < 1e-9).ToString(), "gain par cycle x fréquence = revenu (même plafonné)");
        cg.TryTurbo();
        Eq("True", (System.Math.Abs(cg.CycleGain(1) * cg.VisualFrequency(1) - cg.StationIncome(1) * cg.BoostMultiplier) < 1e-9).ToString(), "turbo compris");

        // camionnette de l'Expédition
        Eq("0.00 0.00 0.65 -2.20 0.00 0.00", F(ShipMachine.VanOffset(0.5f, 2.6f, 2.2f), "0.00") + " " + F(ShipMachine.VanOffset(0.72f, 2.6f, 2.2f), "0.00") + " " + F(ShipMachine.VanOffset(0.8f, 2.6f, 2.2f), "0.00")
           + " " + F(ShipMachine.VanOffset(0.88f, 2.6f, 2.2f), "0.00") + " " + F(ShipMachine.VanOffset(1.0f, 2.6f, 2.2f), "0.00") + " " + F(ShipMachine.VanOffset(0f, 2.6f, 2.2f), "0.00"), "camionnette : à l'arrêt, part, arrive, s'arrête");
        Eq("True", (ShipMachine.VanOffset(0.95f, 2.6f, 2.2f) < 0f && ShipMachine.VanOffset(0.95f, 2.6f, 2.2f) > -2.2f).ToString(), "camionnette : arrive en ralentissant");

        // ---------------------------------------------------------------- caméra de la scène (U2a)
        foreach (float aspect in new[] { 390f / 844f, 0.56f, 1.5f })
        {
            Vector3 camPos; Quaternion camRot;
            Vector3 slabMin = new Vector3(0f, -0.3f, 0f), slabSize = new Vector3(IdleLayout.FloorDepth, 0.3f, IdleLayout.FloorWidth);
            float vy = aspect < 0.8f ? 0.72f : 0.5f;
            IdleCameraRig.Compute(aspect, 30f, 55f, 225f, slabMin, slabSize, 0.96f, vy, out camPos, out camRot);
            Matrix4x4 clip = Matrix4x4.Perspective(30f, aspect, 0.3f, 400f) * (Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camPos, camRot, Vector3.one).inverse);
            float minNx = 9f, maxNx = -9f, minNy = 9f, maxNy = -9f;
            for (int ci = 0; ci < 8; ci++)
            {
                Vector3 corner = slabMin + new Vector3((ci & 1) != 0 ? slabSize.x : 0f, (ci & 2) != 0 ? slabSize.y : 0f, (ci & 4) != 0 ? slabSize.z : 0f);
                Vector4 pp = clip * new Vector4(corner.x, corner.y, corner.z, 1f);
                minNx = Mathf.Min(minNx, pp.x / pp.w); maxNx = Mathf.Max(maxNx, pp.x / pp.w); minNy = Mathf.Min(minNy, pp.y / pp.w); maxNy = Mathf.Max(maxNy, pp.y / pp.w);
            }
            Eq("True", (minNx >= -1f && maxNx <= 1f && minNy >= -1f && maxNy <= 1f).ToString(), "caméra (L/H " + F(aspect, "0.00") + ") : la dalle tient dans l'écran");
            if (aspect < 0.8f) Eq("True", (maxNx - minNx > 1.8f).ToString(), "caméra portrait : la dalle remplit la largeur (" + F((maxNx - minNx) / 2f, "0.00") + ")");
            Vector4 ctr = clip * new Vector4(slabSize.x * 0.5f, 0f, slabSize.z * 0.5f, 1f);
            float vyOut = (ctr.y / ctr.w + 1f) * 0.5f;
            Eq("True", (Mathf.Abs(vyOut - vy) < 0.04f).ToString(), "caméra : centre de la dalle à la hauteur " + vy + " (obtenu " + F(vyOut, "0.00") + ")");
        }
        Eq("(2.40, 0.00, 1.55)", new Vector3(IdleLayout.ToWorld(1.55f, 2.4f).x, 0f, IdleLayout.ToWorld(1.55f, 2.4f).z).ToString("F2", inv), "repère : x du web -> Z, y du web -> X");
        Eq("6", IdleLayout.Stations.Length + "", "6 postes sur le plan");

        // ---------------------------------------------------------------- utilitaires conservés
        Eq("fallback", Loc.Or("cle.inexistante", "fallback"), "Loc.Or : texte de repli");
        {   // option « Moins de secousses »
            bool before = MotionSettings.ReduceMotion;
            MotionSettings.ReduceMotion = false;
            Eq("0.08", MotionSettings.ShakeAmplitude(0.08f).ToString("0.00", inv), "secousse normale");
            Eq("True", MotionSettings.AllowCameraGlide.ToString(), "glissement permis");
            MotionSettings.ReduceMotion = true;
            Eq("0.00", MotionSettings.ShakeAmplitude(0.08f).ToString("0.00", inv), "secousse coupée");
            Eq("False", MotionSettings.AllowCameraGlide.ToString(), "glissement coupé");
            Eq("1", PlayerPrefs.GetInt("reduce_motion", 0).ToString(), "option enregistrée");
            MotionSettings.ReduceMotion = false;
            Eq("False", MotionSettings.ReduceMotion.ToString(), "le cache suit le réglage");
            MotionSettings.ReduceMotion = before;
        }
        Eq("21.0", AccessibilityAudit.Contrast(Color.white, Color.black).ToString("0.0", inv), "contraste blanc / noir");
        Eq("1.0", AccessibilityAudit.Contrast(Color.gray, Color.gray).ToString("0.0", inv), "contraste d'une couleur avec elle-même");
        {   // états d'un bouton (PressScale)
            var go = new GameObject("TestButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(PressScale));
            var btn = go.GetComponent<UnityEngine.UI.Button>(); var ps = go.GetComponent<PressScale>();
            btn.interactable = false; ps.OnPointerDown(null);
            Eq("False", ps.IsPressed.ToString(), "bouton désactivé : pas d'appui");
            btn.interactable = true; ps.Silent = true; ps.OnPointerDown(null);
            Eq("True", ps.IsPressed.ToString(), "bouton actif : appui pris en compte");
            ps.OnPointerUp(null);
            Eq("False", ps.IsPressed.ToString(), "relâché : plus d'appui");
            Object.DestroyImmediate(go);
        }
        Object.DestroyImmediate(cfg);

        if (failures.Count == 0)
            Debug.Log("Tests unitaires : " + count + " vérifications, toutes réussies.");
        else
            Debug.LogError("Tests unitaires : " + failures.Count + " échec(s) sur " + count + "\n" + string.Join("\n", failures));
    }

    static string Inv(Vector2 v) { return "(" + v.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", " + v.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")"; }

    static string Fr(float value) { return value.ToString("0.00", new System.Globalization.CultureInfo("fr-FR")); }

    // Écran assez large pour que le décalage vers la gauche soit entier.
    static float IdleFramingViewX(IdleEconomyConfig cfg, int extraOpen)
    {
        var game = new IdleGame(cfg, 2, 0);
        for (int i = 1; i <= extraOpen; i++) game.Stations[i].unlocked = true;
        return IdleFraming.Default(game, 20f, 40f, 55f, 0.18f).viewportX;
    }

    // Deux plaques à 0,85 m l'une de l'autre, zone de toucher 0,7 m de chaque côté.
    static int IdleFramingPick(float x)
    {
        Vector3 ground = new Vector3(x, 0f, 0f), a = Vector3.zero, b = new Vector3(0.85f, 0f, 0f);
        float da = IdlePadField.PadDistance(a, ground), db = IdlePadField.PadDistance(b, ground), limit = IdleLayout.PadHitSize * 0.5f;
        if (da > limit && db > limit) return -1;
        return da <= db ? 0 : 1;
    }

}
