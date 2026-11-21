using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace TursibBackend.Tests;

/// <summary>
/// Trasabilitatea cerințelor (docs/requirements.md) e verificată automat: fiecare cerință are implementare și cel puțin
/// o verificare automată, iar fișierele și testele indicate există. Un test redenumit sau un fișier mutat strică build-ul,
/// nu doar documentația.
/// </summary>
public partial class RequirementsTraceabilityTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "requirements.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Nu găsesc docs/requirements.md");
    }

    public record Requirement(string Id, string Implementation, string Verification);

    public static IReadOnlyList<Requirement> Parse(string markdown)
    {
        var result = new List<Requirement>();
        foreach (var section in Regex.Split(markdown, @"^### ", RegexOptions.Multiline).Skip(1))
        {
            var id = Regex.Match(section, @"^(REQ-[FQ]-\d{2})").Groups[1].Value;
            if (id.Length == 0) continue;
            string Line(string label) => Regex.Match(section, $@"^- \*\*{label}:\*\* (.+)$", RegexOptions.Multiline).Groups[1].Value;
            result.Add(new Requirement(id, Line("Implementare"), Line("Verificare")));
        }
        return result;
    }

    private static IReadOnlyList<Requirement> Requirements() => Parse(File.ReadAllText(Path.Combine(RepoRoot(), "docs", "requirements.md")));

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Backticked();

    public static TheoryData<string> Ids() => new(Requirements().Select(r => r.Id));

    [Fact]
    public void TheDocumentListsTheExpectedRequirements_WithoutDuplicates()
    {
        var ids = Requirements().Select(r => r.Id).ToList();
        Assert.True(ids.Count >= 20, $"doar {ids.Count} cerințe găsite");
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains("REQ-F-13", ids);
        Assert.Contains("REQ-Q-04", ids);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryImplementationFileExists(string id)
    {
        var r = Requirements().Single(x => x.Id == id);
        var paths = Backticked().Matches(r.Implementation).Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(paths);
        foreach (var path in paths)
            Assert.True(File.Exists(Path.Combine(RepoRoot(), path)) || Directory.Exists(Path.Combine(RepoRoot(), path)), $"{id}: lipsește {path}");
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryRequirementIsVerifiedByTestsThatExist(string id)
    {
        var r = Requirements().Single(x => x.Id == id);
        var tests = Backticked().Matches(r.Verification).Select(m => m.Groups[1].Value).ToList();
        var testFiles = Regex.Matches(r.Verification, @"test:(\S+?)(?:,|$|\s)").Select(m => m.Groups[1].Value).ToList();
        Assert.True(tests.Count + testFiles.Count > 0, $"{id} nu are nicio verificare automată");

        var assembly = typeof(RequirementsTraceabilityTests).Assembly;
        foreach (var reference in tests)
        {
            var parts = reference.Split('.');
            var type = assembly.GetTypes().SingleOrDefault(t => t.Name == parts[0] && t.Namespace == "TursibBackend.Tests");
            Assert.True(type != null, $"{id}: nu există clasa de test {parts[0]}");
            var testMethods = type!.GetMethods().Where(m => m.GetCustomAttributes<FactAttribute>(inherit: true).Any()).ToList();
            Assert.True(testMethods.Count > 0, $"{id}: {parts[0]} nu conține teste");
            if (parts.Length == 2)
                Assert.True(testMethods.Any(m => m.Name == parts[1]), $"{id}: nu există testul {reference}");
        }
        foreach (var file in testFiles)
            Assert.True(File.Exists(Path.Combine(RepoRoot(), file)), $"{id}: lipsește fișierul de test {file}");
    }

    [Fact]
    public void TheSysmlModelOnlyReferencesDocumentedRequirements()
    {
        var sysml = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "architecture", "tursib.sysml"));
        var modelled = Regex.Matches(sysml, @"<'(REQ-[FQ]-\d{2})'>").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(modelled);
        var documented = Requirements().Select(r => r.Id).ToHashSet();
        Assert.All(modelled, id => Assert.Contains(id, documented));
    }
}
