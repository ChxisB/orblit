namespace Orblit.Ui;

/// <summary>
/// Which plural form a count takes, named as the Unicode CLDR names them. Only the languages listed
/// here have their own rule, and counts are whole. Add a language here when a game ships it.
/// </summary>
internal static class PluralRules
{
    private static readonly (Func<long, string> Rule, string[] Languages)[] Groups =
    [
        (Other, ["ja", "zh", "ko", "vi", "th", "id"]),
        (ZeroOrOne, ["fr", "pt", "hi"]),
        (EastSlavic, ["ru", "uk"]),
        (OneOrOther, ["pt-pt"]),
        (Polish, ["pl"]),
        (Czech, ["cs", "sk"]),
        (Arabic, ["ar"]),
    ];

    private static readonly Dictionary<string, Func<long, string>> Rules =
        Groups.SelectMany(group => group.Languages.Select(language => (language, group.Rule)))
            .ToDictionary(entry => entry.language, entry => entry.Rule);

    /// <summary>
    /// The form <paramref name="count"/> takes in the language <paramref name="tag"/>, which is already
    /// in its clean form. A region with no rule of its own takes its language's. Any other language takes
    /// one and other. The count is taken as positive, so the most negative long stays negative.
    /// </summary>
    public static string Form(string tag, long count)
    {
        var rule = Rules.GetValueOrDefault(tag) ?? Rules.GetValueOrDefault(tag.Split('-')[0]) ?? OneOrOther;
        return rule(unchecked(count < 0 ? -count : count));
    }

    // C# keeps the sign of the dividend. A remainder that is never negative keeps the most negative long on the right ending.
    private static long Mod(long number, long divisor) => ((number % divisor) + divisor) % divisor;

    private static string Other(long _) => "other";

    private static string OneOrOther(long n) => n == 1 ? "one" : "other";

    private static string ZeroOrOne(long n) => n is 0 or 1 ? "one" : "other";

    // Eleven to fourteen take the many form in the Slavic rules whatever their last digit is.
    private static bool Teen(long n) => Mod(n, 100) is >= 11 and <= 14;

    private static bool FewEnding(long n) => Mod(n, 10) is >= 2 and <= 4 && !Teen(n);

    private static string EastSlavic(long n)
    {
        if (Mod(n, 10) == 1 && Mod(n, 100) != 11)
        {
            return "one";
        }

        return FewEnding(n) ? "few" : "many";
    }

    private static string Polish(long n)
    {
        if (n == 1)
        {
            return "one";
        }

        return FewEnding(n) ? "few" : "many";
    }

    private static string Czech(long n)
    {
        if (n == 1)
        {
            return "one";
        }

        return n is >= 2 and <= 4 ? "few" : "other";
    }

    private static string Arabic(long n)
    {
        var last = Mod(n, 100);
        return n switch
        {
            0 => "zero",
            1 => "one",
            2 => "two",
            _ when last is >= 3 and <= 10 => "few",
            _ => last >= 11 ? "many" : "other",
        };
    }
}
