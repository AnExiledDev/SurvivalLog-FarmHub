using System.Reflection;
using System.Text.RegularExpressions;
using ExiledToolkit.Kit;
using FarmHub.Core;
using Xunit;

namespace FarmHub.Tests;

/// <summary>Every word on screen exists in English and Chinese, and page.js knows every word the plugin sends.</summary>
public class TextsTests
{
    private static IEnumerable<Phrase> AllPhrases(bool withSingulars = true)
    {
        foreach (var field in typeof(Texts).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            switch (field.GetValue(null))
            {
                case Phrase phrase:
                    yield return phrase;
                    break;
                case Counted counted:
                    if (withSingulars) yield return counted.One;
                    yield return counted.Many;
                    break;
            }
        }

        foreach (var (_, phrase) in Texts.Page) yield return phrase;
    }

    private static string PageScriptText()
    {
        using var stream = typeof(TextsTests).Assembly.GetManifestResourceStream("FarmHub.page.js") ?? throw new InvalidOperationException("page.js is not embedded");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    [Fact]
    public void Every_text_has_plain_english_and_a_chinese_translation()
    {
        foreach (var phrase in AllPhrases())
        {
            var hasWords = Regex.Replace(phrase.En, @"\{\d\}", "").Any(char.IsLetter);

            Assert.False(string.IsNullOrWhiteSpace(phrase.En));
            Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
            Assert.True(!hasWords || phrase.Zh.Any(ch => ch >= (char)0x4e00 && ch <= (char)0x9fff), phrase.Zh);
        }
    }

    /// <summary>English singulars spell out "1"; Chinese has one form, so only the plural forms are held to this.</summary>
    [Fact]
    public void Both_languages_fill_in_the_same_blanks()
    {
        foreach (var phrase in AllPhrases(withSingulars: false))
        {
            Assert.True(phrase.En.Contains("{0}") == phrase.Zh.Contains("{0}"), phrase.En);
            Assert.True(phrase.En.Contains("{1}") == phrase.Zh.Contains("{1}"), phrase.En);
        }
    }

    [Fact]
    public void Page_js_falls_back_to_exactly_the_english_words_the_plugin_sends()
    {
        var block = Regex.Match(PageScriptText(), @"var ENGLISH = \{(.*?)\n  \};", RegexOptions.Singleline).Groups[1].Value;
        var english = Regex.Matches(block, @"'?([A-Za-z]+)'?: '((?:[^'\\]|\\.)*)'").ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.Replace("\\'", "'"));

        Assert.Equal(Texts.Page.Select(w => w.Key).OrderBy(k => k), english.Keys.OrderBy(k => k));

        foreach (var (key, phrase) in Texts.Page) Assert.True(phrase.En == english[key], $"{key}: \"{phrase.En}\" vs \"{english[key]}\"");
    }

    [Fact]
    public void Page_keys_are_not_listed_twice()
    {
        Assert.Equal(Texts.Page.Count, Texts.Page.Select(w => w.Key).Distinct().Count());
    }

    [Fact]
    public void Every_word_page_js_looks_up_exists()
    {
        var script = PageScriptText();
        var keys = Texts.Page.Select(w => w.Key).ToHashSet();
        var used = Regex.Matches(script, @"say\('([A-Za-z]+)'").Select(m => m.Groups[1].Value)
            .Concat(Regex.Matches(script, @"\b[a-z]+: '((?:tab|st|p|t|from|f)[A-Z][A-Za-z]*)'").Select(m => m.Groups[1].Value));

        foreach (var key in used) Assert.True(keys.Contains(key), key);
    }
}
