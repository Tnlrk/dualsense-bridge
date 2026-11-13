using System.IO;
using System.Text.RegularExpressions;
using DualShift.Localization;

namespace DualShift.Tests;

[Collection("Loc")]
public class LocalizationTests
{
    [Fact]
    public void BothLanguagesHaveSameKeys()
    {
        var missingInEn = Strings.Cs.Keys.Except(Strings.En.Keys).ToList();
        var missingInCs = Strings.En.Keys.Except(Strings.Cs.Keys).ToList();
        Assert.Empty(missingInEn);
        Assert.Empty(missingInCs);
    }

    [Fact]
    public void FormatPlaceholdersMatch()
    {
        foreach (var (key, cs) in Strings.Cs)
        {
            var a = Placeholders(cs);
            var b = Placeholders(Strings.En[key]);
            Assert.True(a.SetEquals(b), $"Klíč {key}: {string.Join(",", a)} vs {string.Join(",", b)}");
        }
    }

    private static HashSet<string> Placeholders(string s) =>
        Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).ToHashSet();

    [Fact]
    public void AllUsedKeysExist()
    {
        // Najde Loc.T("…") / Loc.F("…") / [key] v kódu a XAML a ověří, že klíče existují.
        var root = FindSrc();
        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.EndsWith("Loc.cs")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"Loc\.[TF]\(""([a-zA-Z_]+)""")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"Binding \[([a-zA-Z_]+)\]")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"""(tr_hint_[a-z]+)""")) used.Add(m.Groups[1].Value);
        }
        Assert.NotEmpty(used);
        var missing = used.Where(k => !Strings.Cs.ContainsKey(k)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void HelpHasSameStructureInBothLanguages()
    {
        var cs = HelpContent.For(Loc.Czech);
        var en = HelpContent.For(Loc.English);
        Assert.Equal(cs.Count, en.Count);
        for (int i = 0; i < cs.Count; i++)
            Assert.Equal(cs[i].Blocks.Select(b => b.GetType()), en[i].Blocks.Select(b => b.GetType()));
    }

    [Fact]
    public void SwitchLanguage()
    {
        var before = Loc.Language;
        try
        {
            Loc.SetLanguage(Loc.English);
            Assert.Equal("Settings…", Loc.T("tray_settings"));
            Loc.SetLanguage(Loc.Czech);
            Assert.Equal("Nastavení…", Loc.T("tray_settings"));
        }
        finally
        {
            Loc.SetLanguage(before);
        }
    }

    private static string FindSrc()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "DualShift")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "DualShift");
    }
}
