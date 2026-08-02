using System.Collections.Generic;

public enum HodHierarchyPruneKind
{
    Unavailable,
    DeterministicOrphanRanges
}

struct HodHierarchyPruneRange
{
    public int start;
    public int count;

    public HodHierarchyPruneRange(int startIndex, int removeCount)
    {
        start = startIndex;
        count = removeCount;
    }
}

public sealed class HodHierarchyPrunePlan
{
    const int MaxPreviewRanges = 8;

    readonly int[] originalTreeDepths;
    readonly int[] originalChildCounts;
    readonly string[] originalNames;
    readonly HodHierarchyPruneRange[] ranges;
    readonly int[] repairedTreeDepths;
    readonly int[] repairedChildCounts;

    public HodHierarchyPruneKind Kind { get; private set; }
    public string Summary { get; private set; }
    public string Details { get; private set; }
    public int RemovedPartCount { get; private set; }
    public bool CanApply
    {
        get { return Kind == HodHierarchyPruneKind.DeterministicOrphanRanges; }
    }

    internal HodHierarchyPrunePlan(
        HodHierarchyPruneKind kind,
        string summary,
        string details,
        int removedPartCount,
        int[] originalDepths,
        int[] originalCounts,
        string[] names,
        HodHierarchyPruneRange[] pruneRanges,
        int[] targetDepths,
        int[] targetCounts)
    {
        Kind = kind;
        Summary = summary;
        Details = details;
        RemovedPartCount = removedPartCount;
        originalTreeDepths = originalDepths;
        originalChildCounts = originalCounts;
        originalNames = names;
        ranges = pruneRanges;
        repairedTreeDepths = targetDepths;
        repairedChildCounts = targetCounts;
    }

    public string BuildPreview(IList<hod2v0_Part> parts)
    {
        if (!CanApply || parts == null || ranges == null)
            return "";

        List<string> lines = new List<string>();
        for (int i = 0; i < ranges.Length && i < MaxPreviewRanges; i++)
        {
            HodHierarchyPruneRange range = ranges[i];
            string name = range.start < parts.Count && !string.IsNullOrEmpty(parts[range.start].name)
                ? parts[range.start].name
                : "<名称なし>";
            int descendantCount = range.count - 1;
            string descendants = descendantCount > 0 ? $"（配下{descendantCount}個を含む）" : "";
            lines.Add($"パーツ[{range.start}]「{name}」{descendants}");
        }

        if (ranges.Length > lines.Count)
            lines.Add($"ほか{ranges.Length - lines.Count}範囲");
        lines.Add($"合計{RemovedPartCount}パーツを除外します。");
        return string.Join("\n", lines);
    }

    public bool CanApplyTo(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = "安全に除外できる孤立パーツを一意に特定できません。";
            return false;
        }

        return TryValidateTarget(ani, out error);
    }

    public bool TryApply(ani2 ani, out string error)
    {
        if (!CanApplyTo(ani, out error))
            return false;

        List<hod2v0_Part> newStructureParts = BuildStructureParts(ani.structure.parts);
        List<hod2v1> targetFrames = new List<hod2v1>();
        List<List<hod2v1_Part>> newFrameParts = new List<List<hod2v1_Part>>();

        foreach (animation animationData in ani.animations)
        {
            foreach (hod2v1 frame in animationData.frames)
            {
                targetFrames.Add(frame);
                newFrameParts.Add(BuildFrameParts(frame.parts));
            }
        }

        if (newStructureParts.Count != repairedTreeDepths.Length)
        {
            error = "除外後の構造HODパーツ数が修復計画と一致しません。";
            return false;
        }

        if (!HodHierarchyValidator.TryValidate(newStructureParts, out error))
        {
            error = "除外後の階層検証に失敗しました。\n" + error;
            return false;
        }

        ani.structure.parts = newStructureParts;
        for (int i = 0; i < targetFrames.Count; i++)
            targetFrames[i].parts = newFrameParts[i];

        error = "";
        return true;
    }

    List<hod2v0_Part> BuildStructureParts(IList<hod2v0_Part> sourceParts)
    {
        List<hod2v0_Part> result = new List<hod2v0_Part>();
        int rangeIndex = 0;
        int targetIndex = 0;
        int sourceIndex = 0;

        while (sourceIndex < sourceParts.Count)
        {
            if (rangeIndex < ranges.Length && sourceIndex == ranges[rangeIndex].start)
            {
                sourceIndex += ranges[rangeIndex].count;
                rangeIndex++;
                continue;
            }

            hod2v0_Part part = sourceParts[sourceIndex];
            part.treeDepth = repairedTreeDepths[targetIndex];
            part.childCount = repairedChildCounts[targetIndex];
            result.Add(part);
            sourceIndex++;
            targetIndex++;
        }

        return result;
    }

    List<hod2v1_Part> BuildFrameParts(IList<hod2v1_Part> sourceParts)
    {
        List<hod2v1_Part> result = new List<hod2v1_Part>();
        int rangeIndex = 0;
        int targetIndex = 0;
        int sourceIndex = 0;

        while (sourceIndex < sourceParts.Count)
        {
            if (rangeIndex < ranges.Length && sourceIndex == ranges[rangeIndex].start)
            {
                sourceIndex += ranges[rangeIndex].count;
                rangeIndex++;
                continue;
            }

            hod2v1_Part part = sourceParts[sourceIndex];
            part.treeDepth = repairedTreeDepths[targetIndex];
            part.childCount = repairedChildCounts[targetIndex];
            result.Add(part);
            sourceIndex++;
            targetIndex++;
        }

        return result;
    }

    
    bool TryValidateTarget(ani2 ani, out string error)
    {
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            error = "構造HODがありません。";
            return false;
        }

        int originalPartCount = originalTreeDepths == null ? 0 : originalTreeDepths.Length;
        if (ani.structure.parts.Count != originalPartCount)
        {
            error = $"構造HODのパーツ数が除外計画と一致しません（{ani.structure.parts.Count}/{originalPartCount}）。";
            return false;
        }

        for (int i = 0; i < originalPartCount; i++)
        {
            hod2v0_Part part = ani.structure.parts[i];
            if (part.treeDepth != originalTreeDepths[i]
                || part.childCount != originalChildCounts[i]
                || !string.Equals(part.name, originalNames[i], System.StringComparison.Ordinal))
            {
                error = $"構造HODのパーツ[{i}]が除外計画の作成後に変更されています。";
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

                if (frame.parts.Count != originalPartCount)
                {
                    error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ数が"
                        + $"構造HODと一致しません（{frame.parts.Count}/{originalPartCount}）。";
                    return false;
                }

                for (int partIndex = 0; partIndex < originalPartCount; partIndex++)
                {
                    hod2v1_Part framePart = frame.parts[partIndex];
                    if (framePart.treeDepth != originalTreeDepths[partIndex]
                        || framePart.childCount != originalChildCounts[partIndex])
                    {
                        error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]の階層情報が"
                            + $"構造HODと一致しません（位置{partIndex}）。";
                        return false;
                    }

                    string structureName = originalNames[partIndex];
                    if (!string.IsNullOrEmpty(structureName)
                        && !string.IsNullOrEmpty(framePart.name)
                        && !string.Equals(structureName, framePart.name, System.StringComparison.Ordinal))
                    {
                        error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ順が"
                            + $"構造HODと一致しません（位置{partIndex}: 「{framePart.name}」/「{structureName}」）。";
                        return false;
                    }
                }
            }
        }

        error = "";
        return true;
    }
}

public static class HodHierarchyPrune
{
    public static HodHierarchyPrunePlan CreatePlan(
        IList<hod2v0_Part> parts,
        HodHierarchyRepairPlan repairPlan)
    {
        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);
        string[] originalNames = CopyNames(parts);

        if (parts == null || parts.Count == 0)
        {
            return Unavailable(
                "パーツ情報がないため除外できません。",
                originalDepths, originalCounts, originalNames);
        }

        if (repairPlan == null)
            repairPlan = HodHierarchyRepair.CreatePlan(parts);

        if (repairPlan.Kind != HodHierarchyRepairKind.Unrepairable)
        {
            return Unavailable(
                "値の修復が可能、または削除対象が曖昧なため、自動除外は行いません。",
                originalDepths, originalCounts, originalNames);
        }

        if (parts[0].treeDepth != 0)
        {
            return Unavailable(
                "先頭ルートのtreeDepthが不正なため、安全な除外範囲を決められません。",
                originalDepths, originalCounts, originalNames);
        }

        List<HodHierarchyPruneRange> ranges = new List<HodHierarchyPruneRange>();
        int lastRetainedDepth = 0;
        int index = 1;
        while (index < parts.Count)
        {
            int depth = parts[index].treeDepth;
            if (depth < 0)
            {
                return Unavailable(
                    $"パーツ[{index}]のtreeDepthが負のため、子階層の範囲を決められません。",
                    originalDepths, originalCounts, originalNames);
            }

            bool isOrphan = depth == 0 || depth > lastRetainedDepth + 1;
            if (!isOrphan)
            {
                lastRetainedDepth = depth;
                index++;
                continue;
            }

            int endIndex = index + 1;
            while (endIndex < parts.Count && parts[endIndex].treeDepth > depth)
                endIndex++;

            ranges.Add(new HodHierarchyPruneRange(index, endIndex - index));
            index = endIndex;
        }

        if (ranges.Count == 0)
        {
            return Unavailable(
                "親へ接続不能な孤立パーツを一意に特定できません。",
                originalDepths, originalCounts, originalNames);
        }

        List<hod2v0_Part> retainedParts = CopyWithoutRanges(parts, ranges);
        if (retainedParts.Count == 0 || retainedParts[0].treeDepth != 0)
        {
            return Unavailable(
                "除外後に有効なルートパーツが残りません。",
                originalDepths, originalCounts, originalNames);
        }

        int[] repairedDepths = CopyDepths(retainedParts);
        int[] repairedCounts;
        string depthError;
        if (!TryBuildChildCountsFromDepths(retainedParts, out repairedCounts, out depthError))
        {
            return Unavailable(
                "除外後のtreeDepthを有効な階層として構築できません。\n" + depthError,
                originalDepths, originalCounts, originalNames);
        }

        for (int i = 0; i < retainedParts.Count; i++)
        {
            hod2v0_Part part = retainedParts[i];
            part.childCount = repairedCounts[i];
            retainedParts[i] = part;
        }

        string validationError;
        if (!HodHierarchyValidator.TryValidate(retainedParts, out validationError))
        {
            return Unavailable(
                "除外後の階層検証に失敗しました。\n" + validationError,
                originalDepths, originalCounts, originalNames);
        }

        int removedCount = 0;
        for (int i = 0; i < ranges.Count; i++)
            removedCount += ranges[i].count;

        return new HodHierarchyPrunePlan(
            HodHierarchyPruneKind.DeterministicOrphanRanges,
            $"親へ接続できない孤立パーツを{removedCount}個除外できます。",
            "除外後は残ったtreeDepthを正としてchildCountを再構築します。",
            removedCount,
            originalDepths,
            originalCounts,
            originalNames,
            ranges.ToArray(),
            repairedDepths,
            repairedCounts);
    }

    static List<hod2v0_Part> CopyWithoutRanges(
        IList<hod2v0_Part> parts,
        IList<HodHierarchyPruneRange> ranges)
    {
        List<hod2v0_Part> retained = new List<hod2v0_Part>();
        int rangeIndex = 0;
        int index = 0;
        while (index < parts.Count)
        {
            if (rangeIndex < ranges.Count && index == ranges[rangeIndex].start)
            {
                index += ranges[rangeIndex].count;
                rangeIndex++;
                continue;
            }

            retained.Add(parts[index]);
            index++;
        }
        return retained;
    }

    static bool TryBuildChildCountsFromDepths(
        IList<hod2v0_Part> parts,
        out int[] childCounts,
        out string error)
    {
        childCounts = new int[parts.Count];
        if (parts.Count == 0 || parts[0].treeDepth != 0)
        {
            error = "先頭パーツがルートではありません。";
            return false;
        }

        int previousDepth = 0;
        for (int i = 1; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            if (depth <= 0 || depth > previousDepth + 1)
            {
                error = $"パーツ[{i}]を親階層へ接続できません（treeDepth={depth}）。";
                return false;
            }

            bool parentFound = false;
            for (int parentIndex = i - 1; parentIndex >= 0; parentIndex--)
            {
                if (parts[parentIndex].treeDepth == depth - 1)
                {
                    childCounts[parentIndex]++;
                    parentFound = true;
                    break;
                }
            }

            if (!parentFound)
            {
                error = $"パーツ[{i}]の親候補がありません。";
                return false;
            }

            previousDepth = depth;
        }

        error = "";
        return true;
    }

    static int[] CopyDepths(IList<hod2v0_Part> parts)
    {
        if (parts == null)
            return null;
        int[] values = new int[parts.Count];
        for (int i = 0; i < parts.Count; i++)
            values[i] = parts[i].treeDepth;
        return values;
    }

    static int[] CopyChildCounts(IList<hod2v0_Part> parts)
    {
        if (parts == null)
            return null;
        int[] values = new int[parts.Count];
        for (int i = 0; i < parts.Count; i++)
            values[i] = parts[i].childCount;
        return values;
    }

    static string[] CopyNames(IList<hod2v0_Part> parts)
    {
        if (parts == null)
            return null;
        string[] values = new string[parts.Count];
        for (int i = 0; i < parts.Count; i++)
            values[i] = parts[i].name;
        return values;
    }

    static HodHierarchyPrunePlan Unavailable(
        string details,
        int[] originalDepths,
        int[] originalCounts,
        string[] originalNames)
    {
        return new HodHierarchyPrunePlan(
            HodHierarchyPruneKind.Unavailable,
            "安全に除外できる不整合パーツはありません。",
            details,
            0,
            originalDepths,
            originalCounts,
            originalNames,
            null,
            null,
            null);
    }
}
