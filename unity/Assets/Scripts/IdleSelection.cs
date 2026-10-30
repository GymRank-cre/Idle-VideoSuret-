using UnityEngine;

// Poste sélectionné (UL) : toucher une machine ou un fantôme pose un anneau jaune au sol, centre la caméra et fait monter sa fiche en bas de l'écran.
// La fiche est la ligne du poste, empruntée à la feuille « Postes » ; elle y retourne à la fermeture, comme dans le web.
public class IdleSelection : MonoBehaviour
{
    [SerializeField] IdleCameraRig rig;
    [SerializeField] IdleDock dock;
    [SerializeField] Transform ring;
    [SerializeField] float ringHeight = 0.13f;   // au-dessus de l'embase des machines
    [SerializeField] RectTransform fiche;
    [SerializeField] IdleStationRow[] rows;

    int selected = -1;
    Transform homeParent;
    Vector2 homePosition;
    int homeIndex;

    public int Selected => selected;

    // Toucher le poste déjà sélectionné referme sa fiche.
    public static int Next(int current, int tapped) { return current == tapped ? -1 : tapped; }

    void Start()
    {
        ring.gameObject.SetActive(false);
    }

    public void Tap(int station)
    {
        int next = Next(selected, station);
        Deselect();
        if (next >= 0) Select(next);
        AudioManager.Play(SfxId.Click);
    }

    void Select(int station)
    {
        selected = station;
        var row = (RectTransform)rows[station].transform;
        homeParent = row.parent;
        homePosition = row.anchoredPosition;
        homeIndex = row.GetSiblingIndex();
        row.SetParent(fiche, false);
        row.anchoredPosition = new Vector2(homePosition.x, 0f);

        ring.position = IdleLayout.StationCenter(station) + Vector3.up * ringHeight;
        ring.gameObject.SetActive(true);
        rig.FrameStation(station);
        dock.ShowFiche(true);
    }

    public void Deselect()
    {
        if (selected < 0) return;
        var row = (RectTransform)rows[selected].transform;
        row.SetParent(homeParent, false);
        row.SetSiblingIndex(homeIndex);
        row.anchoredPosition = homePosition;
        selected = -1;
        ring.gameObject.SetActive(false);
        dock.ShowFiche(false);
    }
}
