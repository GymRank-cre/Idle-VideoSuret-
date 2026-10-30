using UnityEngine;

// Géométrie de la scène idle (U2) : mêmes emplacements que `LAY`, `PLATE` et `BELTS` de la référence web (usine-web/src/template.html).
// Le plan (x, y) du web devient (Z, X) dans Unity : X = y du web, Z = x du web ; le dessus de la dalle est à y = 0.
public static class IdleLayout
{
    public const float FloorWidth = 7.8f;     // web x -> Z
    public const float FloorDepth = 6.6f;     // web y -> X
    public const float SlabThickness = 0.3f;
    public const float StationSize = 1.6f;

    // Coin « arrière » de l'empreinte de chaque poste (x, y du web) : Réception, Assemblage, Test, Emballage, Expédition.
    public static readonly Vector2[] Stations =
    {
        new Vector2(0.55f, 0.55f), new Vector2(3.2f, 0.55f), new Vector2(4.9f, 2.7f), new Vector2(3.2f, 4.4f), new Vector2(0.55f, 4.4f),
    };

    // Plaques d'achat au sol (x, y du web) : une par poste.
    public static readonly Vector2[] Plates =
    {
        new Vector2(1.35f, 3.3f), new Vector2(4.0f, 3.2f), new Vector2(7.05f, 3.5f), new Vector2(5.95f, 5.7f), new Vector2(1.35f, 6.95f),
    };

    // 4 tapis (suites de points x, y du web) entre les postes.
    public static readonly Vector2[][] Belts =
    {
        new[] { new Vector2(2.15f, 1.35f), new Vector2(3.2f, 1.35f) },
        new[] { new Vector2(4.8f, 1.35f), new Vector2(5.7f, 1.35f), new Vector2(5.7f, 2.7f) },
        new[] { new Vector2(5.7f, 4.3f), new Vector2(5.7f, 5.2f), new Vector2(4.8f, 5.2f) },
        new[] { new Vector2(3.2f, 5.2f), new Vector2(2.15f, 5.2f) },
    };

    // Point de la scène à partir du plan du web (x, y) et d'une hauteur.
    public static Vector3 ToWorld(float webX, float webY, float height = 0f)
    {
        return new Vector3(webY, height, webX);
    }

    public static Vector3 ToWorld(Vector2 web, float height = 0f)
    {
        return ToWorld(web.x, web.y, height);
    }

    // Centre de l'empreinte d'un poste, au sol.
    public static Vector3 StationCenter(int i)
    {
        Vector2 s = Stations[i];
        return ToWorld(s.x + StationSize * 0.5f, s.y + StationSize * 0.5f);
    }
}
