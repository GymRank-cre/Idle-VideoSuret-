using System.Text;

// Nombres abrégés (K1) : 999 -> 999, 1 234 -> 1,2K, 1 200 000 -> 1,2M, puis B, T, aa, ab, ac…
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

        // une décimale tronquée en dessous de 100, aucune au-dessus
        string number;
        if (scaled >= 100d)
        {
            number = ((long)scaled).ToString();
        }
        else
        {
            long tenths = (long)(scaled * 10d);
            number = (tenths / 10) + (tenths % 10 == 0 ? "" : "," + (tenths % 10));
        }
        return number + Suffix(group);
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
