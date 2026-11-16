using System.Globalization;
using System.Text;

namespace CompanyApi.Application.Relevance;

/// <summary>Turns company names and hosts into comparable lowercase, accent-free tokens.</summary>
public static class TextNormalizer
{
    // Trailing legal-form suffixes, as token sequences after punctuation removal ("S.A. de C.V." -> sa de cv).
    private static readonly string[][] LegalSuffixes =
    [
        ["sa", "de", "cv"],
        ["s", "a", "de", "c", "v"],
        ["inc"], ["llc"], ["ltd"], ["corp"], ["co"], ["gmbh"], ["sa"],
        ["incorporated"], ["corporation"], ["limited"], ["plc"], ["ag"],
    ];

    // Second-level labels that form part of a country suffix (acme.co.uk, mycompany.com.mx).
    private static readonly HashSet<string> SecondLevelSuffixLabels =
        ["co", "com", "net", "org", "gob", "gov", "edu", "ac", "or", "ne"];

    /// <summary>Lowercases, strips accents and punctuation, splits into tokens and removes trailing legal suffixes.</summary>
    public static IReadOnlyList<string> NameTokens(string text)
    {
        var tokens = Tokenize(text);
        RemoveLegalSuffixes(tokens);
        return tokens;
    }

    /// <summary>Lowercase, without a trailing dot and without a leading "www.".</summary>
    public static string NormalizeHost(string host)
    {
        var h = host.Trim().TrimEnd('.').ToLowerInvariant();
        return h.StartsWith("www.", StringComparison.Ordinal) ? h[4..] : h;
    }

    /// <summary>
    /// The registrable label of a host without www. and TLD: "www.acme-tools.co.uk" -> "acme-tools".
    /// </summary>
    public static string DomainLabel(string host)
    {
        var labels = NormalizeHost(host).Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0) return string.Empty;
        if (labels.Length == 1) return RemoveDiacritics(labels[0]);

        var end = labels.Length - 1; // drop TLD
        if (labels.Length >= 3 && labels[^1].Length == 2 && SecondLevelSuffixLabels.Contains(labels[end - 1]))
        {
            end--; // drop "co" in .co.uk
        }

        return RemoveDiacritics(labels[end - 1]);
    }

    /// <summary>Tokens of the domain label, split on hyphens: "acme-tools" -> [acme, tools].</summary>
    public static IReadOnlyList<string> DomainTokens(string label) =>
        label.Split('-', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Lowercase, accent-free text with punctuation turned into spaces (used for content matching).</summary>
    public static string NormalizeText(string text) => string.Join(' ', Tokenize(text));

    public static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<string> Tokenize(string text)
    {
        var lowered = RemoveDiacritics(text.ToLowerInvariant());
        var sb = new StringBuilder(lowered.Length);
        foreach (var c in lowered)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is '.' or '\'' or '’' or '&') continue; // "s.a." -> "sa", "AT&T" -> "att"
            else sb.Append(' ');
        }

        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private static void RemoveLegalSuffixes(List<string> tokens)
    {
        bool removed;
        do
        {
            removed = false;
            foreach (var suffix in LegalSuffixes)
            {
                // Never strip the whole name ("Co" alone stays "co").
                if (tokens.Count <= suffix.Length || !EndsWith(tokens, suffix)) continue;
                tokens.RemoveRange(tokens.Count - suffix.Length, suffix.Length);
                removed = true;
                break;
            }
        } while (removed);
    }

    private static bool EndsWith(List<string> tokens, string[] suffix)
    {
        var offset = tokens.Count - suffix.Length;
        for (var i = 0; i < suffix.Length; i++)
        {
            if (tokens[offset + i] != suffix[i]) return false;
        }

        return true;
    }
}
