// Son d'un achat, le même partout (plaques au sol, boutons du tiroir, bandeau Meilleur achat).
public static class IdlePurchaseSound
{
    public static bool Bought(IdleGame.PurchaseResult result)
    {
        return result == IdleGame.PurchaseResult.Ok || result == IdleGame.PurchaseResult.Milestone;
    }

    public static void Play(IdleGame.PurchaseResult result, bool unlock)
    {
        if (result == IdleGame.PurchaseResult.TooExpensive) AudioManager.Play(SfxId.Refuse);
        else if (result == IdleGame.PurchaseResult.Milestone || (result == IdleGame.PurchaseResult.Ok && unlock)) AudioManager.Play(SfxId.Unlock);
        else if (result == IdleGame.PurchaseResult.Ok) AudioManager.Play(SfxId.Upgrade);
    }
}
