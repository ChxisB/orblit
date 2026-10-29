namespace Orblit.Ui.Tests;

public class StringsTests
{
    private static UiStrings Menu()
    {
        var strings = new UiStrings();
        strings.Add("en", new Dictionary<string, string>
        {
            ["play"] = "Play",
            ["greet"] = "Hello, {name}",
            ["items.one"] = "{count} item",
            ["items.other"] = "{count} items",
        });
        strings.Add("fr", new Dictionary<string, string> { ["play"] = "Jouer", ["greet"] = "Bonjour, {name}" });
        strings.Add("fr-ca", new Dictionary<string, string> { ["play"] = "Joue" });
        return strings;
    }

    private static Dictionary<string, object?> With(string name, object? value) => new() { [name] = value };

    [Fact]
    public void ARegionalLanguageThenItsLanguageThenTheDefault()
    {
        var strings = Menu();
        strings.Use("fr_CA");

        Assert.Equal("fr-ca", strings.Locale);
        Assert.Equal("Joue", strings.Text("play"));
        Assert.Equal("Bonjour, Ann", strings.Text("greet", With("name", "Ann")));
        Assert.Equal("2 items", strings.Text("items.other", With("count", 2L)));
    }

    [Fact]
    public void AKeyNobodyHasIsAnsweredAsItself() => Assert.Equal("quit", Menu().Text("quit"));

    [Fact]
    public void ArgumentsFillTheirNamesAndLeaveUnknownOnesAlone()
    {
        var strings = Menu();

        Assert.Equal("Hello, Ann", strings.Text("greet", With("name", "Ann")));
        Assert.Equal("Hello, {name}", strings.Text("greet"));
        Assert.Equal("Hello, {name}", strings.Text("greet", With("name", null)));
    }

    [Fact]
    public void FindIsNullForAKeyNobodyHas()
    {
        Assert.Null(Menu().Find("quit"));
        Assert.Equal("Play", Menu().Find("play"));
    }

    [Fact]
    public void EveryShorterTagIsTriedAndTheDefaultHasItsOwn()
    {
        var strings = new UiStrings("zh-Hant-TW", "en-GB");
        strings.Add("zh-hant", new Dictionary<string, string> { ["play"] = "玩" });
        strings.Add("en", new Dictionary<string, string> { ["quit"] = "Quit" });

        Assert.Equal(["zh-hant-tw", "zh-hant", "zh", "en-gb", "en"], strings.Chain("zh-hant-tw"));
        Assert.Equal("玩", strings.Text("play"));
        Assert.Equal("Quit", strings.Text("quit"));
    }

    [Fact]
    public void ACountPicksTheFormItsLanguageGivesIt()
    {
        var forms = new (string Language, (long Count, string Form)[] Counts)[]
        {
            ("en", [(0, "other"), (1, "one"), (2, "other")]),
            ("fr", [(0, "one"), (1, "one"), (2, "other")]),
            ("ja", [(1, "other"), (5, "other")]),
            ("ru", [(1, "one"), (2, "few"), (5, "many"), (11, "many"), (21, "one"), (22, "few")]),
            ("pl", [(1, "one"), (2, "few"), (5, "many"), (12, "many"), (22, "few")]),
            ("cs", [(1, "one"), (3, "few"), (5, "other")]),
            ("ar", [(0, "zero"), (1, "one"), (2, "two"), (7, "few"), (15, "many"), (100, "other")]),
        };

        foreach (var (language, counts) in forms)
        {
            foreach (var (count, form) in counts)
            {
                Assert.True(form == UiStrings.PluralForm(language, count), $"{count} in {language}");
            }
        }
    }

    [Fact]
    public void ACountChoosesBetweenTheKeysOfATable()
    {
        var strings = Menu();

        Assert.Equal("1 item", strings.Text("items", With("count", 1L)));
        Assert.Equal("3 items", strings.Text("items", With("count", 3L)));
    }

    [Fact]
    public void ACountWithNoFractionIsAWholeCount()
    {
        var strings = Menu();

        Assert.Equal("1.0 item", strings.Text("items", With("count", 1.0)));
        Assert.Equal("3.0 items", strings.Text("items", With("count", 3.0)));
        Assert.Equal("items", strings.Text("items", With("count", 1.5)));
    }

    [Fact]
    public void PortugueseOfPortugalCountsZeroAsOtherAndOfBrazilAsOne()
    {
        Assert.Equal("other", UiStrings.PluralForm("pt-PT", 0));
        Assert.Equal("one", UiStrings.PluralForm("pt-BR", 0));
    }

    [Fact]
    public void ATextFoundInTheDefaultTakesTheDefaultsPlurals()
    {
        var strings = Menu();
        strings.Use("ru");

        // 21 is one in Russian and other in English, so the wrong rule shows.
        Assert.Equal("21 items", strings.Text("items", With("count", 21L)));
    }

    [Fact]
    public void ALanguageFileIsOneObjectOfText()
    {
        var strings = new UiStrings();
        strings.AddJson("de", """{"play": "Spielen"}""");
        strings.Use("de");

        Assert.Equal("Spielen", strings.Text("play"));
        foreach (var bad in new[] { "[]", """{"play": 3}""", "{\"play\"" })
        {
            Assert.ThrowsAny<FormatException>(() => strings.AddJson("de", bad));
        }
    }

    [Fact]
    public void ALanguageFileCanBeDroppedWhichTellsAListener()
    {
        var strings = Menu();
        var heard = 0;
        strings.Changed += () => heard++;

        strings.Clear();

        Assert.Empty(strings.Locales);
        Assert.Equal(1, heard);
    }

    [Fact]
    public void ATagIsNotEmpty() => Assert.Throws<ArgumentException>(() => new UiStrings(" "));

    [Fact]
    public void GapsListTheKeysEachLanguageHasNoTextFor()
    {
        var strings = Menu();
        strings.Add("de", new Dictionary<string, string> { ["items.other"] = "Dinge" });

        var gaps = strings.Gaps(["quit"]);

        Assert.Equal(["de", "en", "fr", "fr-ca"], gaps.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["greet", "play", "quit"], gaps["de"]);
        Assert.Equal(["quit"], gaps["en"]);
        Assert.Equal(["items", "quit"], gaps["fr"]);
        Assert.Equal(["items", "quit"], gaps["fr-ca"]);
    }

    [Fact]
    public void GapsCountAUsedKeyThatLooksLikeAPluralFormAsWritten()
    {
        var strings = new UiStrings();
        strings.Add("en", new Dictionary<string, string> { ["settings.other"] = "Other" });

        Assert.Empty(strings.Gaps(["settings.other"]));
    }

    [Fact]
    public void GapsAreEmptyWhenEveryLanguageHasEveryKey() => Assert.Empty(new UiStrings().Gaps());
}
