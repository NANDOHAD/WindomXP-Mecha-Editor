using System.Collections.Generic;

public static class HodHierarchyValidator
{
    const int MaxReportedIssues = 8;

    public static bool TryValidate(IList<hod2v0_Part> parts, out string details)
    {
        List<string> issues = new List<string>();
        int issueCount = 0;

        if (parts == null || parts.Count == 0)
        {
            details = "パーツ情報がありません。";
            return false;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            hod2v0_Part part = parts[i];
            string partLabel = GetPartLabel(part, i);

            if (part.treeDepth < 0)
                AddIssue(issues, ref issueCount, $"{partLabel}: treeDepthが負の値です（{part.treeDepth}）。");

            if (i == 0 && part.treeDepth != 0)
                AddIssue(issues, ref issueCount, $"{partLabel}: 先頭パーツのtreeDepthは0である必要があります（{part.treeDepth}）。");

            if (i > 0)
            {
                int previousDepth = parts[i - 1].treeDepth;
                if (part.treeDepth > previousDepth + 1)
                {
                    AddIssue(issues, ref issueCount,
                        $"{partLabel}: treeDepthが直前のパーツから2段以上深くなっています（{previousDepth}→{part.treeDepth}）。");
                }

                if (part.treeDepth == 0)
                    AddIssue(issues, ref issueCount, $"{partLabel}: 2個目以降のルートパーツです。");
            }

            if (part.treeDepth > 0 && !HasPreviousParent(parts, i, part.treeDepth - 1))
                AddIssue(issues, ref issueCount, $"{partLabel}: treeDepth={part.treeDepth - 1}の親候補が前方にありません。");

            int actualChildCount = CountDirectChildren(parts, i);
            if (part.childCount != actualChildCount)
            {
                AddIssue(issues, ref issueCount,
                    $"{partLabel}: childCountが一致しません（記録={part.childCount}、深度列から算出={actualChildCount}）。");
            }
        }

        if (issueCount == 0)
        {
            details = "";
            return true;
        }

        details = string.Join("\n", issues);
        if (issueCount > issues.Count)
            details += $"\nほか{issueCount - issues.Count}件の不整合があります。";
        return false;
    }

    static bool HasPreviousParent(IList<hod2v0_Part> parts, int index, int parentDepth)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (parts[i].treeDepth == parentDepth)
                return true;
        }
        return false;
    }

    static int CountDirectChildren(IList<hod2v0_Part> parts, int parentIndex)
    {
        int parentDepth = parts[parentIndex].treeDepth;
        int childCount = 0;

        for (int i = parentIndex + 1; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            if (depth <= parentDepth)
                break;
            if (depth == parentDepth + 1)
                childCount++;
        }

        return childCount;
    }

    static string GetPartLabel(hod2v0_Part part, int index)
    {
        string name = string.IsNullOrEmpty(part.name) ? "<名称なし>" : part.name;
        return $"パーツ[{index}]「{name}」";
    }

    static void AddIssue(List<string> issues, ref int issueCount, string issue)
    {
        issueCount++;
        if (issues.Count < MaxReportedIssues)
            issues.Add(issue);
    }
}
