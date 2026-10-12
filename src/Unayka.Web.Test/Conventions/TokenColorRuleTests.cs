using System.Text.RegularExpressions;

namespace Unayka.Web.Test.Conventions;

public sealed partial class TokenColorRuleTests
{
    private const string SolutionFileName = "Unayka.slnx";
    private const string WebProjectName = "Unayka.Web";

    [Fact]
    public void Razor_markup_uses_only_theme_token_colors()
    {
        var violations = FindRazorFiles()
            .SelectMany(file => NonTokenColors().Matches(File.ReadAllText(file)).Select(match => $"{Path.GetFileName(file)}: {match.Value}"))
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static IEnumerable<string> FindRazorFiles() =>
        Directory.EnumerateFiles(FindWebRoot(), "*.razor", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindWebRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return Path.Combine(directory.FullName, WebProjectName);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Solution root not found.");
    }

    [GeneratedRegex(@"(?<![\w-])(?:[\w\[\]=-]+:)*(?:bg|text|border|ring|ring-offset|from|via|to|fill|stroke|divide|outline|decoration|placeholder|accent|caret|shadow)-(?:white|black|paper|cream|stone|sand|clay|espresso|sage|rose|(?:slate|gray|zinc|neutral|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink)-\d{2,3}|\[#[^\]]*\]|\[(?:rgb|hsl|oklch)[^\]]*\])(?![\w])")]
    private static partial Regex NonTokenColors();
}
