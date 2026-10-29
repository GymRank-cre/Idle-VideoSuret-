using System.Collections.Generic;

// Table de chaînes (FR pour l'instant) : tout nouveau texte du jeu passe par ici, pour traduire plus tard
// en ajoutant une langue sans toucher au code. T(clé, args) remplace {0}, {1}… ; une clé inconnue s'affiche telle quelle.
public static class Loc
{
    static readonly Dictionary<string, string> fr = new Dictionary<string, string>
    {
        { "settings.title", "Réglages" },
        { "settings.erase", "Effacer la sauvegarde" },
        { "settings.erase.ask", "Effacer toute la progression ?" },
        { "settings.erase.warn", "Cette action est définitive." },
        { "settings.erase.yes", "Effacer" },
        { "settings.erase.no", "Annuler" },
        { "settings.version", "Version : {0}" },
        { "settings.diag", "Diagnostic" },
        { "offline.title", "Bon retour !" },
        { "offline.away", "Absent depuis {0}" },
        { "offline.gain", "+$ {0}" },
        { "upgrades.title", "Améliorations" },
        { "upgrades.level", "Niv {0}/{1}" },
        { "upgrades.maxed", "Niveau maximum" },
        { "upgrades.max", "MAX" },
        { "upgrades.next", "Prochain : {0}" },
        { "title.logo", "Idle Sécurité" },
        { "title.sub", "Installation de sécurité électronique" },
        { "title.play", "Jouer" },
        { "diag.session", "Session : {0} s" },
        { "diag.events", "Événements :" },
        { "diag.none", "aucun" },
    };

    public static string T(string key, params object[] args)
    {
        string s;
        if (!fr.TryGetValue(key, out s)) return key;
        return args == null || args.Length == 0 ? s : string.Format(s, args);
    }

    public static bool Has(string key) { return fr.ContainsKey(key); }
}
