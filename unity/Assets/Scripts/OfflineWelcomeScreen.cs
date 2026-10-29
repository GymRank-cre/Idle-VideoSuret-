using System;
using UnityEngine;
using UnityEngine.UI;

// Écran « Bon retour ! » : durée d'absence, gain, bouton Récupérer. Le bouton ×2 est un emplacement grisé
// (une vidéo récompensée viendra plus tard, aucune publicité n'est ajoutée).
public class OfflineWelcomeScreen : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] Text durationText;
    [SerializeField] Text amountText;
    [SerializeField] Button claimButton;

    Action onClaim;

    void Awake()
    {
        root.SetActive(false);
        claimButton.onClick.AddListener(Claim);
    }

    public void Show(long seconds, int amount, Action claimed)
    {
        onClaim = claimed;
        durationText.text = "Absent depuis " + FormatDuration(seconds);
        amountText.text = "+$ " + amount;
        root.SetActive(true);
    }

    void Claim()
    {
        root.SetActive(false);
        AudioManager.Play(SfxId.Click);
        onClaim?.Invoke();
    }

    public static string FormatDuration(long seconds)
    {
        long minutes = seconds / 60;
        if (minutes < 60) return Math.Max(1, minutes) + " min";
        return (minutes / 60) + " h " + (minutes % 60).ToString("00") + " min";
    }
}
