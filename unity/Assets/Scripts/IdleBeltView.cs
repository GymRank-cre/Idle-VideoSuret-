using UnityEngine;

// Les 4 tapis de la scène idle (U2b) : un tapis apparaît quand le poste qu'il alimente est ouvert ; ses chevrons défilent et ses caisses
// avancent le long de son tracé (`IdleLayout.Belts`). Visuel seulement : aucune logique de production ici.
public class IdleBeltView : MonoBehaviour
{
    [SerializeField] Transform[] belts;          // Belt_0..3 : le tapis i est visible quand le poste i+1 est ouvert
    [SerializeField] Renderer[] chevronTops;     // dessus à chevrons de chaque tapis (texture qui défile)
    [SerializeField, Min(0.01f)] float itemSpeed = 0.55f;       // mètres par seconde
    [SerializeField, Min(0.01f)] float chevronPeriod = 0.45f;   // mètres par motif de chevron
    [SerializeField, Min(0.01f)] float chevronSpeed = 0.9f;
    [SerializeField] float itemHeight = 0.16f;

    Vector3[][] paths;
    float[] lengths;
    Transform[][] items;
    bool[] shown;
    MaterialPropertyBlock block;
    float time;
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    void Awake()
    {
        int n = belts.Length;
        paths = new Vector3[n][];
        lengths = new float[n];
        items = new Transform[n][];
        shown = new bool[n];
        block = new MaterialPropertyBlock();
        for (int i = 0; i < n; i++)
        {
            Vector2[] web = IdleLayout.Belts[i];
            paths[i] = new Vector3[web.Length];
            for (int k = 0; k < web.Length; k++)
            {
                paths[i][k] = IdleLayout.ToWorld(web[k], itemHeight);
                if (k > 0) lengths[i] += Vector3.Distance(paths[i][k - 1], paths[i][k]);
            }
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in belts[i]) if (c.name.StartsWith("Item")) list.Add(c);
            items[i] = list.ToArray();
            belts[i].gameObject.SetActive(false);
        }
    }

    void Update()
    {
        IdleHost host = IdleHost.Instance;
        if (host == null || host.Game == null) return;
        time += Time.deltaTime;
        for (int i = 0; i < belts.Length; i++)
        {
            bool on = i + 1 < host.Game.Stations.Count && host.Game.Stations[i + 1].unlocked;
            if (on != shown[i]) { shown[i] = on; belts[i].gameObject.SetActive(on); }
            if (!on) continue;

            // chevrons : la texture défile dans le sens de la marche
            if (chevronTops != null && i < chevronTops.Length && chevronTops[i] != null)
            {
                block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, -time * chevronSpeed / chevronPeriod));
                chevronTops[i].SetPropertyBlock(block);
            }

            // caisses : régulièrement espacées, elles avancent en boucle
            int n = items[i].Length;
            if (n == 0 || lengths[i] <= 0f) continue;
            float step = lengths[i] / n, offset = (time * itemSpeed) % step;
            for (int k = 0; k < n; k++) items[i][k].position = PointAt(i, offset + k * step);
        }
    }

    Vector3 PointAt(int belt, float distance)
    {
        Vector3[] p = paths[belt];
        float acc = 0f;
        for (int s = 1; s < p.Length; s++)
        {
            float len = Vector3.Distance(p[s - 1], p[s]);
            if (distance <= acc + len) return Vector3.Lerp(p[s - 1], p[s], (distance - acc) / Mathf.Max(0.0001f, len));
            acc += len;
        }
        return p[p.Length - 1];
    }
}
