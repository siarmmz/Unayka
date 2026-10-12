using System.Text.RegularExpressions;

namespace Unayka.Web.Test.Conventions;

public sealed partial class RazorTextRuleTests
{
    private const string SolutionFileName = "Unayka.slnx";
    private const string WebProjectName = "Unayka.Web";

    [Fact]
    public void Razor_markup_has_no_text_outside_Text_calls()
    {
        var violations = new List<string>();

        foreach (var file in FindRazorFiles())
        {
            var markup = RemoveCodeBlock(File.ReadAllText(file));
            foreach (var line in markup.Split('\n'))
            {
                if (HasLooseText(line))
                {
                    violations.Add($"{Path.GetFileName(file)}: {line.Trim()}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Razor_files_have_no_style_attribute_or_scoped_css()
    {
        var webRoot = FindWebRoot();
        var scopedCss = Directory.EnumerateFiles(webRoot, "*.razor.css", SearchOption.AllDirectories);
        var styled = FindRazorFiles().Where(file => File.ReadAllText(file).Contains("style=\"", StringComparison.Ordinal));

        Assert.Empty(scopedCss);
        Assert.Empty(styled);
    }

    private static bool HasLooseText(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('@') || trimmed is "{" or "}")
        {
            return false;
        }

        var withoutTags = TagRegex().Replace(trimmed, string.Empty);
        var withoutExpressions = ExpressionRegex().Replace(withoutTags, string.Empty);
        return withoutExpressions.Any(char.IsLetterOrDigit);
    }

    private static string RemoveCodeBlock(string content)
    {
        var index = content.IndexOf("@code", StringComparison.Ordinal);
        return index < 0 ? content : content[..index];
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

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"@[\w.]+(\([^)]*\))?")]
    private static partial Regex ExpressionRegex();
}
