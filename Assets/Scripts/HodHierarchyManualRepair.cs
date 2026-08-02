using System;
using System.Collections.Generic;

public sealed class HodHierarchyManualRepairSession
{
    sealed class FrameSnapshot
    {
        public hod2v1 frame;
        public string[] names;
        public int[] treeDepths;
        public int[] childCounts;
    }

    sealed class FrameReplacement
    {
        public hod2v1 frame;
        public List<hod2v1_Part> parts;
    }

    readonly hod2v0 originalStructure;
    readonly string[] originalNames;
    readonly int[] originalTreeDepths;
    readonly int[] originalChildCounts;
    readonly List<FrameSnapshot> frameSnapshots;
    readonly int[] parentIndices;

    public int PartCount
    {
        get { return parentIndices.Length; }
    }

    public int UnassignedCount
    {
        get
        {
            int count = 0;
            for (int i = 1; i < parentIndices.Length; i++)
            {
                if (parentIndices[i] < 0)
                    count++;
            }
            return count;
        }
    }

    HodHierarchyManualRepairSession(
        hod2v0 structure,
        string[] names,
        int[] depths,
        int[] childCounts,
        List<FrameSnapshot> frames)
    {
        originalStructure = structure;
        originalNames = names;
        originalTreeDepths = depths;
        originalChildCounts = childCounts;
        frameSnapshots = frames;
        parentIndices = new int[names.Length];

        for (int i = 0; i < parentIndices.Length; i++)
            parentIndices[i] = -1;

        GuessParentsFromTreeDepth(structure.parts);
        GuessParentsFromChildCount(structure.parts);
    }

    public static bool TryCreate(ani2 ani, out HodHierarchyManualRepairSession session, out string error)
    {
        session = null;
        if (ani == null || ani.structure == null || ani.structure.parts == null
            || ani.structure.parts.Count == 0)
        {
            error = "構造HODのパーツ情報がありません。";
            return false;
        }

        if (ani.animations == null)
        {
            error = "アニメーション情報がありません。";
            return false;
        }

        int partCount = ani.structure.parts.Count;
        string[] names = new string[partCount];
        int[] depths = new int[partCount];
        int[] childCounts = new int[partCount];
        for (int i = 0; i < partCount; i++)
        {
            names[i] = ani.structure.parts[i].name;
            depths[i] = ani.structure.parts[i].treeDepth;
            childCounts[i] = ani.structure.parts[i].childCount;
        }

        List<FrameSnapshot> frames = new List<FrameSnapshot>();
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

                FrameSnapshot snapshot = new FrameSnapshot();
                snapshot.frame = frame;
                snapshot.names = new string[partCount];
                snapshot.treeDepths = new int[partCount];
                snapshot.childCounts = new int[partCount];

                for (int partIndex = 0; partIndex < partCount; partIndex++)
                {
                    hod2v1_Part framePart = frame.parts[partIndex];
                    string structureName = names[partIndex];
                    if (!string.IsNullOrEmpty(structureName)
                        && !string.IsNullOrEmpty(framePart.name)
                        && !string.Equals(structureName, framePart.name, StringComparison.Ordinal))
                    {
                        error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]のパーツ順が"
                            + $"構造HODと一致しません（位置{partIndex}: 「{framePart.name}」/「{structureName}」）。";
                        return false;
                    }

                    snapshot.names[partIndex] = framePart.name;
                    snapshot.treeDepths[partIndex] = framePart.treeDepth;
                    snapshot.childCounts[partIndex] = framePart.childCount;
                }

                frames.Add(snapshot);
            }
        }

        session = new HodHierarchyManualRepairSession(
            ani.structure, names, depths, childCounts, frames);
        error = "";
        return true;
    }

    public int GetParentIndex(int partIndex)
    {
        return partIndex >= 0 && partIndex < parentIndices.Length
            ? parentIndices[partIndex]
            : -1;
    }

    public string GetPartLabel(int partIndex)
    {
        if (partIndex < 0 || partIndex >= originalNames.Length)
            return "<範囲外>";

        string name = string.IsNullOrEmpty(originalNames[partIndex])
            ? "<名称なし>"
            : originalNames[partIndex];
        return $"[{partIndex}] {name}";
    }

    public string GetAssignmentLabel(int partIndex)
    {
        string child = GetPartLabel(partIndex);
        if (partIndex == 0)
            return child + "（ルート固定）";

        int parentIndex = GetParentIndex(partIndex);
        return parentIndex < 0
            ? child + " → 未接続"
            : child + " → " + GetPartLabel(parentIndex);
    }

    public bool TrySetParent(int partIndex, int parentIndex, out string error)
    {
        if (partIndex <= 0 || partIndex >= parentIndices.Length)
        {
            error = "ルートパーツは固定されているため、親を変更できません。";
            return false;
        }

        if (parentIndex < -1 || parentIndex >= partIndex)
        {
            error = "親には、このパーツより前に並んでいるパーツだけを指定できます。";
            return false;
        }

        parentIndices[partIndex] = parentIndex;
        error = "";
        return true;
    }

    public bool TryApply(ani2 ani, out string error)
    {
        if (!TryValidateTarget(ani, out error))
            return false;

        if (UnassignedCount > 0)
        {
            error = $"親が未設定のパーツが{UnassignedCount}個あります。";
            return false;
        }

        int partCount = parentIndices.Length;
        List<int>[] children = new List<int>[partCount];
        for (int i = 0; i < partCount; i++)
            children[i] = new List<int>();

        for (int i = 1; i < partCount; i++)
        {
            int parentIndex = parentIndices[i];
            if (parentIndex < 0 || parentIndex >= i)
            {
                error = $"パーツ[{i}]の親指定が不正です。";
                return false;
            }
            children[parentIndex].Add(i);
        }

        List<int> order = new List<int>(partCount);
        int[] repairedDepths = new int[partCount];
        AppendSubtree(0, 0, children, order, repairedDepths);
        if (order.Count != partCount)
        {
            error = "すべてのパーツをルートへ接続できません。";
            return false;
        }

        List<hod2v0_Part> repairedStructureParts = new List<hod2v0_Part>(partCount);
        for (int newIndex = 0; newIndex < order.Count; newIndex++)
        {
            int oldIndex = order[newIndex];
            hod2v0_Part part = ani.structure.parts[oldIndex];
            part.treeDepth = repairedDepths[oldIndex];
            part.childCount = children[oldIndex].Count;
            repairedStructureParts.Add(part);
        }

        string validationError;
        if (!HodHierarchyValidator.TryValidate(repairedStructureParts, out validationError))
        {
            error = "手動指定から有効なパーツ階層を構築できませんでした。\n" + validationError;
            return false;
        }

        List<FrameReplacement> repairedFrames = new List<FrameReplacement>(frameSnapshots.Count);
        for (int frameIndex = 0; frameIndex < frameSnapshots.Count; frameIndex++)
        {
            hod2v1 frame = frameSnapshots[frameIndex].frame;
            List<hod2v1_Part> repairedParts = new List<hod2v1_Part>(partCount);
            for (int newIndex = 0; newIndex < order.Count; newIndex++)
            {
                int oldIndex = order[newIndex];
                hod2v1_Part part = frame.parts[oldIndex];
                part.treeDepth = repairedDepths[oldIndex];
                part.childCount = children[oldIndex].Count;
                repairedParts.Add(part);
            }

            FrameReplacement replacement = new FrameReplacement();
            replacement.frame = frame;
            replacement.parts = repairedParts;
            repairedFrames.Add(replacement);
        }

        ani.structure.parts = repairedStructureParts;
        for (int i = 0; i < repairedFrames.Count; i++)
            repairedFrames[i].frame.parts = repairedFrames[i].parts;

        error = "";
        return true;
    }

    void GuessParentsFromTreeDepth(IList<hod2v0_Part> parts)
    {
        for (int i = 1; i < parts.Count; i++)
        {
            int depth = parts[i].treeDepth;
            if (depth <= 0)
                continue;

            for (int parentIndex = i - 1; parentIndex >= 0; parentIndex--)
            {
                if (parts[parentIndex].treeDepth == depth - 1)
                {
                    parentIndices[i] = parentIndex;
                    break;
                }
            }
        }
    }

    void GuessParentsFromChildCount(IList<hod2v0_Part> parts)
    {
        List<int> stackIndices = new List<int>();
        List<int> remainingChildren = new List<int>();
        stackIndices.Add(0);
        remainingChildren.Add(Math.Max(0, parts[0].childCount));

        for (int i = 1; i < parts.Count; i++)
        {
            while (remainingChildren.Count > 0
                && remainingChildren[remainingChildren.Count - 1] == 0)
            {
                remainingChildren.RemoveAt(remainingChildren.Count - 1);
                stackIndices.RemoveAt(stackIndices.Count - 1);
            }

            if (remainingChildren.Count > 0)
            {
                int stackTop = remainingChildren.Count - 1;
                if (parentIndices[i] < 0)
                    parentIndices[i] = stackIndices[stackTop];
                remainingChildren[stackTop]--;
            }

            stackIndices.Add(i);
            remainingChildren.Add(Math.Max(0, parts[i].childCount));
        }
    }

    bool TryValidateTarget(ani2 ani, out string error)
    {
        if (ani == null || ani.structure == null
            || !object.ReferenceEquals(ani.structure, originalStructure)
            || ani.structure.parts == null
            || ani.structure.parts.Count != parentIndices.Length)
        {
            error = "構造HODが手動修復の開始後に変更されています。";
            return false;
        }

        for (int i = 0; i < parentIndices.Length; i++)
        {
            hod2v0_Part part = ani.structure.parts[i];
            if (!string.Equals(part.name, originalNames[i], StringComparison.Ordinal)
                || part.treeDepth != originalTreeDepths[i]
                || part.childCount != originalChildCounts[i])
            {
                error = $"構造HODのパーツ[{i}]が手動修復の開始後に変更されています。";
                return false;
            }
        }

        if (ani.animations == null)
        {
            error = "アニメーション情報がありません。";
            return false;
        }

        int snapshotIndex = 0;
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
                if (snapshotIndex >= frameSnapshots.Count)
                {
                    error = "フレーム数が手動修復の開始後に変更されています。";
                    return false;
                }

                FrameSnapshot snapshot = frameSnapshots[snapshotIndex++];
                hod2v1 frame = animationData.frames[frameIndex];
                if (!object.ReferenceEquals(frame, snapshot.frame)
                    || frame.parts == null
                    || frame.parts.Count != parentIndices.Length)
                {
                    error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]が"
                        + "手動修復の開始後に変更されています。";
                    return false;
                }

                for (int partIndex = 0; partIndex < parentIndices.Length; partIndex++)
                {
                    hod2v1_Part part = frame.parts[partIndex];
                    if (!string.Equals(part.name, snapshot.names[partIndex], StringComparison.Ordinal)
                        || part.treeDepth != snapshot.treeDepths[partIndex]
                        || part.childCount != snapshot.childCounts[partIndex])
                    {
                        error = $"アニメーション[{animationIndex}] フレーム[{frameIndex}]の"
                            + $"パーツ[{partIndex}]が手動修復の開始後に変更されています。";
                        return false;
                    }
                }
            }
        }

        if (snapshotIndex != frameSnapshots.Count)
        {
            error = "フレーム数が手動修復の開始後に変更されています。";
            return false;
        }

        error = "";
        return true;
    }

    static void AppendSubtree(
        int partIndex,
        int depth,
        IList<List<int>> children,
        List<int> order,
        int[] repairedDepths)
    {
        repairedDepths[partIndex] = depth;
        order.Add(partIndex);
        for (int i = 0; i < children[partIndex].Count; i++)
            AppendSubtree(children[partIndex][i], depth + 1, children, order, repairedDepths);
    }
}
