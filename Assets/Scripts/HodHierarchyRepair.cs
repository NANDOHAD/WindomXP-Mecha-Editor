using System;
using System.Collections.Generic;
using UnityEngine;

public enum HodHierarchyRepairKind
{
    None,
    RebuildChildCountsFromTreeDepth,
    RebuildTreeDepthFromChildCounts,
    Ambiguous,
    Unrepairable
}

internal sealed class HodFrameOrderRepairPlan
{
    sealed class FrameReplacement
    {
        public hod2v1 Frame;
        public List<hod2v1_Part> Parts;
    }

    readonly List<FrameReplacement> replacements;
    readonly bool requiresHierarchySynchronization;

    HodFrameOrderRepairPlan(
        List<FrameReplacement> replacements,
        bool requiresHierarchySynchronization)
    {
        this.replacements = replacements;
        this.requiresHierarchySynchronization = requiresHierarchySynchronization;
    }

    public bool RequiresChanges =>
        requiresHierarchySynchronization || replacements.Count > 0;

    public void Apply()
    {
        for (int i = 0; i < replacements.Count; i++)
            replacements[i].Frame.parts = replacements[i].Parts;
    }

    public static bool TryCreate(
        ani2 ani,
        IList<hod2v0_Part> structureParts,
        out HodFrameOrderRepairPlan plan,
        out string error)
    {
        plan = null;
        if (ani == null || structureParts == null)
        {
            error = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        if (ani.animations == null)
        {
            error = UILocalization.Get("hod.repair.no_animations", "アニメーション情報がありません。");
            return false;
        }

        Dictionary<string, int> structureIndexByName;
        bool hasUniqueStructureNames = TryBuildStructureIndex(
            structureParts, out structureIndexByName);
        List<FrameReplacement> pendingReplacements = new List<FrameReplacement>();
        bool requiresHierarchySynchronization = false;

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

                if (frame.parts.Count != structureParts.Count)
                {
                    error = UILocalization.Get(
                        "hod.repair.frame_part_count",
                        "アニメーション[{0}] フレーム[{1}]のパーツ数が構造HODと一致しません（{2}/{3}）。",
                        animationIndex,
                        frameIndex,
                        frame.parts.Count,
                        structureParts.Count);
                    return false;
                }

                bool hierarchyMatches = HierarchyMatchesAtEveryPosition(
                    frame.parts, structureParts);

                // 旧ANIではフレーム側のnameが表示名・状態名として変化し、重複も許される。
                // ただし構造HODとフレームHODの名前が一意に対応する場合は、階層列が
                // 同じでも名前の対応を使って構造HODの順序へ並べ替える。兄弟パーツの
                // 入れ替えではtreeDepth/childCountが同じになり、階層列だけでは順序差を
                // 検出できないため、この判定を階層列の一致より先に行う。
                List<hod2v1_Part> reorderedParts;
                bool orderChanged;
                if (TryBuildPartsInStructureOrder(
                    frame.parts,
                    structureParts,
                    hasUniqueStructureNames,
                    structureIndexByName,
                    out reorderedParts,
                    out orderChanged))
                {
                    if (orderChanged)
                    {
                        pendingReplacements.Add(new FrameReplacement
                        {
                            Frame = frame,
                            Parts = reorderedParts
                        });
                    }

                    if (!hierarchyMatches)
                        requiresHierarchySynchronization = true;
                    continue;
                }

                // 旧ANI固有の短縮・重複ラベルしかない場合は、階層列が一致している
                // 既存データのindex対応を維持する。階層列まで違う場合は、安全な対応を
                // 推測せず変換を中止する。
                if (hierarchyMatches)
                    continue;

                requiresHierarchySynchronization = true;
                error = BuildUnresolvedOrderError(
                    animationIndex, frameIndex, frame.parts, structureParts);
                return false;
            }
        }

        plan = new HodFrameOrderRepairPlan(
            pendingReplacements,
            requiresHierarchySynchronization);
        error = "";
        return true;
    }

    public static bool TryCreateBestEffort(
        ani2 ani,
        IList<hod2v0_Part> structureParts,
        out HodFrameOrderRepairPlan plan,
        out string warning)
    {
        plan = null;
        warning = "";
        if (ani == null || structureParts == null || structureParts.Count == 0)
        {
            warning = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        if (ani.animations == null)
        {
            warning = UILocalization.Get("hod.repair.no_animations", "アニメーション情報がありません。");
            return false;
        }

        Dictionary<string, int> structureIndexByName;
        bool hasUniqueStructureNames = TryBuildStructureIndex(
            structureParts, out structureIndexByName);
        List<FrameReplacement> pendingReplacements = new List<FrameReplacement>();
        int unresolvedFrameCount = 0;
        int fallbackFrameCount = 0;
        int heuristicFrameCount = 0;
        int normalizedFrameCount = 0;
        int insertedPartCount = 0;
        int droppedPartCount = 0;

        for (int animationIndex = 0; animationIndex < ani.animations.Count; animationIndex++)
        {
            animation animationData = ani.animations[animationIndex];
            if (animationData == null || animationData.frames == null)
            {
                unresolvedFrameCount++;
                continue;
            }

            for (int frameIndex = 0; frameIndex < animationData.frames.Count; frameIndex++)
            {
                hod2v1 frame = animationData.frames[frameIndex];
                if (frame == null || frame.parts == null)
                {
                    unresolvedFrameCount++;
                    continue;
                }

                List<hod2v1_Part> normalizedParts;
                bool orderChanged;
                int namedMatches;
                int heuristicMatches;
                int frameFallbacks;
                int frameInserted;
                int frameDropped;
                if (!TryBuildBestEffortPartsInStructureOrder(
                    frame.parts,
                    structureParts,
                    hasUniqueStructureNames,
                    structureIndexByName,
                    out normalizedParts,
                    out orderChanged,
                    out namedMatches,
                    out heuristicMatches,
                    out frameFallbacks,
                    out frameInserted,
                    out frameDropped))
                {
                    unresolvedFrameCount++;
                    continue;
                }

                if (orderChanged || frame.parts.Count != structureParts.Count)
                {
                    pendingReplacements.Add(new FrameReplacement
                    {
                        Frame = frame,
                        Parts = normalizedParts
                    });
                }

                if (heuristicMatches > 0)
                    heuristicFrameCount++;
                if (frameFallbacks > 0)
                    fallbackFrameCount++;
                if (frameInserted > 0 || frameDropped > 0)
                    normalizedFrameCount++;
                insertedPartCount += frameInserted;
                droppedPartCount += frameDropped;
            }
        }

        if (unresolvedFrameCount > 0)
        {
            warning = AppendWarning(warning, UILocalization.Get(
                "hod.repair.best_effort.unresolved_frames",
                "内容が欠落したフレーム{0}件は既定姿勢を生成して変換を継続します。",
                unresolvedFrameCount));
        }
        if (heuristicFrameCount > 0)
        {
            warning = AppendWarning(warning, UILocalization.Get(
                "hod.repair.best_effort.heuristic_frames",
                "名前の正規化または階層列から推定して対応付けたフレーム{0}件があります。",
                heuristicFrameCount));
        }
        if (fallbackFrameCount > 0 || !hasUniqueStructureNames)
        {
            warning = AppendWarning(warning, UILocalization.Get(
                "hod.repair.best_effort.fallback_frames",
                "一意に判定できないフレーム{0}件はindex順を仮対応として変換しました。",
                fallbackFrameCount + (!hasUniqueStructureNames ? 1 : 0)));
        }
        if (normalizedFrameCount > 0)
        {
            warning = AppendWarning(warning, UILocalization.Get(
                "hod.repair.best_effort.normalized_frames",
                "パーツ数を構造HODへ合わせたフレーム{0}件（追加{1}／除外{2}）があります。",
                normalizedFrameCount,
                insertedPartCount,
                droppedPartCount));
        }
        if (pendingReplacements.Count > 0)
        {
            warning = AppendWarning(warning, UILocalization.Get(
                "hod.repair.best_effort.reordered_frames",
                "構造HODの順序を基準に{0}フレームを再構成しました。",
                pendingReplacements.Count));
        }
        plan = new HodFrameOrderRepairPlan(pendingReplacements, false);
        return true;
    }

    static string AppendWarning(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right ?? "";
        if (string.IsNullOrEmpty(right))
            return left;
        return left + "\n" + right;
    }

    static bool HierarchyMatchesAtEveryPosition(
        IList<hod2v1_Part> frameParts,
        IList<hod2v0_Part> structureParts)
    {
        for (int i = 0; i < structureParts.Count; i++)
        {
            if (frameParts[i].treeDepth != structureParts[i].treeDepth
                || frameParts[i].childCount != structureParts[i].childCount)
                return false;
        }

        return true;
    }

    static bool TryBuildPartsInStructureOrder(
        IList<hod2v1_Part> frameParts,
        IList<hod2v0_Part> structureParts,
        bool hasUniqueStructureNames,
        Dictionary<string, int> structureIndexByName,
        out List<hod2v1_Part> reorderedParts,
        out bool orderChanged)
    {
        reorderedParts = null;
        orderChanged = false;
        if (!hasUniqueStructureNames)
            return false;

        Dictionary<string, int> frameIndexByName;
        if (!TryBuildFrameIndex(frameParts, out frameIndexByName)
            || frameIndexByName.Count != structureParts.Count)
            return false;

        reorderedParts = new List<hod2v1_Part>(structureParts.Count);
        for (int partIndex = 0; partIndex < structureParts.Count; partIndex++)
        {
            int sourceIndex;
            if (!frameIndexByName.TryGetValue(
                    structureParts[partIndex].name, out sourceIndex))
            {
                reorderedParts = null;
                orderChanged = false;
                return false;
            }

            reorderedParts.Add(frameParts[sourceIndex]);
            if (sourceIndex != partIndex)
                orderChanged = true;
        }

        return true;
    }

    static bool TryBuildBestEffortPartsInStructureOrder(
        IList<hod2v1_Part> frameParts,
        IList<hod2v0_Part> structureParts,
        bool hasUniqueStructureNames,
        Dictionary<string, int> structureIndexByName,
        out List<hod2v1_Part> normalizedParts,
        out bool orderChanged,
        out int namedMatches,
        out int heuristicMatches,
        out int fallbackCount,
        out int insertedCount,
        out int droppedCount)
    {
        normalizedParts = new List<hod2v1_Part>(structureParts.Count);
        orderChanged = false;
        bool metadataChanged = false;
        namedMatches = 0;
        heuristicMatches = 0;
        fallbackCount = 0;
        insertedCount = 0;
        droppedCount = 0;

        bool[] used = new bool[frameParts.Count];
        int[] sourceIndices = new int[structureParts.Count];
        for (int i = 0; i < sourceIndices.Length; i++)
            sourceIndices[i] = -1;

        // Pass 1: exact unique name match. The frame's name is normalized to
        // the structure name in the output because AN2 has no independent
        // legacy frame-label field.
        if (hasUniqueStructureNames)
        {
            for (int targetIndex = 0; targetIndex < structureParts.Count; targetIndex++)
            {
                string targetName = structureParts[targetIndex].name ?? "";
                int found = -1;
                for (int sourceIndex = 0; sourceIndex < frameParts.Count; sourceIndex++)
                {
                    if (used[sourceIndex])
                        continue;
                    if (string.Equals(frameParts[sourceIndex].name, targetName,
                        StringComparison.Ordinal))
                    {
                        found = sourceIndex;
                        break;
                    }
                }
                if (found >= 0)
                {
                    sourceIndices[targetIndex] = found;
                    used[found] = true;
                    namedMatches++;
                }
            }
        }

        // Pass 2: matching hierarchy columns is a useful, but explicitly
        // heuristic, signal for legacy HODs whose labels are display names.
        for (int targetIndex = 0; targetIndex < structureParts.Count; targetIndex++)
        {
            if (sourceIndices[targetIndex] >= 0)
                continue;
            int found = -1;
            int candidates = 0;
            for (int sourceIndex = 0; sourceIndex < frameParts.Count; sourceIndex++)
            {
                if (used[sourceIndex])
                    continue;
                hod2v1_Part candidate = frameParts[sourceIndex];
                if (candidate.treeDepth == structureParts[targetIndex].treeDepth
                    && candidate.childCount == structureParts[targetIndex].childCount)
                {
                    found = sourceIndex;
                    candidates++;
                }
            }
            if (candidates == 1)
            {
                sourceIndices[targetIndex] = found;
                used[found] = true;
                heuristicMatches++;
            }
        }

        // Pass 3: preserve the remaining relative index order. If counts
        // differ, generate an identity part for missing structure entries and
        // drop surplus frame entries. This is intentionally lossy but keeps a
        // valid AN2 record and emits a warning to the caller.
        int nextUnused = 0;
        for (int targetIndex = 0; targetIndex < structureParts.Count; targetIndex++)
        {
            if (sourceIndices[targetIndex] >= 0)
                continue;
            while (nextUnused < frameParts.Count && used[nextUnused])
                nextUnused++;
            if (nextUnused < frameParts.Count)
            {
                sourceIndices[targetIndex] = nextUnused;
                used[nextUnused] = true;
                fallbackCount++;
                nextUnused++;
            }
            else
            {
                fallbackCount++;
                insertedCount++;
            }
        }

        for (int sourceIndex = 0; sourceIndex < used.Length; sourceIndex++)
        {
            if (!used[sourceIndex])
                droppedCount++;
        }

        for (int targetIndex = 0; targetIndex < structureParts.Count; targetIndex++)
        {
            int sourceIndex = sourceIndices[targetIndex];
            hod2v1_Part part;
            if (sourceIndex >= 0)
            {
                part = frameParts[sourceIndex];
                if (sourceIndex != targetIndex)
                    orderChanged = true;
            }
            else
            {
                part = new hod2v1_Part
                {
                    rotation = Quaternion.identity,
                    scale = Vector3.one,
                    position = Vector3.zero,
                    unk1 = Quaternion.identity,
                    unk2 = Quaternion.identity,
                    unk3 = Quaternion.identity
                };
                orderChanged = true;
            }

            if (!string.Equals(part.name, structureParts[targetIndex].name,
                StringComparison.Ordinal)
                || part.treeDepth != structureParts[targetIndex].treeDepth
                || part.childCount != structureParts[targetIndex].childCount)
            {
                metadataChanged = true;
            }
            part.name = structureParts[targetIndex].name;
            part.treeDepth = structureParts[targetIndex].treeDepth;
            part.childCount = structureParts[targetIndex].childCount;
            normalizedParts.Add(part);
        }

        if (metadataChanged)
            orderChanged = true;

        return true;
    }

    static bool TryBuildStructureIndex(
        IList<hod2v0_Part> parts,
        out Dictionary<string, int> indexByName)
    {
        indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < parts.Count; i++)
        {
            string name = parts[i].name;
            if (string.IsNullOrEmpty(name) || indexByName.ContainsKey(name))
                return false;
            indexByName.Add(name, i);
        }

        return true;
    }

    static bool TryBuildFrameIndex(
        IList<hod2v1_Part> parts,
        out Dictionary<string, int> indexByName)
    {
        indexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < parts.Count; i++)
        {
            string name = parts[i].name;
            if (string.IsNullOrEmpty(name) || indexByName.ContainsKey(name))
                return false;
            indexByName.Add(name, i);
        }

        return true;
    }

    static string BuildUnresolvedOrderError(
        int animationIndex,
        int frameIndex,
        IList<hod2v1_Part> frameParts,
        IList<hod2v0_Part> structureParts)
    {
        int mismatchIndex = 0;
        for (int i = 0; i < structureParts.Count; i++)
        {
            if (!string.Equals(
                    frameParts[i].name,
                    structureParts[i].name,
                    StringComparison.Ordinal)
                || frameParts[i].treeDepth != structureParts[i].treeDepth
                || frameParts[i].childCount != structureParts[i].childCount)
            {
                mismatchIndex = i;
                break;
            }
        }

        return UILocalization.Get(
            "hod.repair.frame_order_mismatch",
            "アニメーション[{0}] フレーム[{1}]のパーツ順が構造HODと一致しません（位置{2}: 「{3}」/「{4}」）。",
            animationIndex,
            frameIndex,
            mismatchIndex,
            frameParts[mismatchIndex].name,
            structureParts[mismatchIndex].name);
    }
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

        HodFrameOrderRepairPlan ignored;
        return TryValidateTarget(ani, out ignored, out error);
    }

    
    internal bool TryApplyStructureOnly(ani2 ani, out string error)
    {
        if (!CanApply)
        {
            error = UILocalization.Get(
                "hod.repair.no_unique_method",
                "この不整合には一意な自動修復方法がありません。");
            return false;
        }

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
            hod2v0_Part part = ani.structure.parts[i];
            if (part.treeDepth != originalTreeDepths[i]
                || part.childCount != originalChildCounts[i])
            {
                error = UILocalization.Get(
                    "hod.repair.structure_changed",
                    "構造HODのパーツ[{0}]が修復計画の作成後に変更されています。",
                    i);
                return false;
            }
        }

        for (int i = 0; i < partCount; i++)
        {
            hod2v0_Part part = ani.structure.parts[i];
            part.treeDepth = repairedTreeDepths[i];
            part.childCount = repairedChildCounts[i];
            ani.structure.parts[i] = part;
        }

        error = "";
        return true;
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

        HodFrameOrderRepairPlan frameOrderPlan;
        if (!TryValidateTarget(ani, out frameOrderPlan, out error))
            return false;

        frameOrderPlan.Apply();

        if (!TryApplyStructureOnly(ani, out error))
            return false;

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

    bool TryValidateTarget(
        ani2 ani,
        out HodFrameOrderRepairPlan frameOrderPlan,
        out string error)
    {
        frameOrderPlan = null;
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

        return HodFrameOrderRepairPlan.TryCreate(
            ani, ani.structure.parts, out frameOrderPlan, out error);
    }
}

public enum LegacyAniHierarchyAuthority
{
    TreeDepth,
    ChildCount
}

public sealed class LegacyAniStructureEditPlan
{
    sealed class FrameHierarchySnapshot
    {
        public hod2v1 frame;
        public int[] treeDepths;
        public int[] childCounts;
    }

    readonly HodHierarchyRepairPlan treeDepthPlan;
    readonly HodHierarchyRepairPlan childCountPlan;
    readonly bool alreadyValid;

    public bool RequiresAuthorityChoice { get; private set; }
    public LegacyAniHierarchyAuthority RecommendedAuthority { get; private set; }
    public string Summary { get; private set; }
    public string Details { get; private set; }

    LegacyAniStructureEditPlan(
        HodHierarchyRepairPlan treePlan,
        HodHierarchyRepairPlan childPlan,
        bool valid,
        bool requiresChoice,
        LegacyAniHierarchyAuthority recommendedAuthority,
        string summary,
        string details)
    {
        treeDepthPlan = treePlan;
        childCountPlan = childPlan;
        alreadyValid = valid;
        RequiresAuthorityChoice = requiresChoice;
        RecommendedAuthority = recommendedAuthority;
        Summary = summary ?? "";
        Details = details ?? "";
    }

    public string BuildPreview(
        LegacyAniHierarchyAuthority authority,
        IList<hod2v0_Part> parts)
    {
        HodHierarchyRepairPlan plan = GetRepairPlan(authority);
        return plan != null ? plan.BuildPreview(parts) : "";
    }

    public bool TryApply(
        ani2 ani,
        LegacyAniHierarchyAuthority authority,
        out string error)
    {
        if (ani == null || ani.sourceFormat != AniContainerFormat.LegacyAni)
        {
            error = UILocalization.Get(
                "hod.legacy_edit.not_legacy",
                "旧ANIとして読み込まれたデータではありません。");
            return false;
        }

        if (!ani.canChangeLegacyStructure(out error))
            return false;

        if (!TryValidateFrameIndexHierarchy(ani, out error))
            return false;

        if (alreadyValid)
        {
            error = "";
            return true;
        }

        HodHierarchyRepairPlan repairPlan = GetRepairPlan(authority);
        if (repairPlan == null || !repairPlan.CanApply)
        {
            error = UILocalization.Get(
                "hod.legacy_edit.authority_unavailable",
                "選択した階層情報を正として旧ANIを安全に編集できません。");
            return false;
        }

        int[] structureDepths;
        int[] structureCounts;
        List<FrameHierarchySnapshot> frameSnapshots;
        CaptureHierarchy(ani, out structureDepths, out structureCounts, out frameSnapshots);
        try
        {
            if (!repairPlan.TryApply(ani, out error))
            {
                RestoreHierarchy(ani, structureDepths, structureCounts, frameSnapshots);
                return false;
            }

            string validation;
            if (!HodHierarchyValidator.TryValidate(ani.structure.parts, out validation)
                || !TryValidateFrameIndexHierarchy(ani, out error))
            {
                RestoreHierarchy(ani, structureDepths, structureCounts, frameSnapshots);
                if (string.IsNullOrEmpty(error))
                    error = validation;
                return false;
            }
        }
        catch (Exception exception)
        {
            RestoreHierarchy(ani, structureDepths, structureCounts, frameSnapshots);
            error = UILocalization.Get(
                "hod.legacy_edit.apply_failed",
                "旧ANIの階層を構造編集用に準備できませんでした。\n{0}",
                exception.Message);
            return false;
        }

        error = "";
        return true;
    }

    HodHierarchyRepairPlan GetRepairPlan(LegacyAniHierarchyAuthority authority)
    {
        return authority == LegacyAniHierarchyAuthority.TreeDepth
            ? treeDepthPlan
            : childCountPlan;
    }

    public static bool TryCreate(
        ani2 ani,
        out LegacyAniStructureEditPlan plan,
        out string error)
    {
        plan = null;
        if (ani == null || ani.sourceFormat != AniContainerFormat.LegacyAni
            || ani.structure == null || ani.structure.parts == null)
        {
            error = UILocalization.Get(
                "hod.legacy_edit.not_legacy",
                "旧ANIとして読み込まれたデータではありません。");
            return false;
        }

        if (!ani.canChangeLegacyStructure(out error))
            return false;

        if (!TryValidateFrameIndexHierarchy(ani, out error))
            return false;

        HodHierarchyRepairPlan currentPlan = HodHierarchyRepair.CreatePlan(ani.structure.parts);
        switch (currentPlan.Kind)
        {
            case HodHierarchyRepairKind.None:
                plan = new LegacyAniStructureEditPlan(
                    null, null, true, false,
                    LegacyAniHierarchyAuthority.TreeDepth,
                    currentPlan.Summary, currentPlan.Details);
                error = "";
                return true;

            case HodHierarchyRepairKind.RebuildChildCountsFromTreeDepth:
                plan = new LegacyAniStructureEditPlan(
                    currentPlan, null, false, false,
                    LegacyAniHierarchyAuthority.TreeDepth,
                    currentPlan.Summary, currentPlan.Details);
                error = "";
                return true;

            case HodHierarchyRepairKind.RebuildTreeDepthFromChildCounts:
                plan = new LegacyAniStructureEditPlan(
                    null, currentPlan, false, false,
                    LegacyAniHierarchyAuthority.ChildCount,
                    currentPlan.Summary, currentPlan.Details);
                error = "";
                return true;

            case HodHierarchyRepairKind.Ambiguous:
                HodHierarchyRepairPlan treePlan =
                    HodHierarchyRepair.CreatePlanUsingTreeDepth(ani.structure.parts);
                HodHierarchyRepairPlan childPlan =
                    HodHierarchyRepair.CreatePlanUsingChildCount(ani.structure.parts);
                if (!treePlan.CanApply || !childPlan.CanApply)
                {
                    error = currentPlan.Summary + "\n" + currentPlan.Details;
                    return false;
                }

                plan = new LegacyAniStructureEditPlan(
                    treePlan, childPlan, false, false,
                    LegacyAniHierarchyAuthority.ChildCount,
                    currentPlan.Summary, currentPlan.Details);
                error = "";
                return true;

            default:
                error = currentPlan.Summary;
                if (!string.IsNullOrEmpty(currentPlan.Details))
                    error += "\n" + currentPlan.Details;
                return false;
        }
    }

    static bool TryValidateFrameIndexHierarchy(ani2 ani, out string error)
    {
        if (ani.animations == null)
        {
            error = UILocalization.Get(
                "hod.repair.no_animations",
                "アニメーション情報がありません。");
            return false;
        }

        int partCount = ani.structure.parts.Count;
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
                if (frame == null || frame.parts == null || frame.parts.Count != partCount)
                {
                    error = UILocalization.Get(
                        "hod.repair.frame_part_count",
                        "アニメーション[{0}] フレーム[{1}]のパーツ数が構造HODと一致しません（{2}/{3}）。",
                        animationIndex,
                        frameIndex,
                        frame != null && frame.parts != null ? frame.parts.Count : 0,
                        partCount);
                    return false;
                }

                for (int partIndex = 0; partIndex < partCount; partIndex++)
                {
                    if (frame.parts[partIndex].treeDepth != ani.structure.parts[partIndex].treeDepth
                        || frame.parts[partIndex].childCount != ani.structure.parts[partIndex].childCount)
                    {
                        error = UILocalization.Get(
                            "hod.legacy_edit.frame_index_mismatch",
                            "旧ANIのアニメーション[{0}] フレーム[{1}]は、位置{2}の階層列が構造HODと一致しないため安全に構造編集できません。",
                            animationIndex,
                            frameIndex,
                            partIndex);
                        return false;
                    }
                }
            }
        }

        error = "";
        return true;
    }

    static void CaptureHierarchy(
        ani2 ani,
        out int[] structureDepths,
        out int[] structureCounts,
        out List<FrameHierarchySnapshot> frameSnapshots)
    {
        int partCount = ani.structure.parts.Count;
        structureDepths = new int[partCount];
        structureCounts = new int[partCount];
        for (int i = 0; i < partCount; i++)
        {
            structureDepths[i] = ani.structure.parts[i].treeDepth;
            structureCounts[i] = ani.structure.parts[i].childCount;
        }

        frameSnapshots = new List<FrameHierarchySnapshot>();
        foreach (animation animationData in ani.animations)
        {
            foreach (hod2v1 frame in animationData.frames)
            {
                FrameHierarchySnapshot snapshot = new FrameHierarchySnapshot
                {
                    frame = frame,
                    treeDepths = new int[partCount],
                    childCounts = new int[partCount]
                };
                for (int i = 0; i < partCount; i++)
                {
                    snapshot.treeDepths[i] = frame.parts[i].treeDepth;
                    snapshot.childCounts[i] = frame.parts[i].childCount;
                }
                frameSnapshots.Add(snapshot);
            }
        }
    }

    static void RestoreHierarchy(
        ani2 ani,
        int[] structureDepths,
        int[] structureCounts,
        List<FrameHierarchySnapshot> frameSnapshots)
    {
        for (int i = 0; i < structureDepths.Length; i++)
        {
            hod2v0_Part part = ani.structure.parts[i];
            part.treeDepth = structureDepths[i];
            part.childCount = structureCounts[i];
            ani.structure.parts[i] = part;
        }

        for (int frameIndex = 0; frameIndex < frameSnapshots.Count; frameIndex++)
        {
            FrameHierarchySnapshot snapshot = frameSnapshots[frameIndex];
            for (int i = 0; i < snapshot.treeDepths.Length; i++)
            {
                hod2v1_Part part = snapshot.frame.parts[i];
                part.treeDepth = snapshot.treeDepths[i];
                part.childCount = snapshot.childCounts[i];
                snapshot.frame.parts[i] = part;
            }
        }
    }
}

public static class HodHierarchyRepair
{
    public static bool TryValidateWithoutRepair(ani2 ani, out string details)
    {
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            details = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        HodHierarchyRepairPlan hierarchyPlan = CreatePlan(ani.structure.parts);
        if (hierarchyPlan.Kind != HodHierarchyRepairKind.None)
        {
            details = hierarchyPlan.Summary;
            if (!string.IsNullOrEmpty(hierarchyPlan.Details))
                details += "\n" + hierarchyPlan.Details;
            return false;
        }

        HodFrameOrderRepairPlan framePlan;
        if (!HodFrameOrderRepairPlan.TryCreate(
            ani, ani.structure.parts, out framePlan, out details))
        {
            return false;
        }

        if (framePlan.RequiresChanges)
        {
            details = UILocalization.Get(
                "hod.repair.frame_sync_required",
                "構造HODとアニメーションフレームのパーツ順または階層列が一致しません。");
            return false;
        }

        details = "";
        return true;
    }

    static string AppendWarning(string left, string right)
    {
        if (string.IsNullOrEmpty(left))
            return right ?? "";
        if (string.IsNullOrEmpty(right))
            return left;
        return left + "\n" + right;
    }

    public static bool TryApplyBestEffort(
        ani2 ani,
        out HodHierarchyRepairPlan appliedHierarchyPlan,
        out string warning)
    {
        appliedHierarchyPlan = null;
        warning = "";
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            warning = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        appliedHierarchyPlan = CreatePlan(ani.structure.parts);
        if (appliedHierarchyPlan.Kind != HodHierarchyRepairKind.None)
        {
            HodHierarchyRepairPlan childPlan = CreatePlanUsingChildCount(ani.structure.parts);
            if (childPlan.CanApply)
            {
                if (!childPlan.TryApplyStructureOnly(ani, out string childError))
                {
                    warning = childError;
                    return false;
                }
                appliedHierarchyPlan = childPlan;
            }
            else
            {
                HodHierarchyRepairPlan depthPlan = CreatePlanUsingTreeDepth(ani.structure.parts);
                if (depthPlan.CanApply)
                {
                    if (!depthPlan.TryApplyStructureOnly(ani, out string depthError))
                    {
                        warning = depthError;
                        return false;
                    }
                    appliedHierarchyPlan = depthPlan;
                    warning = "childCountからの復元に失敗したためtreeDepthを使用しました。";
                }
                else
                {
                    ApplyBestEffortHierarchyFallback(ani.structure.parts);
                    warning = "treeDepthとchildCountの両方を復元できないため、元の並びを保った推定階層へ正規化しました。";
                }
            }
        }

        HodFrameOrderRepairPlan framePlan;
        string orderWarning;
        if (!HodFrameOrderRepairPlan.TryCreateBestEffort(
            ani, ani.structure.parts, out framePlan, out orderWarning))
        {
            warning = AppendWarning(warning, orderWarning);
            return false;
        }
        framePlan.Apply();
        warning = AppendWarning(warning, orderWarning);
        return true;
    }

    static void ApplyBestEffortHierarchyFallback(IList<hod2v0_Part> parts)
    {
        if (parts == null || parts.Count == 0)
            return;
        for (int i = 0; i < parts.Count; i++)
        {
            hod2v0_Part part = parts[i];
            part.treeDepth = i == 0 ? 0 : 1;
            part.childCount = i == 0 ? parts.Count - 1 : 0;
            parts[i] = part;
        }
    }

    // 原作確定: FUN_0056fbf0 / FUN_0056fe90 はchildCount回の再帰で親子を構築する。
    // 表示用の対応を計算するだけで、旧ANIの生の階層列や行列は変更しない。
    // childCountが復元不能な既存データだけ、有効なtreeDepthへ互換フォールバックする。
    public static bool TryGetDisplayHierarchy(
        IList<hod2v0_Part> parts, out int[] parents, out int[] depths, out string error)
    {
        parents = null;
        depths = null;
        if (parts == null || parts.Count == 0)
        {
            error = "構造HODのパーツ情報がありません。";
            return false;
        }

        if (!TryBuildDepthsFromChildCounts(parts, out depths, out error))
        {
            int[] ignored;
            if (!TryBuildChildCountsFromDepths(parts, out ignored, out string depthError))
            {
                error += "\n" + depthError;
                return false;
            }
            depths = CopyDepths(parts);
        }

        parents = new int[parts.Count];
        var ancestors = new List<int>();
        for (int i = 0; i < parts.Count; i++)
        {
            while (ancestors.Count > depths[i])
                ancestors.RemoveAt(ancestors.Count - 1);
            parents[i] = depths[i] == 0 ? -1 : ancestors[depths[i] - 1];
            ancestors.Add(i);
        }
        error = "";
        return true;
    }

    public static bool TryApplyChildCountFirst(
        ani2 ani, out HodHierarchyRepairPlan appliedPlan, out string error)
    {
        appliedPlan = null;
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            error = "構造HODがありません。";
            return false;
        }
        var current = CreatePlan(ani.structure.parts);
        // 既に一致する階層は従来と同じ全フレーム事前検査・同期を使う。
        if (current.Kind == HodHierarchyRepairKind.None)
            return TryApplyTreeDepthFirst(ani, out appliedPlan, out error);

        var childPlan = CreatePlanUsingChildCount(ani.structure.parts);
        if (!childPlan.CanApply)
            return TryApplyTreeDepthFirst(ani, out appliedPlan, out error);

        // フレームの対応不明は別の正本へのfallbackで隠さず、変更前に中止する。
        if (!childPlan.TryApply(ani, out error))
            return false;
        appliedPlan = childPlan;
        return true;
    }

    public static bool TryApplyTreeDepthFirst(
        ani2 ani,
        out HodHierarchyRepairPlan appliedPlan,
        out string error)
    {
        appliedPlan = null;
        if (ani == null || ani.structure == null || ani.structure.parts == null)
        {
            error = UILocalization.Get("hod.repair.no_structure", "構造HODがありません。");
            return false;
        }

        HodHierarchyRepairPlan currentPlan = CreatePlan(ani.structure.parts);
        if (currentPlan.Kind == HodHierarchyRepairKind.None)
        {
            HodFrameOrderRepairPlan frameOrderPlan;
            if (!HodFrameOrderRepairPlan.TryCreate(
                ani, ani.structure.parts, out frameOrderPlan, out error))
                return false;

            frameOrderPlan.Apply();
            foreach (animation animationData in ani.animations)
            {
                foreach (hod2v1 frame in animationData.frames)
                {
                    for (int i = 0; i < frame.parts.Count; i++)
                    {
                        hod2v1_Part framePart = frame.parts[i];
                        framePart.treeDepth = ani.structure.parts[i].treeDepth;
                        framePart.childCount = ani.structure.parts[i].childCount;
                        frame.parts[i] = framePart;
                    }
                }
            }

            appliedPlan = currentPlan;
            error = "";
            return true;
        }

        HodHierarchyRepairPlan treeDepthPlan = CreatePlanUsingTreeDepth(ani.structure.parts);
        if (treeDepthPlan.CanApply)
        {
            if (!treeDepthPlan.TryApply(ani, out error))
                return false;

            appliedPlan = treeDepthPlan;
            return true;
        }

        HodHierarchyRepairPlan childCountPlan = CreatePlanUsingChildCount(ani.structure.parts);
        if (childCountPlan.CanApply)
        {
            if (!childCountPlan.TryApply(ani, out error))
                return false;

            appliedPlan = childCountPlan;
            return true;
        }

        appliedPlan = currentPlan;
        error = currentPlan.Summary;
        if (!string.IsNullOrEmpty(currentPlan.Details))
            error += "\n" + currentPlan.Details;
        return false;
    }

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
