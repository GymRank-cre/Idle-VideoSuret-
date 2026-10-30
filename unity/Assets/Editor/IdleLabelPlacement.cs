using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Place les étiquettes de la scène idle : aucune ne recouvre une autre étiquette ni une plaque ; elle monte par pas de 0,3 case jusqu'à ne plus rien toucher.
// La caméra ne tourne jamais : ce qui ne se touche pas vu d'ici ne se touchera pas à l'écran, quel que soit le zoom. Le calcul se fait une fois, à la construction de la scène.
public static class IdleLabelPlacement
{
    public const float Step = 0.3f;
    const float Gap = 0.03f, LabelHeight = 0.34f, PadWidth = 1.05f, PadHeight = 0.75f, PadLift = 0.12f;
    const float StockBase = 0.32f, StockWidth = 1.44f;
    const int MaxSteps = 12, WidestLevel = 3;   // niveau à 3 chiffres : l'étiquette la plus large

    static readonly float[] StationBase = { 1.7f, 1.6f, 1.6f, 1.75f, 2.1f, 1.6f };
    static readonly Quaternion View = Quaternion.Euler(IdleCameraRig.Pitch, IdleCameraRig.Yaw, 0f);

    static float[] stationHeights, stockHeights;

    public struct Box
    {
        public Vector3 position;
        public float width, height;
    }

    // Se touchent-elles, vues de la caméra ?
    public static bool Touch(Box a, Box b)
    {
        Vector3 right = View * Vector3.right, up = View * Vector3.up;
        Vector3 d = a.position - b.position;
        return Mathf.Abs(Vector3.Dot(d, right)) < (a.width + b.width) * 0.5f + Gap && Mathf.Abs(Vector3.Dot(d, up)) < (a.height + b.height) * 0.5f + Gap;
    }

    // Monte chaque étiquette, dans l'ordre, jusqu'à ne toucher ni un obstacle ni une étiquette déjà placée ; renvoie les hauteurs finales.
    public static float[] Resolve(IList<Box> labels, IList<Box> obstacles)
    {
        var placed = new List<Box>(obstacles);
        var heights = new float[labels.Count];
        for (int i = 0; i < labels.Count; i++)
        {
            Box label = labels[i];
            for (int step = 0; step < MaxSteps && Touches(label, placed); step++) label.position += Vector3.up * Step;
            heights[i] = label.position.y;
            placed.Add(label);
        }
        return heights;
    }

    static bool Touches(Box label, List<Box> others)
    {
        foreach (Box other in others) if (Touch(label, other)) return true;
        return false;
    }

    static Box Pad(Vector2 plan)
    {
        return new Box { position = IdleLayout.ToWorld(plan) + View * Vector3.up * PadLift, width = PadWidth, height = PadHeight };
    }

    // Les plaques qui restent à l'écran tant que leur poste est ouvert. Celles des emplacements vides s'en vont avec l'achat : elles passent sous les étiquettes.
    public static List<Box> Pads()
    {
        var pads = new List<Box> { Pad(IdleLayout.PadHire[0]) };
        for (int i = 0; i < IdleLayout.Slots.Length; i++)
        {
            pads.Add(Pad(IdleLayout.PadSpeed[i]));
            pads.Add(Pad(IdleLayout.PadAdd[i]));
        }
        return pads;
    }

    // Toutes les étiquettes à leur place de départ : celles des postes d'abord, elles passent avant celles des stocks.
    public static List<Box> Labels()
    {
        var labels = new List<Box>();
        var config = AssetDatabase.LoadAssetAtPath<IdleEconomyConfig>("Assets/Prototype/IdleEconomyConfig.asset");
        for (int i = 0; i < IdleLayout.Stations.Length; i++)
        {
            int length = Loc.T("label.level", Loc.T(config.stations[i].nameKey), 0).Length + WidestLevel - 1;
            Vector3 at = IdleLayout.StationCenter(i) + Vector3.up * StationBase[i];
            labels.Add(new Box { position = at, width = 0.34f + 0.142f * length, height = LabelHeight });
        }
        for (int i = 1; i < IdleLayout.Stocks.Length; i++)
            labels.Add(new Box { position = IdleLayout.ToWorld(IdleLayout.Stocks[i], StockBase), width = StockWidth, height = LabelHeight });
        return labels;
    }

    static void Compute()
    {
        if (stationHeights != null) return;
        float[] heights = Resolve(Labels(), Pads());
        int stations = IdleLayout.Stations.Length;
        stationHeights = new float[stations];
        stockHeights = new float[heights.Length - stations];
        System.Array.Copy(heights, stationHeights, stations);
        System.Array.Copy(heights, stations, stockHeights, 0, stockHeights.Length);
    }

    public static float StationHeight(int station) { Compute(); return stationHeights[station]; }

    // `station` : le poste dont c'est le stock (à partir de 1).
    public static float StockHeight(int station) { Compute(); return stockHeights[station - 1]; }

    // Paires qui se touchent encore une fois tout placé.
    public static int Collisions()
    {
        List<Box> labels = Labels(), pads = Pads();
        float[] heights = Resolve(labels, pads);
        var placed = new List<Box>();
        for (int i = 0; i < labels.Count; i++)
        {
            Box label = labels[i];
            label.position.y = heights[i];
            placed.Add(label);
        }
        int count = 0;
        for (int i = 0; i < placed.Count; i++)
        {
            foreach (Box pad in pads) if (Touch(placed[i], pad)) count++;
            for (int k = i + 1; k < placed.Count; k++) if (Touch(placed[i], placed[k])) count++;
        }
        return count;
    }
}
