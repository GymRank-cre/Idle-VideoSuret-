using UnityEngine;

// Cadrages de la caméra libre (UL), mêmes règles que le web. Un point du sol s'écrit (a, b) : a = x - y (vers la droite de l'écran), b = x + y (vers le bas),
// x et y étant le plan du web. Calculs purs, testés ; c'est IdleCameraRig qui les applique.
public static class IdleFraming
{
    public const float MinZoom = 0.55f, MaxZoom = 3f, DefaultMaxZoom = 2.6f, StationZoom = 2f;   // 0,55 : toute l'usine ; 1 : une rangée de machines
    public const float EdgeShare = 12f / 390f;   // au cadrage par défaut, rien à moins de 12 px du bord d'un écran de 390
    const float MinA = -13.5f, MaxA = 10f, MinB = 2.2f, MaxB = 25f;
    const float Margin = 1.8f, DownShift = 0.5f, StationDownShift = 1.3f;
    const float WidthShare = 0.94f, HeightShare = 0.56f, MachineHeight = 1f;
    static readonly float Root2 = Mathf.Sqrt(2f);

    public struct View
    {
        public Vector2 focus;
        public float zoom;
        public float viewportX;   // où se place le point visé, en largeur d'écran (0,5 = au milieu)
    }

    public static Vector2 ToPlan(Vector3 world) { return new Vector2(world.z - world.x, world.z + world.x); }

    public static Vector3 ToWorld(Vector2 plan) { return IdleLayout.ToWorld((plan.y + plan.x) * 0.5f, (plan.y - plan.x) * 0.5f); }

    public static Vector2 Clamp(Vector2 focus) { return new Vector2(Mathf.Clamp(focus.x, MinA, MaxA), Mathf.Clamp(focus.y, MinB, MaxB)); }

    public static float ClampZoom(float zoom) { return Mathf.Clamp(zoom, MinZoom, MaxZoom); }

    // Toute la dalle.
    public static View All()
    {
        Vector2 size = new Vector2(IdleLayout.FloorWidth, IdleLayout.FloorDepth);
        return new View { focus = new Vector2((size.x - size.y) * 0.5f, (size.x + size.y) * 0.5f + 0.6f), zoom = MinZoom, viewportX = 0.5f };
    }

    // Un poste, avec la place pour sa fiche en bas ; on ne dézoome jamais.
    public static View Station(int station, float currentZoom)
    {
        Vector2 s = IdleLayout.Stations[station];
        float cx = s.x + IdleLayout.StationSize * 0.5f, cy = s.y + IdleLayout.StationSize * 0.5f;
        return new View { focus = new Vector2(cx - cy, cx + cy + StationDownShift), zoom = Mathf.Max(currentZoom, StationZoom), viewportX = 0.5f };
    }

    // Ce qu'on regarde : la dernière rangée ouverte, ses machines, son stock, ses plaques et la plaque de ce qui vient après ; au début, la serre et la première machine.
    // Le cadre tient dans la zone libre : la colonne de boutons de droite (`rightColumn`, en part de la largeur) est exclue, et il se décale vers la gauche tant qu'il y a la place.
    // `visibleWidth` et `visibleHeight` : ce que voit la caméra au zoom 1, en mètres.
    public static View Default(IdleGame game, float visibleWidth, float visibleHeight, float pitch, float rightColumn)
    {
        float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
        void Add(Vector2 p)
        {
            float a = p.x - p.y, b = p.x + p.y;
            minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
            minB = Mathf.Min(minB, b); maxB = Mathf.Max(maxB, b);
        }
        void AddMachine(int station, int slot)
        {
            Vector2 corner = IdleLayout.Slots[station][slot];
            float size = IdleLayout.StationSize;
            Add(corner); Add(corner + new Vector2(size, size)); Add(corner + new Vector2(size, 0f)); Add(corner + new Vector2(0f, size));
        }

        int last = IdleEconomy.SerreStation;
        for (int i = 0; i < game.Stations.Count; i++) if (game.Stations[i].unlocked) last = i;

        if (last <= 1)
        {
            AddMachine(IdleEconomy.SerreStation, 0);
            if (game.CrewReady) { Add(IdleLayout.PadSpeed[0]); Add(IdleLayout.PadAdd[0]); }
            else foreach (Vector2 pad in IdleLayout.PadHire) Add(pad);
        }
        for (int k = 0; k < game.Stations[last].slots; k++) AddMachine(last, k);
        Add(IdleLayout.Stocks[last]);
        if (game.CrewReady)
        {
            Add(IdleLayout.PadSpeed[last]);
            Add(IdleLayout.PadAdd[last]);
            int next = game.NextLockedStation();
            if (next >= 0) Add(IdleLayout.PadPosition(next, IdleGame.PadKind.Unlock));
        }

        float needWidth = (maxA - minA + Margin) / Root2;
        float needHeight = (maxB - minB + Margin) / Root2 * Mathf.Sin(pitch * Mathf.Deg2Rad) + MachineHeight;
        float zoom = Mathf.Min(visibleWidth * WidthShare * (1f - rightColumn) / needWidth, visibleHeight * HeightShare / needHeight);
        zoom = Mathf.Clamp(zoom, MinZoom, DefaultMaxZoom);

        float shift = Mathf.Clamp((1f - needWidth * zoom / visibleWidth) * 0.5f - EdgeShare, 0f, rightColumn * 0.5f);
        return new View { focus = new Vector2((minA + maxA) * 0.5f, (minB + maxB) * 0.5f + DownShift), zoom = zoom, viewportX = 0.5f - shift };
    }
}
