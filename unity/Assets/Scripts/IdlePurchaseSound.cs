using UnityEngine;

// Son d'un achat, le même partout (plaques au sol, boutons, bandeau Meilleur achat). Des achats enchaînés montent d'un demi-ton à chaque fois.
public static class IdlePurchaseSound
{
    public const int MaxSteps = 8;
    public const float ChainSeconds = 1.2f;

    static int steps;
    static float lastBuy = -10f;

    // Demi-tons de cet achat, d'après ceux du précédent et le temps écoulé.
    public static int NextSteps(int previous, float elapsed) { return elapsed < ChainSeconds ? Mathf.Min(previous + 1, MaxSteps) : 0; }

    public static float Pitch(int semitones) { return Mathf.Pow(2f, semitones / 12f); }

    public static bool Bought(IdleGame.PurchaseResult result)
    {
        return result == IdleGame.PurchaseResult.Ok || result == IdleGame.PurchaseResult.Milestone;
    }

    public static void Play(IdleGame.PurchaseResult result, bool unlock)
    {
        if (result == IdleGame.PurchaseResult.TooExpensive) { AudioManager.Play(SfxId.Refuse); return; }
        if (!Bought(result)) return;
        float now = Time.unscaledTime;
        steps = NextSteps(steps, now - lastBuy);
        lastBuy = now;
        bool big = result == IdleGame.PurchaseResult.Milestone || unlock;
        AudioManager.Play(big ? SfxId.Unlock : SfxId.Upgrade, Pitch(steps));
    }
}
