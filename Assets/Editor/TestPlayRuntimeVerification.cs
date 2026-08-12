using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TestPlayRuntimeVerification
{
    const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("Tools/WindomXP/Test Play/Run Runtime Verification")]
    public static void RunFromMenu()
    {
        int assertions = RunAll();
        Debug.Log("[TestPlayVerification] Passed " + assertions + " assertions.");
    }

    public static int RunAll()
    {
        int assertions = 0;
        VerifyParserAndState(ref assertions);
        VerifySptStatusInitialization(ref assertions);
        VerifyNestedIf(ref assertions);
        VerifyFlowInterruption(ref assertions);
        VerifyRepeatInterval(ref assertions);
        VerifyOriginalAttackAndBurnerArguments(ref assertions);
        VerifyOriginalSoundIds(ref assertions);
        VerifyOriginalTextureTables(ref assertions);
        VerifyOriginalSimulationClockAndLock(ref assertions);
        VerifyCameraControllerLifecycle(ref assertions);
        VerifyMovementReferenceModes(ref assertions);
        VerifyOriginalDirectionMovement(ref assertions);
        VerifyOriginalJumpAndBoost(ref assertions);
        return assertions;
    }

    static void VerifyParserAndState(ref int assertions)
    {
        TestPlayStateTable state = new TestPlayStateTable();
        state.ResetDefaults();
        TestPlayScriptVM vm = new TestPlayScriptVM(state);
        List<string> commands = new List<string>();
        int fallbackAssignments = 0;
        vm.commandHandler = (name, args, raw) => commands.Add(name);
        vm.assignmentHandler = (name, op, values, raw) => fallbackAssignments++;

        vm.Execute("@int[0]=2; @int[0]+=3; Move(1,STOP,2); BURNER(4,0.5); ' ignored\r\n@float[1]=1.25;");

        Require(state.GetInt(0) == 5, "compound @int assignment", ref assertions);
        Require(Mathf.Approximately(state.GetFloat(1), 1.25f), "invariant @float literal", ref assertions);
        Require(commands.Count == 2 && commands[0] == "Move" && commands[1] == "BURNER", "semicolon command splitting", ref assertions);

        vm.Execute("@int[200]=99;");
        Require(state.GetInt(200) == 0, "script variables are limited to 0..199", ref assertions);
        Require(fallbackAssignments == 1, "out-of-range reference is diagnosed as unsupported", ref assertions);
    }

    static void VerifySptStatusInitialization(ref int assertions)
    {
        SptRuntimeData data = SptParser.Parse(
            "Name=StatusTest;\n" +
            "NameEng=StatusTestEN;\n" +
            "HP=5200;\n" +
            "Generator=3600;\n" +
            "Energy=1700;\n" +
            "Score=800;\n" +
            "RestBody=2;\n" +
            "LockDist=125;\n" +
            "SubLockDist(0,90);\n" +
            "SubLockDist=3,45;\n");

        Require(data.HasHP && data.HP == 5200, "SPT HP is parsed with presence", ref assertions);
        Require(data.HasGenerator && data.Generator == 3600, "SPT Generator is parsed", ref assertions);
        Require(data.HasEnergy && data.Energy == 1700, "SPT Energy is parsed separately", ref assertions);
        Require(data.HasScore && data.Score == 800 && data.HasRestBody && data.RestBody == 2,
            "SPT Score and RestBody are parsed", ref assertions);
        Require(data.HasLockDist && Mathf.Approximately(data.LockDist, 125f), "SPT LockDist is parsed", ref assertions);
        Require(data.SubLockDistances.Count == 2 && data.SubLockDistances[0] == 90 && data.SubLockDistances[3] == 45,
            "SPT SubLockDist command and compatibility forms are parsed", ref assertions);

        GameObject sptObject = new GameObject("TestPlayVerification_SPT");
        GameObject controllerObject = new GameObject("TestPlayVerification_SPTController");
        try
        {
            UI_SPT spt = sptObject.AddComponent<UI_SPT>();
            SetField(spt, "<LastSptData>k__BackingField", data);
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            controller.sptSource = spt;
            controller.state.ResetDefaults();

            MethodInfo initialize = typeof(TestPlayController).GetMethod("InitializeOriginalSptStatus", InstancePrivate);
            MethodInfo energyTick = typeof(TestPlayController).GetMethod("UpdateOriginalMovementEnergy", InstancePrivate);
            initialize.Invoke(controller, null);

            Require(Mathf.Approximately(controller.maximumHP, 5200f) &&
                    Mathf.Approximately(controller.currentHP, 5200f),
                "test player HP is initialized from SPT HP", ref assertions);
            Require(Mathf.Approximately(controller.maximumEnergy, 3600f) &&
                    Mathf.Approximately(controller.currentEnergy, 3600f),
                "movement gauge is initialized from SPT Generator", ref assertions);
            Require(Mathf.Approximately(controller.maximumAuxiliaryEnergy, 1700f) &&
                    Mathf.Approximately(controller.currentAuxiliaryEnergy, 1700f),
                "auxiliary gauge is initialized independently from SPT Energy", ref assertions);
            Require(controller.state.GetInt(100) == 3600 && controller.state.GetInt(101) == 3600 &&
                    controller.state.GetInt(102) == 1700 && controller.state.GetInt(103) == 1700,
                "original integer resource slots 100..103 mirror SPT runtime values", ref assertions);
            Require(controller.configuredScore == 800 && controller.configuredRestBody == 2,
                "SPT Score and RestBody reach test-play runtime state", ref assertions);

            controller.SetAirborneFlag(true);
            controller.state.SetInt(100, 3500);
            controller.state.SetInt(103, 1600);
            energyTick.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 3500f),
                "original @int[100] changes update the movement gauge", ref assertions);
            Require(Mathf.Approximately(controller.currentAuxiliaryEnergy, 1600f),
                "original @int[103] changes update the auxiliary gauge", ref assertions);

            controller.ApplySelfDamage(200f);
            Require(Mathf.Approximately(controller.currentHP, 5000f), "self damage uses SPT HP capacity", ref assertions);
            controller.RestoreSelfHP(500f);
            Require(Mathf.Approximately(controller.currentHP, 5200f), "self HP recovery clamps to SPT maximum", ref assertions);
            Require(controller.TryGetSubLockDistance(3, out float subLockDistance) &&
                    Mathf.Approximately(subLockDistance, 45f),
                "parsed SubLockDist is exposed to future sub-target actions", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(sptObject);
        }
    }

    static void VerifyNestedIf(ref int assertions)
    {
        TestPlayStateTable state = new TestPlayStateTable();
        TestPlayScriptVM vm = new TestPlayScriptVM(state);
        vm.Execute(
            "@int[10]=1;" +
            "IF(@int[10],==,1);" +
            " IF(3,>,2); @int[11]=7; ELSE; @int[11]=8; ENDIF;" +
            "ELSE; @int[11]=9; ENDIF;" +
            "IF(2,!=,3); @int[12]=4; ENDIF;");

        Require(state.GetInt(11) == 7, "nested IF/ELSE/ENDIF", ref assertions);
        Require(state.GetInt(12) == 4, "six original IF comparators", ref assertions);
    }

    static void VerifyFlowInterruption(ref int assertions)
    {
        TestPlayStateTable state = new TestPlayStateTable();
        TestPlayScriptVM vm = new TestPlayScriptVM(state);
        List<string> commands = new List<string>();
        bool stop = false;
        vm.commandHandler = (name, args, raw) =>
        {
            commands.Add(name);
            if (name == "GoScriptIndex")
                stop = true;
        };
        vm.shouldStopExecution = () => stop;
        vm.Execute("Snd(1);GoScriptIndex(3);Voice(AfterJump);");

        Require(commands.Count == 2, "flow command stops the remaining instruction scan", ref assertions);
        Require(commands[1] == "GoScriptIndex", "flow stops at the jump command", ref assertions);
    }

    static void VerifyRepeatInterval(ref int assertions)
    {
        GameObject go = new GameObject("TestPlayVerification_Controller");
        try
        {
            TestPlayController controller = go.AddComponent<TestPlayController>();
            SetField(controller, "executeScriptRepeatedly", true);
            SetField(controller, "scriptRepeatInterval", 2);
            SetField(controller, "scriptRepeatCounter", 2);
            MethodInfo consume = typeof(TestPlayController).GetMethod("ConsumeScriptRepeatTick", InstancePrivate);

            Require(!(bool)consume.Invoke(controller, null), "repeat interval tick 1", ref assertions);
            Require(!(bool)consume.Invoke(controller, null), "repeat interval tick 2", ref assertions);
            Require((bool)consume.Invoke(controller, null), "ExecScriptEveryTime(2) repeats on tick 3", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void VerifyOriginalAttackAndBurnerArguments(ref int assertions)
    {
        GameObject go = new GameObject("TestPlayVerification_Commands");
        try
        {
            TestPlayController controller = go.AddComponent<TestPlayController>();
            controller.logUnhandledCommands = false;
            MethodInfo handle = typeof(TestPlayController).GetMethod("HandleCommand", InstancePrivate);
            SetField(controller, "moveCommand", new Vector3(1f, 2f, 3f));
            handle.Invoke(controller, new object[]
            {
                "Move",
                new List<TestPlayScriptValue>
                {
                    TestPlayScriptValue.Number(0f),
                    TestPlayScriptValue.Stop(),
                    TestPlayScriptValue.Number(4f)
                },
                "Move(0,STOP,4);"
            });
            Vector3 move = (Vector3)GetField(controller, "moveCommand");
            Require(move == new Vector3(1f, 0f, 4f), "Move numeric zero preserves and STOP clears an axis", ref assertions);
            MethodInfo resetBlock = typeof(TestPlayController).GetMethod("ResetOriginalBlockState", InstancePrivate);
            resetBlock.Invoke(controller, null);
            Require((Vector3)GetField(controller, "moveCommand") == Vector3.zero, "block entry resets original transient state", ref assertions);

            handle.Invoke(controller, new object[]
            {
                "ATTACK",
                Values(80f, 150f, 0.4f, 2f),
                "ATTACK(80,150,0.4,2);"
            });

            Require(controller.attackProfile.power == 80, "ATTACK power", ref assertions);
            Require(controller.attackProfile.down == 150, "ATTACK down", ref assertions);
            Require(Mathf.Approximately(controller.attackProfile.force, 0.4f), "ATTACK force", ref assertions);
            Require(Mathf.Approximately(controller.attackProfile.forceY, 2f), "ATTACK forceY", ref assertions);

            handle.Invoke(controller, new object[]
            {
                "BURNER",
                Values(3f, 0.75f),
                "BURNER(3,0.75);"
            });
            Dictionary<int, float> outputs = (Dictionary<int, float>)GetField(controller, "burnerRequestedOutputs");
            Require(outputs.ContainsKey(3) && Mathf.Approximately(outputs[3], 0.75f), "BURNER output value", ref assertions);

            handle.Invoke(controller, new object[]
            {
                "BURNER",
                Values(20f, 1f),
                "BURNER(20,1);"
            });
            Require(!outputs.ContainsKey(20), "BURNER id range 0..19", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    static void VerifyOriginalSoundIds(ref int assertions)
    {
        RequireSound(0, "shot.wav", ref assertions);
        RequireSound(20, "magic02.wav", ref assertions);
        RequireSound(97, "PI2.wav", ref assertions);
        RequireSound(98, "PI.wav", ref assertions);
        RequireSound(99, "cursor27.wav", ref assertions);

        Require(!TestPlayOriginalSoundSetup.TryGetFileName(21, out _), "unconfirmed Snd IDs stay unmapped", ref assertions);
    }

    static void RequireSound(int id, string expectedFileName, ref int assertions)
    {
        bool found = TestPlayOriginalSoundSetup.TryGetFileName(id, out string fileName);
        Require(found && string.Equals(fileName, expectedFileName, StringComparison.OrdinalIgnoreCase), "original Snd(" + id + ") mapping", ref assertions);
    }

    static void VerifyOriginalTextureTables(ref int assertions)
    {
        RequireLoadTexture(0, "Beam.bmp", ref assertions);
        RequireLoadTexture(10, "GSmoke.png", ref assertions);
        RequireLoadTexture(13, "beam2.png", ref assertions);
        RequireLoadTexture(43, "burner4.png", ref assertions);
        RequireScriptTexture(6, "GSmoke.png", ref assertions);
        RequireScriptTexture(12, "sabel.png", ref assertions);
        RequireScriptTexture(14, "beam2.png", ref assertions);
        RequireScriptTexture(39, "blueLight.bmp", ref assertions);
        Require(!TestPlayOriginalTextureSetup.TryGetScriptTextureFileName(11, out _), "guide texture id 11 stays unmapped", ref assertions);
    }

    static void RequireLoadTexture(int loadSequence, string expectedFileName, ref int assertions)
    {
        bool found = TestPlayOriginalTextureSetup.TryGetLoadSequenceFileName(loadSequence, out string fileName);
        Require(found && string.Equals(fileName, expectedFileName, StringComparison.OrdinalIgnoreCase),
            "original texture load sequence " + loadSequence, ref assertions);
    }

    static void RequireScriptTexture(int textureId, string expectedFileName, ref int assertions)
    {
        bool found = TestPlayOriginalTextureSetup.TryGetScriptTextureFileName(textureId, out string fileName);
        Require(found && string.Equals(fileName, expectedFileName, StringComparison.OrdinalIgnoreCase),
            "guide script texture id " + textureId, ref assertions);
    }

    static void VerifyOriginalSimulationClockAndLock(ref int assertions)
    {
        GameObject controllerObject = new GameObject("TestPlayVerificationClockLockController");
        GameObject rootObject = new GameObject("TestPlayVerificationClockLockRoot");
        GameObject targetObject = new GameObject("TestPlayVerificationClockLockTarget");
        GameObject aimObject = new GameObject("TestPlayVerificationClockLockAim");
        try
        {
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            RoboStructure robo = controllerObject.AddComponent<RoboStructure>();
            UI_SPT spt = controllerObject.AddComponent<UI_SPT>();
            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            robo.root = rootObject;
            controller.robo = robo;
            controller.target = target;
            controller.sptSource = spt;
            aimObject.transform.SetParent(rootObject.transform, false);
            targetObject.transform.position = new Vector3(4f, 1f, 0f);

            Require(Mathf.Abs(controller.GetOriginalTickDeltaTime() - 1f / 60f) < 0.000001f,
                "test play owns an original 60 Hz tick independent of Unity FixedUpdate", ref assertions);
            Require(controller.SecondsToOriginalTicks(0.3f) == 18,
                "0.3-second input windows are converted to 18 original ticks", ref assertions);
            MethodInfo tickWindow = typeof(TestPlayController).GetMethod("IsTickWithinWindow", StaticPrivate);
            Require(tickWindow != null &&
                    (bool)tickWindow.Invoke(null, new object[] { 10, 28, 18 }) &&
                    !(bool)tickWindow.Invoke(null, new object[] { 10, 29, 18 }),
                "double-tap windows use simulation ticks rather than render time", ref assertions);

            MethodInfo updateInput = typeof(TestPlayController).GetMethod("UpdateInputState", InstancePrivate);
            MethodInfo consumeInput = typeof(TestPlayController).GetMethod("ConsumeLatchedInput", InstancePrivate);
            SetField(controller, "latchedShotKeyPress", true);
            updateInput.Invoke(controller, null);
            Require(controller.state.GetInt(192) == 1, "a short render-frame shot press is latched for one simulation tick", ref assertions);
            consumeInput.Invoke(controller, null);
            updateInput.Invoke(controller, null);
            Require(controller.state.GetInt(192) == 0, "a consumed short press is not repeated on a catch-up tick", ref assertions);

            SetField(spt, "<LastSptData>k__BackingField", new SptRuntimeData { LockDist = 5f });
            Require(Mathf.Approximately(controller.GetConfiguredLockDistance(), 5f),
                "target lock uses Script.spt LockDist", ref assertions);

            MethodInfo updateLock = typeof(TestPlayController).GetMethod("UpdateTargetLock", InstancePrivate);
            MethodInfo updateTargetState = typeof(TestPlayController).GetMethod("UpdateTargetState", InstancePrivate);
            controller.state.SetInt(195, 1);
            updateLock.Invoke(controller, null);
            updateTargetState.Invoke(controller, null);
            Require(controller.targetLockActive && controller.lockedTarget == target,
                "lock input acquires the configured candidate target", ref assertions);
            Require(Mathf.Abs(controller.state.GetFloat(99) - Mathf.Sqrt(17f)) < 0.0001f &&
                    controller.state.GetInt(154) == target.stateId,
                "locked target distance and state feed the original state slots", ref assertions);

            Quaternion rootRotationBeforeAim = rootObject.transform.rotation;
            MethodInfo handle = typeof(TestPlayController).GetMethod("HandleCommand", InstancePrivate);
            MethodInfo applyAim = typeof(TestPlayController).GetMethod("ApplyQueuedAimCommands", InstancePrivate);
            MethodInfo resetBlock = typeof(TestPlayController).GetMethod("ResetOriginalBlockState", InstancePrivate);
            handle.Invoke(controller, new object[] { "LockBodyUpTarget", Values(-1f, -1f), "LockBodyUpTarget(-1,-1);" });
            applyAim.Invoke(controller, null);
            Require(rootObject.transform.rotation == rootRotationBeforeAim,
                "unmapped body aim no longer rotates the locomotion root", ref assertions);

            resetBlock.Invoke(controller, null);
            controller.bodyUpAimRoot = aimObject.transform;
            handle.Invoke(controller, new object[] { "LockBodyUpTarget", Values(-1f, -1f), "LockBodyUpTarget(-1,-1);" });
            applyAim.Invoke(controller, null);
            Require(aimObject.transform.rotation != Quaternion.identity && rootObject.transform.rotation == rootRotationBeforeAim,
                "mapped body aim rotates only its explicit visual transform", ref assertions);

            controller.state.SetInt(195, 0);
            updateLock.Invoke(controller, null);
            targetObject.transform.position = new Vector3(6f, 0f, 0f);
            updateLock.Invoke(controller, null);
            Require(!controller.targetLockActive && controller.GetLockedTargetTransform() == null,
                "a target outside LockDist is released", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(aimObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(rootObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    static void VerifyCameraControllerLifecycle(ref int assertions)
    {
        GameObject cameraObject = new GameObject("TestPlayVerificationCamera");
        GameObject controllerObject = new GameObject("TestPlayVerificationController");
        GameObject rootObject = new GameObject("TestPlayVerificationRoot");
        GameObject targetObject = new GameObject("TestPlayVerificationTarget");
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            FreeCam freeCamera = cameraObject.AddComponent<FreeCam>();
            TestPlayCameraController testCamera = cameraObject.AddComponent<TestPlayCameraController>();
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            RoboStructure robo = controllerObject.AddComponent<RoboStructure>();
            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();

            Vector3 originalPosition = new Vector3(2f, 3f, 4f);
            Quaternion originalRotation = Quaternion.Euler(10f, 20f, 0f);
            cameraObject.transform.SetPositionAndRotation(originalPosition, originalRotation);
            camera.fieldOfView = 40f;
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            targetObject.transform.position = new Vector3(8f, 0f, 8f);
            robo.root = rootObject;
            controller.robo = robo;
            controller.target = target;
            Require(controller.TryAcquireTargetLock(), "camera test acquires an explicit target lock", ref assertions);
            testCamera.controlledCamera = camera;
            testCamera.editorCamera = freeCamera;

            testCamera.EnterTestPlayCamera(controller);
            Require(testCamera.cameraModeActive && !freeCamera.enabled, "test camera replaces editor FreeCam", ref assertions);
            Require(testCamera.usingLockTarget, "test camera uses available lock target", ref assertions);
            Require(Mathf.Approximately(camera.fieldOfView, 60f), "test camera applies the original 60-degree FOV", ref assertions);
            Require(cameraObject.transform.position != originalPosition, "test camera enters follow pose", ref assertions);

            MethodInfo updateCamera = typeof(TestPlayCameraController).GetMethod("UpdateFollowCamera", InstancePrivate);
            Require(updateCamera != null, "locked camera direction verification is available", ref assertions);
            targetObject.transform.position = Vector3.forward * 10f;
            controller.state.SetInt(190, 4);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.left);
            updateCamera.Invoke(testCamera, new object[] { rootObject.transform, 0f });
            Require(cameraObject.transform.position.x > rootObject.transform.position.x && cameraObject.transform.forward.x < 0f,
                "left movement turns locked camera left", ref assertions);

            controller.state.SetInt(190, 6);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.right);
            updateCamera.Invoke(testCamera, new object[] { rootObject.transform, 0f });
            Require(cameraObject.transform.position.x < rootObject.transform.position.x && cameraObject.transform.forward.x > 0f,
                "right movement turns locked camera right", ref assertions);

            controller.state.SetInt(190, 8);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            updateCamera.Invoke(testCamera, new object[] { rootObject.transform, 0f });
            Vector3 forwardCameraPosition = cameraObject.transform.position;

            controller.state.SetInt(190, 2);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.back);
            updateCamera.Invoke(testCamera, new object[] { rootObject.transform, 0f });
            Require(cameraObject.transform.position.z < forwardCameraPosition.z,
                "backward movement pulls locked camera away from target", ref assertions);
            Require(cameraObject.transform.position.y > forwardCameraPosition.y,
                "backward movement raises locked camera framing", ref assertions);
            Require(cameraObject.transform.position.z < rootObject.transform.position.z && cameraObject.transform.forward.z > 0f,
                "backward movement keeps lock target in front of camera", ref assertions);

            controller.state.SetInt(190, 1);
            rootObject.transform.rotation = Quaternion.LookRotation((Vector3.back + Vector3.left).normalized);
            updateCamera.Invoke(testCamera, new object[] { rootObject.transform, 0f });
            Require(cameraObject.transform.position.z <= rootObject.transform.position.z + 0.001f && cameraObject.transform.forward.z > 0f,
                "backward diagonal movement does not cross in front of player", ref assertions);

            testCamera.ExitTestPlayCamera();
            Require(!testCamera.cameraModeActive && freeCamera.enabled, "test camera restores editor control", ref assertions);
            Require(cameraObject.transform.position == originalPosition && cameraObject.transform.rotation == originalRotation &&
                    Mathf.Approximately(camera.fieldOfView, 40f), "test camera restores transform and FOV", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(rootObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    static void VerifyMovementReferenceModes(ref int assertions)
    {
        GameObject controllerObject = new GameObject("TestPlayVerificationMovementReferenceController");
        GameObject rootObject = new GameObject("TestPlayVerificationMovementReferenceRoot");
        GameObject targetObject = new GameObject("TestPlayVerificationMovementReferenceTarget");
        GameObject cameraObject = new GameObject("TestPlayVerificationMovementReferenceCamera");
        TestPlayCameraController testCamera = null;
        try
        {
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            RoboStructure robo = controllerObject.AddComponent<RoboStructure>();
            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            Camera camera = cameraObject.AddComponent<Camera>();
            testCamera = cameraObject.AddComponent<TestPlayCameraController>();
            float defaultInputTurnDegrees = controller.inputTurnDegreesPerTick;
            robo.root = rootObject;
            controller.robo = robo;
            controller.target = target;
            controller.cameraController = testCamera;
            testCamera.controlledCamera = camera;
            targetObject.transform.position = Vector3.forward * 10f;
            Require(controller.TryAcquireTargetLock(), "movement test acquires an explicit target lock", ref assertions);
            controller.currentAnimationIndex = controller.moveAction;
            controller.inputTurnDegreesPerTick = 0f;
            SetField(controller, "moveAnimationActive", true);
            testCamera.EnterTestPlayCamera(controller);

            MethodInfo buildMove = typeof(TestPlayController).GetMethod("TryGetOriginalStyleInputWorldMove", InstancePrivate);
            MethodInfo resetHeading = typeof(TestPlayController).GetMethod("ResetInputMoveHeading", InstancePrivate);
            Require(buildMove != null && resetHeading != null, "movement reference switching is available", ref assertions);
            Require(Mathf.Approximately(defaultInputTurnDegrees, 12f) &&
                    Mathf.Approximately(testCamera.lateralMovementHeadingWeight, 0.5f) &&
                    Mathf.Approximately(testCamera.maxLockedOrbitAngle, 60f),
                "original steering and reduced lock-camera orbit are the defaults", ref assertions);

            controller.state.SetInt(190, 8);
            SetField(testCamera, "movementReferenceForward", Vector3.right);
            SetField(testCamera, "hasMovementReference", true);
            object[] targetArgs = { rootObject.transform, new Vector3(0f, 0f, 0.2f), Vector3.zero };
            bool targetMove = (bool)buildMove.Invoke(controller, targetArgs);
            Require(targetMove && Vector3.Dot(((Vector3)targetArgs[2]).normalized, Vector3.forward) > 0.999f,
                "target-relative movement has priority while lock mode is enabled", ref assertions);

            controller.useTargetRelativeMovement = false;
            controller.useCameraRelativeMovement = true;
            testCamera.useLockTargetWhenAvailable = false;
            resetHeading.Invoke(controller, null);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.right);
            object[] cameraArgs = { rootObject.transform, new Vector3(0f, 0f, 0.2f), Vector3.zero };
            bool cameraMove = (bool)buildMove.Invoke(controller, cameraArgs);
            Require(cameraMove && Vector3.Dot(((Vector3)cameraArgs[2]).normalized, Vector3.right) > 0.999f,
                "camera-relative mode maps forward input to the live camera heading", ref assertions);

            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.back);
            object[] updatedCameraArgs = { rootObject.transform, new Vector3(0f, 0f, 0.2f), Vector3.zero };
            bool updatedCameraMove = (bool)buildMove.Invoke(controller, updatedCameraArgs);
            Require(updatedCameraMove && Vector3.Dot(((Vector3)updatedCameraArgs[2]).normalized, Vector3.back) > 0.999f,
                "held input follows an updated logical camera heading", ref assertions);

            controller.useCameraRelativeMovement = false;
            resetHeading.Invoke(controller, null);
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            object[] legacyArgs = { rootObject.transform, new Vector3(0f, 0f, 0.2f), Vector3.zero };
            bool legacyMove = (bool)buildMove.Invoke(controller, legacyArgs);
            Require(legacyMove && Vector3.Dot(((Vector3)legacyArgs[2]).normalized, Vector3.left) > 0.999f,
                "disabling both reference switches preserves the input-start mech heading", ref assertions);
        }
        finally
        {
            if (testCamera != null)
                testCamera.ExitTestPlayCamera();
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(rootObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }


    static void VerifyOriginalDirectionMovement(ref int assertions)
    {
        MethodInfo encode = typeof(TestPlayController).GetMethod("EncodeDirectionInput", StaticPrivate);
        MethodInfo directionVector = typeof(TestPlayController).GetMethod("GetDirectionVector", StaticPrivate);
        Require(encode != null && directionVector != null, "original direction helpers are available", ref assertions);

        Require(
            (int)encode.Invoke(null, new object[] { 0, 1 }) == 8 &&
            (int)encode.Invoke(null, new object[] { 0, -1 }) == 2 &&
            (int)encode.Invoke(null, new object[] { -1, 0 }) == 4 &&
            (int)encode.Invoke(null, new object[] { 1, 0 }) == 6,
            "cardinal input uses original keypad direction codes", ref assertions);
        Require(
            (int)encode.Invoke(null, new object[] { -1, 1 }) == 7 &&
            (int)encode.Invoke(null, new object[] { 1, 1 }) == 9 &&
            (int)encode.Invoke(null, new object[] { -1, -1 }) == 1 &&
            (int)encode.Invoke(null, new object[] { 1, -1 }) == 3,
            "combined arrows use original diagonal direction codes", ref assertions);

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        Vector3 left = (Vector3)directionVector.Invoke(null, new object[] { 4, forward, right });
        Vector3 back = (Vector3)directionVector.Invoke(null, new object[] { 2, forward, right });
        Vector3 diagonal = (Vector3)directionVector.Invoke(null, new object[] { 9, forward, right });
        Require(left == Vector3.left && back == Vector3.back, "left and back map away from forward Move", ref assertions);
        Require(Mathf.Approximately(diagonal.magnitude, 1f) && diagonal.x > 0f && diagonal.z > 0f,
            "diagonal movement is normalized", ref assertions);

        GameObject controllerObject = new GameObject("TestPlayVerificationDirectionController");
        GameObject rootObject = new GameObject("TestPlayVerificationDirectionRoot");
        GameObject targetObject = new GameObject("TestPlayVerificationDirectionTarget");
        try
        {
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            RoboStructure robo = controllerObject.AddComponent<RoboStructure>();
            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            robo.root = rootObject;
            controller.robo = robo;
            controller.target = target;
            controller.currentAnimationIndex = controller.moveAction;
            controller.inputTurnDegreesPerTick = 0f;
            controller.inputMoveMagnitude = 0.08f;
            targetObject.transform.position = Vector3.forward * 10f;
            Require(controller.TryAcquireTargetLock(), "direction test acquires an explicit target lock", ref assertions);
            SetField(controller, "moveAnimationActive", true);

            MethodInfo buildMove = typeof(TestPlayController).GetMethod("TryGetOriginalStyleInputWorldMove", InstancePrivate);
            MethodInfo resetHeading = typeof(TestPlayController).GetMethod("ResetInputMoveHeading", InstancePrivate);
            Require(buildMove != null && resetHeading != null, "directional movement runtime is available", ref assertions);

            controller.state.SetInt(190, 4);
            object[] leftArgs = { rootObject.transform, new Vector3(0f, 0f, 0.2f), Vector3.zero };
            bool movedLeft = (bool)buildMove.Invoke(controller, leftArgs);
            Vector3 leftMove = (Vector3)leftArgs[2];
            Require(movedLeft && Vector3.Distance(leftMove, Vector3.left * 0.2f) < 0.0001f,
                "script forward magnitude is redirected left", ref assertions);
            Require(Vector3.Dot(rootObject.transform.forward, Vector3.left) > 0.999f,
                "movement heading turns the mech", ref assertions);

            resetHeading.Invoke(controller, null);
            controller.state.SetInt(190, 2);
            object[] backArgs = { rootObject.transform, new Vector3(0f, 0f, 0.15f), Vector3.zero };
            bool movedBack = (bool)buildMove.Invoke(controller, backArgs);
            Vector3 backMove = (Vector3)backArgs[2];
            Require(movedBack && Vector3.Distance(backMove, Vector3.back * 0.15f) < 0.0001f,
                "script forward magnitude is redirected backward", ref assertions);

            resetHeading.Invoke(controller, null);
            controller.state.SetInt(190, 6);
            object[] fallbackArgs = { rootObject.transform, Vector3.zero, Vector3.zero };
            bool movedRight = (bool)buildMove.Invoke(controller, fallbackArgs);
            Vector3 rightMove = (Vector3)fallbackArgs[2];
            Require(movedRight && Vector3.Distance(rightMove, Vector3.right * controller.inputMoveMagnitude) < 0.0001f,
                "missing script Move uses input fallback magnitude in the requested direction", ref assertions);

            controller.useColliderGrounding = false;
            controller.inputTurnDegreesPerTick = 0f;
            SetField(controller, "moveCommand", new Vector3(0f, 0f, 0.08f));
            MethodInfo applyRootMotion = typeof(TestPlayController).GetMethod("ApplyRootMotion", InstancePrivate);
            Require(applyRootMotion != null, "root motion direction verification is available", ref assertions);

            Vector3 upDelta = RunRootMotionDirection(controller, rootObject.transform, applyRootMotion, resetHeading, 8);
            Vector3 downDelta = RunRootMotionDirection(controller, rootObject.transform, applyRootMotion, resetHeading, 2);
            Vector3 leftDelta = RunRootMotionDirection(controller, rootObject.transform, applyRootMotion, resetHeading, 4);
            Vector3 rightDelta = RunRootMotionDirection(controller, rootObject.transform, applyRootMotion, resetHeading, 6);
            Vector3 diagonalDelta = RunRootMotionDirection(controller, rootObject.transform, applyRootMotion, resetHeading, 9);
            Require(
                Vector3.Dot(upDelta.normalized, Vector3.forward) > 0.999f &&
                Vector3.Dot(downDelta.normalized, Vector3.back) > 0.999f &&
                Vector3.Dot(leftDelta.normalized, Vector3.left) > 0.999f &&
                Vector3.Dot(rightDelta.normalized, Vector3.right) > 0.999f &&
                diagonalDelta.x > 0f && diagonalDelta.z > 0f &&
                Mathf.Abs(diagonalDelta.magnitude - upDelta.magnitude) < 0.0001f,
                "ApplyRootMotion moves in all requested directions without diagonal speed gain", ref assertions);

            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            resetHeading.Invoke(controller, null);
            controller.state.SetInt(190, 4);
            controller.inputTurnDegreesPerTick = 12f;
            for (int i = 0; i < 8; i++)
                applyRootMotion.Invoke(controller, null);
            Require(Vector3.Dot(rootObject.transform.forward, Vector3.left) > 0.999f && rootObject.transform.position.x < 0f,
                "original direction steering reaches left over eight 12-degree ticks", ref assertions);

            MethodInfo prepareStep = typeof(TestPlayController).GetMethod("PrepareStepFromDirection", InstancePrivate);
            MethodInfo finishStep = typeof(TestPlayController).GetMethod("ShouldFinishOriginalStyleStep", InstancePrivate);
            MethodInfo resolveStepTicks = typeof(TestPlayController).GetMethod("ResolveStepScriptTicks", InstancePrivate);
            MethodInfo forceAirborneByAction = typeof(TestPlayController).GetMethod("ShouldForceAirborneByAction", InstancePrivate);
            MethodInfo stepExitAction = typeof(TestPlayController).GetMethod("GetOriginalStepExitAction", InstancePrivate);
            MethodInfo heldActionFromInput = typeof(TestPlayController).GetMethod("GetHeldActionFromInput", InstancePrivate);
            MethodInfo updateStepRecoveryGate = typeof(TestPlayController).GetMethod("UpdateStepRecoveryInputGate", InstancePrivate);
            Require(
                prepareStep != null && finishStep != null && resolveStepTicks != null &&
                forceAirborneByAction != null && stepExitAction != null && heldActionFromInput != null &&
                updateStepRecoveryGate != null,
                "original step runtime helpers are available", ref assertions);

            rootObject.transform.position = Vector3.zero;
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            int leftInputFromForward = (int)prepareStep.Invoke(controller, new object[] { 4 });
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            int leftInputFromRight = (int)prepareStep.Invoke(controller, new object[] { 4 });
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            int leftInputFromBack = (int)prepareStep.Invoke(controller, new object[] { 4 });
            rootObject.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            int leftInputFromLeft = (int)prepareStep.Invoke(controller, new object[] { 4 });
            Require(
                leftInputFromForward == controller.leftStepAction &&
                leftInputFromRight == controller.backStepAction &&
                leftInputFromBack == controller.rightStepAction &&
                leftInputFromLeft == controller.forwardStepAction,
                "step animation 9..12 follows input direction relative to the pre-step body facing", ref assertions);

            controller.stepTurnDegreesPerTick = 3f;
            controller.preserveStepReferenceFacing = true;
            controller.useOriginalStepFacing = true;
            Vector3 stepForward = RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 8);
            Vector3 stepBack = RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 2);
            Vector3 stepLeft = RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 4);
            Vector3 stepRight = RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 6);
            Require(
                Vector3.Dot(stepForward.normalized, Vector3.forward) > 0.999f &&
                Vector3.Dot(stepBack.normalized, Vector3.back) > 0.999f &&
                stepLeft.x < 0f && stepLeft.z > 0f &&
                stepRight.x > 0f && stepRight.z > 0f,
                "step root motion follows all four input directions with the original forward bias", ref assertions);
            Require(
                Mathf.Abs(stepLeft.normalized.z - (0.3f / Mathf.Sqrt(1.09f))) < 0.0001f &&
                Mathf.Abs(stepRight.magnitude - stepForward.magnitude) < 0.0001f,
                "side-step bias is 0.3 before normalization without speed gain", ref assertions);

            Vector3 stepDiagonal = RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 9);
            Vector3 expectedDiagonal = new Vector3(1f, 0f, 1.3f).normalized;
            Require(Vector3.Dot(stepDiagonal.normalized, expectedDiagonal) > 0.999f,
                "diagonal step keeps the original raw direction plus forward bias", ref assertions);
            Require(Mathf.Abs(Vector3.Angle(Vector3.forward, rootObject.transform.forward) - 3f) < 0.01f &&
                    Vector3.Dot(rootObject.transform.forward, Vector3.right) > 0f,
                "original diagonal step turns the chosen forward-step ANI toward world movement by at most three degrees per tick", ref assertions);

            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            controller.state.SetInt(190, 4);
            int leftStep = (int)prepareStep.Invoke(controller, new object[] { 4 });
            controller.currentAnimationIndex = leftStep;
            SetField(controller, "stepSequenceActive", true);
            SetField(controller, "actionTick", 1);
            SetField(controller, "moveCommand", new Vector3(0f, 0f, 0.51f));
            applyRootMotion.Invoke(controller, null);
            Require(leftStep == controller.leftStepAction &&
                    Vector3.Angle(Vector3.forward, rootObject.transform.forward) < 0.001f &&
                    rootObject.transform.position.x < 0f,
                "left input selects left-step ANI and preserves the body facing while moving left", ref assertions);
            SetField(controller, "stepSequenceActive", false);

            controller.useOriginalStepFacing = false;
            controller.preserveStepReferenceFacing = true;
            RunStepRootMotionDirection(controller, rootObject.transform, applyRootMotion, prepareStep, 9);
            Require(Vector3.Angle(Vector3.forward, rootObject.transform.forward) < 0.001f,
                "legacy reference-facing approximation remains available behind its compatibility switch", ref assertions);
            controller.useOriginalStepFacing = true;

            controller.state.SetInt(190, 8);
            int durationAction = (int)prepareStep.Invoke(controller, new object[] { 8 });
            controller.currentAnimationIndex = durationAction;
            SetField(controller, "stepSequenceActive", true);
            SetField(controller, "actionTick", 15);
            controller.state.SetInt(190, 0);
            bool finishesAt15 = (bool)finishStep.Invoke(controller, null);
            SetField(controller, "actionTick", 16);
            bool finishesAt16 = (bool)finishStep.Invoke(controller, null);
            Require(!finishesAt15 && finishesAt16,
                "step release is ignored through tick 15 and accepted from tick 16", ref assertions);

            controller.state.SetInt(190, 8);
            SetField(controller, "actionTick", 60);
            bool finishesAt60 = (bool)finishStep.Invoke(controller, null);
            SetField(controller, "actionTick", 61);
            bool finishesAt61 = (bool)finishStep.Invoke(controller, null);
            Require(!finishesAt60 && finishesAt61,
                "held step has the original 61-tick hard limit", ref assertions);

            controller.maximumEnergy = 100f;
            controller.currentEnergy = 0f;
            controller.state.SetFloat(100, 0f);
            SetField(controller, "actionTick", 1);
            Require((bool)finishStep.Invoke(controller, null),
                "depleted movement energy terminates a step without waiting for tick 16", ref assertions);

            controller.currentAnimationIndex = durationAction;
            SetField(controller, "stepSequenceActive", true);
            controller.SetAirborneFlag(false);
            controller.groundedFlag = true;
            Require(!(bool)forceAirborneByAction.Invoke(controller, null),
                "a grounded step does not force the character into the airborne state", ref assertions);
            Require((int)stepExitAction.Invoke(controller, null) == controller.stepLandingAction,
                "a grounded step exits through dedicated step-landing action 6 instead of air movement", ref assertions);

            controller.SetAirborneFlag(true);
            controller.groundedFlag = false;
            Require((int)stepExitAction.Invoke(controller, null) == controller.airIdleAction,
                "an airborne step exits through original air-stop action 8", ref assertions);

            controller.currentAnimationIndex = controller.airIdleAction;
            SetField(controller, "stepSequenceActive", false);
            SetField(controller, "stepRecoveryActive", true);
            SetField(controller, "stepRecoveryDirection", 8);
            controller.state.SetInt(190, 8);
            Require((int)heldActionFromInput.Invoke(controller, null) == controller.airIdleAction,
                "holding the step direction keeps action 8 and cannot enter upward-Force action 4", ref assertions);
            controller.state.SetInt(190, 0);
            updateStepRecoveryGate.Invoke(controller, null);
            controller.state.SetInt(190, 8);
            Require((int)heldActionFromInput.Invoke(controller, null) == controller.airMoveAction,
                "releasing the direction clears step recovery so a later new input can use air movement", ref assertions);

            SetField(controller, "stepRecoveryActive", true);
            SetField(controller, "stepRecoveryDirection", 8);
            controller.state.SetInt(190, 6);
            updateStepRecoveryGate.Invoke(controller, null);
            Require((int)heldActionFromInput.Invoke(controller, null) == controller.airMoveAction,
                "changing direction also clears step recovery without inheriting the old held direction", ref assertions);

            controller.maxStepScriptTicksPerBlock = 5;
            int resolvedTicks = (int)resolveStepTicks.Invoke(controller, new object[] { new script { unk = 120 } });
            Require(resolvedTicks == 120,
                "step ANI block length is no longer truncated to five ticks", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(rootObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    static void VerifyOriginalJumpAndBoost(ref int assertions)
    {
        GameObject controllerObject = new GameObject("TestPlayVerificationJumpBoostController");
        GameObject rootObject = new GameObject("TestPlayVerificationJumpBoostRoot");
        GameObject targetObject = new GameObject("TestPlayVerificationJumpBoostTarget");
        try
        {
            TestPlayController controller = controllerObject.AddComponent<TestPlayController>();
            RoboStructure robo = controllerObject.AddComponent<RoboStructure>();
            TestPlayTargetDummy target = targetObject.AddComponent<TestPlayTargetDummy>();
            robo.root = rootObject;
            controller.robo = robo;
            controller.target = target;
            controller.useColliderGrounding = false;
            controller.requireLockInput = false;
            controller.maximumEnergy = 1000f;
            controller.currentEnergy = 1000f;
            controller.state.SetFloat(100, 1000f);
            targetObject.transform.position = Vector3.forward * 10f;

            MethodInfo normalizeActions = typeof(TestPlayController).GetMethod("NormalizeActionIds", InstancePrivate);
            MethodInfo airborneAction = typeof(TestPlayController).GetMethod("GetAirborneLocomotionAction", InstancePrivate);
            MethodInfo boostMove = typeof(TestPlayController).GetMethod("TryGetOriginalStyleBoostWorldMove", InstancePrivate);
            MethodInfo resetBoost = typeof(TestPlayController).GetMethod("ResetBoostRuntimeState", InstancePrivate);
            MethodInfo endBoost = typeof(TestPlayController).GetMethod("ShouldEndOriginalStyleBoost", InstancePrivate);
            MethodInfo endRise = typeof(TestPlayController).GetMethod("ShouldEndOriginalStyleRise", InstancePrivate);
            MethodInfo canAirRise = typeof(TestPlayController).GetMethod("CanStartOriginalAirRise", InstancePrivate);
            MethodInfo startBoost = typeof(TestPlayController).GetMethod("StartBoostAction", InstancePrivate);
            MethodInfo forceAirborne = typeof(TestPlayController).GetMethod("ShouldForceAirborneByAction", InstancePrivate);
            MethodInfo riseSteering = typeof(TestPlayController).GetMethod("ApplyOriginalRiseSteering", InstancePrivate);
            MethodInfo energyTick = typeof(TestPlayController).GetMethod("UpdateOriginalMovementEnergy", InstancePrivate);
            MethodInfo integrateForce = typeof(TestPlayController).GetMethod("IntegrateOriginalForceVelocity", InstancePrivate);
            MethodInfo captureDrivenVelocity = typeof(TestPlayController).GetMethod("CaptureDrivenHorizontalVelocity", InstancePrivate);
            MethodInfo applyDrivenInertia = typeof(TestPlayController).GetMethod("ApplyPendingDrivenHorizontalInertia", InstancePrivate);
            MethodInfo applyRootMotion = typeof(TestPlayController).GetMethod("ApplyRootMotion", InstancePrivate);
            Require(
                normalizeActions != null && airborneAction != null && boostMove != null && resetBoost != null &&
                endBoost != null && endRise != null && canAirRise != null && startBoost != null &&
                forceAirborne != null && riseSteering != null && energyTick != null &&
                integrateForce != null && captureDrivenVelocity != null && applyDrivenInertia != null && applyRootMotion != null,
                "original jump/boost runtime helpers are available", ref assertions);

            controller.landingAction = controller.riseStartAction;
            normalizeActions.Invoke(controller, null);
            controller.stepLandingAction = controller.landingAction;
            normalizeActions.Invoke(controller, null);
            Require(controller.landingAction == 5 && controller.stepLandingAction == 6 && controller.airIdleAction == 8,
                "legacy landing IDs normalize to original regular landing 5 and step landing 6", ref assertions);

            controller.state.SetInt(190, 0);
            Require((int)airborneAction.Invoke(controller, null) == controller.airIdleAction,
                "neutral airborne state uses original air-stop action 8", ref assertions);
            controller.state.SetInt(190, 8);
            Require((int)airborneAction.Invoke(controller, null) == controller.airMoveAction,
                "directional airborne state uses original air-move action 4", ref assertions);

            controller.currentAnimationIndex = controller.boostAction;
            SetField(controller, "boostMotionActive", true);
            SetField(controller, "actionTick", 1);
            controller.state.SetInt(190, 4);
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            resetBoost.Invoke(controller, null);
            object[] initialBoostArgs = { rootObject.transform, new Vector3(0f, 0f, 0.25f), Vector3.zero };
            Require((bool)boostMove.Invoke(controller, initialBoostArgs), "boost root motion is handled", ref assertions);
            Vector3 initialBoostMove = (Vector3)initialBoostArgs[2];
            Require(Mathf.Abs(Vector3.Angle(Vector3.forward, initialBoostMove) - 15f) < 0.01f &&
                    Mathf.Abs(initialBoostMove.magnitude - 0.25f) < 0.0001f,
                "boost starts with original 15-degree steering and preserves ANI speed", ref assertions);

            SetField(controller, "actionTick", 10);
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            resetBoost.Invoke(controller, null);
            object[] sustainedBoostArgs = { rootObject.transform, new Vector3(0f, 0f, 0.25f), Vector3.zero };
            boostMove.Invoke(controller, sustainedBoostArgs);
            Require(Mathf.Abs(Vector3.Angle(Vector3.forward, (Vector3)sustainedBoostArgs[2]) - 2.5f) < 0.01f,
                "sustained 90-degree boost turn uses original 2.5-degree formula", ref assertions);

            SetField(controller, "riseKeyHeld", false);
            controller.state.SetInt(190, 0);
            SetField(controller, "actionTick", 30);
            Require(!(bool)endBoost.Invoke(controller, null), "boost ignores full release through tick 30", ref assertions);
            SetField(controller, "actionTick", 31);
            Require((bool)endBoost.Invoke(controller, null), "boost ends after tick 30 when all movement input is released", ref assertions);
            controller.state.SetInt(190, 8);
            Require(!(bool)endBoost.Invoke(controller, null), "direction input sustains boost after jump-key release", ref assertions);
            Require((bool)forceAirborne.Invoke(controller, null), "active boost disables gravity even after jump-key release", ref assertions);

            controller.currentAnimationIndex = controller.riseAction;
            SetField(controller, "riseSequenceActive", true);
            SetField(controller, "riseKeyHeld", true);
            controller.state.SetInt(191, 0);
            SetField(controller, "actionTick", controller.riseMaximumTicks - 2);
            Require(!(bool)endRise.Invoke(controller, null),
                "rise remains active until the original final action-7 callback", ref assertions);
            SetField(controller, "actionTick", controller.riseMaximumTicks - 1);
            Require((bool)endRise.Invoke(controller, null),
                "held neutral rise exits after the original 12 action-7 callbacks", ref assertions);
            SetField(controller, "actionTick", controller.riseMinimumReleaseTicks - 1);
            SetField(controller, "riseKeyHeld", false);
            Require(!(bool)endRise.Invoke(controller, null),
                "rise release is ignored before the original minimum callback window", ref assertions);
            SetField(controller, "actionTick", controller.riseMinimumReleaseTicks);
            Require((bool)endRise.Invoke(controller, null),
                "rise release is accepted after the original minimum callback window", ref assertions);

            controller.currentAnimationIndex = controller.airIdleAction;
            SetField(controller, "actionTick", controller.airRiseMinimumIdleTicks - 1);
            Require(!(bool)canAirRise.Invoke(controller, null),
                "air-stop cannot immediately restart rise while Z remains held", ref assertions);
            SetField(controller, "actionTick", controller.airRiseMinimumIdleTicks);
            Require((bool)canAirRise.Invoke(controller, null),
                "air-stop accepts re-rise only after the original c38 > 10 window", ref assertions);

            controller.maximumEnergy = 1000f;
            controller.currentEnergy = 1000f;
            controller.state.SetInt(100, 1000);
            SetField(controller, "riseKeyHeld", true);
            controller.state.SetInt(191, 1);
            controller.currentAnimationIndex = controller.airIdleAction;
            startBoost.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 800f),
                "airborne boost entry charges Generator/5 once before per-tick drain", ref assertions);

            controller.currentAnimationIndex = controller.riseAction;
            SetField(controller, "riseSequenceActive", true);
            SetField(controller, "riseKeyHeld", true);
            controller.state.SetInt(190, 4);
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            riseSteering.Invoke(controller, new object[] { rootObject.transform });
            Require(Mathf.Abs(Vector3.Angle(Vector3.forward, rootObject.transform.forward) - 14f) < 0.01f,
                "held rise steers by the original 14-degree maximum", ref assertions);

            controller.currentAnimationIndex = controller.boostAction;
            captureDrivenVelocity.Invoke(controller, new object[] { Vector3.forward * 0.25f });
            controller.currentAnimationIndex = controller.riseAction;
            SetField(controller, "riseSequenceActive", true);
            SetField(controller, "gvEnable", false);
            controller.SetAirborneFlag(true);
            SetField(controller, "velocity", Vector3.zero);
            SetField(controller, "forceCommand", new Vector3(0f, 0.04f, 0f));
            applyDrivenInertia.Invoke(controller, null);
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            applyRootMotion.Invoke(controller, null);
            Vector3 inertialRisePosition = rootObject.transform.position;
            float expectedRiseDistance = 0.04f * controller.aniUnitsToUnityScale;
            float expectedInertiaDistance = 0.25f * controller.airborneHorizontalForceRetention * controller.aniUnitsToUnityScale;
            Require(Mathf.Abs(inertialRisePosition.y - expectedRiseDistance) < 0.0001f &&
                    Mathf.Abs(inertialRisePosition.z - expectedInertiaDistance) < 0.0001f,
                "rise applies one ANI Force/tick integration and one Unity-unit conversion", ref assertions);

            SetField(controller, "velocity", Vector3.up * 7f);
            SetField(controller, "forceCommand", new Vector3(0f, 0.02f, 0f));
            integrateForce.Invoke(controller, null);
            Require(Mathf.Abs(((Vector3)GetField(controller, "velocity")).y - 0.17f) < 0.0001f,
                "rise clamps to original 0.15 vertical speed before adding ANI Force", ref assertions);

            controller.currentAnimationIndex = controller.airMoveAction;
            SetField(controller, "riseSequenceActive", false);
            SetField(controller, "velocity", Vector3.up * 70f);
            SetField(controller, "forceCommand", new Vector3(0f, 0.02f, 0f));
            for (int i = 0; i < 120; i++)
                integrateForce.Invoke(controller, null);
            Require(Mathf.Abs(((Vector3)GetField(controller, "velocity")).y - 0.17f) < 0.0001f,
                "directional air move cannot accumulate upward Force beyond the original rise limit", ref assertions);

            controller.currentAnimationIndex = controller.airIdleAction;
            controller.state.SetInt(190, 0);
            controller.useColliderGrounding = true;
            SetField(controller, "gvEnable", true);
            SetField(controller, "forceCommand", Vector3.zero);
            SetField(controller, "moveCommand", Vector3.zero);
            controller.verticalFallSpeed = 0f;
            rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float releasedRisePeak = rootObject.transform.position.y;
            for (int i = 0; i < 25; i++)
            {
                applyRootMotion.Invoke(controller, null);
                releasedRisePeak = Mathf.Max(releasedRisePeak, rootObject.transform.position.y);
            }
            Require(rootObject.transform.position.y < releasedRisePeak - 0.01f,
                "released directional air move turns from bounded rise into gravity-driven descent", ref assertions);

            controller.useColliderGrounding = false;
            controller.currentAnimationIndex = controller.airIdleAction;
            controller.SetAirborneFlag(true);
            SetField(controller, "velocity", Vector3.zero);
            SetField(controller, "forceCommand", Vector3.zero);
            SetField(controller, "gvEnable", true);
            integrateForce.Invoke(controller, null);
            Require(Mathf.Abs(((Vector3)GetField(controller, "velocity")).y + 0.013f) < 0.0001f,
                "GvEnable applies the original 0.013 downward velocity per tick", ref assertions);
            for (int i = 0; i < 100; i++)
                integrateForce.Invoke(controller, null);
            Require(Mathf.Abs(((Vector3)GetField(controller, "velocity")).y + 0.8f) < 0.0001f,
                "original gravity stops at the -0.8 terminal tick velocity", ref assertions);

            SetField(controller, "velocity", Vector3.right);
            SetField(controller, "gvEnable", false);
            SetField(controller, "vFMulti", 0.5f);
            integrateForce.Invoke(controller, null);
            Require(Mathf.Abs(((Vector3)GetField(controller, "velocity")).x - 0.475f) < 0.0001f,
                "airborne damping is followed by one vF_Multi application", ref assertions);
            SetField(controller, "vFMulti", 1f);

            controller.currentAnimationIndex = controller.riseAction;
            SetField(controller, "riseSequenceActive", true);
            controller.useColliderGrounding = false;
            controller.currentEnergy = 100f;
            controller.maximumEnergy = 100f;
            controller.state.SetFloat(100, 100f);
            energyTick.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 95f),
                "rise consumes five original energy units per tick", ref assertions);
            controller.currentAnimationIndex = controller.boostAction;
            SetField(controller, "boostMotionActive", true);
            energyTick.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 90f),
                "boost consumes five original energy units per tick", ref assertions);
            controller.currentAnimationIndex = controller.forwardStepAction;
            SetField(controller, "boostMotionActive", false);
            SetField(controller, "stepSequenceActive", true);
            energyTick.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 86f),
                "grounded step consumes four original energy units instead of recovering energy", ref assertions);
            SetField(controller, "stepSequenceActive", false);
            controller.currentEnergy = 0f;
            controller.state.SetFloat(100, 0f);
            Require((bool)endBoost.Invoke(controller, null), "empty energy ends boost", ref assertions);

            controller.currentAnimationIndex = controller.idleAction;
            controller.currentEnergy = 50f;
            controller.state.SetFloat(100, 50f);
            SetField(controller, "riseSequenceActive", false);
            SetField(controller, "boostMotionActive", false);
            controller.SetAirborneFlag(false);
            energyTick.Invoke(controller, null);
            Require(Mathf.Approximately(controller.currentEnergy, 74f),
                "grounded recovery uses the original initialized value 24 per tick", ref assertions);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(targetObject);
            UnityEngine.Object.DestroyImmediate(rootObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    static Vector3 RunRootMotionDirection(
        TestPlayController controller,
        Transform root,
        MethodInfo applyRootMotion,
        MethodInfo resetHeading,
        int direction)
    {
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        resetHeading.Invoke(controller, null);
        controller.state.SetInt(190, direction);
        applyRootMotion.Invoke(controller, null);
        return root.position;
    }

    static Vector3 RunStepRootMotionDirection(
        TestPlayController controller,
        Transform root,
        MethodInfo applyRootMotion,
        MethodInfo prepareStep,
        int direction)
    {
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        controller.state.SetInt(190, direction);
        int action = (int)prepareStep.Invoke(controller, new object[] { direction });
        controller.currentAnimationIndex = action;
        SetField(controller, "stepSequenceActive", true);
        SetField(controller, "actionTick", 1);
        SetField(controller, "moveCommand", new Vector3(0f, 0f, 0.51f));
        applyRootMotion.Invoke(controller, null);
        SetField(controller, "stepSequenceActive", false);
        return root.position;
    }

    static List<TestPlayScriptValue> Values(params float[] values)
    {
        List<TestPlayScriptValue> result = new List<TestPlayScriptValue>();
        for (int i = 0; i < values.Length; i++)
            result.Add(TestPlayScriptValue.Number(values[i]));
        return result;
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, InstancePrivate);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        field.SetValue(target, value);
    }

    static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, InstancePrivate);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, name);
        return field.GetValue(target);
    }

    static void Require(bool condition, string label, ref int assertions)
    {
        if (!condition)
            throw new InvalidOperationException("[TestPlayVerification] Failed: " + label);
        assertions++;
    }
}
