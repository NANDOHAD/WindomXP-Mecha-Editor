using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class TestPlayGoldenTraceVerification
{
    const string MechId = "ガンダムTR-1ヘイズル改";
    const string SelectedMechFolderEditorPref = "WindomXP.TestPlay.SelectedOriginalMechFolder";
    static bool running;

    sealed class RunCapture
    {
        public TestPlayGoldenTraceSession session;
        public readonly HashSet<int> observedActions = new HashSet<int>();
        public int riseActionEntries;
        public int riseActionTicks;
        public int riseFrameCount;
        public string riseScriptTiming = "";
        public int maximumRiseFrameIndex = -1;
        public int consecutiveRiseFinalFrameTicks;
        public int maximumRiseFinalFrameTicks;
        public bool sawRiseFinalFrameWhileHeld;
        public bool sawAirIdleAfterRiseRelease;
        public int lastAnimationIndex = int.MinValue;
        public int lastLogicalAction = int.MinValue;
        public bool sawRiseAction;
        public bool sawFirstStepTapWithoutStep;
        public int stepActionEntries;
        public int stepActionTicks;
        public int stepEntryTick = -1;
        public int stepEntryAction = -1;
        public int stepExitTick = -1;
        public int stepExitAction = -1;
        public bool stepEntryOnSecondTap;
        public bool stepExitedAfterRelease;
        public bool stepHorizontalMovementObserved;
        public bool stepEnergySemanticsValid = true;
        public float stepEnergyConsumed;
        public int stepEnergyDrainTicks;
        public bool sawFirstBoostTapWithoutBoost;
        public int boostActionEntries;
        public int boostActionTicks;
        public int boostEntryTick = -1;
        public int boostExitTick = -1;
        public int boostExitAction = -1;
        public bool boostEntryOnSecondTap;
        public bool boostExitedAfterRelease;
        public bool boostInitialFiveTicksStationary = true;
        public bool boostMovedAfterInitialWindow;
        public bool boostEnergySemanticsValid = true;
        public float boostEntryEnergyCost;
        public float boostPerTickEnergyConsumed;
        public int boostEnergyDrainTicks;
        public int airMoveActionEntries;
        public int airMoveActionTicks;
        public int airIdleEntriesAfterAirMove;
        public int airIdleTicksAfterAirMove;
        public bool airMoveSemanticsValid = true;
        public bool airIdleSemanticsValid = true;
        public bool airMoveExitedOnRelease;
        public bool sawAirIdleMoveDecay;
        public bool sawAirIdleVerticalBrake;
        public bool sawAirIdleBrakeBoundary;
        public bool sawAirIdleDescent;
        public bool hasGt004PreviousStep;
        public TestPlayMotionStep previousGt004Step;
    }

    [MenuItem("Tools/WindomXP/Test Play/Run Real-Mech Golden Traces")]
    public static async void RunFromMenu()
    {
        if (running)
        {
            Debug.LogWarning("[TestPlayGolden] A real-mech golden trace run is already active.");
            return;
        }

        running = true;
        try
        {
            await RunAllAsync();
        }
        catch (Exception ex)
        {
            Debug.LogError("[TestPlayGolden] Failed: " + ex);
        }
        finally
        {
            running = false;
        }
    }

    [MenuItem("Tools/WindomXP/Test Play/Run Selected-Mech GT-001 Reference Trace")]
    public static async void RunSelectedMechGt001FromMenu()
    {
        if (running)
        {
            Debug.LogWarning("[TestPlayGolden] A real-mech golden trace run is already active.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string selectedFolder = EditorUtility.OpenFolderPanel(
            "原作EXEで使用する機体フォルダーを選択",
            Path.Combine(projectRoot, "Windom_Data", "Robo"),
            "");
        if (string.IsNullOrEmpty(selectedFolder))
            return;

        EditorPrefs.SetString(SelectedMechFolderEditorPref, selectedFolder);

        await RunSelectedMechGt001FromFolderAsync(selectedFolder);
    }

    [MenuItem("Tools/WindomXP/Test Play/Run Last Selected-Mech GT-001 Reference Trace")]
    public static async void RunLastSelectedMechGt001FromMenu()
    {
        string selectedFolder = EditorPrefs.GetString(SelectedMechFolderEditorPref, "");
        if (string.IsNullOrWhiteSpace(selectedFolder))
        {
            Debug.LogError("[TestPlayGolden] No selected-mech folder is remembered. " +
                           "Run Selected-Mech GT-001 Reference Trace first.");
            return;
        }

        await RunSelectedMechGt001FromFolderAsync(selectedFolder);
    }

    static async Task RunSelectedMechGt001FromFolderAsync(string selectedFolder)
    {
        if (running)
        {
            Debug.LogWarning("[TestPlayGolden] A real-mech golden trace run is already active.");
            return;
        }

        running = true;
        try
        {
            await RunSelectedMechGt001Async(selectedFolder);
        }
        catch (Exception ex)
        {
            Debug.LogError("[TestPlayGolden] Selected-mech GT-001 failed: " + ex);
        }
        finally
        {
            running = false;
        }
    }

    static async Task RunAllAsync()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string mechFolder = Path.Combine(projectRoot, "Windom_Data", "Robo", MechId);
        string aniPath = Path.Combine(mechFolder, "Script.ani");
        string sptPath = Path.Combine(mechFolder, "Script.spt");
        if (!File.Exists(aniPath) || !File.Exists(sptPath))
            throw new FileNotFoundException("Real-mech Script.ani/Script.spt is missing: " + mechFolder);

        string aniHash = ComputeFileSha256(aniPath);
        string sptHash = ComputeFileSha256(sptPath);
        Debug.Log("[TestPlayGolden] Loading real ANI: " + MechId + " sha256=" + aniHash);

        ani2 data = new ani2();
        if (!await data.load(aniPath))
            throw new InvalidDataException("ani2.load returned false for " + aniPath);
        if (data.animations == null || data.animations.Count < 200)
            throw new InvalidDataException("Expected the original 200-slot ANI table.");

        CypherTranscoder transcoder = new CypherTranscoder();
        string sptText = USEncoder.ToEncoding.ToUnicode(transcoder.Transcode(sptPath));
        SptRuntimeData sptData = SptParser.Parse(sptText);
        string outputFolder = Path.Combine(projectRoot, "Logs", "TestPlayGolden");
        Directory.CreateDirectory(outputFolder);

        int passed = 0;
        IReadOnlyList<TestPlayGoldenScenarioDefinition> definitions = TestPlayGoldenScenarioCatalog.All;
        for (int i = 0; i < definitions.Count; i++)
        {
            TestPlayGoldenScenarioDefinition definition = definitions[i];
            ValidateRealDataRequirements(data, definition);
            RunCapture first = RunScenario(data, sptData, definition, aniHash, sptHash, MechId, mechFolder);
            RunCapture second = RunScenario(data, sptData, definition, aniHash, sptHash, MechId, mechFolder);
            int mismatch = TestPlayGoldenTraceSession.FindFirstMismatch(first.session, second.session);
            if (mismatch >= 0)
                throw new InvalidOperationException(definition.id + " diverged at tick index " + mismatch);
            ValidateObservedActions(definition, first.observedActions);
            ValidateFocusedScenarioOutcome(definition, first);

            string firstHash = first.session.ComputeTraceHash();
            string secondHash = second.session.ComputeTraceHash();
            if (!string.Equals(firstHash, secondHash, StringComparison.Ordinal))
                throw new InvalidOperationException(definition.id + " session hashes differ.");

            File.WriteAllText(
                Path.Combine(outputFolder, definition.id + ".unity-reference.jsonl"),
                first.session.SerializeJsonLines(),
                new UTF8Encoding(false));
            Debug.Log("[TestPlayGolden] " + definition.id + " passed ticks=" +
                      first.session.TickCount + " sha256=" + firstHash);
            passed++;
            await Task.Yield();
        }

        Debug.Log("[TestPlayGolden] Passed " + passed + "/" + definitions.Count +
                  " real-mech scenarios twice with exact tick-trace equality. Output=" + outputFolder);
    }

    public static async Task RunSelectedMechGt001Async(string mechFolder)
    {
        string aniPath = Path.Combine(mechFolder, "Script.ani");
        string sptPath = Path.Combine(mechFolder, "Script.spt");
        if (!File.Exists(aniPath) || !File.Exists(sptPath))
            throw new FileNotFoundException("Selected folder requires Script.ani and Script.spt: " + mechFolder);

        string mechId = new DirectoryInfo(mechFolder).Name;
        string aniHash = ComputeFileSha256(aniPath);
        string sptHash = ComputeFileSha256(sptPath);
        ani2 data = new ani2();
        if (!await data.load(aniPath))
            throw new InvalidDataException("ani2.load returned false for " + aniPath);
        if (data.animations == null || data.animations.Count < 200)
            throw new InvalidDataException("Expected the original 200-slot ANI table.");

        CypherTranscoder transcoder = new CypherTranscoder();
        string sptText = USEncoder.ToEncoding.ToUnicode(transcoder.Transcode(sptPath));
        SptRuntimeData sptData = SptParser.Parse(sptText);
        TestPlayGoldenScenarioDefinition definition = TestPlayGoldenScenarioCatalog.Find("GT-001");
        ValidateRealDataRequirements(data, definition);

        RunCapture first = RunScenario(data, sptData, definition, aniHash, sptHash, mechId, mechFolder);
        RunCapture second = RunScenario(data, sptData, definition, aniHash, sptHash, mechId, mechFolder);
        int mismatch = TestPlayGoldenTraceSession.FindFirstMismatch(first.session, second.session);
        if (mismatch >= 0)
            throw new InvalidOperationException("GT-001 diverged at tick index " + mismatch);
        ValidateObservedActions(definition, first.observedActions);
        if (!string.Equals(first.session.ComputeTraceHash(), second.session.ComputeTraceHash(),
                StringComparison.Ordinal))
            throw new InvalidOperationException("GT-001 session hashes differ.");

        string outputFolder = GetHashSpecificOutputFolder(aniHash, sptHash);
        Directory.CreateDirectory(outputFolder);
        string outputPath = Path.Combine(outputFolder, "GT-001.unity-reference.jsonl");
        File.WriteAllText(outputPath, first.session.SerializeJsonLines(), new UTF8Encoding(false));
        Debug.Log("[TestPlayGolden] Selected-mech GT-001 passed twice with exact tick-trace equality. " +
                  "mech=" + mechId + " ani=" + aniHash + " spt=" + sptHash + " Output=" + outputPath);
    }

    public static string GetHashSpecificOutputFolder(string aniHash, string sptHash)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, "Logs", "TestPlayGolden", "ByDataHash",
            NormalizeHash(aniHash).Substring(0, 16) + "_" + NormalizeHash(sptHash).Substring(0, 16));
    }

    static RunCapture RunScenario(
        ani2 data,
        SptRuntimeData sptData,
        TestPlayGoldenScenarioDefinition definition,
        string aniHash,
        string sptHash,
        string mechId,
        string mechFolder)
    {
        GameObject host = new GameObject("TestPlayGolden_" + definition.id);
        GameObject root = new GameObject("TestPlayGoldenRoot");
        GameObject targetObject = new GameObject("TestPlayGoldenTarget");
        try
        {
            RoboStructure robo = host.AddComponent<RoboStructure>();
            robo.root = root;
            robo.parts = new List<GameObject> { root };
            robo.ani = data;
            robo.folder = mechFolder;
            robo.transcoder = new CypherTranscoder();

            UI_SPT spt = host.AddComponent<UI_SPT>();
            spt.robo = robo;
            PropertyInfo property = typeof(UI_SPT).GetProperty(
                "LastSptData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            property.SetValue(spt, sptData, null);

            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            target.logHits = false;
            targetObject.transform.position = Vector3.forward * 2f;

            TestPlayController controller = host.AddComponent<TestPlayController>();
            controller.robo = robo;
            controller.sptSource = spt;
            controller.target = target;
            controller.useColliderGrounding = false;
            controller.logUnhandledCommands = false;
            controller.logMotionAssignments = false;
            controller.blendActionTransitions = false;
            controller.BeginDeterministicTraceSession(definition.setup);

            RunCapture capture = new RunCapture
            {
                session = new TestPlayGoldenTraceSession(new TestPlayGoldenSessionHeader
                {
                    schemaVersion = 1,
                    source = "unity-core",
                    scenarioId = definition.id,
                    mechId = mechId,
                    aniHash = aniHash,
                    sptHash = sptHash,
                    tickRate = Mathf.RoundToInt(controller.originalTickRate),
                    baseline = definition.baseline
                })
            };

            TestPlayGoldenInputFrame[] frames = definition.ExpandInputFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                float energyBeforeTick = controller.currentEnergy;
                Vector3 positionBeforeTick = root.transform.position;
                string tickTrace = controller.SimulateDeterministicTraceTick(frames[i]);
                capture.session.AddTick(tickTrace);
                capture.observedActions.Add(controller.currentAnimationIndex);
                capture.observedActions.Add(controller.CurrentActionSelection.logicalActionId);
                CaptureFocusedScenarioState(
                    definition.id,
                    capture,
                    controller,
                    frames[i],
                    i + 1,
                    energyBeforeTick,
                    positionBeforeTick,
                    root.transform.position);
            }
            controller.EndDeterministicTraceSession();
            return capture;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    static void ValidateRealDataRequirements(ani2 data, TestPlayGoldenScenarioDefinition definition)
    {
        for (int i = 0; i < definition.requiredActionIds.Length; i++)
        {
            int actionId = definition.requiredActionIds[i];
            if (!HasUsableAction(data, actionId))
                throw new InvalidDataException(definition.id + " requires missing ANI action " + actionId);
        }

        for (int i = 0; i < definition.requiredCommands.Length; i++)
        {
            string command = definition.requiredCommands[i];
            if (!ContainsCommand(data, command))
                throw new InvalidDataException(definition.id + " requires missing ANI command " + command);
        }
    }

    static void ValidateObservedActions(
        TestPlayGoldenScenarioDefinition definition,
        HashSet<int> observedActions)
    {
        for (int i = 0; i < definition.requiredActionIds.Length; i++)
        {
            int actionId = definition.requiredActionIds[i];
            if (!observedActions.Contains(actionId))
                throw new InvalidOperationException(definition.id + " did not observe action " + actionId);
        }
    }

    static void CaptureFocusedScenarioState(
        string scenarioId,
        RunCapture capture,
        TestPlayController controller,
        TestPlayGoldenInputFrame input,
        int traceTick,
        float energyBeforeTick,
        Vector3 positionBeforeTick,
        Vector3 positionAfterTick)
    {
        int action = controller.currentAnimationIndex;
        int logicalAction = controller.CurrentActionSelection.logicalActionId;
        if (action == controller.riseAction)
        {
            capture.sawRiseAction = true;
            if (capture.lastAnimationIndex != controller.riseAction)
                capture.riseActionEntries++;
            capture.riseActionTicks++;

            animation riseAnimation = controller.robo != null && controller.robo.ani != null &&
                                      controller.robo.ani.animations != null &&
                                      controller.riseAction >= 0 &&
                                      controller.riseAction < controller.robo.ani.animations.Count
                ? controller.robo.ani.animations[controller.riseAction]
                : null;
            int frameCount = riseAnimation != null && riseAnimation.frames != null
                ? riseAnimation.frames.Count
                : 0;
            capture.riseFrameCount = frameCount;
            if (string.IsNullOrEmpty(capture.riseScriptTiming) &&
                riseAnimation != null && riseAnimation.scripts != null)
            {
                StringBuilder timing = new StringBuilder();
                for (int i = 0; i < riseAnimation.scripts.Count; i++)
                {
                    if (i > 0)
                        timing.Append(',');
                    timing.Append(i).Append(':')
                        .Append(riseAnimation.scripts[i].unk).Append('/')
                        .Append(riseAnimation.scripts[i].time.ToString("R", CultureInfo.InvariantCulture));
                }
                capture.riseScriptTiming = timing.ToString();
            }
            capture.maximumRiseFrameIndex = Mathf.Max(
                capture.maximumRiseFrameIndex, controller.frameIndex);
            if (input.rise &&
                capture.riseActionTicks >= controller.riseMinimumReleaseTicks &&
                frameCount > 0 &&
                controller.frameIndex == frameCount - 1)
            {
                capture.consecutiveRiseFinalFrameTicks++;
                capture.maximumRiseFinalFrameTicks = Mathf.Max(
                    capture.maximumRiseFinalFrameTicks,
                    capture.consecutiveRiseFinalFrameTicks);
                // The representative real ANI holds its last HOD frame in a
                // long zero-progress script block instead of reaching the
                // synthetic end-pose flag. Require a sustained hold so a
                // transient final frame or animation loop cannot pass.
                if (capture.consecutiveRiseFinalFrameTicks >= 10)
                    capture.sawRiseFinalFrameWhileHeld = true;
            }
            else
                capture.consecutiveRiseFinalFrameTicks = 0;
        }
        else if (capture.sawRiseAction && !input.rise && action == controller.airIdleAction)
        {
            capture.sawAirIdleAfterRiseRelease = true;
        }

        float energyDelta = energyBeforeTick - controller.currentEnergy;
        Vector3 horizontalDelta = positionAfterTick - positionBeforeTick;
        horizontalDelta.y = 0f;

        if (scenarioId == "GT-004")
        {
            CaptureAirMoveReleaseScenarioState(
                capture, controller, input, logicalAction, energyDelta);
        }
        else if (scenarioId == "GT-005")
        {
            CaptureStepScenarioState(
                capture, controller, input, traceTick, logicalAction, energyDelta, horizontalDelta);
        }
        else if (scenarioId == "GT-006")
        {
            CaptureBoostScenarioState(
                capture, controller, input, traceTick, logicalAction, energyDelta, horizontalDelta);
        }

        capture.lastAnimationIndex = action;
        capture.lastLogicalAction = logicalAction;
    }

    static void CaptureAirMoveReleaseScenarioState(
        RunCapture capture,
        TestPlayController controller,
        TestPlayGoldenInputFrame input,
        int logicalAction,
        float energyDelta)
    {
        TestPlayMotionStep step = controller.LastMotionStep;
        bool wasAirMove = capture.lastLogicalAction == controller.airMoveAction;
        bool isAirMove = logicalAction == controller.airMoveAction;
        bool isAirIdleAfterMove = capture.airMoveActionTicks > 0 &&
                                  logicalAction == controller.airIdleAction;

        if (isAirMove)
        {
            if (!wasAirMove)
                capture.airMoveActionEntries++;
            capture.airMoveActionTicks++;

            float expectedForceY = capture.airMoveActionTicks <= 5 ? 0.04f : 0.02f;
            capture.airMoveSemanticsValid &= input.direction == 8 &&
                NearlyEqual(energyDelta, 0f) &&
                NearlyEqual(step.forcePerTick.x, 0f) &&
                NearlyEqual(step.forcePerTick.y, expectedForceY) &&
                NearlyEqual(step.forcePerTick.z, 0f) &&
                NearlyEqual(step.scriptedVelocityBeforeRetention.x, 0f) &&
                NearlyEqual(step.scriptedVelocityBeforeRetention.y, 0f) &&
                NearlyEqual(step.scriptedVelocityBeforeRetention.z, 0.06f) &&
                NearlyEqual(step.velocityAfterForce.y,
                    step.velocityAfterRiseClamp.y + expectedForceY);
        }

        if (wasAirMove && isAirIdleAfterMove)
        {
            capture.airIdleEntriesAfterAirMove++;
            capture.airMoveExitedOnRelease = input.direction == 0;
        }

        if (isAirIdleAfterMove)
        {
            capture.airIdleTicksAfterAirMove++;
            capture.airIdleSemanticsValid &= input.direction == 0 &&
                NearlyEqual(energyDelta, 0f) &&
                VectorsNearlyEqual(step.forcePerTick, Vector3.zero) &&
                NearlyEqual(step.scriptedMoveRetention, 0.99f) &&
                VectorsNearlyEqual(
                    step.scriptedVelocityAfterRetention,
                    step.scriptedVelocityBeforeRetention * 0.99f);

            if (step.scriptedVelocityAfterRetention.sqrMagnitude + 0.00000001f <
                step.scriptedVelocityBeforeRetention.sqrMagnitude)
            {
                capture.sawAirIdleMoveDecay = true;
            }

            if (capture.hasGt004PreviousStep)
            {
                capture.airIdleSemanticsValid &= VectorsNearlyEqual(
                    step.scriptedVelocityBeforeRetention,
                    capture.previousGt004Step.scriptedVelocityAfterRetention);

                bool brakeExpected =
                    capture.airIdleTicksAfterAirMove < controller.airIdleVerticalBrakeTicks &&
                    capture.previousGt004Step.velocityAfterMultiplier.y <
                        controller.airIdleVerticalBrakeVelocityThreshold;
                float expectedVerticalAdjustment = brakeExpected
                    ? controller.airIdleVerticalBrakePerTick
                    : 0f;
                float actualVerticalAdjustment =
                    step.velocityBefore.y - capture.previousGt004Step.velocityAfterMultiplier.y;
                capture.airIdleSemanticsValid &= NearlyEqual(
                    actualVerticalAdjustment, expectedVerticalAdjustment);
                if (brakeExpected && NearlyEqual(
                        actualVerticalAdjustment, controller.airIdleVerticalBrakePerTick))
                {
                    capture.sawAirIdleVerticalBrake = true;
                }
                if (capture.airIdleTicksAfterAirMove == controller.airIdleVerticalBrakeTicks &&
                    NearlyEqual(actualVerticalAdjustment, 0f))
                {
                    capture.sawAirIdleBrakeBoundary = true;
                }
            }

            if (step.velocityAfterMultiplier.y < 0f)
                capture.sawAirIdleDescent = true;
        }

        capture.previousGt004Step = step;
        capture.hasGt004PreviousStep = true;
    }

    static void CaptureStepScenarioState(
        RunCapture capture,
        TestPlayController controller,
        TestPlayGoldenInputFrame input,
        int traceTick,
        int logicalAction,
        float energyDelta,
        Vector3 horizontalDelta)
    {
        bool wasStep = IsStepAction(controller, capture.lastLogicalAction);
        bool isStep = IsStepAction(controller, logicalAction);
        if (traceTick == 1 && input.direction != 0 && !isStep)
            capture.sawFirstStepTapWithoutStep = true;

        if (isStep)
        {
            if (!wasStep)
            {
                capture.stepActionEntries++;
                capture.stepEntryTick = traceTick;
                capture.stepEntryAction = logicalAction;
                capture.stepEntryOnSecondTap =
                    capture.sawFirstStepTapWithoutStep && input.direction == 8;
                capture.stepEnergySemanticsValid &= NearlyEqual(energyDelta, 0f);
            }

            capture.stepActionTicks++;
            if (horizontalDelta.sqrMagnitude > 0.000001f)
                capture.stepHorizontalMovementObserved = true;
        }

        // UpdateOriginalMovementEnergy runs before action selection. Therefore
        // the entry tick is free, every following active tick costs 4, and the
        // transition tick pays the final 4 before leaving the step action.
        if (wasStep)
        {
            capture.stepEnergyDrainTicks++;
            capture.stepEnergyConsumed += energyDelta;
            capture.stepEnergySemanticsValid &=
                NearlyEqual(energyDelta, Mathf.Max(0f, controller.stepEnergyPerTick));
        }

        if (wasStep && !isStep)
        {
            capture.stepExitTick = traceTick;
            capture.stepExitAction = logicalAction;
            capture.stepExitedAfterRelease =
                input.direction == 0 && logicalAction == controller.stepLandingAction;
        }
    }

    static void CaptureBoostScenarioState(
        RunCapture capture,
        TestPlayController controller,
        TestPlayGoldenInputFrame input,
        int traceTick,
        int logicalAction,
        float energyDelta,
        Vector3 horizontalDelta)
    {
        bool wasBoost = capture.lastLogicalAction == controller.boostAction;
        bool isBoost = logicalAction == controller.boostAction;
        if (traceTick == 1 && input.rise && !isBoost)
            capture.sawFirstBoostTapWithoutBoost = true;

        if (isBoost)
        {
            if (!wasBoost)
            {
                capture.boostActionEntries++;
                capture.boostEntryTick = traceTick;
                capture.boostEntryOnSecondTap =
                    capture.sawFirstBoostTapWithoutBoost && input.rise;
                capture.boostEntryEnergyCost = energyDelta;
                float expectedEntryCost = Mathf.Floor(Mathf.Max(0f, controller.maximumEnergy) / 5f);
                capture.boostEnergySemanticsValid &= NearlyEqual(energyDelta, expectedEntryCost);
            }

            capture.boostActionTicks++;
            if (capture.boostActionTicks <= 5)
            {
                if (horizontalDelta.sqrMagnitude > 0.000001f)
                    capture.boostInitialFiveTicksStationary = false;
            }
            else if (horizontalDelta.sqrMagnitude > 0.000001f)
            {
                capture.boostMovedAfterInitialWindow = true;
            }
        }

        // As with step, the normal per-tick drain begins on the tick after
        // entry and includes the tick that transitions out of action 22.
        if (wasBoost)
        {
            capture.boostEnergyDrainTicks++;
            capture.boostPerTickEnergyConsumed += energyDelta;
            capture.boostEnergySemanticsValid &=
                NearlyEqual(energyDelta, Mathf.Max(0f, controller.boostEnergyPerTick));
        }

        if (wasBoost && !isBoost)
        {
            capture.boostExitTick = traceTick;
            capture.boostExitAction = logicalAction;
            capture.boostExitedAfterRelease =
                !input.rise && logicalAction == controller.airIdleAction;
        }
    }

    static bool IsStepAction(TestPlayController controller, int action)
    {
        return action == controller.forwardStepAction ||
               action == controller.backStepAction ||
               action == controller.leftStepAction ||
               action == controller.rightStepAction;
    }

    static bool NearlyEqual(float actual, float expected)
    {
        return Mathf.Abs(actual - expected) < 0.0001f;
    }

    static bool VectorsNearlyEqual(Vector3 actual, Vector3 expected)
    {
        return (actual - expected).sqrMagnitude < 0.00000001f;
    }

    static void ValidateFocusedScenarioOutcome(
        TestPlayGoldenScenarioDefinition definition,
        RunCapture capture)
    {
        if (definition.id == "GT-002")
        {
            if (capture.riseActionEntries != 1 || !capture.sawAirIdleAfterRiseRelease)
            {
                throw new InvalidOperationException(
                    "GT-002 must enter rise action 7 once and leave it for air-stop action 8 after Z release.");
            }
            return;
        }

        if (definition.id == "GT-003" &&
            (capture.riseActionEntries != 1 ||
             !capture.sawRiseFinalFrameWhileHeld ||
             !capture.sawAirIdleAfterRiseRelease))
        {
            throw new InvalidOperationException(
                "GT-003 must play rise action 7 once, keep its final HOD frame while Z is held, " +
                "and leave it for air-stop action 8 after release. " +
                "entries=" + capture.riseActionEntries +
                " riseTicks=" + capture.riseActionTicks +
                " maxFrame=" + capture.maximumRiseFrameIndex +
                " frameCount=" + capture.riseFrameCount +
                " scriptTiming=" + capture.riseScriptTiming +
                " finalFrameTicks=" + capture.maximumRiseFinalFrameTicks +
                " finalFrameHeld=" + capture.sawRiseFinalFrameWhileHeld +
                " airIdleAfterRelease=" + capture.sawAirIdleAfterRiseRelease);
        }

        if (definition.id == "GT-004")
        {
            if (capture.airMoveActionEntries != 1 ||
                capture.airMoveActionTicks != 15 ||
                capture.airIdleEntriesAfterAirMove != 1 ||
                capture.airIdleTicksAfterAirMove != 35 ||
                !capture.airMoveExitedOnRelease ||
                !capture.airMoveSemanticsValid ||
                !capture.airIdleSemanticsValid ||
                !capture.sawAirIdleMoveDecay ||
                !capture.sawAirIdleVerticalBrake ||
                !capture.sawAirIdleBrakeBoundary ||
                !capture.sawAirIdleDescent)
            {
                throw new InvalidOperationException(
                    "GT-004 must run real-ANI air-move action 4 for 15 ticks with Move(0,0,0.06) " +
                    "and Force Y 0.04 then 0.02, release to air-stop action 8 for 35 ticks, retain " +
                    "inherited Move by 0.99, apply +0.012 vertical braking only before tick 31 " +
                    "below Y 0.05, then resume gravity-driven descent. " +
                    "airMoveEntries=" + capture.airMoveActionEntries +
                    " airMoveTicks=" + capture.airMoveActionTicks +
                    " airIdleEntries=" + capture.airIdleEntriesAfterAirMove +
                    " airIdleTicks=" + capture.airIdleTicksAfterAirMove +
                    " released=" + capture.airMoveExitedOnRelease +
                    " airMoveValid=" + capture.airMoveSemanticsValid +
                    " airIdleValid=" + capture.airIdleSemanticsValid +
                    " moveDecay=" + capture.sawAirIdleMoveDecay +
                    " verticalBrake=" + capture.sawAirIdleVerticalBrake +
                    " brakeBoundary=" + capture.sawAirIdleBrakeBoundary +
                    " descended=" + capture.sawAirIdleDescent);
            }
            return;
        }

        if (definition.id == "GT-005")
        {
            bool durationWithinOriginalBoundary =
                capture.stepActionTicks >= 16 && capture.stepActionTicks <= 61;
            bool totalEnergyMatches = NearlyEqual(
                capture.stepEnergyConsumed,
                capture.stepEnergyDrainTicks * 4f);
            if (!capture.sawFirstStepTapWithoutStep ||
                capture.stepActionEntries != 1 ||
                capture.stepEntryAction != 11 ||
                !capture.stepEntryOnSecondTap ||
                !durationWithinOriginalBoundary ||
                !capture.stepExitedAfterRelease ||
                !capture.stepHorizontalMovementObserved ||
                !capture.stepEnergySemanticsValid ||
                capture.stepEnergyDrainTicks != capture.stepActionTicks ||
                !totalEnergyMatches)
            {
                throw new InvalidOperationException(
                    "GT-005 must reject the first direction tap, enter forward step action 11 once " +
                    "on the second tap, move horizontally, drain 4 movement-energy units per step tick, " +
                    "and leave through grounded step landing action 6 after release inside ticks 16-61. " +
                    "firstTapRejected=" + capture.sawFirstStepTapWithoutStep +
                    " entries=" + capture.stepActionEntries +
                    " entryTick=" + capture.stepEntryTick +
                    " entryAction=" + capture.stepEntryAction +
                    " entryOnSecondTap=" + capture.stepEntryOnSecondTap +
                    " actionTicks=" + capture.stepActionTicks +
                    " exitTick=" + capture.stepExitTick +
                    " exitAction=" + capture.stepExitAction +
                    " moved=" + capture.stepHorizontalMovementObserved +
                    " energyValid=" + capture.stepEnergySemanticsValid +
                    " drainTicks=" + capture.stepEnergyDrainTicks +
                    " consumed=" + capture.stepEnergyConsumed.ToString("R", CultureInfo.InvariantCulture));
            }
            return;
        }

        if (definition.id == "GT-006")
        {
            bool durationPastOriginalReleaseBoundary = capture.boostActionTicks >= 31;
            bool totalEnergyMatches = NearlyEqual(
                capture.boostPerTickEnergyConsumed,
                capture.boostEnergyDrainTicks * 5f);
            if (!capture.sawFirstBoostTapWithoutBoost ||
                capture.boostActionEntries != 1 ||
                !capture.boostEntryOnSecondTap ||
                !durationPastOriginalReleaseBoundary ||
                !capture.boostExitedAfterRelease ||
                !capture.boostInitialFiveTicksStationary ||
                !capture.boostMovedAfterInitialWindow ||
                !capture.boostEnergySemanticsValid ||
                capture.boostEnergyDrainTicks != capture.boostActionTicks ||
                !totalEnergyMatches)
            {
                throw new InvalidOperationException(
                    "GT-006 must reject the first Z tap, enter boost action 22 once on the second tap, " +
                    "charge Generator/5 once, drain 5 movement-energy units per boost tick, keep the " +
                    "representative ANI stationary for its first five action ticks, then move and leave " +
                    "through air-stop action 8 after release beyond tick 30. " +
                    "firstTapRejected=" + capture.sawFirstBoostTapWithoutBoost +
                    " entries=" + capture.boostActionEntries +
                    " entryTick=" + capture.boostEntryTick +
                    " entryOnSecondTap=" + capture.boostEntryOnSecondTap +
                    " actionTicks=" + capture.boostActionTicks +
                    " exitTick=" + capture.boostExitTick +
                    " exitAction=" + capture.boostExitAction +
                    " entryCost=" + capture.boostEntryEnergyCost.ToString("R", CultureInfo.InvariantCulture) +
                    " initialStationary=" + capture.boostInitialFiveTicksStationary +
                    " movedAfterInitial=" + capture.boostMovedAfterInitialWindow +
                    " energyValid=" + capture.boostEnergySemanticsValid +
                    " drainTicks=" + capture.boostEnergyDrainTicks +
                    " perTickConsumed=" + capture.boostPerTickEnergyConsumed.ToString("R", CultureInfo.InvariantCulture));
            }
        }
    }

    static bool HasUsableAction(ani2 data, int actionId)
    {
        if (data == null || data.animations == null || actionId < 0 || actionId >= data.animations.Count)
            return false;
        animation candidate = data.animations[actionId];
        return candidate != null &&
               (!string.IsNullOrWhiteSpace(candidate.squirrelInit) ||
                (candidate.scripts != null && candidate.scripts.Count > 0) ||
                (candidate.frames != null && candidate.frames.Count > 0));
    }

    static bool ContainsCommand(ani2 data, string command)
    {
        if (data == null || data.animations == null || string.IsNullOrWhiteSpace(command))
            return false;
        for (int i = 0; i < data.animations.Count; i++)
        {
            animation candidate = data.animations[i];
            if (candidate == null)
                continue;
            if (!string.IsNullOrEmpty(candidate.squirrelInit) &&
                candidate.squirrelInit.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (candidate.scripts == null)
                continue;
            for (int scriptIndex = 0; scriptIndex < candidate.scripts.Count; scriptIndex++)
            {
                if (!string.IsNullOrEmpty(candidate.scripts[scriptIndex].squirrel) &&
                    candidate.scripts[scriptIndex].squirrel.IndexOf(command, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    static string ComputeFileSha256(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(stream);
            StringBuilder builder = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    static string NormalizeHash(string value)
    {
        string normalized = (value ?? "").Trim().ToLowerInvariant();
        if (normalized.Length != 64)
            throw new ArgumentException("SHA-256 must contain exactly 64 hexadecimal characters.", nameof(value));
        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                throw new ArgumentException("SHA-256 contains a non-hexadecimal character.", nameof(value));
        }
        return normalized;
    }
}
