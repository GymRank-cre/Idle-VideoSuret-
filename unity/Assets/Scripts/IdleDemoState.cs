#if IDLE_DEMO
using UnityEngine;

// Build de mesure, jamais publié (symbole IDLE_DEMO) : l'usine complète dès la première seconde, pour mesurer le rendu des 12 machines dans un vrai navigateur.
// La caméra montre toute l'usine, puis une rangée de près.
public class IdleDemoState : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        new GameObject("Demo").AddComponent<IdleDemoState>();
    }

    System.Collections.IEnumerator Start()
    {
        yield return null;
        SaveSystem.Suspended = true;
        IdleGame g = IdleHost.Instance.Game;
        g.NewGame();
        g.Crew = g.Eco.Config.hirePrices.Length;
        g.Recalc();
        g.Cash = 1e15;
        g.Buys = 50;
        for (int i = 2; i < g.Stations.Count; i++) g.BuyUnlock(i);
        for (int i = 0; i < g.Stations.Count; i++)
        {
            g.Stations[i].speed = 40 + i;
            g.Stations[i].add = 30 + i;
            g.Stations[i].slots = g.Eco.Config.stations[i].slots;
        }
        g.Recalc();
        g.Cash = 5e9;
        IdleCameraRig rig = FindFirstObjectByType<IdleCameraRig>();
        rig.FrameAll(true);
        yield return new WaitForSecondsRealtime(25f);
        rig.FrameStation(3);
        rig.SetZoom(1.3f);
    }
}
#endif
