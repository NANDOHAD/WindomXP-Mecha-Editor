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
            changes.Add(UILocalization.Get(
                "hod.repair.preview_change",
                "パーツ[{0}]「{1}」: treeDepth {2}→{3}, childCount {4}→{5}",
                i,
                name,
                originalTreeDepths[i],
                repairedTreeDepths[i],
                originalChildCounts[i],
                repairedChildCounts[i]));
        }

        string preview = string.Join("\n", changes);
        if (changeCount > changes.Count)
            preview += UILocalization.Get(
                "hod.repair.preview_more",
                "\nほか{0}件を修復します。",
                changeCount - changes.Count);
        return preview;
    }

    public bool CanApplyTo(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = UILocalization.Get(
                "hod.repair.no_unique_method",
                "この不整合には一意な自動修復方法がありません。");
            return false;
        }

        return TryValidateTarget(ani, out error);
    }

    
    public bool TryApply(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = UILocalization.Get(
                "hod.repair.no_unique_method",
                "この不整合には一意な自動修復方法がありません。");
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
            error = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        int partCount = repairedTreeDepths == null ? 0 : repairedTreeDepths.Length;
        if (ani.structure.parts.Count != partCount)
        {
            error = UILocalization.Get(
                "hod.repair.structure_part_count",
                "構造HODのパーツ数が修復計画と一致しません（{0}/{1}）。",
                ani.structure.parts.Count,
                partCount);
            return false;
        }

        for (int i = 0; i < partCount; i++)
        {
            hod2v0_Part structurePart = ani.structure.parts[i];
            if (structurePart.treeDepth != originalTreeDepths[i]
                || structurePart.childCount != originalChildCounts[i])
            {
                error = UILocalization.Get(
                    "hod.repair.structure_changed",
                    "構造HODのパーツ[{0}]が修復計画の作成後に変更されています。",
                    i);
                return false;
            }
        }

        if (ani.animations == null)
        {
            error = UILocalization.Get("hod.repair.no_animations", "アニメーション情報がありません。");
            return false;
        }

        for (int animationIndex = 0; animationIndex < ani.animations.Count; animationIndex++)
        {
            animation animationData = ani.animations[animationIndex];
            if (animationData == null || animationData.frames == null)
            {
                error = UILocalization.Get(
                    "hod.repair.animation_frames_missing",
                    "アニメーション[{0}]のフレーム情報がありません。",
                    animationIndex);
                return false;
            }

            for (int frameIndex = 0; frameIndex < animationData.frames.Count; frameIndex++)
            {
                hod2v1 frame = animationData.frames[frameIndex];
                if (frame == null || frame.parts == null)
                {
                    error = UILocalization.Get(
                        "hod.repair.frame_parts_missing",
                        "アニメーション[{0}] フレーム[{1}]のパーツ情報がありません。",
                        animationIndex,
                        frameIndex);
                    return false;
                }

                if (frame.parts.Count != partCount)
                {
                    error = UILocalization.Get(
                        "hod.repair.frame_part_count",
                        "アニメーション[{0}] フレーム[{1}]のパーツ数が構造HODと一致しません（{2}/{3}）。",
                        animationIndex,
                        frameIndex,
                        frame.parts.Count,
                        partCount);
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
                        error = UILocalization.Get(
                            "hod.repair.frame_order_mismatch",
                            "アニメーション[{0}] フレーム[{1}]のパーツ順が構造HODと一致しません（位置{2}: 「{3}」/「{4}」）。",
                            animationIndex,
                            frameIndex,
                            partIndex,
                            frameName,
                            structureName);
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
                UILocalization.Get(
                    "hod.repair.tree_depth.no_parts_summary",
                    "パーツ情報がないためtreeDepthを正として修復できません。"),
                UILocalization.Get(
                    "hod.repair.tree_depth.no_parts_details",
                    "treeDepthから階層を復元できません。"),
                null, null, null, null);
        }

        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);
        int[] childCountsFromDepth;
        string reason;
        if (!TryBuildChildCountsFromDepths(parts, out childCountsFromDepth, out reason))
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                UILocalization.Get(
                    "hod.repair.tree_depth.unrepairable",
                    "treeDepthを正として修復できません。"), reason,
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        return NewPlan(HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth,
            UILocalization.Get(
                "hod.repair.tree_depth.summary",
                "treeDepthを正としてchildCountを修復します。"),
            UILocalization.Get(
                "hod.repair.tree_depth.details",
                "treeDepthが表す階層を維持し、全パーツのchildCountを再計算します。"),
            originalDepths, originalCounts, originalDepths, childCountsFromDepth);
    }

    public static HodHierarchyRepairPlan CreatePlanUsingChildCount(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                UILocalization.Get(
                    "hod.repair.child_count.no_parts_summary",
                    "パーツ情報がないためchildCountを正として修復できません。"),
                UILocalization.Get(
                    "hod.repair.child_count.no_parts_details",
                    "childCountから階層を復元できません。"),
                null, null, null, null);
        }

        int[] originalDepths = CopyDepths(parts);
        int[] originalCounts = CopyChildCounts(parts);
        int[] treeDepthsFromCounts;
        string reason;
        if (!TryBuildDepthsFromChildCounts(parts, out treeDepthsFromCounts, out reason))
        {
            return NewPlan(HodHierarchyRepairKind.Unrepairable,
                UILocalization.Get(
                    "hod.repair.child_count.unrepairable",
                    "childCountを正として修復できません。"), reason,
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        return NewPlan(HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts,
            UILocalization.Get(
                "hod.repair.child_count.summary",
                "childCountを正としてtreeDepthを修復します。"),
            UILocalization.Get(
                "hod.repair.child_count.details",
                "childCountが表す階層を維持し、全パーツのtreeDepthを再計算します。"),
            originalDepths, originalCounts, treeDepthsFromCounts, originalCounts);
    }

    public static HodHierarchyRepairPlan CreatePlan(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
        {
            return NewPlan(
                HodHierarchyRepairKind.Unrepairable,
                UILocalization.Get(
                    "hod.repair.no_parts_summary",
                    "パーツ情報がないため修復できません。"),
                UILocalization.Get(
                    "hod.repair.no_parts_details",
                    "treeDepthとchildCountのどちらからも階層を復元できません。"),
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
                    UILocalization.Get(
                        "hod.repair.no_inconsistency",
                        "パーツ階層に不整合はありません。"),
                    "",
                    originalDepths, originalCounts, originalDepths, originalCounts);
            }

            return NewPlan(
                HodHierarchyRepairKind.Ambiguous,
                UILocalization.Get(
                    "hod.repair.ambiguous.summary",
                    "treeDepthとchildCountが、それぞれ別の有効な階層を表しています。"),
                UILocalization.Get(
                    "hod.repair.ambiguous.details",
                    "どちらを正しい値とみなすか一意に決められないため、自動修復は行いません。"),
                originalDepths, originalCounts, originalDepths, originalCounts);
        }

        if (depthValid)
        {
            return NewPlan(
                HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth,
                UILocalization.Get(
                    "hod.repair.tree_depth.can_repair_summary",
                    "treeDepthを正としてchildCountを修復できます。"),
                UILocalization.Get(
                    "hod.repair.tree_depth.can_repair_details",
                    "treeDepthの並びは有効ですが、childCountからは有効な階層を復元できません。"),
                originalDepths, originalCounts, originalDepths, childCountsFromDepth);
        }

        if (childCountsValid)
        {
            return NewPlan(
                HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts,
                UILocalization.Get(
                    "hod.repair.child_count.can_repair_summary",
                    "childCountを正としてtreeDepthを修復できます。"),
                UILocalization.Get(
                    "hod.repair.child_count.can_repair_details",
                    "childCountの並びは有効ですが、treeDepthからは有効な階層を復元できません。"),
                originalDepths, originalCounts, treeDepthsFromCounts, originalCounts);
        }

        return NewPlan(
            HodHierarchyRepairKind.Unrepairable,
            UILocalization.Get(
                "hod.repair.both_invalid.summary",
                "treeDepthとchildCountの両方に不整合があるため修復できません。"),
            UILocalization.Get(
                "hod.repair.both_invalid.details",
                "treeDepth: {0}\nchildCount: {1}",
                depthReason,
                childCountReason),
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
            reason = UILocalization.Get(
                "hod.repair.reason.first_tree_depth",
                "先頭パーツのtreeDepthが0ではありません（{0}）。",
                parts[0].treeDepth);
            return false;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            if (depth < 0)
            {
                reason = UILocalization.Get(
                    "hod.repair.reason.tree_depth_negative",
                    "パーツ[{0}]のtreeDepthが負の値です（{1}）。",
                    i,
                    depth);
                return false;
            }

            if (i > 0)
            {
                int previousDepth = parts[i - 1].treeDepth;
                if (depth == 0)
                {
                    reason = UILocalization.Get(
                        "hod.repair.reason.additional_root",
                        "パーツ[{0}]が2個目以降のルートになっています。",
                        i);
                    return false;
                }

                if (depth > previousDepth + 1)
                {
                    reason = UILocalization.Get(
                        "hod.repair.reason.tree_depth_jump",
                        "パーツ[{0}]のtreeDepthが2段以上増加しています（{1}→{2}）。",
                        i,
                        previousDepth,
                        depth);
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
            reason = UILocalization.Get(
                "hod.repair.reason.child_count_negative",
                "パーツ[{0}]のchildCountが負の値です（{1}）。",
                0,
                parts[0].childCount);
            return false;
        }

        treeDepths[0] = 0;
        remainingChildren.Add(parts[0].childCount);

        for (int i = 1; i < parts.Count; i++)
        {
            if (parts[i].childCount < 0)
            {
                reason = UILocalization.Get(
                    "hod.repair.reason.child_count_negative",
                    "パーツ[{0}]のchildCountが負の値です（{1}）。",
                    i,
                    parts[i].childCount);
                return false;
            }

            while (remainingChildren.Count > 0
                && remainingChildren[remainingChildren.Count - 1] == 0)
            {
                remainingChildren.RemoveAt(remainingChildren.Count - 1);
            }

            if (remainingChildren.Count == 0)
            {
                reason = UILocalization.Get(
                    "hod.repair.reason.no_parent_slot",
                    "パーツ[{0}]を接続できる親のchildCount枠がありません。",
                    i);
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
            reason = UILocalization.Get(
                "hod.repair.reason.missing_children",
                "childCountが要求する子パーツが{0}個不足しています。",
                missingCount);
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
