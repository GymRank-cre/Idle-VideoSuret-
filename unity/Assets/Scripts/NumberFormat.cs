using System.Text;

// Nombres abrégés (K1) : 999 -> 999, 1 234 -> 1,23K, 8 921 000 -> 8,92M, puis B, T, aa, ab, ac… (3 chiffres significatifs, comme le web)
// Virgule française. Tronque (jamais d'arrondi vers le haut : 999 999 s'affiche 999K et non 1000K).
public static class NumberFormat
{
    static readonly string[] Named = { "", "K", "M", "B", "T" };

    public static string Short(long value)
    {
        if (value < 0) return "-" + Short(-value);
        if (value < 1000) return value.ToString();

        int group = 0;
        double scaled = value;
        while (scaled >= 1000d)
        {
            scaled /= 1000d;
            group++;
        }

        // deux décimales sous 10, une sous 100, aucune au-dessus (toujours tronquées, zéros de fin retirés)
        const double Rounding = 1e-9;   // 1,2 x 100 ne doit pas donner 119,99…
        string number;
        if (scaled >= 100d)
        {
            number = ((long)scaled).ToString();
        }
        else if (scaled >= 10d)
        {
            long tenths = (long)(scaled * 10d + Rounding);
            number = (tenths / 10) + (tenths % 10 == 0 ? "" : "," + (tenths % 10));
        }
        else
        {
            long hundredths = (long)(scaled * 100d + Rounding);
            long decimals = hundredths % 100;
            number = (hundredths / 100).ToString();
            if (decimals > 0) number += "," + (decimals % 10 == 0 ? (decimals / 10).ToString() : decimals.ToString("00"));
        }
        return number + Suffix(group);
    }

    // Gain affiché (« +x ») : une décimale sous 10 (1,7), jamais « 0 » tant que le gain est positif.
    public static string Gain(double value)
    {
        if (value <= 0d) return "0";
        if (value >= 10d) return Short((long)value);
        double tenths = System.Math.Floor(value * 10d) / 10d;
        if (tenths < 0.1d) return "0,1";
        long t = (long)System.Math.Round(tenths * 10d);
        return (t / 10) + (t % 10 == 0 ? "" : "," + (t % 10));
    }

    // Revenu par seconde : une décimale sous 10 (6,1/s), entier arrondi ensuite, abrégé au-delà de 1000.
    public static string Rate(double perSecond) { return RateValue(perSecond) + "/s"; }

    // Le même nombre sans « /s » (gain affiché sur les boutons).
    public static string RateValue(double perSecond)
    {
        if (perSecond < 0d) perSecond = 0d;
        if (perSecond >= 10d) return Short((long)System.Math.Round(perSecond, System.MidpointRounding.AwayFromZero));
        long t = (long)System.Math.Round(perSecond * 10d, System.MidpointRounding.AwayFromZero);
        return (t / 10) + (t % 10 == 0 ? "" : "," + (t % 10));
    }

    // Durée lisible (fmtTime du web) : 45 s, 3 min 20 s, 2 h 5 min.
    public static string Time(double seconds)
    {
        long s = (long)System.Math.Round(System.Math.Max(0d, seconds), System.MidpointRounding.AwayFromZero);
        if (s < 60) return s + " s";
        if (s < 3600) return s / 60 + " min" + (s % 60 > 0 ? " " + s % 60 + " s" : "");
        long m = s % 3600 / 60;
        return s / 3600 + " h" + (m > 0 ? " " + m + " min" : "");
    }

    // Durées du bouton Turbo : secondes arrondies au-dessus, minutes au-delà de 60 s (comme le web).
    public static string Seconds(double seconds) { return System.Math.Ceiling(seconds) + " s"; }

    public static string Wait(double seconds)
    {
        return seconds > 60d ? System.Math.Ceiling(seconds / 60d) + " min" : Seconds(seconds);
    }

    // group 1 = K, 2 = M, 3 = B, 4 = T, 5 = aa, 6 = ab, ... 30 = az, 31 = ba...
    static string Suffix(int group)
    {
        if (group < Named.Length) return Named[group];
        int n = group - Named.Length; // 0 = aa
        var sb = new StringBuilder(2);
        sb.Append((char)('a' + (n / 26) % 26));
        sb.Append((char)('a' + n % 26));
        return sb.ToString();
    }
}
