using System.Text.RegularExpressions;

namespace Ember;

public sealed record OptimizeFinding(
    int Rank,
    string Title,
    string Severity,
    string? Savings,
    string Body,
    string? ActionText);

public sealed record OptimizeReport(
    string? Grade,
    int Score,
    int Issues,
    string? TotalSavings,
    IReadOnlyList<OptimizeFinding> Findings)
{
    private static readonly Regex SectionRx = new(
        @"^\s*─+\s*(\d+)\.\s*(.+?)\s*─+\s*(High|Medium|Low)\s*─+\s*$");
    private static readonly Regex HealthRx = new(
        @"Health:\s*([A-F][+-]?)\s*\((\d+)/100,\s*(\d+)\s*issues?\)");
    private static readonly Regex SavingsRx = new(
        @"Potential savings:.*?\(~([^,\)]+)");
    private static readonly Regex ActionRx = new(@"^\s*--\s*(.+?)\s*─*\s*$");

    public static OptimizeReport Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        string? grade = null, totalSavings = null;
        int score = 0, issues = 0;
        var findings = new List<OptimizeFinding>();

        int rank = 0;
        string title = "", severity = "";
        string? savings = null, actionText = null;
        var body = new List<string>();
        var action = new List<string>();
        bool inAction = false;

        void Flush()
        {
            if (rank == 0) return;
            actionText = action.Count > 0
                ? string.Join("\n", action.Select(l => l.Trim())).Trim()
                : null;
            findings.Add(new OptimizeFinding(rank, title, severity, savings,
                string.Join(" ", body.Select(l => l.Trim())).Trim(), actionText));
            body.Clear();
            action.Clear();
            savings = null;
            inAction = false;
        }

        foreach (var line in lines)
        {
            var section = SectionRx.Match(line);
            if (section.Success)
            {
                Flush();
                rank = int.Parse(section.Groups[1].Value);
                title = section.Groups[2].Value;
                severity = section.Groups[3].Value;
                continue;
            }

            if (rank == 0)
            {
                var h = HealthRx.Match(line);
                if (h.Success)
                {
                    grade = h.Groups[1].Value;
                    score = int.Parse(h.Groups[2].Value);
                    issues = int.Parse(h.Groups[3].Value);
                }
                var ts = SavingsRx.Match(line);
                if (ts.Success && totalSavings is null)
                    totalSavings = ts.Groups[1].Value.Trim();
                continue;
            }

            var sv = SavingsRx.Match(line);
            if (sv.Success && savings is null)
            {
                savings = sv.Groups[1].Value.Trim();
                continue;
            }

            if (ActionRx.IsMatch(line) && line.TrimStart().StartsWith("--"))
            {
                inAction = true;
                continue;
            }

            if (inAction)
            {
                if (!string.IsNullOrWhiteSpace(line)) action.Add(line);
            }
            else if (savings is null && !string.IsNullOrWhiteSpace(line)
                     && !line.TrimStart().StartsWith("──"))
            {
                body.Add(line);
            }
        }
        Flush();

        return new OptimizeReport(grade, score, issues, totalSavings, findings);
    }
}
