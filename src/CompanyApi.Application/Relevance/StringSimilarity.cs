namespace CompanyApi.Application.Relevance;

public static class StringSimilarity
{
    /// <summary>Jaro-Winkler similarity in [0, 1]; tolerant of typos and rewards shared prefixes.</summary>
    public static double JaroWinkler(string a, string b, double prefixScale = 0.1)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;
        if (a == b) return 1.0;

        var window = Math.Max(0, Math.Max(a.Length, b.Length) / 2 - 1);
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        var matches = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var start = Math.Max(0, i - window);
            var end = Math.Min(b.Length - 1, i + window);
            for (var j = start; j <= end; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = bMatched[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }

        double m = matches;
        var jaro = (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3.0;

        var prefix = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)) && a[i] == b[i]; i++) prefix++;

        return Math.Clamp(jaro + prefix * prefixScale * (1 - jaro), 0.0, 1.0);
    }
}
