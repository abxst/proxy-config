using System.Text;

namespace ProxyConfig.Core.Syntax;

/// <summary>A bounded line-oriented diff for reviewing imported source edits.</summary>
public static class ConfigTextDiff
{
    public static string Build(string original, string current, int maxCells = 500_000)
    {
        if (original == current)
            return "No changes from imported source.";

        var before = original.Split('\n');
        var after = current.Split('\n');
        if ((long)before.Length * after.Length > maxCells)
            return $"Diff exceeds the {maxCells:N0}-cell safety limit; compare the exported file externally.";

        var width = after.Length + 1;
        var matrix = new int[(before.Length + 1) * width];
        int At(int i, int j) => matrix[i * width + j];
        for (var i = before.Length - 1; i >= 0; i--)
        for (var j = after.Length - 1; j >= 0; j--)
        {
            matrix[i * width + j] = before[i] == after[j]
                ? At(i + 1, j + 1) + 1
                : Math.Max(At(i + 1, j), At(i, j + 1));
        }

        var output = new StringBuilder("--- imported\n+++ current\n");
        var left = 0;
        var right = 0;
        var lines = 0;
        while ((left < before.Length || right < after.Length) && lines < 2_000)
        {
            if (left < before.Length && right < after.Length && before[left] == after[right])
            {
                output.Append("  ").AppendLine(before[left]);
                left++;
                right++;
            }
            else if (right < after.Length &&
                (left == before.Length || At(left, right + 1) >= At(left + 1, right)))
            {
                output.Append("+ ").AppendLine(after[right++]);
            }
            else
            {
                output.Append("- ").AppendLine(before[left++]);
            }
            lines++;
        }
        if (left < before.Length || right < after.Length)
            output.AppendLine("… diff truncated after 2,000 lines …");
        return output.ToString();
    }
}
