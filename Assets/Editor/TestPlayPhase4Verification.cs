using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class TestPlayPhase4Verification
{
    const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int RunAll()
    {
        int assertions = 0;
        VerifyTypedCommandEvents(ref assertions);
        VerifyBurnerBoundaries(ref assertions);
        VerifyProcAndVisualEvidence(ref assertions);
        VerifyPresentationTrace(ref assertions);
        VerifyControllerAdapter(ref assertions);
        return assertions;
    }

    static void VerifyTypedCommandEvents(ref int assertions)
    {
        List<TestPlayScriptValue> soundArguments = Values(9f);
        TestPlayPresentationEvent sound = TestPlayPresentationCore.CreateSound(
            soundArguments, TestPlayPresentationAdapterKind.AudioClip);
        Require(sound.type == TestPlayPresentationEventType.Sound && sound.command == "Snd",
            "Snd becomes a typed presentation event", ref assertions);
        Require(sound.originalId == 9 && sound.symbol == "9",
            "Snd retains the original fixed ID", ref assertions);
        Require(sound.evidence == TestPlayPresentationEvidence.OriginalExecutableConfirmed &&
                sound.adapter == TestPlayPresentationAdapterKind.AudioClip,
            "Snd evidence and Unity adapter are independent fields", ref assertions);
        soundArguments[0] = TestPlayScriptValue.Number(3f);
        Require(sound.arguments.Length == 1 && sound.arguments[0].AsInt() == 9,
            "presentation arguments are immutable snapshots", ref assertions);

        TestPlayPresentationEvent voice = TestPlayPresentationCore.CreateVoice(
            new[] { TestPlayScriptValue.Symbol("Damage") },
            TestPlayPresentationAdapterKind.None);
        Require(voice.type == TestPlayPresentationEventType.Voice && voice.symbol == "Damage",
            "Voice retains the original symbolic key", ref assertions);
        Require(voice.evidence == TestPlayPresentationEvidence.OriginalDataObserved,
            "Voice is not promoted beyond observed original data", ref assertions);

        TestPlayPresentationEvent camera = TestPlayPresentationCore.CreateCameraEffect(
            Values(2f), 2f, TestPlayPresentationAdapterKind.CameraShakeApproximation);
        Require(camera.originalId == 2 && Mathf.Approximately(camera.output, 2f),
            "CamEffect retains its original value", ref assertions);
        Require(camera.adapter == TestPlayPresentationAdapterKind.CameraShakeApproximation &&
                camera.diagnostic == "OriginalPerValueCameraFormulaUnknown",
            "camera shake is explicitly marked as a Unity approximation", ref assertions);
    }

    static void VerifyBurnerBoundaries(ref int assertions)
    {
        TestPlayPresentationEvent burner;
        Require(TestPlayPresentationCore.TryCreateBurner(
                Values(0f, 0.25f), TestPlayPresentationAdapterKind.BurnerCone, out burner),
            "BURNER accepts original ID zero", ref assertions);
        Require(burner.originalId == 0 && Mathf.Approximately(burner.output, 0.25f) &&
                burner.adapter == TestPlayPresentationAdapterKind.BurnerCone,
            "BURNER keeps ID, output, and adapter", ref assertions);
        Require(TestPlayPresentationCore.TryCreateBurner(
                Values(19f, 1f), TestPlayPresentationAdapterKind.ParticleSystem, out burner) &&
                burner.originalId == 19,
            "BURNER accepts original ID nineteen", ref assertions);
        Require(!TestPlayPresentationCore.TryCreateBurner(
                Values(-1f, 1f), TestPlayPresentationAdapterKind.None, out burner) &&
                burner.type == TestPlayPresentationEventType.Diagnostic,
            "BURNER rejects IDs below the original range", ref assertions);
        Require(burner.diagnostic == "BurnerIdOutsideOriginalRange",
            "invalid BURNER ID has a stable diagnostic", ref assertions);
        Require(!TestPlayPresentationCore.TryCreateBurner(
                Values(20f, 1f), TestPlayPresentationAdapterKind.None, out burner),
            "BURNER rejects IDs above the original range", ref assertions);
        Require(!TestPlayPresentationCore.TryCreateBurner(
                new List<TestPlayScriptValue>(), TestPlayPresentationAdapterKind.None, out burner) &&
                burner.diagnostic == "MissingBurnerId",
            "missing BURNER ID remains traceable", ref assertions);
        Require(TestPlayPresentationCore.TryCreateBurner(
                Values(3f), TestPlayPresentationAdapterKind.None, out burner) &&
                Mathf.Approximately(burner.output, 1f),
            "one-argument MOD compatibility keeps output one", ref assertions);
        Require(burner.diagnostic == "UnityCompatibilityDefaultOutput",
            "one-argument BURNER fallback is not labelled original", ref assertions);

        TestPlayPresentationEvent burner2 = TestPlayPresentationCore.CreateUnsupported(
            "BURNER2", Values(1f), "OriginalParserRejectsCommandObject");
        Require(burner2.type == TestPlayPresentationEventType.Diagnostic &&
                burner2.adapter == TestPlayPresentationAdapterKind.None,
            "BURNER2 stays unsupported instead of gaining an effect", ref assertions);
    }

    static void VerifyProcAndVisualEvidence(ref int assertions)
    {
        TestPlayPresentationEvent sword = TestPlayPresentationCore.CreateProc(
            true, Values(1f, 55f, 1f, 200f, 12f, 13f), TestPlayPresentationAdapterKind.None);
        Require(sword.command == "RunProc2" && sword.originalId == 1 && sword.procType == 55,
            "RunProc2 keeps order and proc type", ref assertions);
        Require(sword.subtype == -1 && sword.evidence == TestPlayPresentationEvidence.OriginalDataObserved,
            "type 55 is classified from observed original data", ref assertions);

        TestPlayPresentationEvent melee = TestPlayPresentationCore.CreateProc(
            true, Values(1f, 57f, 1f), TestPlayPresentationAdapterKind.CombatOnly);
        Require(melee.procType == 57 && melee.adapter == TestPlayPresentationAdapterKind.CombatOnly,
            "type 57 remains on the Combat Core boundary", ref assertions);

        TestPlayPresentationEvent special = TestPlayPresentationCore.CreateProc(
            true, Values(0f, 62f, 28f, 7f), TestPlayPresentationAdapterKind.None);
        Require(special.procType == 62 && special.subtype == 7,
            "type 62 retains its subtype", ref assertions);
        Require(special.evidence == TestPlayPresentationEvidence.IncompleteInference,
            "incomplete type 62 parameter meanings remain inferred", ref assertions);

        TestPlayPresentationEvent texture = TestPlayPresentationCore.CreateTexture(
            "RunProc2:55", 13, "line.png", TestPlayPresentationAdapterKind.OriginalTextureQuad);
        Require(texture.type == TestPlayPresentationEventType.Texture && texture.textureId == 13,
            "texture event retains the script texture ID", ref assertions);
        Require(texture.resourceName == "line.png" &&
                texture.adapter == TestPlayPresentationAdapterKind.OriginalTextureQuad,
            "resource name and Unity quad adapter are separated", ref assertions);

        TestPlayPresentationEvent fallback = TestPlayPresentationCore.CreateVisual(
            "RunProc2:1", 12, TestPlayPresentationAdapterKind.PrimitiveFallback);
        Require(fallback.type == TestPlayPresentationEventType.Visual && fallback.textureId == 12,
            "visual fallback does not discard the requested texture ID", ref assertions);
        Require(fallback.adapter == TestPlayPresentationAdapterKind.PrimitiveFallback,
            "primitive fallback is explicit", ref assertions);
    }

    static void VerifyPresentationTrace(ref int assertions)
    {
        TestPlayPresentationEvent sound = TestPlayPresentationCore.CreateSound(
            new[] { TestPlayScriptValue.Symbol("quote\"line\n") },
            TestPlayPresentationAdapterKind.None);
        sound.tick = 12;
        sound.actionIndex = 100;
        sound.scriptIndex = 2;
        TestPlayPresentationEvent proc = TestPlayPresentationCore.CreateProc(
            true, Values(0f, 62f, 28f, 3f), TestPlayPresentationAdapterKind.None);
        proc.tick = 12;

        TestPlayPresentationSnapshot snapshot = new TestPlayPresentationSnapshot
        {
            events = new[] { sound, proc }
        };
        string phase3 = "{\"tick\":12,\"combat\":{}}";
        string first = TestPlayPhase4TickTrace.Serialize(phase3, snapshot);
        string second = TestPlayPhase4TickTrace.Serialize(phase3, snapshot);
        Require(first == second, "Phase 4 trace serialization is deterministic", ref assertions);
        Require(first.Contains("\"presentation\":{\"events\":["),
            "presentation extends the Phase 3 tick record", ref assertions);
        Require(first.Contains("\"action\":100") && first.Contains("\"script\":2"),
            "trace records action and script context", ref assertions);
        Require(first.Contains("quote\\\"line\\n"),
            "trace escapes symbolic arguments", ref assertions);
        Require(first.Contains("\"kind\":\"Symbol\"") && first.Contains("\"procType\":62"),
            "trace preserves argument kinds and proc identifiers", ref assertions);
        Require(first.Contains("\"evidence\":\"OriginalExecutableConfirmed\"") &&
                first.Contains("\"adapter\":\"None\""),
            "trace carries evidence and adapter independently", ref assertions);
    }

    static void VerifyControllerAdapter(ref int assertions)
    {
        GameObject go = new GameObject("TestPlayPhase4Verification_Controller");
        AudioClip clip = null;
        try
        {
            TestPlayController controller = go.AddComponent<TestPlayController>();
            controller.logUnhandledCommands = false;
            TestPlayPresentationRuntime presentation = go.AddComponent<TestPlayPresentationRuntime>();
            presentation.enabled = false;
            clip = AudioClip.Create("Phase4Snd", 64, 1, 8000, false);
            presentation.sounds.Add(new TestPlayAudioBinding { key = "9", clip = clip });
            controller.presentationRuntime = presentation;

            MethodInfo beginTrace = typeof(TestPlayController).GetMethod("BeginPresentationTraceTick", InstancePrivate);
            MethodInfo handle = typeof(TestPlayController).GetMethod("HandleCommand", InstancePrivate);
            Require(beginTrace != null && handle != null,
                "controller presentation adapter helpers are available", ref assertions);

            List<TestPlayPresentationEvent> raised = new List<TestPlayPresentationEvent>();
            int legacyEvents = 0;
            controller.PresentationEventRaised += value => raised.Add(value);
            controller.RuntimeEventRaised += value => legacyEvents++;
            controller.tick = 42;
            controller.currentAnimationIndex = 100;
            controller.scriptIndex = 3;
            beginTrace.Invoke(controller, null);

            InvokeCommand(handle, controller, "Snd", Values(9f));
            InvokeCommand(handle, controller, "Voice", new List<TestPlayScriptValue>
            {
                TestPlayScriptValue.Symbol("Damage")
            });
            InvokeCommand(handle, controller, "BURNER", Values(3f, 0.75f));
            InvokeCommand(handle, controller, "CamEffect", Values(2f));
            InvokeCommand(handle, controller, "RunProc2", Values(0f, 57f, 1f));
            InvokeCommand(handle, controller, "BURNER2", Values(1f));

            Require(raised.Count == 6, "all Phase 4 commands publish typed events", ref assertions);
            Require(raised[0].type == TestPlayPresentationEventType.Sound &&
                    raised[0].adapter == TestPlayPresentationAdapterKind.AudioClip,
                "controller resolves mapped audio before publishing", ref assertions);
            Require(raised[1].type == TestPlayPresentationEventType.Voice &&
                    raised[1].adapter == TestPlayPresentationAdapterKind.None,
                "missing voice mapping remains an event without a fake clip", ref assertions);
            Require(raised[2].type == TestPlayPresentationEventType.Burner &&
                    raised[2].adapter == TestPlayPresentationAdapterKind.None,
                "missing SPT burner mapping is separated from the original request", ref assertions);
            Require(raised[3].type == TestPlayPresentationEventType.CameraEffect &&
                    raised[3].adapter == TestPlayPresentationAdapterKind.None,
                "camera approximation is absent when no camera adapter is assigned", ref assertions);
            Require(raised[4].procType == 57 &&
                    raised[4].adapter == TestPlayPresentationAdapterKind.CombatOnly,
                "controller routes proc type 57 to combat", ref assertions);
            Require(raised[5].type == TestPlayPresentationEventType.Diagnostic &&
                    raised[5].command == "BURNER2",
                "controller traces rejected BURNER2", ref assertions);
            Require(raised[0].tick == 42 && raised[0].actionIndex == 100 && raised[0].scriptIndex == 3,
                "controller stamps presentation context", ref assertions);
            Require(legacyEvents == 11,
                "legacy command and specialized runtime events remain for external subscribers", ref assertions);

            string trace = controller.CapturePhase4TickTrace();
            Require(trace.Contains("\"presentation\":{") && trace.Contains("\"originalId\":9"),
                "controller exposes presentation events in Phase 4 trace", ref assertions);
            Require(trace.Contains("\"adapter\":\"AudioClip\"") &&
                    trace.Contains("\"adapter\":\"CombatOnly\""),
                "controller trace records selected adapter boundaries", ref assertions);
            Require(trace.Contains("OriginalParserRejectsCommandObject"),
                "controller trace retains unsupported-command diagnostics", ref assertions);
        }
        finally
        {
            if (clip != null)
                UnityEngine.Object.DestroyImmediate(clip);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void InvokeCommand(
        MethodInfo handle,
        TestPlayController controller,
        string command,
        List<TestPlayScriptValue> arguments)
    {
        handle.Invoke(controller, new object[] { command, arguments, command + "(...);" });
    }

    static List<TestPlayScriptValue> Values(params float[] values)
    {
        List<TestPlayScriptValue> result = new List<TestPlayScriptValue>();
        for (int i = 0; i < values.Length; i++)
            result.Add(TestPlayScriptValue.Number(values[i]));
        return result;
    }

    static void Require(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("[TestPlayPhase4Verification] " + message);
    }
}
