using UnityEngine;

// Étiquette d'un poste : « Nom  Niv N » sur une pastille sombre, qui s'élargit avec le nombre de chiffres du niveau (un maillage prêt par longueur).
public class IdleStationLabel : MonoBehaviour
{
    [SerializeField] int station;
    [SerializeField] string stationName;
    [SerializeField] TextMesh text;
    [SerializeField] MeshFilter pill;
    [SerializeField] Mesh[] pillByDigits;     // [0] = 1 chiffre, [1] = 2 chiffres...

    int shownLevel = -1;

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        IdleEconomy.StationLevels st = host.Game.Stations[station];
        int level = st.speed + st.add;
        if (level == shownLevel) return;
        shownLevel = level;
        text.text = Loc.T("label.level", stationName, level);
        int digits = level < 10 ? 1 : level < 100 ? 2 : 3;
        pill.sharedMesh = pillByDigits[Mathf.Min(digits, pillByDigits.Length) - 1];
    }
}
