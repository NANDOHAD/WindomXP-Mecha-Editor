using System;
using System.Collections.Generic;

public enum TestPlayPresentationEventType
{
    Sound,
    Voice,
    Burner,
    Proc,
    Texture,
    Visual,
    CameraEffect,
    Diagnostic
}

public enum TestPlayPresentationEvidence
{
    OriginalExecutableConfirmed,
    OriginalDataObserved,
    IncompleteInference,
    Unknown
}

public enum TestPlayPresentationAdapterKind
{
    None,
    AudioClip,
    BurnerCone,
    ParticleSystem,
    MappedPrefab,
    OriginalTextureQuad,
    PrimitiveFallback,
    CameraShakeApproximation,
    CombatOnly
}

public struct TestPlayPresentationEvent
{
    public TestPlayPresentationEventType type;
    public TestPlayPresentationEvidence evidence;
    public TestPlayPresentationAdapterKind adapter;
    public int tick;
    public int actionIndex;
    public int scriptIndex;
    public string command;
    public string source;
    public string symbol;
    public string resourceName;
    public string diagnostic;
    public int originalId;
    public int procType;
    public int subtype;
    public int textureId;
    public float output;
    public TestPlayScriptValue[] arguments;
}

public struct TestPlaySwordBeamParameters
{
    public int weaponPointId;
    public float targetLength;
    public int primaryTextureId;
    public int lineTextureId;
    public float initialLength;
    public bool replaceManagedBeam;
    public int lifetimeTicks;
}

public static class TestPlayPresentationCore
{
    public const int MinimumBurnerId = 0;
    public const int MaximumBurnerId = 19;
    public const int WindLineProcType = 53;
    public const int WindRingProcType = 54;
    public const int OriginalWindLineCount = 7;
    public const float OriginalWindLineWidth = 0.07f;
    public const float OriginalWindLineLength = 3f;
    public const float OriginalWindRingRadius = 1f;
    public const int SwordBeamProcType = 55;
    public const float OriginalSwordBeamWidth = 0.075f;
    public const float OriginalSwordBeamGrowthPerTick = 0.2f;
    public const int OriginalSwordBeamInfiniteLifetime = 999999999;

    public static TestPlayPresentationEvent CreateSound(
        IReadOnlyList<TestPlayScriptValue> arguments,
        TestPlayPresentationAdapterKind adapter)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Sound,
            "Snd",
            arguments,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            adapter);
        value.originalId = GetInt(arguments, 0, -1);
        value.symbol = GetText(arguments, 0);
        return value;
    }

    public static TestPlayPresentationEvent CreateVoice(
        IReadOnlyList<TestPlayScriptValue> arguments,
        TestPlayPresentationAdapterKind adapter)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Voice,
            "Voice",
            arguments,
            TestPlayPresentationEvidence.OriginalDataObserved,
            adapter);
        value.symbol = GetText(arguments, 0);
        return value;
    }

    public static bool TryCreateBurner(
        IReadOnlyList<TestPlayScriptValue> arguments,
        TestPlayPresentationAdapterKind adapter,
        out TestPlayPresentationEvent value)
    {
        value = CreateBase(
            TestPlayPresentationEventType.Burner,
            "BURNER",
            arguments,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            adapter);
        value.originalId = GetInt(arguments, 0, -1);
        value.output = arguments != null && arguments.Count > 1
            ? arguments[1].AsFloat(1f)
            : 1f;

        if (arguments == null || arguments.Count == 0)
        {
            value.type = TestPlayPresentationEventType.Diagnostic;
            value.diagnostic = "MissingBurnerId";
            return false;
        }

        if (value.originalId < MinimumBurnerId || value.originalId > MaximumBurnerId)
        {
            value.type = TestPlayPresentationEventType.Diagnostic;
            value.diagnostic = "BurnerIdOutsideOriginalRange";
            return false;
        }

        if (arguments.Count == 1)
            value.diagnostic = "UnityCompatibilityDefaultOutput";
        return true;
    }

    public static TestPlayPresentationEvent CreateUnsupported(
        string command,
        IReadOnlyList<TestPlayScriptValue> arguments,
        string diagnostic)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Diagnostic,
            command,
            arguments,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            TestPlayPresentationAdapterKind.None);
        value.diagnostic = diagnostic ?? "";
        return value;
    }

    public static TestPlayPresentationEvent CreateCameraEffect(
        IReadOnlyList<TestPlayScriptValue> arguments,
        float effectId,
        TestPlayPresentationAdapterKind adapter)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.CameraEffect,
            "CamEffect",
            arguments,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            adapter);
        value.originalId = UnityEngine.Mathf.RoundToInt(effectId);
        value.output = effectId;
        if (adapter == TestPlayPresentationAdapterKind.CameraShakeApproximation)
            value.diagnostic = "OriginalPerValueCameraFormulaUnknown";
        return value;
    }

    public static TestPlayPresentationEvent CreateProc(
        bool extended,
        IReadOnlyList<TestPlayScriptValue> arguments,
        TestPlayPresentationAdapterKind adapter)
    {
        int procType = GetInt(arguments, 1, -1);
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Proc,
            extended ? "RunProc2" : "RunProc",
            arguments,
            IsOriginalWindProc(extended, procType) || IsOriginalSwordBeamProc(extended, procType)
                ? TestPlayPresentationEvidence.OriginalExecutableConfirmed
                : procType == 57
                    ? TestPlayPresentationEvidence.OriginalDataObserved
                    : TestPlayPresentationEvidence.IncompleteInference,
            adapter);
        value.originalId = GetInt(arguments, 0, -1);
        value.procType = procType;
        value.subtype = procType == 62 ? GetInt(arguments, 3, -1) : -1;
        return value;
    }

    public static bool IsOriginalWindProc(bool extended, int procType)
    {
        return !extended && (procType == WindLineProcType || procType == WindRingProcType);
    }

    public static int GetOriginalWindVisualCount(bool extended, int procType)
    {
        if (!IsOriginalWindProc(extended, procType))
            return 0;
        return procType == WindLineProcType ? OriginalWindLineCount : 1;
    }

    public static bool IsOriginalSwordBeamProc(bool extended, int procType)
    {
        return extended && procType == SwordBeamProcType;
    }

    public static bool TryCreateOriginalSwordBeamParameters(
        bool extended,
        IReadOnlyList<TestPlayScriptValue> arguments,
        out TestPlaySwordBeamParameters value)
    {
        value = default(TestPlaySwordBeamParameters);
        if (!IsOriginalSwordBeamProc(extended, GetInt(arguments, 1, -1)) ||
            arguments == null || arguments.Count < 12)
            return false;

        int scriptedLifetime = GetInt(arguments, 11, 0);
        value = new TestPlaySwordBeamParameters
        {
            weaponPointId = GetInt(arguments, 2, -1),
            targetLength = arguments[3].AsFloat() / 100f,
            primaryTextureId = GetInt(arguments, 4, -1),
            lineTextureId = GetInt(arguments, 5, -1),
            initialLength = arguments[6].AsFloat() / 100f,
            replaceManagedBeam = GetInt(arguments, 10, 0) != 0,
            lifetimeTicks = scriptedLifetime == 0
                ? OriginalSwordBeamInfiniteLifetime
                : scriptedLifetime
        };
        return true;
    }

    public static bool AdvanceOriginalSwordBeam(
        ref float currentLength,
        float targetLength,
        ref int remainingTicks)
    {
        // BB_SwordBeam::Update (FUN_00497cd0) keeps the sentinel unchanged,
        // otherwise decrements before checking the expiry boundary.
        if (remainingTicks < OriginalSwordBeamInfiniteLifetime)
            remainingTicks--;
        if (remainingTicks < 1)
            return false;

        if (currentLength < targetLength)
        {
            currentLength += OriginalSwordBeamGrowthPerTick;
            if (currentLength > targetLength)
                currentLength = targetLength;
        }
        return true;
    }

    public static TestPlayPresentationEvent CreateTexture(
        string source,
        int textureId,
        string resourceName,
        TestPlayPresentationAdapterKind adapter)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Texture,
            "Texture",
            null,
            TestPlayPresentationEvidence.OriginalDataObserved,
            adapter);
        value.source = source ?? "";
        value.textureId = textureId;
        value.resourceName = resourceName ?? "";
        return value;
    }

    public static TestPlayPresentationEvent CreateVisual(
        string source,
        int textureId,
        TestPlayPresentationAdapterKind adapter)
    {
        return CreateVisual(
            source,
            textureId,
            adapter,
            textureId >= 0
                ? TestPlayPresentationEvidence.OriginalDataObserved
                : TestPlayPresentationEvidence.IncompleteInference,
            "");
    }

    public static TestPlayPresentationEvent CreateVisual(
        string source,
        int textureId,
        TestPlayPresentationAdapterKind adapter,
        TestPlayPresentationEvidence evidence,
        string diagnostic)
    {
        TestPlayPresentationEvent value = CreateBase(
            TestPlayPresentationEventType.Visual,
            "Visual",
            null,
            evidence,
            adapter);
        value.source = source ?? "";
        value.textureId = textureId;
        value.diagnostic = diagnostic ?? "";
        return value;
    }

    static TestPlayPresentationEvent CreateBase(
        TestPlayPresentationEventType type,
        string command,
        IReadOnlyList<TestPlayScriptValue> arguments,
        TestPlayPresentationEvidence evidence,
        TestPlayPresentationAdapterKind adapter)
    {
        return new TestPlayPresentationEvent
        {
            type = type,
            evidence = evidence,
            adapter = adapter,
            command = command ?? "",
            source = "",
            symbol = "",
            resourceName = "",
            diagnostic = "",
            originalId = -1,
            procType = -1,
            subtype = -1,
            textureId = -1,
            arguments = CopyArguments(arguments)
        };
    }

    static TestPlayScriptValue[] CopyArguments(IReadOnlyList<TestPlayScriptValue> arguments)
    {
        int count = arguments != null ? arguments.Count : 0;
        TestPlayScriptValue[] result = new TestPlayScriptValue[count];
        for (int i = 0; i < count; i++)
            result[i] = arguments[i];
        return result;
    }

    static int GetInt(IReadOnlyList<TestPlayScriptValue> arguments, int index, int fallback)
    {
        return arguments != null && index >= 0 && index < arguments.Count
            ? arguments[index].AsInt(fallback)
            : fallback;
    }

    static string GetText(IReadOnlyList<TestPlayScriptValue> arguments, int index)
    {
        return arguments != null && index >= 0 && index < arguments.Count
            ? arguments[index].ToString()
            : "";
    }
}
