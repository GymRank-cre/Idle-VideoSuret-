using UnityEngine;

// Messages des moments forts du jeu (U3d) : poste débloqué et palier ×2.
public class IdleMilestoneToasts : MonoBehaviour
{
    [SerializeField] IdleToasts toasts;

    IdleGame subscribed;

    void Start()
    {
        subscribed = IdleHost.Instance.Game;
        subscribed.OnUnlocked += Unlocked;
        subscribed.OnMilestone += Milestone;
    }

    void OnDestroy()
    {
        if (subscribed == null) return;
        subscribed.OnUnlocked -= Unlocked;
        subscribed.OnMilestone -= Milestone;
    }

    string StationName(int station) { return Loc.T(subscribed.Eco.Config.stations[station].nameKey); }

    void Unlocked(int station) { toasts.Show(Loc.T("toast.unlocked", StationName(station)), IdleToasts.Icon.Lock); }
    void Milestone(int station) { toasts.Show(Loc.T("toast.milestone", StationName(station))); }
}
