using UnityEngine;

// Géométrie de la scène idle. Le plan (x, y) du web devient (Z, X) dans Unity : X = y du web, Z = x du web ; le dessus de la dalle est à y = 0.
// La chaîne serpente sur la dalle : la serre, 4 rangées de 3 emplacements (Assemblage, Programmation, Test, Emballage), puis l'Expédition. Chaque rangée a un tapis devant ses machines,
// un stock à son entrée et ses plaques sous le tapis ; un chariot porte ce qui en sort jusqu'au stock de la rangée suivante.
public static class IdleLayout
{
    public const float FloorWidth = 11.4f;    // web x -> Z
    public const float FloorDepth = 15.2f;    // web y -> X
    public const float SlabThickness = 0.3f;
    public const float StationSize = 1.6f;

    // Coin « arrière » de chaque emplacement, le premier à s'ouvrir d'abord, celui le plus près du stock.
    public static readonly Vector2[][] Slots =
    {
        new[] { new Vector2(0.55f, 0.55f) },
        new[] { new Vector2(3.2f, 0.55f), new Vector2(5.5f, 0.55f), new Vector2(7.8f, 0.55f) },
        new[] { new Vector2(7.8f, 4.1f), new Vector2(5.5f, 4.1f), new Vector2(3.2f, 4.1f) },
        new[] { new Vector2(3.2f, 7.65f), new Vector2(5.5f, 7.65f), new Vector2(7.8f, 7.65f) },
        new[] { new Vector2(7.8f, 11.2f), new Vector2(5.5f, 11.2f), new Vector2(3.2f, 11.2f) },
        new[] { new Vector2(0.55f, 11.2f) },
    };

    public static readonly Vector2[] Stations = System.Array.ConvertAll(Slots, s => s[0]);

    // Stock à l'entrée de chaque poste (la serre n'en a pas).
    public static readonly Vector2[] Stocks =
    {
        Vector2.zero, new Vector2(2.72f, 2.08f), new Vector2(10.15f, 6f), new Vector2(2.45f, 9.55f), new Vector2(10.15f, 13.1f), new Vector2(1.35f, 13.7f),
    };

    // Plaques d'achat au sol (centres) : sous le tapis, au plus près de la première machine de la rangée.
    public const float PadSize = 0.9f;
    public const float PadHitSize = PadSize * 1.7f;   // zone de toucher, plus grande que la plaque
    public static readonly Vector2[] PadSpeed = { new Vector2(0.5f, 2.8f), new Vector2(3.75f, 3.3f), new Vector2(8f, 6.85f), new Vector2(3.75f, 10.4f), new Vector2(8f, 13.95f), new Vector2(0.85f, 14.65f) };
    public static readonly Vector2[] PadAdd = { new Vector2(1.55f, 2.8f), new Vector2(4.8f, 3.3f), new Vector2(9.05f, 6.85f), new Vector2(4.8f, 10.4f), new Vector2(9.05f, 13.95f), new Vector2(1.9f, 14.65f) };
    public static readonly Vector2[] PadHire = { new Vector2(1.5f, 2.85f), new Vector2(1.5f, 2.85f), new Vector2(1.5f, 2.85f) };   // un recrutement à la fois, toujours au même endroit

    // Début de la chaîne : du découpeur au stock, le long du bord avant de la serre.
    public static readonly Vector2 CutterSpot = new Vector2(0.95f, 1.8f), CutPile = new Vector2(1.32f, 1.98f), EtcherSpot = new Vector2(1.68f, 1.8f), RawPile = new Vector2(2.0f, 1.98f);

    // Un tapis par rangée de machines, de son stock à son bout.
    public static readonly Vector2[][] Belts =
    {
        new[] { new Vector2(3.3f, 2.45f), new Vector2(9.4f, 2.45f) },
        new[] { new Vector2(9.55f, 6f), new Vector2(3.2f, 6f) },
        new[] { new Vector2(3.05f, 9.55f), new Vector2(9.4f, 9.55f) },
        new[] { new Vector2(9.55f, 13.1f), new Vector2(3.2f, 13.1f) },
    };

    // Chariots : du bout d'un tapis au stock suivant (indice : le poste qui sort).
    public static readonly Vector2[] CartFrom = { Vector2.zero, new Vector2(9.95f, 2.45f), new Vector2(2.7f, 6f), new Vector2(9.95f, 9.55f), new Vector2(2.7f, 13.1f) };

    public static Vector2 SlotPlan(int station, int slot)
    {
        Vector2[] slots = Slots[station];
        Vector2 corner = slots[Mathf.Clamp(slot, 0, slots.Length - 1)];
        return corner + new Vector2(StationSize * 0.5f, StationSize * 0.5f);
    }

    // Plaque d'un poste ; une plaque Débloquer ou Emplacement se pose sur la place de la machine à venir.
    public static Vector2 PadPosition(int station, IdleGame.PadKind kind, int slot = 0)
    {
        if (kind == IdleGame.PadKind.Hire) return PadHire[station];
        if (kind == IdleGame.PadKind.Speed) return PadSpeed[station];
        if (kind == IdleGame.PadKind.Add) return PadAdd[station];
        return SlotPlan(station, kind == IdleGame.PadKind.Slot ? slot : 0);
    }

    // Point de la scène à partir du plan du web (x, y) et d'une hauteur.
    public static Vector3 ToWorld(float webX, float webY, float height = 0f)
    {
        return new Vector3(webY, height, webX);
    }

    public static Vector3 ToWorld(Vector2 web, float height = 0f)
    {
        return ToWorld(web.x, web.y, height);
    }

    public static Vector3 SlotCenter(int station, int slot) { return ToWorld(SlotPlan(station, slot)); }

    // Centre de l'empreinte du premier emplacement d'un poste, au sol.
    public static Vector3 StationCenter(int i) { return SlotCenter(i, 0); }
}
