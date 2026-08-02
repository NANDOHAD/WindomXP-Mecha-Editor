using System.Collections.Generic;

public enum HodHierarchyRepairKind
{
    None,
    RebuildChildCountsFromTreeDepth,
    RebuildTreeDepthFromChildCounts,
    Ambiguous,
    Unrepairable
}

public sealed class HodHierarchyRepairPlan
{
    const int MaxPreviewChanges = 8;

    readonly int[] originalTreeDepths;
    readonly int[] originalChildCounts;
    readonly int[] repairedTreeDepths;
    readonly int[] repairedChildCounts;

    public HodHierarchyRepairKind Kind { get; private set; }
    public string Summary { get; private set; }
    public string Details { get; private set; }

    public bool CanApply
    {
        get
        {
            return Kind == HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth
                || Kind == HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts;
        }
    }

    internal HodHierarchyRepairPlan(
        HodHierarchyRepairKind kind,
        string summary,
        string details,
        int[] originalDepths,
        int[] originalCounts,
        int[] repairedDepths,
        int[] repairedCounts)
    {
        Kind = kind;
        Summary = summary;
        Details = details;
        originalTreeDepths = originalDepths;
        originalChildCounts = originalCounts;
        repairedTreeDepths = repairedDepths;
        repairedChildCounts = repairedCounts;
    }

    public string BuildPreview(IList<hod2v0_Part> parts)
    {
        if (!CanApply || parts == null)
            return "";

        List<string> changes = new List<string>();
        int changeCount = 0;
        int count = repairedTreeDepths == null ? 0 : repairedTreeDepths.Length;
        for (int i = 0; i < count; i++)
        {
            if (originalTreeDepths[i] == repairedTreeDepths[i]
                && originalChildCounts[i] == repairedChildCounts[i])
                continue;

            changeCount++;
            if (changes.Count >= MaxPreviewChanges)
                continue;

            string name = i < parts.Count && !string.IsNullOrEmpty(parts[i].name)
                ? parts[i].name
                : "<名称なし>";
            changes.Add($"パーツ[{i}]「{name}」: "
                + $"treeDepth {originalTreeDepths[i]}→{repairedTreeDepths[i]}, "
                + $"childCount {originalChildCounts[i]}→{repairedChildCounts[i]}");
        }

        string preview = string.Join("\n", changes);
        if (changeCount > changes.Count)
            preview += $"\nほか{changeCount - changes.Count}件を修復します。";
        return preview;
    }

    public bool CanApplyTo(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = "この不整合には一意な自動修復方法がありません。";
            return false;
        }

        return TryValidateTarget(ani, out error);
    }

    
    public bool TryApply(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = "この不整合には一意な自動修復方法がありません。";
            return false;
        }

        if (!TryValidateTarget(ani, out error))
            return false;

        for (int i = 0; i < ani.structure.parts.Count; i++)
        {
            hod2v0_Part part = ani.structure.parts[i];
            part.treeDepth = repairedTreeDepths[i];
            part.childCount = repairedChildCounts[i];
            ani.structure.parts[i] = part;
        }

        foreach (animation animationData in ani.animations)
        {
            foreach (hod2v1 frame in animationData.frames)
            {
                for (int i = 0; i < frame.parts.Count; i++)
                {
                    hod2v1_Part part = frame.parts[i];
                    part.treeDepth = repairedTreeDepths[i];
                    part.childCount = repairedChildCounts[i];
                    frame.parts[i] = part;
                }
            }
        }

        error = "";
        return true;
    }

    bool TryValidateTarget(ani2 ani, out string error)
    {
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            error = "構造HODがありません。";
            return false;
        }

        int partCount = repairedTreeDepths == null ? 0 : repairedTreeDepths.Length;
        if (ani.structure.parts.Count != partCount)
        {
            error = $"構造HODのパーツ数が修復計画と一致しません（{ani.structure.parts.Count}/{partCount}）。";
            return false;
        }

        for (int i = 0; i < partCount; i++)
        {
            hod2v0_Part structurePart = ani.structure.parts[i];
            if (structurePart.treeDepth != originalTreeDepths[i]
                || structurePart.childCount != originalChildCounts[i])
            {
                error = $"構造HODのパーツ[{i}]が修復計画の作成後に変更されています。";
                return false;
            }
        }

        if (ani.animations == null)
        {
            error = "アニメーション情報がありません。";
            return false;
        }

        for (int animationIndex = 0; animationIndex < ani.animations.Count; animationIndex++)
        {
            animation animationData = ani.animations[animationIndex];
            if (animationData == null || animationData.frames == null)
            {
                error = $"アニメーション[{animationIndex}]のフレーム情報がありません。";
                return false;
            }

            for (int frameIndex = 0; frameIndex < animationData.frames.Count; frameIndex++)
            {
                hod2v1 frame = animationData.frames[frameIndex];
                if (frame == null || frame.parts == null)
                {
                    error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ情報がありません。";
                    return false;
                }

                if (frame.parts.Count != partCount)
                {
                    error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ数が"
                        + $"構造HODと一致しません（{frame.parts.Count}/{partCount}）。";
                    return false;
                }

                for (int partIndex = 0; partIndex < partCount; partIndex++)
                {
                    string structureName = ani.structure.parts[partIndex].name;
                    string frameName = frame.parts[partIndex].name;
                    if (!string.IsNullOrEmpty(structureName)
                        && !string.IsNullOrEmpty(frameName)
                        && !string.Equals(structureName, frameName, System.StringComparison.Ordinal))
                    {
                        error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ順が"
                            + $"構造HODと一致しません（位置{partIndex}: 「{frameName}」/「{structureName}」）。";
                        return false;
                    }
                }
            }
        }

        error = "";
        return true;
    }
}

public static class HodHierarchyRepair
{
    public static HodHierarchyRepairPlan CreatePlanUsingTreeDepth(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                "パーツ情報がないためtreeDepthを正として修復できません。",
                "treeDepthから階層を復元できません。", null, null, null, null);
        }

        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);
        int[] childCountsFromDepth;
        string reason;
        if (!TryBuildChildCountsFromDepths(parts, out childCountsFromDepth, out reason))
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                "treeDepthを正として修復できません。", reason,
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        return NewPlan(HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth,
            "treeDepthを正としてchildCountを修復します。",
            "treeDepthが表す階層を維持し、全パーツのchildCountを再計算します。",
            originalDepths, originalCounts, originalDepths, childCountsFromDepth);
    }

    public static HodHierarchyRepairPlan CreatePlanUsingChildCount(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                "パーツ情報がないためchildCountを正として修復できません。",
                "childCountから階層を復元できません。", null, null, null, null);
        }

        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);
        int[] treeDepthsFromCounts;
        string reason;
        if (!TryBuildDepthsFromChildCounts(parts, out treeDepthsFromCounts, out reason))
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                "childCountを正として修復できません。", reason,
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        return NewPlan(HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts,
            "childCountを正としてtreeDepthを修復します。",
            "childCountが表す階層を維持し、全パーツのtreeDepthを再計算します。",
            originalDepths, originalCounts, treeDepthsFromCounts, originalCounts);
    }

    public static HodHierarchyRepairPlan CreatePlan(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return NewPlan(
                HodHierarchyRepairKind.Unrepairable,
                "パーツ情報がないため修復できません。",
                "treeDepthとchildCountのどちらからも階層を復元できません。",
                null, null, null, null);
        }

        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);

        int[] childCountsFromDepth;
        string depthReason;
        bool depthValid = TryBuildChildCountsFromDepths(parts, out childCountsFromDepth, out depthReason);

        int[] treeDepthsFromCounts;
        string childCountReason;
        bool childCountsValid = TryBuildDepthsFromChildCounts(parts, out treeDepthsFromCounts, out childCountReason);

        if (depthValid && childCountsValid)
        {
            bool depthMatches = ArraysEqual(originalDepths, treeDepthsFromCounts);
            bool childCountMatches = ArraysEqual(originalCounts, childCountsFromDepth);
            if (depthMatches && childCountMatches)
            {
                return NewPlan(
                    HodHierarchyRepairKind.None,
                    "パーツ階層に不整合はありません。",
                    "",
                    originalDepths, originalCounts, originalDepths, originalCounts);
            }

            return NewPlan(
                HodHierarchyRepairKind.Ambiguous,
                "treeDepthとchildCountが、それぞれ別の有効な階層を表しています。",
                "どちらを正しい値とみなすか一意に決められないため、自動修復は行いません。",
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        if (depthValid)
        {
            return NewPlan(
                HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth,
                "treeDepthを正としてchildCountを修復できます。",
                "treeDepthの並びは有効ですが、childCountからは有効な階層を復元できません。",
                originalDepths, originalCounts, originalDepths, childCountsFromDepth);
        }

        if (childCountsValid)
        {
            return NewPlan(
                HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts,
                "childCountを正としてtreeDepthを修復できます。",
                "childCountの並びは有効ですが、treeDepthからは有効な階層を復元できません。",
                originalDepths, originalCounts, treeDepthsFromCounts, originalCounts);
        }

        return NewPlan(
            HodHierarchyRepairKind.Unrepairable,
            "treeDepthとchildCountの両方に不整合があるため修復できません。",
            $"treeDepth: {depthReason}\nchildCount: {childCountReason}",
            originalDepths, originalCounts, originalDepths, originalCounts);
    }

    static bool TryBuildChildCountsFromDepths(
        IList<hod2v0_Part> parts,
        out int[] childCounts,
        out string reason)
    {
        childCounts = new int[parts.Count];
        if (parts[0].treeDepth != 0)
        {
            reason = $"先頭パーツのtreeDepthが0ではありません（{parts[0].treeDepth}）。";
            return false;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            if (depth < 0)
            {
                reason = $"パーツ[{i}]のtreeDepthが負の値です（{depth}）。";
                return false;
            }

            if (i > 0)
            {
                int previousDepth = parts[i - 1].treeDepth;
                if (depth == 0)
                {
                    reason = $"パーツ[{i}]が2個目以降のルートになっています。";
                    return false;
                }

                if (depth > previousDepth + 1)
                {
                    reason = $"パーツ[{i}]のtreeDepthが2段以上増加しています（{previousDepth}→{depth}）。";
                    return false;
                }
            }
        }

        for (int i = 1; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            for (int parentIndex = i - 1; parentIndex >= 0; parentIndex--)
            {
                if (parts[parentIndex].treeDepth == depth - 1)
                {
                    childCounts[parentIndex]++;
                    break;
                }
            }
        }

        reason = "";
        return true;
    }

    static bool TryBuildDepthsFromChildCounts(
        IList<hod2v0_Part> parts,
        out int[] treeDepths,
        out string reason)
    {
        treeDepths = new int[parts.Count];
        List<int> remainingChildren = new List<int>();

        if (parts[0].childCount < 0)
        {
            reason = $"パーツ[0]のchildCountが負の値です（{parts[0].childCount}）。";
            return false;
        }

        treeDepths[0] = 0;
        remainingChildren.Add(parts[0].childCount);

        for (int i = 1; i < parts.Count; i++)
        {
            if (parts[i].childCount < 0)
            {
                reason = $"パーツ[{i}]のchildCountが負の値です（{parts[i].childCount}）。";
                return false;
            }

            while (remainingChildren.Count > 0
                && remainingChildren[remainingChildren.Count - 1] == 0)
            {
                remainingChildren.RemoveAt(remainingChildren.Count - 1);
            }

            if (remainingChildren.Count == 0)
            {
                reason = $"パーツ[{i}]を接続できる親のchildCount枠がありません。";
                return false;
            }

            int parentIndex = remainingChildren.Count - 1;
            remainingChildren[parentIndex]--;
            treeDepths[i] = remainingChildren.Count;
            remainingChildren.Add(parts[i].childCount);
        }

        while (remainingChildren.Count > 0
            && remainingChildren[remainingChildren.Count - 1] == 0)
        {
            remainingChildren.RemoveAt(remainingChildren.Count - 1);
        }

        if (remainingChildren.Count > 0)
        {
            int missingCount = 0;
            for (int i = 0; i < remainingChildren.Count; i++)
                missingCount += remainingChildren[i];
            reason = $"childCountが要求する子パーツが{missingCount}個不足しています。";
            return false;
        }

        reason = "";
        return true;
    }

    static int[] CopyDepths(IList<hod2v0_Part> parts)
    {
        int[] values = new int[parts.Count];
        for (int i = 0; i < parts.Count; i++)
            values[i] = parts[i].treeDepth;
        return values;
    }

    static int[] CopyChildCounts(IList<hod2v0_Part> parts)
    {
        int[] values = new int[parts.Count];
        for (int i = 0; i < parts.Count; i++)
            values[i] = parts[i].childCount;
        return values;
    }

    static bool ArraysEqual(int[] left, int[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }
        return true;
    }

    static HodHierarchyRepairPlan NewPlan(
        HodHierarchyRepairKind kind,
        string summary,
        string details,
        int[] originalDepths,
        int[] originalCounts,
        int[] repairedDepths,
        int[] repairedCounts)
    {
        return new HodHierarchyRepairPlan(
            kind, summary, details,
            originalDepths, originalCounts, repairedDepths, repairedCounts);
    }
}