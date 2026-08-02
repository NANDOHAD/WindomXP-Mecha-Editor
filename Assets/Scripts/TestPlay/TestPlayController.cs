using System;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayController : MonoBehaviour
{
    [Header("References")]
    public RoboStructure robo;
    public TestPlayTargetDummy target;
    public UI_SPT sptSource;

    [Header("Mode")]
    public bool playModeActive;
    public bool startOnPlay;
    public bool logCommands;
    public bool logUnhandledCommands = true;

    [Header("Action IDs")]
    public int idleAction = 0;
    public int moveAction = 1;
    public int forwardStepAction = 11;
    public int backStepAction = 12;
    public int leftStepAction = 9;
    public int rightStepAction = 10;
    public int riseStartAction = 3;
    public int riseAction = 7;
    public int airMoveAction = 4;
    public int landingAction = 3;
    public int shotAction = 100;
    public int meleeAction = 130;
    public int boostAction = 22;
    public int guardAction = 19;
    public int special1Action = 104;
    public int special2Action = 105;
    public int special3Action = 109;

    [Header("Motion")]
    public float moveScale = 35f;
    public float forceScale = 35f;
    public float damping = 4f;
    public float aimTurnSpeed = 540f;
    public float doubleTapStepSeconds = 0.3f;
    public float inputMoveMagnitude = 0.08f;
    public float stepFallbackMoveMagnitude = 0.51f;
    public float stepFallbackDistance = 3f;
    [Min(1)]
    public int maxStepScriptTicksPerBlock = 5;

    [Header("Grounding")]
    public bool useColliderGrounding = true;
    public float characterControllerRadius = 0.7f;
    public float characterControllerHeight = 2.4f;
    public Vector3 characterControllerCenter = new Vector3(0f, 1.2f, 0f);
    public float characterControllerSkinWidth = 0.08f;
    public float characterControllerStepOffset = 0.3f;
    [Range(0f, 89f)]
    public float characterControllerSlopeLimit = 60f;
    public float gravity = 30f;
    public float terminalFallSpeed = 50f;
    public float groundedVerticalSpeed = -1f;
    public bool applyGravityDuringForcedAirborneActions;
    public bool logGroundingDebug;

    [Header("Weapon Preview")]
    public float defaultProjectileSpeed = 35f;
    public float defaultProjectileDamage = 50f;
    public float meleeRange = 3f;
    public float projectileRadius = 0.25f;

    [Header("Burner Preview")]
    public bool useConeBurnerEffects = true;
    public float burnerLengthMultiplier = 1f;
    [Range(0.02f, 1f)]
    public float burnerRadiusRatio = 0.25f;
    public float burnerFadeSpeed = 18f;
    public Color burnerConeColor = new Color(0.35f, 0.85f, 1f, 0.65f);

    [Header("Transitions")]
    public bool blendActionTransitions = true;
    [Range(0f, 0.5f)]
    public float actionTransitionSeconds = 0.12f;
    [Range(0f, 0.5f)]
    public float heldReleaseTransitionSeconds = 0.08f;

    [Header("Debug State")]
    public int currentAnimationIndex;
    public int scriptIndex;
    public int frameIndex;
    public float frameTime;
    public int tick;
    public bool airborneFlag;
    public bool groundedFlag;
    public float verticalFallSpeed;
    public string currentAnimationName;
    public string heldWeapon = "GUN";
    public TestPlayStateTable state = new TestPlayStateTable();

    [Header("Debug Logging")]
    public bool logMotionDebug;
    public bool logMotionAssignments = true;
    [Min(1)]
    public int motionDebugIntervalTicks = 10;

    TestPlayScriptVM vm;
    animation currentAnimation;
    int scriptTick;
    bool initFired;
    bool executeScriptEveryTick;
    bool animeLoop;
    bool moveLocked;
    bool shieldGuard;
    bool gvEnable;
    int attackFlag;
    int swordCancelAction = -1;
    float shotTurnAng;
    float turnMoveAng;
    float camEffect;
    float vFMulti = 1f;
    Vector3 moveCommand;
    Vector3 forceCommand;
    Vector3 velocity;
    bool riseKeyHeld;
    bool previousRiseKeyHeld;
    bool riseSequenceActive;
    bool landingSequenceActive;
    bool stepSequenceActive;
    bool previousAirborneFlag;
    bool boostFromRiseActive;
    bool boostMotionActive;
    bool moveAnimationActive;
    int actionTick;
    float stepMoveBudgetDistance;
    float stepMovedDistance;
    int previousDirectionInput;
    int lastDirectionTap;
    int stepDirection;
    float lastDirectionTapTime = -1f;
    readonly HashSet<int> burnerRequestedIds = new HashSet<int>();
    readonly HashSet<int> validBurnerIds = new HashSet<int>();
    readonly Dictionary<int, TestPlayBurnerCone> burnerCones = new Dictionary<int, TestPlayBurnerCone>();
    TestPlayPosePart[] transitionFromPose;
    int transitionTick;
    int transitionTickTotal;
    CharacterController groundingController;
    CollisionFlags groundingCollisionFlags;

    struct TestPlayPosePart
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    void Awake()
    {
        NormalizeActionIds();

        if (state == null)
            state = new TestPlayStateTable();

        vm = new TestPlayScriptVM(state);
        vm.commandHandler = HandleCommand;
        vm.assignmentHandler = HandleAssignment;
        vm.unhandledLineHandler = LogUnhandled;
    }

    void Start()
    {
        if (startOnPlay)
            StartTestPlay();
    }

    void FixedUpdate()
    {
        if (!playModeActive)
            return;

        if (robo == null || robo.ani == null || robo.ani.animations == null || robo.ani.animations.Count == 0)
            return;

        tick++;
        UpdateInputState();
        UpdateTargetState();
        UpdateActionFromInput();
        TickAnimation();
        ApplyRootMotion();
    }

    public void StartTestPlay()
    {
        if (robo == null)
            robo = FindObjectOfType<RoboStructure>();
        if (target == null)
            target = FindObjectOfType<TestPlayTargetDummy>();
        if (sptSource == null)
            sptSource = FindObjectOfType<UI_SPT>();

        EnsureGroundingController();
        state.ResetDefaults();
        ResetRuntimeFlags();
        playModeActive = true;
        ChangeAnimation(idleAction);
    }

    public void StopTestPlay()
    {
        playModeActive = false;
        velocity = Vector3.zero;
        verticalFallSpeed = 0f;
        moveCommand = Vector3.zero;
        forceCommand = Vector3.zero;
        StopAllBurnerEffects();
    }

    public void ChangeAnimation(int actionId)
    {
        ChangeAnimation(actionId, false);
    }

    void ChangeAnimation(int actionId, bool restartSameAction)
    {
        NormalizeActionIds();

        if (ShouldRedirectBoostToAirIdle(actionId))
        {
            actionId = airMoveAction;
            restartSameAction = false;
        }

        if (robo == null || robo.ani == null || robo.ani.animations == null)
            return;
        if (actionId < 0 || actionId >= robo.ani.animations.Count)
        {
            LogUnhandled("ChangeAnimation out of range: " + actionId);
            return;
        }

        if (!restartSameAction && currentAnimation != null && currentAnimationIndex == actionId)
            return;

        StartPoseTransition(currentAnimationIndex, actionId);

        currentAnimationIndex = actionId;
        currentAnimation = robo.ani.animations[actionId];
        currentAnimationName = currentAnimation != null ? currentAnimation.name : "";
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        actionTick = 0;
        initFired = false;
        executeScriptEveryTick = false;
        animeLoop = false;
        StopAllBurnerEffects();

        if (actionId == riseStartAction && !landingSequenceActive)
            riseSequenceActive = true;
        else if (actionId != riseAction)
            riseSequenceActive = false;

        bool stepAction = IsStepAction(actionId);
        if (stepAction)
        {
            ClearHeldMotionState();
            stepSequenceActive = true;
            stepMoveBudgetDistance = CalculateStepMoveBudgetDistance(currentAnimation);
            stepMovedDistance = 0f;
        }
        else if (actionId != landingAction)
        {
            stepSequenceActive = false;
            stepMoveBudgetDistance = 0f;
            stepMovedDistance = 0f;
        }

        if (actionId != boostAction || !IsBoostInputHeld())
            boostMotionActive = false;

        if (!landingSequenceActive && (actionId == riseStartAction || actionId == riseAction || (actionId == boostAction && boostMotionActive) || stepAction))
            SetAirborneFlag(true);

        if (stepAction)
        {
            stepDirection = 0;
            lastDirectionTap = 0;
            lastDirectionTapTime = -1f;
        }

        if (actionId != moveAction)
            moveAnimationActive = false;

        if (actionId == idleAction)
            ClearHeldMotionState();
    }

    void TickAnimation()
    {
        if (currentAnimation == null)
            ChangeAnimation(idleAction);
        if (currentAnimation == null)
            return;

        if (!initFired)
        {
            initFired = true;
            int initAction = currentAnimationIndex;
            ExecuteScript(currentAnimation.squirrelInit);
            if (currentAnimationIndex != initAction)
                return;
        }

        actionTick++;
        bool hasScripts = currentAnimation.scripts != null && currentAnimation.scripts.Count > 0;
        if (hasScripts)
        {
            scriptIndex = Mathf.Clamp(scriptIndex, 0, currentAnimation.scripts.Count - 1);
            script currentScript = currentAnimation.scripts[scriptIndex];

            if (scriptTick == 0 || executeScriptEveryTick)
            {
                int actionBeforeScript = currentAnimationIndex;
                burnerRequestedIds.Clear();
                ExecuteScript(currentScript.squirrel);
                ApplyBurners();
                if (currentAnimationIndex != actionBeforeScript)
                    return;
            }

            int length = GetRuntimeScriptLengthTicks(currentScript);
            scriptTick++;
            if (scriptTick >= length)
            {
                scriptTick = 0;
                scriptIndex++;
                if (scriptIndex >= currentAnimation.scripts.Count)
                {
                    if (currentAnimationIndex == landingAction && landingSequenceActive)
                    {
                        landingSequenceActive = false;
                        ChangeAnimation(idleAction);
                        return;
                    }

                    if (stepSequenceActive && IsStepAction(currentAnimationIndex))
                    {
                        if (stepMoveBudgetDistance > 0f)
                        {
                            RestartCurrentAnimationLoop();
                            return;
                        }

                        StartAirIdleSequence();
                        return;
                    }

                    if (currentAnimationIndex == riseStartAction && riseSequenceActive)
                    {
                        ChangeAnimation(riseAction);
                        return;
                    }

                    if (animeLoop || ShouldLoopHeldAction())
                    {
                        RestartCurrentAnimationLoop();
                    }
                    else
                    {
                        ChangeAnimation(idleAction);
                        return;
                    }
                }
            }

            frameTime += currentScript.time;
            while (frameTime >= 1f)
            {
                frameTime -= 1f;
                frameIndex++;
            }

            if (TryFinishFiniteActionByTicks())
                return;
        }
        else
        {
            StopAllBurnerEffects();
            if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            {
                frameIndex++;
                int frameCount = currentAnimation.frames != null ? currentAnimation.frames.Count : 0;
                if (frameCount <= 0 || frameIndex >= frameCount)
                {
                    StartAirIdleSequence();
                    return;
                }
            }

            if (landingSequenceActive && currentAnimationIndex == landingAction)
            {
                frameIndex++;
                int frameCount = currentAnimation.frames != null ? currentAnimation.frames.Count : 0;
                if (frameCount <= 0 || frameIndex >= frameCount)
                {
                    landingSequenceActive = false;
                    ChangeAnimation(idleAction);
                    return;
                }
            }
        }

        ApplyPose();
    }

    void ApplyPose()
    {
        if (robo == null || robo.parts == null || currentAnimation == null || currentAnimation.frames == null || currentAnimation.frames.Count == 0)
            return;

        int lastFrame = currentAnimation.frames.Count - 1;
        frameIndex = Mathf.Clamp(frameIndex, 0, lastFrame);
        int nextFrame = Mathf.Min(frameIndex + 1, lastFrame);
        float t = Mathf.Clamp01(frameTime);

        int count = Mathf.Min(robo.parts.Count, currentAnimation.frames[frameIndex].parts.Count);
        for (int i = 1; i < count; i++)
        {
            GameObject go = robo.parts[i];
            if (go == null)
                continue;

            hod2v1_Part a = currentAnimation.frames[frameIndex].parts[i];
            hod2v1_Part b = currentAnimation.frames[nextFrame].parts[i];
            Vector3 targetPosition = Vector3.Lerp(a.position, b.position, t);
            Quaternion targetRotation = Quaternion.Lerp(SafeRotation(a.rotation), SafeRotation(b.rotation), t);
            Vector3 targetScale = Vector3.Lerp(a.scale, b.scale, t);

            if (IsPoseTransitionActive() && i < transitionFromPose.Length)
            {
                float blend = GetPoseTransitionBlend();
                TestPlayPosePart from = transitionFromPose[i];
                go.transform.localPosition = Vector3.Lerp(from.position, targetPosition, blend);
                go.transform.localRotation = Quaternion.Lerp(SafeRotation(from.rotation), targetRotation, blend);
                go.transform.localScale = Vector3.Lerp(from.scale, targetScale, blend);
            }
            else
            {
                go.transform.localPosition = targetPosition;
                go.transform.localRotation = targetRotation;
                go.transform.localScale = targetScale;
            }
        }

        AdvancePoseTransition();
    }

    void ApplyRootMotion()
    {
        if (robo == null || robo.root == null)
            return;

        float dt = Time.fixedDeltaTime;
        Transform root = robo.root.transform;
        Vector3 positionBefore = root.position;
        Vector3 localMove = moveLocked ? Vector3.zero : moveCommand;
        bool usingInputMove = false;
        bool usingStepFallbackMove = false;
        if (!moveLocked && localMove.sqrMagnitude < 0.000001f)
        {
            usingStepFallbackMove = TryGetStepFallbackMoveVector(out localMove);
            if (!usingStepFallbackMove)
                usingInputMove = TryGetInputMoveVector(out localMove);
        }
        Vector3 worldMove = root.right * localMove.x + Vector3.up * localMove.y + root.forward * localMove.z;
        velocity += forceCommand * forceScale * dt;
        velocity = Vector3.Lerp(velocity, Vector3.zero, damping * dt);
        Vector3 scriptedMove = worldMove * moveScale * vFMulti * dt;
        Vector3 requestedMove = scriptedMove + velocity * dt;
        Vector3 appliedMove = MoveRootWithColliderGrounding(root, requestedMove, dt);

        bool finishStepAfterLog = false;
        float debugStepMovedDistance = stepMovedDistance;
        float debugStepTargetDistance = GetActiveStepMoveTargetDistance();
        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
        {
            if (debugStepTargetDistance > 0f)
            {
                stepMovedDistance += scriptedMove.magnitude;
                debugStepMovedDistance = stepMovedDistance;
                if (stepMovedDistance + 0.001f >= debugStepTargetDistance)
                    finishStepAfterLog = true;
            }
            else if (actionTick >= GetStepFallbackDurationTicks())
            {
                finishStepAfterLog = true;
            }
        }

        LogMotionRootDebug(root, positionBefore, localMove, worldMove, scriptedMove, appliedMove, usingInputMove, usingStepFallbackMove, debugStepMovedDistance, debugStepTargetDistance);

        if (finishStepAfterLog)
            StartAirIdleSequence();
    }

    Vector3 MoveRootWithColliderGrounding(Transform root, Vector3 requestedMove, float dt)
    {
        if (!useColliderGrounding || !EnsureGroundingController())
        {
            root.position += requestedMove;
            groundedFlag = false;
            groundingCollisionFlags = CollisionFlags.None;
            return requestedMove;
        }

        bool forceAirborne = ShouldForceAirborneByAction();
        bool applyGravity = !forceAirborne || applyGravityDuringForcedAirborneActions;
        Vector3 positionBefore = root.position;
        Vector3 move = requestedMove;

        if (applyGravity)
        {
            if (groundedFlag && verticalFallSpeed < 0f)
                verticalFallSpeed = groundedVerticalSpeed;
            else
                verticalFallSpeed = Mathf.Max(verticalFallSpeed - gravity * dt, -Mathf.Abs(terminalFallSpeed));

            move += Vector3.up * verticalFallSpeed * dt;
        }
        else
        {
            verticalFallSpeed = Mathf.Max(0f, verticalFallSpeed);
        }

        groundingCollisionFlags = groundingController.Move(move);
        bool controllerGrounded = groundingController.isGrounded || (groundingCollisionFlags & CollisionFlags.Below) != 0;
        if ((groundingCollisionFlags & CollisionFlags.Above) != 0 && verticalFallSpeed > 0f)
            verticalFallSpeed = 0f;
        if (controllerGrounded && verticalFallSpeed < 0f)
            verticalFallSpeed = groundedVerticalSpeed;

        groundedFlag = controllerGrounded;
        if (forceAirborne)
            SetAirborneFlag(true);
        else if (landingSequenceActive && currentAnimationIndex == landingAction)
            SetAirborneFlag(false);
        else
            SetAirborneFlag(!controllerGrounded);

        Vector3 actualMove = root.position - positionBefore;
        LogGroundingRootDebug(forceAirborne, applyGravity, requestedMove, move, actualMove);
        return actualMove;
    }

    bool EnsureGroundingController()
    {
        if (!useColliderGrounding || robo == null || robo.root == null)
            return false;

        if (groundingController == null || groundingController.gameObject != robo.root)
            groundingController = robo.root.GetComponent<CharacterController>();
        if (groundingController == null)
            groundingController = robo.root.AddComponent<CharacterController>();

        float radius = Mathf.Max(0.01f, characterControllerRadius);
        float height = Mathf.Max(characterControllerHeight, radius * 2f + 0.01f);
        groundingController.radius = radius;
        groundingController.height = height;
        groundingController.center = characterControllerCenter;
        groundingController.skinWidth = Mathf.Clamp(characterControllerSkinWidth, 0.001f, radius);
        groundingController.stepOffset = Mathf.Clamp(characterControllerStepOffset, 0f, height);
        groundingController.slopeLimit = characterControllerSlopeLimit;
        groundingController.detectCollisions = true;
        groundingController.enableOverlapRecovery = true;

        return groundingController.enabled;
    }

    void UpdateInputState()
    {
        int dir = 0;
        if (Input.GetKey(KeyCode.UpArrow)) dir = 8;
        else if (Input.GetKey(KeyCode.DownArrow)) dir = 2;
        else if (Input.GetKey(KeyCode.LeftArrow)) dir = 4;
        else if (Input.GetKey(KeyCode.RightArrow)) dir = 6;

        UpdateDirectionTapState(dir);

        riseKeyHeld = Input.GetKey(KeyCode.Z);
        bool riseKeyPressed = riseKeyHeld && !previousRiseKeyHeld;
        if (riseKeyPressed && (currentAnimationIndex == riseStartAction || currentAnimationIndex == riseAction || currentAnimationIndex == airMoveAction || currentAnimationIndex == boostAction))
            boostFromRiseActive = true;
        if (!riseKeyHeld)
            boostFromRiseActive = false;
        previousRiseKeyHeld = riseKeyHeld;

        state.SetInt(190, dir);
        state.SetInt(191, boostFromRiseActive ? 1 : 0);
        state.SetInt(192, Input.GetKey(KeyCode.X) ? 1 : 0);
        state.SetInt(193, Input.GetKey(KeyCode.C) ? 1 : 0);
        state.SetInt(194, Input.GetKey(KeyCode.V) ? 1 : 0);
        state.SetInt(195, Input.GetKey(KeyCode.S) ? 1 : 0);
        state.SetInt(196, Input.GetKey(KeyCode.A) ? 1 : 0);
        state.SetInt(197, Input.GetKey(KeyCode.D) ? 1 : 0);
        state.SetInt(198, Input.GetKey(KeyCode.F) ? 1 : 0);
        state.SetInt(199, riseKeyHeld ? 1 : 0);
        state.SetInt(200, airborneFlag ? 1 : 0);
    }

    void UpdateDirectionTapState(int dir)
    {
        if (dir == 0)
        {
            previousDirectionInput = 0;
            stepDirection = 0;
            return;
        }

        if (dir != previousDirectionInput)
        {
            bool doubleTap = dir == lastDirectionTap &&
                             Time.time - lastDirectionTapTime <= doubleTapStepSeconds;
            if (doubleTap)
            {
                stepDirection = dir;
                lastDirectionTap = 0;
                lastDirectionTapTime = -1f;
            }
            else
            {
                stepDirection = 0;
                lastDirectionTap = dir;
                lastDirectionTapTime = Time.time;
            }
        }

        previousDirectionInput = dir;
    }

    void UpdateTargetState()
    {
        if (target == null || robo == null || robo.root == null)
            return;

        float distance = Vector3.Distance(robo.root.transform.position, target.transform.position);
        state.SetFloat(99, distance);
        state.SetInt(154, target.stateId);
    }

    void UpdateActionFromInput()
    {
        if (ShouldForceAirborneByAction())
            SetAirborneFlag(true);

        if (currentAnimationIndex == airMoveAction && previousAirborneFlag && !airborneFlag)
        {
            StartLandingSequence();
            return;
        }
        previousAirborneFlag = airborneFlag;

        if (landingSequenceActive)
            return;

        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            return;

        int oneShotAction = GetOneShotActionFromInput();
        if (oneShotAction >= 0 && (CanStartActionFromCurrent() || IsHeldAction(currentAnimationIndex)))
        {
            ChangeAnimation(oneShotAction);
            return;
        }

        int heldAction = GetHeldActionFromInput();

        if (currentAnimationIndex == riseStartAction && riseSequenceActive)
        {
            if (IsBoostInputHeld())
                StartBoostAction();
            else if (!riseKeyHeld)
                ChangeToAirMoveOrLanding();
            return;
        }

        if (currentAnimationIndex == riseAction && riseSequenceActive)
        {
            if (IsBoostInputHeld())
                StartBoostAction();
            else if (!riseKeyHeld)
                ChangeToAirMoveOrLanding();
            return;
        }

        if (currentAnimationIndex == boostAction && boostMotionActive)
        {
            if (!IsBoostInputHeld())
                ChangeToAirMoveOrLanding();
            return;
        }

        if (currentAnimationIndex == airMoveAction)
        {
            if (!airborneFlag)
            {
                StartLandingSequence();
                return;
            }

            if (IsBoostInputHeld())
                StartBoostAction();
            else if (heldAction >= 0 && heldAction != airMoveAction)
                ChangeAnimation(heldAction);
            return;
        }

        if (CanStartActionFromCurrent())
        {
            if (heldAction == moveAction && IsMoveInputHeld())
            {
                if (currentAnimationIndex != moveAction || currentAnimation == null)
                    ChangeAnimation(moveAction);

                if (!moveAnimationActive)
                {
                    moveAnimationActive = true;
                    RestartCurrentAnimationLoop();
                }
                return;
            }

            moveAnimationActive = false;
            if (currentAnimationIndex == moveAction && heldAction < 0)
                ChangeAnimation(idleAction);
            if (heldAction >= 0)
                ChangeAnimation(heldAction);
            return;
        }

        if (IsHeldAction(currentAnimationIndex))
        {
            if (heldAction == currentAnimationIndex)
                return;

            ChangeAnimation(heldAction >= 0 ? heldAction : idleAction);
            return;
        }

        moveAnimationActive = false;
    }

    int GetOneShotActionFromInput()
    {
        if (state.GetInt(196) != 0) return special1Action;
        if (state.GetInt(197) != 0) return special2Action;
        if (state.GetInt(198) != 0) return special3Action;
        if (state.GetInt(192) != 0) return shotAction;
        if (state.GetInt(193) != 0) return meleeAction;
        return -1;
    }

    int GetHeldActionFromInput()
    {
        if (state.GetInt(194) != 0) return guardAction;
        if (IsBoostInputHeld()) return boostAction;
        if (riseKeyHeld)
            return currentAnimationIndex == riseStartAction || currentAnimationIndex == riseAction ? riseAction : riseStartAction;

        int stepAction = GetStepActionFromDirection(stepDirection);
        if (stepAction >= 0) return stepAction;

        if (airborneFlag) return airMoveAction;

        if (IsMoveInputHeld())
            return moveAction;

        return -1;
    }

    public void SetAirborneFlag(bool value)
    {
        airborneFlag = value;
        if (state != null)
            state.SetInt(200, airborneFlag ? 1 : 0);
    }

    bool IsBoostInputHeld()
    {
        return riseKeyHeld && state != null && state.GetInt(191) != 0;
    }

    bool ShouldForceAirborneByAction()
    {
        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            return true;
        if (currentAnimationIndex == boostAction && boostMotionActive && IsBoostInputHeld())
            return true;
        if ((currentAnimationIndex == riseStartAction || currentAnimationIndex == riseAction) && riseSequenceActive && riseKeyHeld)
            return true;
        return false;
    }

    bool ShouldRedirectBoostToAirIdle(int actionId)
    {
        return actionId == boostAction &&
               actionId != airMoveAction &&
               airborneFlag &&
               !IsBoostInputHeld();
    }

    void NormalizeActionIds()
    {
        if (idleAction == moveAction)
            idleAction = 0;
        if (airMoveAction == boostAction)
            airMoveAction = 4;
    }

    void ChangeToAirMoveOrLanding()
    {
        boostMotionActive = false;
        if (airborneFlag)
        {
            StartAirIdleSequence();
            return;
        }

        StartLandingSequence();
    }

    void StartAirIdleSequence()
    {
        landingSequenceActive = false;
        riseSequenceActive = false;
        stepSequenceActive = false;
        boostFromRiseActive = false;
        boostMotionActive = false;
        moveAnimationActive = false;
        stepMoveBudgetDistance = 0f;
        stepMovedDistance = 0f;
        ClearHeldMotionState();
        SetAirborneFlag(true);
        previousAirborneFlag = true;
        ChangeAnimation(airMoveAction, currentAnimationIndex == airMoveAction);
    }

    void StartBoostAction()
    {
        boostMotionActive = true;
        SetAirborneFlag(true);
        ChangeAnimation(boostAction, currentAnimationIndex == boostAction);
    }

    void StartLandingSequence()
    {
        landingSequenceActive = true;
        riseSequenceActive = false;
        stepSequenceActive = false;
        boostFromRiseActive = false;
        boostMotionActive = false;
        moveAnimationActive = false;
        stepMoveBudgetDistance = 0f;
        stepMovedDistance = 0f;
        SetAirborneFlag(false);
        previousAirborneFlag = false;
        ChangeAnimation(landingAction, true);
    }

    bool CanStartActionFromCurrent()
    {
        return currentAnimation == null ||
               currentAnimationIndex == 0 ||
               currentAnimationIndex == idleAction ||
               currentAnimationIndex == moveAction ||
               moveAnimationActive;
    }

    bool IsMoveInputHeld()
    {
        return state.GetInt(190) != 0 && stepDirection == 0;
    }

    bool TryGetInputMoveVector(out Vector3 localMove)
    {
        localMove = Vector3.zero;
        if (currentAnimationIndex != moveAction || !moveAnimationActive || !IsMoveInputHeld())
            return false;

        float amount = Mathf.Max(0f, inputMoveMagnitude);
        switch (state.GetInt(190))
        {
            case 8:
                localMove.z = amount;
                return amount > 0f;
            case 2:
                localMove.z = -amount;
                return amount > 0f;
            case 4:
                localMove.x = -amount;
                return amount > 0f;
            case 6:
                localMove.x = amount;
                return amount > 0f;
            default:
                return false;
        }
    }

    bool TryGetStepFallbackMoveVector(out Vector3 localMove)
    {
        localMove = Vector3.zero;
        if (!stepSequenceActive || !IsStepAction(currentAnimationIndex) || stepMoveBudgetDistance > 0f)
            return false;

        float amount = Mathf.Max(0f, stepFallbackMoveMagnitude);
        if (amount <= 0f)
            return false;

        if (currentAnimationIndex == forwardStepAction)
            localMove.z = amount;
        else if (currentAnimationIndex == backStepAction)
            localMove.z = -amount;
        else if (currentAnimationIndex == leftStepAction)
            localMove.x = -amount;
        else if (currentAnimationIndex == rightStepAction)
            localMove.x = amount;
        else
            return false;

        return true;
    }

    float GetActiveStepMoveTargetDistance()
    {
        if (!stepSequenceActive || !IsStepAction(currentAnimationIndex))
            return 0f;

        if (stepMoveBudgetDistance > 0f)
            return stepMoveBudgetDistance;

        return Mathf.Max(0f, stepFallbackDistance);
    }

    int GetStepFallbackDurationTicks()
    {
        int duration = GetFiniteActionDurationTicks();
        return Mathf.Max(1, Mathf.Min(duration, 30));
    }

    int GetRuntimeScriptLengthTicks(script scriptBlock)
    {
        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            return ResolveStepScriptTicks(scriptBlock);

        return Mathf.Max(1, scriptBlock.unk);
    }

    int ResolveStepScriptTicks(script scriptBlock)
    {
        int maxTicks = Mathf.Max(1, maxStepScriptTicksPerBlock);
        int rawTicks = Mathf.Max(1, scriptBlock.unk);
        return Mathf.Min(rawTicks, maxTicks);
    }

    int GetStepActionFromDirection(int dir)
    {
        switch (dir)
        {
            case 8: return forwardStepAction;
            case 2: return backStepAction;
            case 4: return leftStepAction;
            case 6: return rightStepAction;
            default: return -1;
        }
    }

    bool IsStepAction(int actionId)
    {
        return actionId == forwardStepAction ||
               actionId == backStepAction ||
               actionId == leftStepAction ||
               actionId == rightStepAction;
    }

    bool IsHeldAction(int actionId)
    {
        return actionId == guardAction ||
               actionId == boostAction ||
               actionId == riseStartAction ||
               actionId == riseAction ||
               actionId == airMoveAction ||
               IsStepAction(actionId);
    }

    bool ShouldLoopHeldAction()
    {
        if (currentAnimationIndex == moveAction && moveAnimationActive && IsMoveInputHeld())
            return true;
        if (currentAnimationIndex == boostAction && boostMotionActive)
            return IsBoostInputHeld();
        if (currentAnimationIndex == airMoveAction && airborneFlag)
            return true;
        if (currentAnimationIndex == boostAction)
            return false;
        return IsHeldAction(currentAnimationIndex) && GetHeldActionFromInput() == currentAnimationIndex;
    }

    void RestartCurrentAnimationLoop()
    {
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        actionTick = 0;
    }

    bool TryFinishFiniteActionByTicks()
    {
        bool stepAction = stepSequenceActive && IsStepAction(currentAnimationIndex);
        bool landingActionActive = landingSequenceActive && currentAnimationIndex == landingAction;
        if (!stepAction && !landingActionActive)
            return false;

        int durationTicks = GetFiniteActionDurationTicks();
        ApplyFiniteActionPoseProgress(durationTicks);

        if (stepAction && GetActiveStepMoveTargetDistance() > 0f)
            return false;

        if (actionTick < durationTicks)
            return false;

        if (stepAction)
        {
            StartAirIdleSequence();
            return true;
        }

        landingSequenceActive = false;
        ChangeAnimation(idleAction);
        return true;
    }

    int GetFiniteActionDurationTicks()
    {
        if (currentAnimation != null && currentAnimation.scripts != null && currentAnimation.scripts.Count > 0)
        {
            int ticks = 0;
            for (int i = 0; i < currentAnimation.scripts.Count; i++)
                ticks += GetRuntimeScriptLengthTicks(currentAnimation.scripts[i]);
            return Mathf.Max(1, ticks);
        }

        if (currentAnimation != null && currentAnimation.frames != null)
            return Mathf.Max(1, currentAnimation.frames.Count);

        return 1;
    }

    void ApplyFiniteActionPoseProgress(int durationTicks)
    {
        if (currentAnimation == null || currentAnimation.frames == null || currentAnimation.frames.Count <= 1)
            return;

        float progress = durationTicks <= 1 ? 1f : Mathf.Clamp01((float)actionTick / durationTicks);
        float pose = progress * (currentAnimation.frames.Count - 1);
        frameIndex = Mathf.Min(Mathf.FloorToInt(pose), currentAnimation.frames.Count - 1);
        frameTime = Mathf.Clamp01(pose - frameIndex);
    }

    float CalculateStepMoveBudgetDistance(animation anim)
    {
        if (anim == null || anim.scripts == null || anim.scripts.Count == 0)
            return 0f;

        Vector3 simulatedMove = Vector3.zero;
        float simulatedVFMulti = Mathf.Max(0f, vFMulti);
        float distance = 0f;
        bool sawMove = false;

        for (int i = 0; i < anim.scripts.Count; i++)
        {
            script block = anim.scripts[i];
            ApplyScriptMotionPreview(block.squirrel, ref simulatedMove, ref simulatedVFMulti, ref sawMove);
            int ticks = ResolveStepScriptTicks(block);
            distance += simulatedMove.magnitude * moveScale * simulatedVFMulti * Time.fixedDeltaTime * ticks;
        }

        return sawMove ? Mathf.Max(0f, distance) : 0f;
    }

    void ApplyScriptMotionPreview(string scriptText, ref Vector3 simulatedMove, ref float simulatedVFMulti, ref bool sawMove)
    {
        if (string.IsNullOrEmpty(scriptText))
            return;

        string[] lines = scriptText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = CleanScriptPreviewLine(lines[i]);
            if (string.IsNullOrEmpty(line))
                continue;

            if (IsPreviewControlLine(line))
                continue;

            string op;
            int opIndex;
            if (TryFindPreviewAssignment(line, out op, out opIndex))
            {
                string left = NormalizeName(line.Substring(0, opIndex));
                string right = line.Substring(opIndex + op.Length).Trim();
                if (right.StartsWith("(", StringComparison.Ordinal) && right.EndsWith(")", StringComparison.Ordinal))
                    right = right.Substring(1, right.Length - 2);
                ApplyMotionPreviewValue(left, op, ParsePreviewArgs(right), ref simulatedMove, ref simulatedVFMulti, ref sawMove);
                continue;
            }

            int paren = line.IndexOf('(');
            int close = line.LastIndexOf(')');
            if (paren > 0 && close > paren)
            {
                string name = NormalizeName(line.Substring(0, paren));
                List<TestPlayScriptValue> args = ParsePreviewArgs(line.Substring(paren + 1, close - paren - 1));
                ApplyMotionPreviewValue(name, "", args, ref simulatedMove, ref simulatedVFMulti, ref sawMove);
            }
        }
    }

    void ApplyMotionPreviewValue(string key, string op, List<TestPlayScriptValue> values, ref Vector3 simulatedMove, ref float simulatedVFMulti, ref bool sawMove)
    {
        switch (key)
        {
            case "move":
                SetVector(ref simulatedMove, values);
                if (simulatedMove.sqrMagnitude > 0.000001f)
                    sawMove = true;
                break;
            case "vf_multi":
                if (values.Count > 0)
                    simulatedVFMulti = Mathf.Max(0f, values[0].AsFloat(simulatedVFMulti));
                break;
        }
    }

    List<TestPlayScriptValue> ParsePreviewArgs(string argsText)
    {
        List<string> tokens = SplitPreviewArgs(argsText);
        List<TestPlayScriptValue> values = new List<TestPlayScriptValue>();
        for (int i = 0; i < tokens.Count; i++)
        {
            TestPlayScriptValue value;
            TestPlayScriptValue.TryParse(tokens[i], state, out value);
            values.Add(value);
        }
        return values;
    }

    static List<string> SplitPreviewArgs(string argsText)
    {
        List<string> args = new List<string>();
        if (string.IsNullOrWhiteSpace(argsText))
            return args;

        int start = 0;
        bool inQuote = false;
        char quote = '\0';
        for (int i = 0; i < argsText.Length; i++)
        {
            char c = argsText[i];
            if ((c == '"' || c == '\'') && (i == 0 || argsText[i - 1] != '\\'))
            {
                if (!inQuote)
                {
                    inQuote = true;
                    quote = c;
                }
                else if (quote == c)
                {
                    inQuote = false;
                }
            }
            else if (c == ',' && !inQuote)
            {
                args.Add(argsText.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        args.Add(argsText.Substring(start).Trim());
        return args;
    }

    static string CleanScriptPreviewLine(string rawLine)
    {
        string line = (rawLine ?? "").Trim();
        if (line.Length == 0 || line.StartsWith("'", StringComparison.Ordinal))
            return "";
        if (line.EndsWith(";", StringComparison.Ordinal))
            line = line.Substring(0, line.Length - 1).Trim();
        return line;
    }

    static bool IsPreviewControlLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return true;

        string trimmed = line.Trim();
        if (string.Equals(trimmed, "ELSE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "ENDIF", StringComparison.OrdinalIgnoreCase))
            return true;

        return StartsWithPreviewCommand(trimmed, "IF");
    }

    static bool StartsWithPreviewCommand(string line, string command)
    {
        if (!line.StartsWith(command, StringComparison.OrdinalIgnoreCase))
            return false;

        int index = command.Length;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;
        return index < line.Length && line[index] == '(';
    }

    static bool TryFindPreviewAssignment(string line, out string op, out int index)
    {
        int firstParen = line.IndexOf('(');
        string[] ops = { "+=", "-=", "*=", "/=", "=" };
        for (int i = 0; i < ops.Length; i++)
        {
            index = line.IndexOf(ops[i], StringComparison.Ordinal);
            if (index >= 0)
            {
                if (firstParen >= 0 && index > firstParen)
                    continue;
                if (ops[i] == "=" && IsComparisonEquals(line, index))
                    continue;
                op = ops[i];
                return true;
            }
        }

        op = "";
        index = -1;
        return false;
    }

    static bool IsComparisonEquals(string line, int index)
    {
        if (index > 0 && (line[index - 1] == '=' || line[index - 1] == '!' || line[index - 1] == '<' || line[index - 1] == '>'))
            return true;
        return index + 1 < line.Length && line[index + 1] == '=';
    }

    void ClearHeldMotionState()
    {
        moveCommand = Vector3.zero;
        forceCommand = Vector3.zero;
        moveLocked = false;
        shieldGuard = false;
    }

    void ExecuteScript(string text)
    {
        if (string.IsNullOrEmpty(text) || vm == null)
            return;
        vm.Execute(text);
    }

    void HandleCommand(string name, List<TestPlayScriptValue> args, string rawLine)
    {
        if (logCommands)
            Debug.Log($"[TestPlayCommand] A{currentAnimationIndex} S{scriptIndex}: {rawLine.Trim()}");

        string key = NormalizeName(name);
        switch (key)
        {
            case "move":
                SetVector(ref moveCommand, args);
                LogMotionAssignment("Move", rawLine);
                break;
            case "force":
                SetVector(ref forceCommand, args);
                LogMotionAssignment("Force", rawLine);
                break;
            case "movelock":
                moveLocked = true;
                LogMotionAssignment("MoveLock", rawLine);
                break;
            case "lockbodyuptarget":
            case "lockbodydowntarget":
            case "lockbodytarget":
            case "lockarm1target":
            case "lockarm2target":
            case "lockarmtarget":
                AimAtTarget();
                break;
            case "attack": HandleAttack(args); break;
            case "weaponattack": SpawnWeapon(args, "WeaponAttack"); break;
            case "weaponattack2": SpawnWeapon(args, "WeaponAttack2"); break;
            case "runproc": SpawnRunProc(args, false); break;
            case "runproc2": SpawnRunProc(args, true); break;
            case "burner": if (args.Count > 0) burnerRequestedIds.Add(args[0].AsInt()); break;
            case "snd": Debug.Log("[TestPlay] Snd " + ArgsToString(args)); break;
            case "voice": Debug.Log("[TestPlay] Voice " + ArgsToString(args)); break;
            case "changescript":
            case "goscriptindex": if (args.Count > 0) scriptIndex = Mathf.Max(0, args[0].AsInt()); scriptTick = 0; break;
            case "goposeindex": if (args.Count > 0) frameIndex = Mathf.Max(0, args[0].AsInt()); frameTime = 0f; break;
            case "changeanime": if (args.Count > 0) ChangeAnimation(args[0].AsInt()); break;
            case "changewapon":
            case "changeweapon": if (args.Count > 0) heldWeapon = args[0].ToString(); state.SetInt(152, string.Equals(heldWeapon, "SWORD", StringComparison.OrdinalIgnoreCase) ? 1 : 0); break;
            case "attackdelay": break;
            case "execscripteverytime": executeScriptEveryTick = args.Count == 0 || args[0].AsBool(); break;
            case "addenergy": if (args.Count > 0) state.SetFloat(100, state.GetFloat(100) + args[0].AsFloat()); break;
            case "addexgauge": if (args.Count > 0) state.SetInt(155, state.GetInt(155) + args[0].AsInt()); break;
            default:
                LogUnhandled("Command: " + name + " raw=" + rawLine.Trim());
                break;
        }
    }

    void HandleAssignment(string name, string op, List<TestPlayScriptValue> values, string rawLine)
    {
        string key = NormalizeName(name);
        switch (key)
        {
            case "move":
                SetVector(ref moveCommand, values);
                LogMotionAssignment("Move", rawLine);
                break;
            case "force":
                SetVector(ref forceCommand, values);
                LogMotionAssignment("Force", rawLine);
                break;
            case "gvenable": gvEnable = values.Count > 0 && values[0].AsBool(); break;
            case "shotturnang": shotTurnAng = values.Count > 0 ? values[0].AsFloat() : 0f; break;
            case "turnmoveang": turnMoveAng = values.Count > 0 ? values[0].AsFloat() : 0f; break;
            case "shildguard": shieldGuard = values.Count > 0 && values[0].AsBool(); break;
            case "attackflag": attackFlag = values.Count > 0 ? values[0].AsInt() : 0; break;
            case "cameffect": camEffect = values.Count > 0 ? values[0].AsFloat() : 0f; break;
            case "vf_multi":
                vFMulti = values.Count > 0 ? values[0].AsFloat(1f) : 1f;
                LogMotionAssignment("vF_Multi", rawLine);
                break;
            case "animeloop": animeLoop = values.Count > 0 && values[0].AsBool(); break;
            case "swordcancel": swordCancelAction = values.Count > 0 ? values[0].AsInt(-1) : -1; break;
            case "movelock":
                moveLocked = values.Count == 0 || values[0].AsBool();
                LogMotionAssignment("MoveLock", rawLine);
                break;
            default:
                LogUnhandled("Assignment: " + name + op + ArgsToString(values) + " raw=" + rawLine.Trim());
                break;
        }
    }

    void SetVector(ref Vector3 targetVector, List<TestPlayScriptValue> args)
    {
        if (args.Count > 0 && args[0].type != TestPlayScriptValueType.Stop) targetVector.x = args[0].AsFloat(targetVector.x);
        if (args.Count > 1 && args[1].type != TestPlayScriptValueType.Stop) targetVector.y = args[1].AsFloat(targetVector.y);
        if (args.Count > 2 && args[2].type != TestPlayScriptValueType.Stop) targetVector.z = args[2].AsFloat(targetVector.z);
    }

    void AimAtTarget()
    {
        if (target == null || robo == null || robo.root == null)
            return;

        Vector3 toTarget = target.transform.position - robo.root.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        robo.root.transform.rotation = Quaternion.RotateTowards(robo.root.transform.rotation, desired, aimTurnSpeed * Time.fixedDeltaTime);
    }

    void HandleAttack(List<TestPlayScriptValue> args)
    {
        float damage = args.Count > 0 ? args[0].AsFloat(defaultProjectileDamage) : defaultProjectileDamage;
        if (target == null || robo == null || robo.root == null)
            return;

        float distance = Vector3.Distance(robo.root.transform.position, target.transform.position);
        if (distance <= meleeRange + target.hitRadius)
            target.ApplyDamage(damage, target.transform.position, "ATTACK");
    }

    void SpawnWeapon(List<TestPlayScriptValue> args, string source)
    {
        int weaponType = args.Count > 1 ? args[1].AsInt() : 0;
        float energy = args.Count > 3 ? args[3].AsFloat() : 0f;
        if (energy > 0f)
            state.SetFloat(100, state.GetFloat(100) - energy);

        float damage = EstimateDamageForWeapon(weaponType);
        float speed = EstimateSpeed(args, defaultProjectileSpeed);
        SpawnProjectile(source + ":" + weaponType, damage, speed, IsHomingWeapon(weaponType));
    }

    void SpawnRunProc(List<TestPlayScriptValue> args, bool extended)
    {
        int procType = args.Count > 1 ? args[1].AsInt() : 0;
        if (procType == 55 || procType == 57)
        {
            HandleAttack(new List<TestPlayScriptValue> { TestPlayScriptValue.Number(procType == 57 ? 80f : 50f) });
            return;
        }

        if (procType == 62)
        {
            int subtype = args.Count > 3 ? args[3].AsInt() : 0;
            if (subtype == 3)
                SpawnSimpleEffect(Color.cyan, 0.35f, 0.3f);
            return;
        }

        SpawnProjectile((extended ? "RunProc2:" : "RunProc:") + procType, EstimateDamageForWeapon(procType), EstimateSpeed(args, defaultProjectileSpeed), IsHomingWeapon(procType));
    }

    void SpawnProjectile(string source, float damage, float speed, bool homing)
    {
        if (robo == null || robo.root == null)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TestPlayProjectile_" + source;
        go.transform.position = robo.root.transform.position + robo.root.transform.forward * 1.5f + Vector3.up * 1.2f;
        go.transform.rotation = robo.root.transform.rotation;
        go.transform.localScale = Vector3.one * projectileRadius;
        TestPlayProjectile projectile = go.AddComponent<TestPlayProjectile>();
        projectile.target = target;
        projectile.damage = damage;
        projectile.speed = speed;
        projectile.hitRadius = projectileRadius;
        projectile.homingTurnRate = homing ? 180f : 0f;
        projectile.sourceCommand = source;
    }

    void SpawnSimpleEffect(Color color, float scale, float life)
    {
        if (robo == null || robo.root == null)
            return;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "TestPlayEffect";
        go.transform.position = robo.root.transform.position - robo.root.transform.forward * 0.5f;
        go.transform.localScale = Vector3.one * scale;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
        Destroy(go, life);
    }

    void ApplyBurners()
    {
        if (sptSource == null || sptSource.LastSptData == null)
        {
            HideAllBurnerCones();
            return;
        }

        if (useConeBurnerEffects)
        {
            ApplyConeBurners(sptSource.LastSptData);
            return;
        }

        ApplyParticleBurners(sptSource.LastSptData);
    }

    void ApplyParticleBurners(SptRuntimeData data)
    {
        HideAllBurnerCones();

        foreach (var kv in data.BurnerSets)
        {
            BurnerSetInfo info = kv.Value;
            if (info == null || info.Ps == null)
                continue;

            bool requested = burnerRequestedIds.Contains(info.Id);
            if (requested)
            {
                if (!info.Ps.isPlaying)
                    info.Ps.Play(true);
            }
            else if (!info.Ps.isStopped)
            {
                info.Ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    void ApplyConeBurners(SptRuntimeData data)
    {
        validBurnerIds.Clear();

        foreach (var kv in data.BurnerSets)
        {
            BurnerSetInfo info = kv.Value;
            if (info == null)
                continue;

            validBurnerIds.Add(info.Id);
            StopParticleBurner(info);

            TestPlayBurnerCone cone = GetOrCreateBurnerCone(info);
            if (cone == null)
                continue;

            bool requested = burnerRequestedIds.Contains(info.Id) && info.Scale > 0f;
            float length = Mathf.Max(0f, info.Scale * burnerLengthMultiplier);
            float radius = Mathf.Max(0.001f, length * burnerRadiusRatio);
            cone.SetTarget(requested, length, radius, burnerConeColor, burnerFadeSpeed);
        }

        foreach (var kv in burnerCones)
        {
            if (!validBurnerIds.Contains(kv.Key) && kv.Value != null)
                kv.Value.HideImmediate();
        }
    }

    TestPlayBurnerCone GetOrCreateBurnerCone(BurnerSetInfo info)
    {
        if (info.BoneTr == null || info.Scale <= 0f)
            return null;

        TestPlayBurnerCone cone;
        if (!burnerCones.TryGetValue(info.Id, out cone) || cone == null)
        {
            GameObject go = new GameObject("TestPlayBurner_" + info.Id + "_" + info.FrameName);
            go.transform.SetParent(info.BoneTr, worldPositionStays: false);
            cone = go.AddComponent<TestPlayBurnerCone>();
            burnerCones[info.Id] = cone;
        }

        if (cone.transform.parent != info.BoneTr)
            cone.transform.SetParent(info.BoneTr, worldPositionStays: false);

        cone.transform.localPosition = Vector3.zero;
        cone.transform.localRotation = BurnerDirectionToRotation(info.Direction);
        return cone;
    }

    void StopParticleBurner(BurnerSetInfo info)
    {
        if (info.Ps != null && !info.Ps.isStopped)
            info.Ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void HideAllBurnerCones()
    {
        foreach (var cone in burnerCones.Values)
        {
            if (cone != null)
                cone.HideImmediate();
        }
    }

    void StopAllBurnerEffects()
    {
        burnerRequestedIds.Clear();
        HideAllBurnerCones();

        if (sptSource == null || sptSource.LastSptData == null)
            return;

        foreach (var kv in sptSource.LastSptData.BurnerSets)
        {
            BurnerSetInfo info = kv.Value;
            if (info != null)
                StopParticleBurner(info);
        }
    }

    float EstimateDamageForWeapon(int weaponType)
    {
        switch (weaponType)
        {
            case 1: return 45f;
            case 3:
            case 4:
            case 9:
            case 19: return 70f;
            case 24:
            case 25:
            case 28: return 100f;
            case 55:
            case 57: return 80f;
            default: return defaultProjectileDamage;
        }
    }

    float EstimateSpeed(List<TestPlayScriptValue> args, float fallback)
    {
        if (args.Count > 5)
            return Mathf.Max(5f, args[5].AsFloat(fallback));
        return fallback;
    }

    bool IsHomingWeapon(int weaponType)
    {
        return weaponType == 3 || weaponType == 4 || weaponType == 10 || weaponType == 13 || weaponType == 19 || weaponType == 21;
    }

    void LogMotionAssignment(string label, string rawLine)
    {
        if (!logMotionDebug || !logMotionAssignments)
            return;

        string rootName = robo != null && robo.root != null ? robo.root.name : "(null)";
        Debug.Log(string.Format(
            "[TestPlay][MotionAssign] tick={0} action={1}:{2} script={3} root={4} label={5} raw=\"{6}\" move={7} force={8} moveLocked={9} vF={10:F3} moveScale={11:F3}",
            tick,
            currentAnimationIndex,
            currentAnimationName,
            scriptIndex,
            rootName,
            label,
            (rawLine ?? "").Trim(),
            FormatVector(moveCommand),
            FormatVector(forceCommand),
            moveLocked,
            vFMulti,
            moveScale));
    }

    void LogMotionRootDebug(Transform root, Vector3 positionBefore, Vector3 localMove, Vector3 worldMove, Vector3 scriptedMove, Vector3 appliedMove, bool usingInputMove, bool usingStepFallbackMove, float debugStepMovedDistance, float debugStepTargetDistance)
    {
        if (!logMotionDebug)
            return;

        int interval = Mathf.Max(1, motionDebugIntervalTicks);
        bool hasMotion = localMove.sqrMagnitude > 0.000001f || forceCommand.sqrMagnitude > 0.000001f || appliedMove.sqrMagnitude > 0.000001f;
        bool shouldLogThisTick = hasMotion || (tick % interval) == 0;
        if (!shouldLogThisTick)
            return;

        Vector3 positionAfter = root.position;
        Debug.Log(string.Format(
            "[TestPlay][RootMotion] tick={0} action={1}:{2} script={3}/{4} root={5} path={6} pos={7}->{8} delta={9} localMove={10} worldMove={11} scriptedMove={12} appliedMove={13} force={14} velocity={15} moveLocked={16} inputMove={17} stepFallbackMove={18} vF={19:F3} moveScale={20:F3} dt={21:F4} step={22} stepDist={23:F4}/{24:F4}",
            tick,
            currentAnimationIndex,
            currentAnimationName,
            scriptIndex,
            scriptTick,
            root.name,
            GetTransformPath(root),
            FormatVector(positionBefore),
            FormatVector(positionAfter),
            FormatVector(positionAfter - positionBefore),
            FormatVector(localMove),
            FormatVector(worldMove),
            FormatVector(scriptedMove),
            FormatVector(appliedMove),
            FormatVector(forceCommand),
            FormatVector(velocity),
            moveLocked,
            usingInputMove,
            usingStepFallbackMove,
            vFMulti,
            moveScale,
            Time.fixedDeltaTime,
            stepSequenceActive && IsStepAction(currentAnimationIndex),
            debugStepMovedDistance,
            debugStepTargetDistance));
    }

    void LogGroundingRootDebug(bool forceAirborne, bool applyGravity, Vector3 requestedMove, Vector3 controllerMove, Vector3 actualMove)
    {
        if (!logGroundingDebug)
            return;

        int interval = Mathf.Max(1, motionDebugIntervalTicks);
        bool hasVerticalMotion = Mathf.Abs(controllerMove.y) > 0.000001f || Mathf.Abs(verticalFallSpeed) > 0.000001f;
        bool shouldLogThisTick = hasVerticalMotion || (tick % interval) == 0;
        if (!shouldLogThisTick)
            return;

        Debug.Log(string.Format(
            "[TestPlay][Grounding] tick={0} action={1}:{2} grounded={3} airborne={4} forceAir={5} gravity={6} fallSpeed={7:F4} flags={8} requested={9} controllerMove={10} actual={11}",
            tick,
            currentAnimationIndex,
            currentAnimationName,
            groundedFlag,
            airborneFlag,
            forceAirborne,
            applyGravity,
            verticalFallSpeed,
            groundingCollisionFlags,
            FormatVector(requestedMove),
            FormatVector(controllerMove),
            FormatVector(actualMove)));
    }

    static string FormatVector(Vector3 value)
    {
        return string.Format("({0:F4},{1:F4},{2:F4})", value.x, value.y, value.z);
    }

    static string GetTransformPath(Transform tr)
    {
        if (tr == null)
            return "(null)";

        string path = tr.name;
        Transform current = tr.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    void ResetRuntimeFlags()
    {
        currentAnimation = null;
        currentAnimationIndex = 0;
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        tick = 0;
        initFired = false;
        executeScriptEveryTick = false;
        animeLoop = false;
        moveLocked = false;
        shieldGuard = false;
        gvEnable = false;
        attackFlag = 0;
        swordCancelAction = -1;
        shotTurnAng = 0f;
        turnMoveAng = 0f;
        camEffect = 0f;
        vFMulti = 1f;
        moveCommand = Vector3.zero;
        forceCommand = Vector3.zero;
        velocity = Vector3.zero;
        verticalFallSpeed = 0f;
        groundedFlag = false;
        groundingCollisionFlags = CollisionFlags.None;
        riseKeyHeld = false;
        previousRiseKeyHeld = false;
        riseSequenceActive = false;
        landingSequenceActive = false;
        stepSequenceActive = false;
        previousAirborneFlag = false;
        boostFromRiseActive = false;
        boostMotionActive = false;
        moveAnimationActive = false;
        actionTick = 0;
        stepMoveBudgetDistance = 0f;
        stepMovedDistance = 0f;
        airborneFlag = false;
        state.SetInt(200, 0);
        previousDirectionInput = 0;
        lastDirectionTap = 0;
        stepDirection = 0;
        lastDirectionTapTime = -1f;
        burnerRequestedIds.Clear();
        HideAllBurnerCones();
        ClearPoseTransition();
    }

    void LogUnhandled(string message)
    {
        if (logUnhandledCommands)
            Debug.LogWarning("[TestPlay] Unhandled " + message);
    }

    static Quaternion SafeRotation(Quaternion q)
    {
        if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f)
            return Quaternion.identity;
        return q;
    }

    void StartPoseTransition(int fromAction, int toAction)
    {
        if (!blendActionTransitions || robo == null || robo.parts == null || robo.parts.Count == 0)
        {
            ClearPoseTransition();
            return;
        }

        if (currentAnimation == null)
        {
            ClearPoseTransition();
            return;
        }

        float seconds = ResolveTransitionSeconds(fromAction, toAction);
        if (seconds <= 0f)
        {
            ClearPoseTransition();
            return;
        }

        transitionFromPose = new TestPlayPosePart[robo.parts.Count];
        for (int i = 0; i < robo.parts.Count; i++)
        {
            GameObject part = robo.parts[i];
            if (part == null)
                continue;

            transitionFromPose[i] = new TestPlayPosePart
            {
                position = part.transform.localPosition,
                rotation = part.transform.localRotation,
                scale = part.transform.localScale
            };
        }

        transitionTick = 0;
        transitionTickTotal = Mathf.Max(1, Mathf.RoundToInt(seconds / Mathf.Max(Time.fixedDeltaTime, 0.0001f)));
    }

    float ResolveTransitionSeconds(int fromAction, int toAction)
    {
        if (toAction == idleAction && IsHeldAction(fromAction))
            return heldReleaseTransitionSeconds;
        return actionTransitionSeconds;
    }

    bool IsPoseTransitionActive()
    {
        return transitionFromPose != null && transitionTick < transitionTickTotal;
    }

    float GetPoseTransitionBlend()
    {
        if (!IsPoseTransitionActive())
            return 1f;

        float raw = transitionTickTotal <= 0 ? 1f : (float)transitionTick / transitionTickTotal;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(raw));
    }

    void AdvancePoseTransition()
    {
        if (!IsPoseTransitionActive())
            return;

        transitionTick++;
        if (transitionTick >= transitionTickTotal)
            ClearPoseTransition();
    }

    void ClearPoseTransition()
    {
        transitionFromPose = null;
        transitionTick = 0;
        transitionTickTotal = 0;
    }

    static Quaternion BurnerDirectionToRotation(SptDirection dir)
    {
        switch (dir)
        {
            case SptDirection.UP: return Quaternion.Euler(180f, 0f, 0f);
            case SptDirection.DOWN: return Quaternion.identity;
            case SptDirection.FORWARD: return Quaternion.Euler(-90f, 0f, 0f);
            case SptDirection.BACK: return Quaternion.Euler(90f, 0f, 0f);
            case SptDirection.LEFT: return Quaternion.Euler(0f, 90f, 0f);
            case SptDirection.RIGHT: return Quaternion.Euler(0f, -90f, 0f);
            default: return Quaternion.identity;
        }
    }

    static string NormalizeName(string name)
    {
        string n = (name ?? "").Trim();
        if (n.StartsWith("Scr_", StringComparison.OrdinalIgnoreCase))
            n = n.Substring(4);
        return n.ToLowerInvariant();
    }

    static string ArgsToString(List<TestPlayScriptValue> args)
    {
        if (args == null || args.Count == 0)
            return "";
        string[] values = new string[args.Count];
        for (int i = 0; i < args.Count; i++)
            values[i] = args[i].ToString();
        return string.Join(",", values);
    }
}
