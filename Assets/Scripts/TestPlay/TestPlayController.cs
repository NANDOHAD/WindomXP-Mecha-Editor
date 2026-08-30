using System;
using System.Collections.Generic;
using UnityEngine;

public class TestPlayController : MonoBehaviour
{
    [Header("References")]
    public RoboStructure robo;
    public TestPlayTargetDummy target;
    public UI_SPT sptSource;
    public TestPlayPresentationRuntime presentationRuntime;
    public TestPlayCameraController cameraController;
    public TestPlayHudRuntime hudRuntime;

    [Header("Mode")]
    public bool playModeActive;
    public bool startOnPlay;
    public bool logCommands;
    public bool logUnhandledCommands = true;

    [Header("Original Simulation")]
    [Min(1f)]
    [Tooltip("原作メインループで確認できる固定更新周波数。UnityのFixed Timestepとは独立して進めます。")]
    public float originalTickRate = 60f;
    [Min(1)]
    [Tooltip("低フレーム時に1フレームで追いつく最大tick数。残り時間は捨てず次フレームへ持ち越します。")]
    public int maximumCatchUpTicks = 8;
    [Tooltip("原作のANI座標1.0/tickをUnity座標へ変換する倍率。旧35 units/s × 0.02秒と同じ既定移動量です。")]
    public float aniUnitsToUnityScale = 0.7f;
    [Min(0f)]
    public float originalGravityPerTick = 0.013f;
    [Min(0f)]
    public float originalTerminalFallSpeed = 0.8f;

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
    public int airIdleAction = 8;
    public int landingAction = 5;
    [Tooltip("原作FUN_004d77e0が接地ステップ終了時に使う専用復帰アクション。通常着地ID 5とは分けます。")]
    public int stepLandingAction = 6;
    [Tooltip("銃状態からC入力で抜刀状態へ切り替える原作アクション。")]
    public int switchToSwordAction = 18;
    [Tooltip("抜刀状態からX入力で銃状態へ切り替える原作アクション。")]
    public int switchToGunAction = 68;
    public int shotAction = 100;
    [Tooltip("ブースト中のX入力で使う原作の飛行射撃アクション。")]
    public int boostShotAction = 106;
    [Tooltip("前方向入力中のCで開始する格闘誘導アクション。")]
    public int meleeAction = 130;
    public int neutralMeleeAction = 131;
    public int meleeApproachFollowupAction = 136;
    public int leftMeleeAction = 141;
    public int rightMeleeAction = 146;
    public int backMeleeAction = 151;
    public int boostAction = 22;
    public int guardAction = 19;
    public int special1Action = 104;
    public int special2Action = 105;
    public int special3Action = 109;

    [Header("Motion")]
    [Tooltip("旧シーン互換用。原作準拠経路ではaniUnitsToUnityScaleを使用します。")]
    public float moveScale = 35f;
    [Tooltip("旧シーン互換用。原作準拠経路ではForceをANI座標/tickのまま積分します。")]
    public float forceScale = 35f;
    [Tooltip("旧シーン互換用。原作準拠のForce速度減衰は下記のtick倍率を使用します。")]
    public float damping = 4f;
    [Min(0f)]
    [Tooltip("原作の上昇・空中移動更新でForce加算前に適用するY速度上限（ANI座標/tick）。")]
    public float riseVerticalVelocityLimit = 0.15f;
    [Range(0f, 1f)]
    public float airborneHorizontalForceRetention = 0.95f;
    [Range(0f, 1f)]
    public float groundedHorizontalForceRetention = 0.9f;
    [Range(0f, 1f)]
    [Tooltip("原作action開始時に接地待機へ適用するANI Move保持率(+0xA88)。Move/STOPの状態とは分離します。")]
    public float idleMoveRetention = 0.8f;
    [Range(0f, 1f)]
    [Tooltip("原作action開始時に通常移動へ適用するANI Move保持率(+0xA88)。")]
    public float moveActionRetention = 1f;
    [Range(0f, 1f)]
    [Tooltip("原作FUN_004d68e0へ入る空中停止action 8のANI Move保持率(+0xA88)。")]
    public float airIdleMoveRetention = 0.99f;
    [Range(0f, 1f)]
    [Tooltip("個別対応をまだ確定していないactionのANI Move保持率。")]
    public float defaultMoveRetention = 1f;
    public float aimTurnSpeed = 540f;
    public float doubleTapStepSeconds = 0.3f;
    public float inputMoveMagnitude = 0.08f;
    [Tooltip("オンの場合、ロック対象が有効なら対象方向を移動基準として優先します。")]
    public bool useTargetRelativeMovement = true;
    [Tooltip("対象基準を使わない場合、揺れ適用前の論理カメラ方位を移動基準として毎tick更新します。オフの場合は入力開始時の自機正面を保持します。")]
    public bool useCameraRelativeMovement = true;
    [Min(0f)]
    public float inputTurnDegreesPerTick = 12f;
    [Min(0f)]
    public float riseTurnDegreesPerTick = 14f;
    [Min(0f)]
    public float boostInitialTurnDegreesPerTick = 15f;
    [Min(1)]
    public int riseMinimumReleaseTicks = 5;
    [Min(1)]
    [Tooltip("旧シーンとのInspector互換用。原作準拠の上昇ID 7はZ保持とエネルギーが続く限り継続するため、終了条件には使用しません。")]
    public int riseMaximumTicks = 12;
    [Min(1)]
    [Tooltip("原作FUN_004d68e0が空中停止から再上昇を受け付ける最小action 8 tick数。c38が10を超えてから受け付けます。")]
    public int airRiseMinimumIdleTicks = 11;
    [Min(0f)]
    [Tooltip("原作FUN_004d68e0が空中停止中、共通重力処理の前にY速度へ加える値。")]
    public float airIdleVerticalBrakePerTick = 0.012f;
    [Min(0f)]
    [Tooltip("空中停止補助を加える直前Y速度の上限。原作はY速度が0.05未満のときだけ加算します。")]
    public float airIdleVerticalBrakeVelocityThreshold = 0.05f;
    [Min(1)]
    [Tooltip("空中停止補助を受け付けるaction 8のtick上限。原作c38 < 31に対応します。")]
    public int airIdleVerticalBrakeTicks = 31;
    [Min(1)]
    public int boostMinimumReleaseTicks = 31;
    public float doubleTapBoostSeconds = 0.3f;
    public float stepFallbackMoveMagnitude = 0.51f;
    [Min(0f)]
    public float stepTargetForwardBias = 0.3f;
    [Tooltip("オンの場合、ステップ中は開始時の移動基準方向へ機体正面を保ち、横・後ステップ時の不自然な回転を抑えます。オフの場合は選択したステップアニメーションと移動方向を一致させる従来回転を使用します。")]
    public bool preserveStepReferenceFacing = true;
    [Tooltip("原作FUN_004d0b70/004d77e0に合わせ、入力方向に対するID 9～12のローカル移動方向が進行方向と一致する姿勢へ最大3度/tickで旋回します。オフ時だけpreserveStepReferenceFacingの旧近似を使います。")]
    public bool useOriginalStepFacing = true;
    [Min(0f)]
    public float stepTurnDegreesPerTick = 3f;
    [Min(1)]
    public int stepMinimumTicks = 16;
    [Min(1)]
    public int stepMaximumTicks = 61;
    [Tooltip("旧実装とのシーン互換用。原作準拠ステップでは距離到達を終了条件にしません。")]
    public float stepFallbackDistance = 3f;
    [Min(1)]
    [Tooltip("旧実装とのシーン互換用。原作準拠ステップではANIのブロックtickを丸めません。")]
    public int maxStepScriptTicksPerBlock = 5;

    [Header("Target Lock")]
    [Tooltip("オンの場合、Sキーを押すまでtargetをロック対象にしません。")]
    public bool requireLockInput = true;
    [Min(0f)]
    [Tooltip("Script.sptのLockDistが未読または0以下の場合に使うロック可能距離。")]
    public float fallbackLockDistance = 100f;
    public TestPlayTargetDummy lockedTarget;

    [Header("Script Aim (Unity approximation)")]
    [Tooltip("LockBodyUpTargetを適用する表示階層。未設定なら機体ルートは回しません。")]
    public Transform bodyUpAimRoot;
    [Tooltip("LockBodyDownTargetを適用する表示階層。未設定なら機体ルートは回しません。")]
    public Transform bodyDownAimRoot;
    [Tooltip("LockArm1Targetを適用する表示階層。未設定なら機体ルートは回しません。")]
    public Transform arm1AimRoot;
    [Tooltip("LockArm2Targetを適用する表示階層。未設定なら機体ルートは回しません。")]
    public Transform arm2AimRoot;

    [Header("Original SPT Status")]
    [Min(1f)]
    [Tooltip("Script.sptのHPが未読または0以下の場合に使う自機HP。")]
    public float fallbackMaximumHP = 1000f;
    [Min(1f)]
    [Tooltip("旧Inspector互換名。原作ではScript.sptのGeneratorが移動用ゲージ最大値(+0xD1C)になります。")]
    public float fallbackMaximumEnergy = 1000f;
    [Min(1f)]
    [Tooltip("Script.sptのEnergyが未読または0以下の場合に使う補助エネルギー最大値(+0xD2C)。")]
    public float fallbackMaximumAuxiliaryEnergy = 1000f;
    [Min(0f)]
    public float riseEnergyPerTick = 5f;
    [Min(0f)]
    public float boostEnergyPerTick = 5f;
    [Min(0f)]
    public float stepEnergyPerTick = 4f;
    [Min(0f)]
    public float airRiseEnergyCost = 80f;
    [Min(0f)]
    public float groundedEnergyRecoveryPerTick = 24f;

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
    [Tooltip("旧Inspector互換用。type 57は原作WEAPONPOINT掃引判定を使用するため、この距離値は命中判定に使いません。")]
    public float meleeRange = 3f;
    public float projectileRadius = 0.25f;
    [Min(1)]
    [Tooltip("原作FUN_004d9dd0が格闘誘導から攻撃へ移る前に待つ最小tick数。")]
    public int meleeApproachMinimumTicks = 6;
    [Min(0f)]
    [Tooltip("原作FUN_004d9dd0が格闘誘導130から136へ移る中心間距離。")]
    public float meleeApproachDistance = 3.5f;
    [Min(0f)]
    public float meleeApproachEnergyPerTick = 5f;

    [Header("Burner Preview")]
    public bool useConeBurnerEffects = true;
    [Tooltip("原作テクスチャ登録表の burner.png をバーナー表示へ使用します。未登録時は従来の単色Coneへフォールバックします。")]
    public bool useOriginalBurnerTexture = true;
    [Tooltip("GUIDEテクスチャ表で burner.png に対応するScript Texture ID。")]
    public int originalBurnerTextureId = 8;
    public float burnerLengthMultiplier = 1f;
    [Range(0.02f, 1f)]
    public float burnerRadiusRatio = 0.25f;
    public float burnerFadeSpeed = 18f;
    public Color burnerConeColor = new Color(0.35f, 0.85f, 1f, 0.65f);

    [Header("RunProc Wind Preview (Unity approximation)")]
    [Min(0f)]
    [Tooltip("原作の描画寿命は未確定です。type 53/54のUnity表示Adapterだけに使います。")]
    public float windEffectLifeSeconds = 0.3f;
    [Min(0f)]
    [Tooltip("type 54リングのUnity表示Adapter用拡大速度です。原作の描画式ではありません。")]
    public float windRingExpansionPerSecond = 3f;
    public Color windEffectColor = new Color(0.8f, 0.92f, 1f, 0.55f);

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
    [Tooltip("Script.sptのHPから初期化した自機HP。")]
    public float currentHP;
    public float maximumHP;
    [Tooltip("旧Inspector互換名。Script.sptのGeneratorから初期化するブースト／ジャンプ／ステップ用ゲージ。")]
    public float currentEnergy;
    public float maximumEnergy;
    [Tooltip("Script.sptのEnergyから初期化する原作の別エネルギーゲージ(+0xD2C/+0xD30)。")]
    public float currentAuxiliaryEnergy;
    public float maximumAuxiliaryEnergy;
    public int configuredScore;
    public int configuredRestBody;
    public bool targetLockActive;
    public float activeTargetDistance;
    public string currentAnimationName;
    public string heldWeapon = "GUN";
    public TestPlayStateTable state = new TestPlayStateTable();
    public TestPlayAttackProfile attackProfile = new TestPlayAttackProfile();

    public event Action<TestPlayRuntimeEvent> RuntimeEventRaised;
    public event Action<TestPlayPresentationEvent> PresentationEventRaised;

    [Header("Debug Logging")]
    public bool logMotionDebug;
    public bool logMotionAssignments = true;
    [Min(1)]
    public int motionDebugIntervalTicks = 10;

    TestPlayScriptVM vm;
    animation currentAnimation;
    animation currentScriptAnimation;
    TestPlayActionSelection currentActionSelection;
    TestPlayMotionStep lastMotionStep;
    int scriptTick;
    bool initFired;
    bool executeScriptRepeatedly;
    int scriptRepeatInterval;
    int scriptRepeatCounter;
    bool abortScriptExecution;
    bool animeLoop;
    bool animationPoseHeldAtEnd;
    bool moveLocked;
    int shieldGuard;
    bool gvEnable;
    int attackFlag;
    int hitStopTicks;
    int meleeGuardFeedbackTicks;
    int swordCancelAction = -1;
    float shotTurnAng;
    float turnMoveAng;
    float camEffect;
    float vFMulti = 1f;
    Vector3 moveCommand;
    float scriptedMoveRetention = 1f;
    Vector3 forceCommand;
    Vector3 velocity;
    Vector3 pendingDrivenHorizontalVelocity;
    bool hasPendingDrivenHorizontalVelocity;
    bool riseKeyHeld;
    bool previousRiseKeyHeld;
    int lastRiseTapTick = int.MinValue;
    bool riseSequenceActive;
    bool landingSequenceActive;
    int groundRecoveryElapsedTicks;
    int groundRecoveryDurationTicks;
    bool groundRecoveryCompletedThisTick;
    bool deterministicGroundPlaneEnabled;
    bool stepSequenceActive;
    bool previousAirborneFlag;
    bool boostFromRiseActive;
    bool boostMotionActive;
    bool moveAnimationActive;
    int actionTick;
    float stepMovedDistance;
    int previousDirectionInput;
    int lastDirectionTap;
    int stepDirection;
    int lastDirectionTapTick = int.MinValue;
    int activeStepInputDirection;
    Vector3 stepReferenceForward;
    Vector3 stepMoveHeading;
    Vector3 stepFacingHeading;
    bool hasStepRuntimeState;
    bool stepRecoveryActive;
    int stepRecoveryDirection;
    Vector3 inputMoveReferenceForward;
    Vector3 inputMoveHeading;
    bool hasInputMoveReference;
    bool hasInputMoveHeading;
    Vector3 boostReferenceForward;
    Vector3 boostMoveHeading;
    bool hasBoostReference;
    bool hasBoostMoveHeading;
    readonly Dictionary<int, float> burnerRequestedOutputs = new Dictionary<int, float>();
    readonly HashSet<int> validBurnerIds = new HashSet<int>();
    readonly Dictionary<int, TestPlayBurnerCone> burnerCones = new Dictionary<int, TestPlayBurnerCone>();
    readonly List<GameObject> spawnedTransientObjects = new List<GameObject>();
    static readonly Vector3[] DeterministicWindLineOffsets =
    {
        new Vector3(-1.2f, 0.45f, -0.75f),
        new Vector3(0.9f, -0.6f, 0.3f),
        new Vector3(-0.3f, 1.2f, 0.9f),
        new Vector3(1.35f, 0.15f, -1.05f),
        new Vector3(-0.75f, -1.05f, 1.35f),
        new Vector3(0.45f, 0.75f, -0.15f),
        new Vector3(0.15f, -0.3f, 0.6f)
    };
    TestPlayPosePart[] transitionFromPose;
    int transitionTick;
    int transitionTickTotal;
    CharacterController groundingController;
    CollisionFlags groundingCollisionFlags;
    float simulationAccumulator;
    int sampledDirectionInput;
    int latchedDirectionInput;
    bool sampledRiseKeyHeld;
    bool sampledShotKeyHeld;
    bool sampledMeleeKeyHeld;
    bool sampledGuardKeyHeld;
    bool sampledLockKeyHeld;
    bool sampledSpecial1KeyHeld;
    bool sampledSpecial2KeyHeld;
    bool sampledSpecial3KeyHeld;
    bool previousShotKeyHeld;
    bool previousMeleeKeyHeld;
    bool shotKeyPressedThisTick;
    bool meleeKeyPressedThisTick;
    bool latchedRiseKeyPress;
    bool latchedShotKeyPress;
    bool latchedMeleeKeyPress;
    bool latchedGuardKeyPress;
    bool latchedLockKeyPress;
    bool latchedSpecial1KeyPress;
    bool latchedSpecial2KeyPress;
    bool latchedSpecial3KeyPress;
    bool previousLockKeyHeld;
    bool bodyUpAimRequested;
    bool bodyDownAimRequested;
    bool arm1AimRequested;
    bool arm2AimRequested;
    int lastSyncedMovementEnergyInt;
    float lastSyncedMovementEnergyFloat;
    int lastSyncedAuxiliaryEnergyInt;
    float lastSyncedAuxiliaryEnergyFloat;
    readonly int[] attackCooldownTicks = new int[5];
    readonly List<TestPlayMeleeAttackState> activeMeleeAttacks = new List<TestPlayMeleeAttackState>();
    readonly List<ActiveSwordBeam> activeSwordBeams = new List<ActiveSwordBeam>();
    readonly List<ActiveThunderEffect> activeThunderEffects = new List<ActiveThunderEffect>();
    ActiveSwordBeam managedSwordBeam;
    bool attackSequenceActive;
    bool meleeApproachActive;
    bool meleeComboInputPending;
    readonly List<TestPlayCombatTraceEvent> currentCombatTraceEvents = new List<TestPlayCombatTraceEvent>();
    readonly List<TestPlayCombatTraceEvent> pendingCombatTraceEvents = new List<TestPlayCombatTraceEvent>();
    bool simulatingCombatTick;
    readonly List<TestPlayPresentationEvent> currentPresentationTraceEvents = new List<TestPlayPresentationEvent>();
    readonly List<TestPlayPresentationEvent> pendingPresentationTraceEvents = new List<TestPlayPresentationEvent>();
    bool simulatingPresentationTick;

    struct TestPlayPosePart
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    sealed class ActiveSwordBeam
    {
        public TestPlaySwordBeamParameters parameters;
        public Transform anchor;
        public GameObject visualRoot;
        public TestPlaySwordBeamEffect visual;
        public float currentLength;
        public int remainingTicks;
    }

    sealed class ActiveThunderEffect
    {
        public TestPlayThunderEffectParameters parameters;
        public GameObject visualRoot;
        public TestPlayThunderEffect visual;
        public float currentWidth;
        public int remainingActiveTicks;
        public float travelDistance;
        public int elapsedTicks;
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
        vm.shouldStopExecution = () => abortScriptExecution;

        if (presentationRuntime == null)
            presentationRuntime = GetComponent<TestPlayPresentationRuntime>();
        if (presentationRuntime != null)
            presentationRuntime.Bind(this);
        if (hudRuntime == null)
            hudRuntime = GetComponent<TestPlayHudRuntime>();
        if (hudRuntime != null)
            hudRuntime.Bind(this);
    }

    void Start()
    {
        if (startOnPlay)
            StartTestPlay();
    }

    void Update()
    {
        if (!playModeActive)
            return;

        SampleInputFrame();

        if (robo == null || robo.ani == null || robo.ani.animations == null || robo.ani.animations.Count == 0)
        {
            simulationAccumulator = 0f;
            return;
        }

        float tickDeltaTime = GetOriginalTickDeltaTime();
        simulationAccumulator += Mathf.Max(0f, Time.deltaTime);
        int processedTicks = 0;
        int catchUpLimit = Mathf.Max(1, maximumCatchUpTicks);
        while (simulationAccumulator + 0.000001f >= tickDeltaTime && processedTicks < catchUpLimit)
        {
            simulationAccumulator -= tickDeltaTime;
            SimulateOriginalTick();
            processedTicks++;
        }
    }

    void SimulateOriginalTick()
    {
        tick++;
        BeginCombatTraceTick();
        BeginPresentationTraceTick();
        groundRecoveryCompletedThisTick = false;
        bool animationHitStopped = UpdateOriginalCombatTimers();
        UpdateOriginalAttackCooldowns();
        UpdateInputState();
        UpdateTargetLock();
        UpdateTargetState();
        UpdateOriginalMovementEnergy();
        UpdateActionFromInput();
        if (!animationHitStopped)
            TickAnimation();
        TickActiveMeleeAttacks();
        TickActiveSwordBeams();
        TickActiveThunderEffects();
        ApplyQueuedAimCommands();
        ApplyRootMotion();
        ConsumeLatchedInput();
        simulatingCombatTick = false;
        simulatingPresentationTick = false;
    }

    bool UpdateOriginalCombatTimers()
    {
        bool animationHitStopped = hitStopTicks > 0;
        hitStopTicks = TestPlayCombatCore.TickPositiveTimer(hitStopTicks);
        meleeGuardFeedbackTicks = TestPlayCombatCore.TickPositiveTimer(meleeGuardFeedbackTicks);
        if (state != null)
        {
            state.SetInt(157, TestPlayCombatCore.TickPositiveTimer(state.GetInt(157)));
            state.SetInt(158, TestPlayCombatCore.TickPositiveTimer(state.GetInt(158)));
        }
        if (target != null)
            target.SimulateOriginalCombatTimerTick();
        return animationHitStopped;
    }

    public void StartTestPlay()
    {
        if (robo == null)
            robo = FindObjectOfType<RoboStructure>();
        if (target == null)
            target = FindObjectOfType<TestPlayTargetDummy>();
        if (sptSource == null)
            sptSource = FindObjectOfType<UI_SPT>();
        EnsureSptRuntimeDataLoaded();
        if (presentationRuntime == null)
            presentationRuntime = GetComponent<TestPlayPresentationRuntime>();
        if (presentationRuntime == null)
            presentationRuntime = gameObject.AddComponent<TestPlayPresentationRuntime>();
        presentationRuntime.Bind(this);
        presentationRuntime.AttachEmitters(robo != null && robo.root != null ? robo.root.transform : null);

        EnsureGroundingController();
        state.ResetDefaults();
        ResetRuntimeFlags();
        ResetInputSamplingState();
        InitializeOriginalSptStatus();
        SetHeldWeapon("GUN");
        playModeActive = true;
        EnsureHudRuntime();
        hudRuntime.ShowHud();
        ChangeAnimation(idleAction);
        EnsureCameraController();
        cameraController?.EnterTestPlayCamera(this);
    }

    void EnsureSptRuntimeDataLoaded()
    {
        if (sptSource != null && sptSource.LastSptData == null)
            sptSource.TryLoadSptRuntimeData();
    }

    public void StopTestPlay()
    {
        playModeActive = false;
        cameraController?.ExitTestPlayCamera();
        velocity = Vector3.zero;
        pendingDrivenHorizontalVelocity = Vector3.zero;
        hasPendingDrivenHorizontalVelocity = false;
        ClearStepRecovery();
        simulationAccumulator = 0f;
        ResetInputSamplingState();
        verticalFallSpeed = 0f;
        moveCommand = Vector3.zero;
        forceCommand = Vector3.zero;
        StopAllBurnerEffects();
        presentationRuntime?.StopPresentation();
        hudRuntime?.HideHud();
        activeMeleeAttacks.Clear();
        DestroyTransientObjects();
    }

    void EnsureHudRuntime()
    {
        if (hudRuntime == null)
            hudRuntime = GetComponent<TestPlayHudRuntime>();
        if (hudRuntime == null)
            hudRuntime = gameObject.AddComponent<TestPlayHudRuntime>();
        hudRuntime.Bind(this);
    }

    void EnsureCameraController()
    {
        if (cameraController == null)
            cameraController = FindObjectOfType<TestPlayCameraController>();
        if (cameraController == null && Camera.main != null)
            cameraController = Camera.main.gameObject.AddComponent<TestPlayCameraController>();
        if (cameraController != null)
            cameraController.Bind(this);
    }

    public void ChangeAnimation(int actionId)
    {
        ChangeAnimation(actionId, false);
    }

    void ChangeAnimation(int actionId, bool restartSameAction)
    {
        NormalizeActionIds();
        ChangeAnimation(ResolveActionSelection(actionId), restartSameAction);
    }

    void ChangeAnimation(TestPlayActionSelection selection, bool restartSameAction)
    {
        int actionId = selection.poseActionId;
        int logicalActionId = selection.logicalActionId;

        if (ShouldRedirectBoostToAirIdle(logicalActionId))
        {
            selection = ResolveActionSelection(airMoveAction);
            actionId = selection.poseActionId;
            logicalActionId = selection.logicalActionId;
            restartSameAction = false;
        }

        if (robo == null || robo.ani == null || robo.ani.animations == null)
            return;
        if (actionId < 0 || actionId >= robo.ani.animations.Count)
        {
            LogUnhandled("ChangeAnimation out of range: " + actionId);
            return;
        }
        if (selection.scriptActionId < 0 || selection.scriptActionId >= robo.ani.animations.Count)
        {
            LogUnhandled("ChangeAnimation script out of range: " + selection.scriptActionId);
            return;
        }

        if (!restartSameAction && currentAnimation != null && currentAnimationIndex == actionId)
            return;

        if (logicalActionId != airIdleAction)
            ClearStepRecovery();

        StartPoseTransition(currentAnimationIndex, actionId);

        currentActionSelection = selection;
        currentAnimationIndex = actionId;
        currentAnimation = robo.ani.animations[actionId];
        currentScriptAnimation = robo.ani.animations[selection.scriptActionId];
        currentAnimationName = currentAnimation != null ? currentAnimation.name : "";
        scriptedMoveRetention = GetScriptedMoveRetention(logicalActionId);
        SyncAniScriptExecutionChannel(actionId);
        CompileAnimationScripts(currentScriptAnimation);
        animationPoseHeldAtEnd = false;
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        actionTick = 0;
        initFired = false;
        ResetScriptRepeatState();
        animeLoop = false;
        StopAllBurnerEffects();

        if (logicalActionId == riseStartAction && !landingSequenceActive)
            riseSequenceActive = true;
        else if (logicalActionId != riseAction)
            riseSequenceActive = false;

        bool stepAction = IsStepAction(actionId);
        if (stepAction)
        {
            ClearHeldMotionState();
            EnsureStepRuntimeState(actionId);
            stepSequenceActive = true;
            stepMovedDistance = 0f;
        }
        else if (!IsGroundRecoveryAction(actionId))
        {
            stepSequenceActive = false;
            stepMovedDistance = 0f;
            ResetStepRuntimeState();
        }

        if (logicalActionId != boostAction || !boostMotionActive)
            boostMotionActive = false;

        if (!landingSequenceActive && (logicalActionId == riseStartAction || logicalActionId == riseAction || (logicalActionId == boostAction && boostMotionActive)))
            SetAirborneFlag(true);

        if (stepAction)
        {
            stepDirection = 0;
            lastDirectionTap = 0;
            lastDirectionTapTick = int.MinValue;
        }

        if (logicalActionId != moveAction)
            moveAnimationActive = false;

        if (logicalActionId == idleAction)
        {
            ClearHeldMotionState(true);
            pendingDrivenHorizontalVelocity = Vector3.zero;
            hasPendingDrivenHorizontalVelocity = false;
        }
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
            bool initInterrupted = ExecuteCurrentAnimationScript(currentScriptAnimation != null ? currentScriptAnimation.squirrelInit : "");
            if (initInterrupted || currentAnimationIndex != initAction)
                return;
        }

        actionTick++;
        if (TryFinishGroundRecoveryByWatchdog())
            return;

        if (animationPoseHeldAtEnd)
        {
            ApplyPose();
            return;
        }

        bool hasScripts = currentScriptAnimation != null &&
                          currentScriptAnimation.scripts != null &&
                          currentScriptAnimation.scripts.Count > 0;
        if (hasScripts)
        {
            scriptIndex = Mathf.Clamp(scriptIndex, 0, currentScriptAnimation.scripts.Count - 1);
            script currentScript = currentScriptAnimation.scripts[scriptIndex];

            if (IsScriptTerminator(currentScript))
            {
                FinishCurrentAnimation();
                return;
            }

            bool enteringBlock = scriptTick == 0;
            if (enteringBlock)
            {
                ResetOriginalBlockState();
                ResetScriptRepeatState();
            }

            bool executeBlock = enteringBlock || ConsumeScriptRepeatTick();
            if (executeBlock)
            {
                int actionBeforeScript = currentAnimationIndex;
                int scriptBeforeExecution = scriptIndex;
                burnerRequestedOutputs.Clear();
                bool interrupted = ExecuteCurrentAnimationScript(currentScript.squirrel);
                ApplyBurners();
                if (interrupted || currentAnimationIndex != actionBeforeScript || scriptIndex != scriptBeforeExecution)
                    return;
            }

            int length = GetRuntimeScriptLengthTicks(currentScript);
            scriptTick++;
            if (scriptTick >= length)
            {
                scriptTick = 0;
                scriptIndex++;
                ResetScriptRepeatState();
                if (scriptIndex >= currentScriptAnimation.scripts.Count || IsScriptTerminator(currentScriptAnimation.scripts[scriptIndex]))
                {
                    FinishCurrentAnimation();
                    return;
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
            if (TryTickScriptlessNormalAttack())
                return;

            if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            {
                ApplyFiniteActionPoseProgress(GetOriginalStepMaximumTicks());
            }

            if (landingSequenceActive && IsGroundRecoveryAction(currentAnimationIndex))
            {
                frameIndex++;
                int frameCount = currentAnimation.frames != null ? currentAnimation.frames.Count : 0;
                if (frameCount <= 0 || frameIndex >= frameCount)
                {
                    CompleteGroundRecoverySequence();
                    return;
                }
            }
        }

        ApplyPose();
    }

    bool ConsumeScriptRepeatTick()
    {
        if (!executeScriptRepeatedly)
            return false;

        if (scriptRepeatCounter <= 0)
        {
            scriptRepeatCounter = scriptRepeatInterval;
            return true;
        }

        scriptRepeatCounter--;
        return false;
    }

    void ResetScriptRepeatState()
    {
        executeScriptRepeatedly = false;
        scriptRepeatInterval = 0;
        scriptRepeatCounter = 0;
    }

    void ResetOriginalBlockState()
    {
        // Original numeric Move=0 preserves the current Move component. Keep
        // the dedicated ANI Move state across block/action entry; only STOP or
        // a new numeric Move command changes it.
        forceCommand = Vector3.zero;
        moveLocked = false;
        shieldGuard = 0;
        gvEnable = true;
        attackFlag = 0;
        camEffect = 0f;
        vFMulti = 1f;
        EnsureAttackProfile();
        attackProfile.Reset();
        burnerRequestedOutputs.Clear();
        bodyUpAimRequested = false;
        bodyDownAimRequested = false;
        arm1AimRequested = false;
        arm2AimRequested = false;
    }

    static bool IsScriptTerminator(script scriptBlock)
    {
        return scriptBlock.unk == 999999999;
    }

    void FinishCurrentAnimation()
    {
        if (attackSequenceActive)
        {
            FinishNormalAttackSequence();
            return;
        }

        if (landingSequenceActive && IsGroundRecoveryAction(currentAnimationIndex))
        {
            CompleteGroundRecoverySequence();
            return;
        }

        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
        {
            int elapsedStepTicks = actionTick;
            RestartCurrentAnimationLoop();
            actionTick = elapsedStepTicks;
            return;
        }

        if (IsCurrentAction(riseStartAction) && riseSequenceActive)
        {
            ApplyPendingDrivenHorizontalInertia();
            ChangeAnimation(riseAction);
            return;
        }

        if (IsCurrentAction(riseAction) && riseSequenceActive)
        {
            HoldCurrentAnimationAtLastPose();
            return;
        }

        if (IsCurrentAction(boostAction) && boostMotionActive)
        {
            if (ShouldEndOriginalStyleBoost())
                ChangeToAirMoveOrLanding();
            else
                RestartCurrentAnimationLoop();
            return;
        }

        if (animeLoop || ShouldLoopHeldAction())
            RestartCurrentAnimationLoop();
        else
            ChangeAnimation(idleAction);
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

        Transform root = robo.root.transform;
        ApplyOriginalShotSteering(root);
        if (stepSequenceActive && IsStepAction(currentAnimationIndex) && ShouldFinishOriginalStyleStep())
        {
            FinishOriginalStyleStep();
            return;
        }

        Vector3 positionBefore = root.position;
        Vector3 localMove = moveLocked ? Vector3.zero : moveCommand;
        bool usingInputMove = false;
        bool usingStepFallbackMove = false;
        Vector3 worldMove;
        if (!moveLocked && TryGetOriginalStyleStepWorldMove(root, localMove, out worldMove, out usingStepFallbackMove))
        {
        }
        else if (!moveLocked && TryGetOriginalStyleBoostWorldMove(root, localMove, out worldMove))
        {
            usingInputMove = true;
        }
        else if (!moveLocked && TryGetOriginalStyleInputWorldMove(root, localMove, out worldMove))
        {
            usingInputMove = true;
        }
        else
        {
            ResetInputMoveHeadingIfInactive();
            if (!moveLocked && localMove.sqrMagnitude < 0.000001f)
            {
                usingInputMove = TryGetInputMoveVector(out localMove);
            }
            worldMove = root.right * localMove.x + Vector3.up * localMove.y + root.forward * localMove.z;
        }
        ApplyOriginalRiseSteering(root);
        ApplyOriginalAirIdleVerticalBrake();
        IntegrateOriginalForceVelocity();
        Vector3 scriptedVelocityBeforeRetention = worldMove;
        if (!CaptureDrivenHorizontalVelocity(scriptedVelocityBeforeRetention))
            DecayPendingDrivenHorizontalVelocity();
        float unitScale = Mathf.Max(0f, aniUnitsToUnityScale);
        lastMotionStep = TestPlayMotionCore.ComposeDisplacement(
            lastMotionStep, scriptedVelocityBeforeRetention, scriptedMoveRetention, unitScale);
        Vector3 scriptedVelocityAfterRetention = lastMotionStep.scriptedVelocityAfterRetention;
        Vector3 scriptedMove = scriptedVelocityAfterRetention * unitScale;
        Vector3 requestedMove = (scriptedVelocityAfterRetention + velocity) * unitScale;
        Vector3 appliedMove = MoveRootWithColliderGrounding(root, requestedMove);

        // FUN_004cd840 writes the retained Move back into the persistent ANI
        // state before the next 60 Hz tick.  Keep this state separate from
        // Force/pending jump inertia: numeric Move values decay here, while
        // STOP has already zeroed only the requested axis in HandleCommand.
        scriptedMoveRetention = Mathf.Clamp01(scriptedMoveRetention);
        moveCommand *= scriptedMoveRetention;

        float debugStepMovedDistance = stepMovedDistance;
        float debugStepTargetDistance = GetActiveStepMoveTargetDistance();
        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
        {
            stepMovedDistance += scriptedMove.magnitude;
            debugStepMovedDistance = stepMovedDistance;
        }

        LogMotionRootDebug(root, positionBefore, localMove, worldMove, scriptedMove, appliedMove, usingInputMove, usingStepFallbackMove, debugStepMovedDistance, debugStepTargetDistance);
    }

    float GetScriptedMoveRetention(int logicalActionId)
    {
        if (logicalActionId == idleAction)
            return Mathf.Clamp01(idleMoveRetention);
        if (logicalActionId == moveAction)
            return Mathf.Clamp01(moveActionRetention);
        if (logicalActionId == airIdleAction)
            return Mathf.Clamp01(airIdleMoveRetention);
        return Mathf.Clamp01(defaultMoveRetention);
    }

    void ApplyOriginalAirIdleVerticalBrake()
    {
        // FUN_004d68e0 adjusts +0xA80 directly before FUN_004cd840. Keep this
        // separate from ANI Force so the trace reports the action-8 command
        // state (zero) at the common integration boundary.
        if (!IsCurrentAction(airIdleAction) ||
            actionTick >= Mathf.Max(1, airIdleVerticalBrakeTicks) ||
            currentEnergy <= 0f ||
            velocity.y >= Mathf.Max(0f, airIdleVerticalBrakeVelocityThreshold))
            return;

        velocity.y += Mathf.Max(0f, airIdleVerticalBrakePerTick);
    }

    void IntegrateOriginalForceVelocity()
    {
        // FUN_004d5b60 / FUN_004d5ec0 clamp the current rise speed before the
        // ANI Force value is added by FUN_004cd840. Force is a per-tick velocity
        // delta in the original, not a per-second acceleration.
        bool limitUpwardVelocity = IsCurrentAction(riseAction) && riseSequenceActive;
        Vector3 appliedForceCommand = forceCommand;
        if (IsCurrentAction(airMoveAction) && appliedForceCommand.y > 0f)
        {
            // Action 4 is used as the directional lower-body/presentation ANI while
            // the original airborne callback remains action 8. Applying action 4's
            // positive Y Force as the single Unity physics action lets a held
            // direction climb forever without entering the energy-gated rise/boost
            // paths. Preserve its horizontal Move, BURNER, and non-positive Force,
            // but reserve physical lift for actions 7 and 22.
            appliedForceCommand.y = 0f;
        }
        lastMotionStep = TestPlayMotionCore.Integrate(new TestPlayMotionInput
        {
            velocity = velocity,
            forcePerTick = appliedForceCommand,
            limitUpwardVelocity = limitUpwardVelocity,
            upwardVelocityLimit = riseVerticalVelocityLimit,
            gravityEnabled = gvEnable,
            gravityPerTick = originalGravityPerTick,
            terminalFallSpeed = originalTerminalFallSpeed,
            airborne = airborneFlag,
            airborneHorizontalRetention = airborneHorizontalForceRetention,
            groundedHorizontalRetention = groundedHorizontalForceRetention,
            velocityMultiplier = vFMulti,
            aniUnitsToUnityScale = aniUnitsToUnityScale
        });
        velocity = lastMotionStep.velocityAfterMultiplier;
    }

    bool CaptureDrivenHorizontalVelocity(Vector3 scriptedVelocity)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(scriptedVelocity, Vector3.up);
        if (horizontal.sqrMagnitude < 0.000001f)
            return false;

        if (!IsCurrentAction(moveAction) &&
            !IsCurrentAction(airMoveAction) &&
            !IsCurrentAction(boostAction) &&
            !IsStepAction(currentAnimationIndex))
            return false;

        pendingDrivenHorizontalVelocity = horizontal;
        hasPendingDrivenHorizontalVelocity = true;
        return true;
    }

    void DecayPendingDrivenHorizontalVelocity()
    {
        if (!hasPendingDrivenHorizontalVelocity)
            return;

        float retention = airborneFlag
            ? Mathf.Clamp01(airborneHorizontalForceRetention)
            : Mathf.Clamp01(groundedHorizontalForceRetention);
        pendingDrivenHorizontalVelocity *= retention;
        if (pendingDrivenHorizontalVelocity.sqrMagnitude < 0.000001f)
        {
            pendingDrivenHorizontalVelocity = Vector3.zero;
            hasPendingDrivenHorizontalVelocity = false;
        }
    }

    void ApplyPendingDrivenHorizontalInertia()
    {
        if (!hasPendingDrivenHorizontalVelocity)
            return;

        // The decompile confirms that numeric Force=(0,y,0) preserves existing
        // horizontal Force velocity. Converting the preceding Move velocity into
        // that state at a locomotion-to-rise boundary is an observed-behaviour
        // approximation because the exact hand-off site is not identified yet.
        velocity.x = pendingDrivenHorizontalVelocity.x;
        velocity.z = pendingDrivenHorizontalVelocity.z;
        pendingDrivenHorizontalVelocity = Vector3.zero;
        hasPendingDrivenHorizontalVelocity = false;
    }

    Vector3 MoveRootWithColliderGrounding(Transform root, Vector3 requestedMove)
    {
        bool groundRecoveryOwnsContact =
            (landingSequenceActive && IsGroundRecoveryAction(currentAnimationIndex)) ||
            groundRecoveryCompletedThisTick;
        if (!useColliderGrounding || !EnsureGroundingController())
        {
            bool fallbackForceAirborne = ShouldForceAirborneByAction();
            if (fallbackForceAirborne)
                SetAirborneFlag(true);
            else if (groundRecoveryOwnsContact)
                SetAirborneFlag(false);

            Vector3 appliedMove = requestedMove;
            bool logicalGroundContact =
                deterministicGroundPlaneEnabled && !airborneFlag && !fallbackForceAirborne;
            if (logicalGroundContact)
            {
                appliedMove.y = 0f;
                velocity.y = 0f;
            }
            root.position += appliedMove;
            groundedFlag = logicalGroundContact || groundRecoveryOwnsContact;
            groundingCollisionFlags = CollisionFlags.None;
            verticalFallSpeed = velocity.y * Mathf.Max(0f, aniUnitsToUnityScale) * Mathf.Max(1f, originalTickRate);
            return appliedMove;
        }

        bool forceAirborne = ShouldForceAirborneByAction();
        Vector3 positionBefore = root.position;
        groundingCollisionFlags = groundingController.Move(requestedMove);
        bool controllerGrounded = groundingController.isGrounded || (groundingCollisionFlags & CollisionFlags.Below) != 0;
        if ((groundingCollisionFlags & CollisionFlags.Above) != 0)
        {
            if (velocity.y > 0f)
                velocity.y = 0f;
        }
        if (controllerGrounded && !forceAirborne)
            velocity.y = 0f;

        verticalFallSpeed = velocity.y * Mathf.Max(0f, aniUnitsToUnityScale) * Mathf.Max(1f, originalTickRate);

        // Actions 5/6 are selected only by the original grounded branch. Keep
        // that logical contact authoritative through their completion tick;
        // otherwise switching to idle before this late physics update can turn
        // the character airborne again for one frame.
        groundedFlag = controllerGrounded || groundRecoveryOwnsContact;
        if (forceAirborne)
            SetAirborneFlag(true);
        else if (groundRecoveryOwnsContact)
            SetAirborneFlag(false);
        else
            SetAirborneFlag(!controllerGrounded);

        Vector3 actualMove = root.position - positionBefore;
        LogGroundingRootDebug(forceAirborne, gvEnable, requestedMove, requestedMove, actualMove);
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

    void SampleInputFrame()
    {
        int horizontal = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                         (Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
        int vertical = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                       (Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
        int direction = EncodeDirectionInput(horizontal, vertical);
        if (direction != 0 && direction != sampledDirectionInput)
            latchedDirectionInput = direction;
        sampledDirectionInput = direction;

        sampledRiseKeyHeld = Input.GetKey(KeyCode.Z);
        sampledShotKeyHeld = Input.GetKey(KeyCode.X);
        sampledMeleeKeyHeld = Input.GetKey(KeyCode.C);
        sampledGuardKeyHeld = Input.GetKey(KeyCode.V);
        sampledLockKeyHeld = Input.GetKey(KeyCode.S);
        sampledSpecial1KeyHeld = Input.GetKey(KeyCode.A);
        sampledSpecial2KeyHeld = Input.GetKey(KeyCode.D);
        sampledSpecial3KeyHeld = Input.GetKey(KeyCode.F);

        latchedRiseKeyPress |= Input.GetKeyDown(KeyCode.Z);
        latchedShotKeyPress |= Input.GetKeyDown(KeyCode.X);
        latchedMeleeKeyPress |= Input.GetKeyDown(KeyCode.C);
        latchedGuardKeyPress |= Input.GetKeyDown(KeyCode.V);
        latchedLockKeyPress |= Input.GetKeyDown(KeyCode.S);
        latchedSpecial1KeyPress |= Input.GetKeyDown(KeyCode.A);
        latchedSpecial2KeyPress |= Input.GetKeyDown(KeyCode.D);
        latchedSpecial3KeyPress |= Input.GetKeyDown(KeyCode.F);
    }

    void UpdateInputState()
    {
        int dir = sampledDirectionInput != 0 ? sampledDirectionInput : latchedDirectionInput;
        UpdateDirectionTapState(dir);

        riseKeyHeld = sampledRiseKeyHeld || latchedRiseKeyPress;
        bool riseKeyPressed = riseKeyHeld && !previousRiseKeyHeld;
        if (riseKeyPressed)
        {
            bool boostContext = airborneFlag || riseSequenceActive ||
                                IsCurrentAction(riseStartAction) ||
                                IsCurrentAction(riseAction) ||
                                IsCurrentAction(airMoveAction) ||
                                IsCurrentAction(airIdleAction) ||
                                IsCurrentAction(boostAction);
            boostFromRiseActive = boostContext && IsTickWithinWindow(
                lastRiseTapTick,
                tick,
                SecondsToOriginalTicks(doubleTapBoostSeconds));
            lastRiseTapTick = tick;
        }
        if (!riseKeyHeld)
            boostFromRiseActive = false;
        previousRiseKeyHeld = riseKeyHeld;

        bool shotKeyHeld = sampledShotKeyHeld || latchedShotKeyPress;
        bool meleeKeyHeld = sampledMeleeKeyHeld || latchedMeleeKeyPress;
        shotKeyPressedThisTick = shotKeyHeld && !previousShotKeyHeld;
        meleeKeyPressedThisTick = meleeKeyHeld && !previousMeleeKeyHeld;
        previousShotKeyHeld = shotKeyHeld;
        previousMeleeKeyHeld = meleeKeyHeld;

        state.SetInt(190, dir);
        state.SetInt(191, boostFromRiseActive ? 1 : 0);
        state.SetInt(192, shotKeyHeld ? 1 : 0);
        state.SetInt(193, meleeKeyHeld ? 1 : 0);
        state.SetInt(194, sampledGuardKeyHeld || latchedGuardKeyPress ? 1 : 0);
        state.SetInt(195, sampledLockKeyHeld || latchedLockKeyPress ? 1 : 0);
        state.SetInt(196, sampledSpecial1KeyHeld || latchedSpecial1KeyPress ? 1 : 0);
        state.SetInt(197, sampledSpecial2KeyHeld || latchedSpecial2KeyPress ? 1 : 0);
        state.SetInt(198, sampledSpecial3KeyHeld || latchedSpecial3KeyPress ? 1 : 0);
        state.SetInt(199, riseKeyHeld ? 1 : 0);
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
                             IsTickWithinWindow(lastDirectionTapTick, tick, SecondsToOriginalTicks(doubleTapStepSeconds));
            if (doubleTap)
            {
                stepDirection = dir;
                lastDirectionTap = 0;
                lastDirectionTapTick = int.MinValue;
            }
            else
            {
                stepDirection = 0;
                lastDirectionTap = dir;
                lastDirectionTapTick = tick;
            }
        }

        previousDirectionInput = dir;
    }

    void ConsumeLatchedInput()
    {
        latchedDirectionInput = 0;
        latchedRiseKeyPress = false;
        latchedShotKeyPress = false;
        latchedMeleeKeyPress = false;
        latchedGuardKeyPress = false;
        latchedLockKeyPress = false;
        latchedSpecial1KeyPress = false;
        latchedSpecial2KeyPress = false;
        latchedSpecial3KeyPress = false;
    }

    void ResetInputSamplingState()
    {
        simulationAccumulator = 0f;
        sampledDirectionInput = 0;
        sampledRiseKeyHeld = false;
        sampledShotKeyHeld = false;
        sampledMeleeKeyHeld = false;
        sampledGuardKeyHeld = false;
        sampledLockKeyHeld = false;
        sampledSpecial1KeyHeld = false;
        sampledSpecial2KeyHeld = false;
        sampledSpecial3KeyHeld = false;
        previousShotKeyHeld = false;
        previousMeleeKeyHeld = false;
        shotKeyPressedThisTick = false;
        meleeKeyPressedThisTick = false;
        previousLockKeyHeld = false;
        ConsumeLatchedInput();
    }

    public float GetOriginalTickDeltaTime()
    {
        return 1f / Mathf.Max(1f, originalTickRate);
    }

    public int SecondsToOriginalTicks(float seconds)
    {
        return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, seconds) * Mathf.Max(1f, originalTickRate)));
    }

    static bool IsTickWithinWindow(int previousTapTick, int currentTapTick, int windowTicks)
    {
        return previousTapTick != int.MinValue && currentTapTick >= previousTapTick &&
               currentTapTick - previousTapTick <= Mathf.Max(0, windowTicks);
    }

    void UpdateTargetState()
    {
        Transform activeTarget = GetLockedTargetTransform();
        if (activeTarget == null || lockedTarget == null || robo == null || robo.root == null)
        {
            activeTargetDistance = 0f;
            state.SetFloat(99, 0f);
            state.SetInt(154, 0);
            return;
        }

        activeTargetDistance = Vector3.Distance(robo.root.transform.position, activeTarget.position);
        state.SetFloat(99, activeTargetDistance);
        state.SetInt(154, lockedTarget.stateId);
    }

    void UpdateTargetLock()
    {
        bool lockKeyHeld = state.GetInt(195) != 0;
        bool lockPressed = lockKeyHeld && !previousLockKeyHeld;
        previousLockKeyHeld = lockKeyHeld;

        if (!requireLockInput)
        {
            if (!IsTargetLockValid(lockedTarget))
                TryAcquireTargetLock();
            targetLockActive = IsTargetLockValid(lockedTarget);
            return;
        }

        if (lockPressed)
        {
            if (targetLockActive)
                ClearTargetLock();
            else
                TryAcquireTargetLock();
        }

        if (targetLockActive && !IsTargetLockValid(lockedTarget))
            ClearTargetLock();
    }

    public bool TryAcquireTargetLock()
    {
        if (!IsTargetLockValid(target))
        {
            ClearTargetLock();
            return false;
        }

        lockedTarget = target;
        targetLockActive = true;
        activeTargetDistance = Vector3.Distance(robo.root.transform.position, lockedTarget.transform.position);
        return true;
    }

    public void ClearTargetLock()
    {
        lockedTarget = null;
        targetLockActive = false;
        activeTargetDistance = 0f;
    }

    public Transform GetLockedTargetTransform()
    {
        if (!targetLockActive || !IsTargetLockValid(lockedTarget))
            return null;
        return lockedTarget.transform;
    }

    public float GetConfiguredLockDistance()
    {
        if (sptSource != null && sptSource.LastSptData != null && sptSource.LastSptData.LockDist > 0f)
            return sptSource.LastSptData.LockDist;
        return Mathf.Max(0f, fallbackLockDistance);
    }

    bool IsTargetLockValid(TestPlayTargetDummy candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive ||
            robo == null || robo.root == null)
            return false;

        float lockDistance = GetConfiguredLockDistance();
        return lockDistance <= 0f ||
               Vector3.Distance(robo.root.transform.position, candidate.transform.position) <= lockDistance;
    }

    void UpdateActionFromInput()
    {
        if (ShouldForceAirborneByAction())
            SetAirborneFlag(true);

        if (IsAirborneLocomotionAction(currentAnimationIndex) && previousAirborneFlag && !airborneFlag)
        {
            StartLandingSequence();
            return;
        }
        previousAirborneFlag = airborneFlag;

        if (landingSequenceActive)
            return;

        if (TryUpdateNormalAttackSequence())
            return;

        int oneShotAction = GetOneShotActionFromInput();
        bool normalAttackInput = IsNormalAttackInputAction(oneShotAction);
        if (oneShotAction >= 0 &&
            (normalAttackInput ? CanAcceptNormalAttackInput(oneShotAction) : CanStartActionFromCurrent() || IsHeldAction(currentAnimationIndex)))
        {
            if (normalAttackInput)
                StartNormalAttackAction(oneShotAction);
            else
                ChangeAnimation(oneShotAction);
            return;
        }

        if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            return;

        UpdateStepRecoveryInputGate();
        int heldAction = GetHeldActionFromInput();

        if (IsCurrentAction(riseStartAction) && riseSequenceActive)
        {
            // FUN_004d59f0 always lets action 3 finish. A short tap therefore
            // still enters action 7 before release is evaluated.
            return;
        }

        if (IsCurrentAction(riseAction) && riseSequenceActive)
        {
            if (IsBoostInputHeld() && HasMovementEnergy())
                StartBoostAction();
            else if (ShouldEndOriginalStyleRise())
                ChangeToAirMoveOrLanding();
            return;
        }

        if (IsCurrentAction(boostAction) && boostMotionActive)
        {
            if (ShouldEndOriginalStyleBoost())
                ChangeToAirMoveOrLanding();
            return;
        }

        if (IsAirborneLocomotionAction(currentAnimationIndex))
        {
            if (!airborneFlag)
            {
                StartLandingSequence();
                return;
            }

            if (IsBoostInputHeld() && HasMovementEnergy())
                StartBoostAction();
            else if (riseKeyHeld && heldAction == riseAction)
            {
                // FUN_004d68e0 accepts the 80-unit re-rise only after c38 > 10.
                // Keeping Z held during the first idle ticks must not immediately
                // restart action 7 (or it makes a jump much longer than the original).
                if (CanStartOriginalAirRise() && TrySpendMovementEnergy(airRiseEnergyCost))
                    StartAirRiseAction();
                return;
            }
            else if (heldAction >= 0 && GetLogicalActionId(currentAnimationIndex) != heldAction)
                ChangeAnimation(heldAction);
            return;
        }

        if (CanStartActionFromCurrent())
        {
            if (heldAction == moveAction && IsMoveInputHeld())
            {
                if (!IsCurrentAction(moveAction) || currentAnimation == null)
                    ChangeAnimation(moveAction);

                if (!moveAnimationActive)
                {
                    moveAnimationActive = true;
                    RestartCurrentAnimationLoop();
                }
                return;
            }

            moveAnimationActive = false;
            if (IsCurrentAction(moveAction) && heldAction < 0)
                ChangeAnimation(idleAction);
            if (heldAction >= 0)
                ChangeAnimation(heldAction);
            return;
        }

        if (IsHeldAction(currentAnimationIndex))
        {
            if (heldAction == GetLogicalActionId(currentAnimationIndex))
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
        if (shotKeyPressedThisTick && attackCooldownTicks[0] <= 0) return ResolveShotInputAction();
        if (meleeKeyPressedThisTick && attackCooldownTicks[1] <= 0)
            return ResolveMeleeInputAction(state != null ? state.GetInt(190) : 0);
        return -1;
    }

    int ResolveShotInputAction()
    {
        int baseActionId = TestPlayCombatCore.ResolveShotInputAction(
            IsCurrentAction(boostAction),
            actionTick,
            HasUsableAction(boostShotAction),
            IsSwordEquipped(),
            HasUsableAction(switchToGunAction),
            shotAction,
            boostShotAction,
            switchToGunAction);
        if (baseActionId != shotAction && baseActionId != boostShotAction)
            return baseActionId;

        Transform lockedTargetTransform = GetLockedTargetTransform();
        if (robo == null || robo.root == null || lockedTargetTransform == null)
            return baseActionId;

        Transform root = robo.root.transform;
        return TestPlayCombatCore.ResolveTargetRelativeShotAction(
            baseActionId,
            root.forward,
            lockedTargetTransform.position - root.position,
            HasUsableAction).selectedActionId;
    }

    int ResolveMeleeInputAction(int direction)
    {
        return TestPlayCombatCore.ResolveMeleeInputAction(
            IsSwordEquipped(),
            HasUsableAction(switchToSwordAction),
            direction,
            switchToSwordAction,
            meleeAction,
            HasUsableAction(meleeAction),
            neutralMeleeAction,
            HasUsableAction(neutralMeleeAction),
            leftMeleeAction,
            HasUsableAction(leftMeleeAction),
            rightMeleeAction,
            HasUsableAction(rightMeleeAction),
            backMeleeAction,
            HasUsableAction(backMeleeAction));
    }

    bool CanAcceptNormalAttackInput(int requestedAction)
    {
        int action = GetLogicalActionId(currentAnimationIndex);
        return TestPlayCombatCore.CanAcceptNormalAttackInput(
            currentAnimation == null,
            action,
            requestedAction,
            boostMotionActive,
            actionTick > 5,
            boostAction,
            boostShotAction,
            idleAction,
            moveAction,
            riseAction,
            airMoveAction,
            airIdleAction,
            IsStepAction(action));
    }

    bool IsNormalAttackInputAction(int actionId)
    {
        return actionId == switchToSwordAction ||
               actionId == switchToGunAction ||
               IsShotAttackAction(actionId) ||
               IsMeleeAttackAction(actionId);
    }

    bool IsShotAttackAction(int actionId)
    {
        return (actionId >= 100 && actionId <= 103) ||
               (actionId >= 106 && actionId <= 108);
    }

    bool IsMeleeAttackAction(int actionId)
    {
        // The original melee table occupies 130..155 (five entries for each
        // neutral/forward/left/right/back family).  Later ANI slots are other
        // actions and must not inherit melee input/cancel handling.
        return actionId >= 130 && actionId <= 155;
    }

    void SyncAniScriptExecutionChannel(int actionId)
    {
        if (state == null)
            return;

        // FUN_004b8250 writes its animation channel parameter to the value
        // exposed as @int[151] before executing each ANI block. Basic actions
        // are executed on both channels by ExecuteCurrentAnimationScript; this
        // value is the initial/fallback channel for all other action families.
        state.SetInt(151, IsMeleeAttackAction(actionId) ? 0 : 1);
    }

    bool ExecuteCurrentAnimationScript(string text)
    {
        if (state == null || !currentActionSelection.UsesDualChannels)
            return ExecuteScript(text);

        // FUN_004d2030 starts the selected ANI on the main channel first and,
        // when param_7 is non-zero, on the secondary channel afterwards. The
        // normal locomotion family and target-relative shot variants both use
        // this route in the original dispatch.
        int actionBeforeMainChannel = currentAnimationIndex;
        state.SetInt(151, 0);
        bool interrupted = ExecuteScript(text);
        if (interrupted || currentAnimationIndex != actionBeforeMainChannel)
            return true;

        state.SetInt(151, 1);
        return ExecuteScript(text);
    }

    void StartNormalAttackAction(int actionId)
    {
        bool startsAttack = IsShotAttackAction(actionId) ||
                            IsMeleeAttackAction(actionId);
        attackSequenceActive = startsAttack;
        meleeApproachActive = startsAttack && actionId == meleeAction;
        meleeComboInputPending = false;
        swordCancelAction = -1;
        if (startsAttack)
        {
            RecordCombatEvent(new TestPlayCombatTraceEvent
            {
                type = TestPlayCombatTraceEventType.AttackStarted,
                actionId = actionId,
                targetActionId = actionId,
                source = "Input"
            });
        }
        if (IsShotAttackAction(actionId))
            ChangeAnimation(ResolveShotActionSelection(actionId), false);
        else
            ChangeAnimation(actionId);
    }

    bool TryUpdateNormalAttackSequence()
    {
        int heldAction = -1;
        bool currentMelee = IsMeleeAttackAction(currentAnimationIndex);
        if (attackSequenceActive && currentMelee && actionTick > 15)
            heldAction = GetHeldActionFromInput();
        bool reachedTarget = target != null && robo != null && robo.root != null &&
                             Vector3.Distance(robo.root.transform.position, target.transform.position) <
                             Mathf.Max(0f, meleeApproachDistance);
        TestPlayCombatSequenceDecision decision = TestPlayCombatCore.EvaluateSequence(
            new TestPlayCombatSequenceInput
            {
                sequenceActive = attackSequenceActive,
                meleePressed = meleeKeyPressedThisTick,
                meleeApproachActive = meleeApproachActive,
                comboInputPending = meleeComboInputPending,
                currentActionIsMelee = currentMelee,
                currentActionId = currentAnimationIndex,
                meleeApproachActionId = meleeAction,
                swordCancelActionId = swordCancelAction,
                swordCancelActionUsable = HasUsableAction(swordCancelAction),
                actionTick = actionTick,
                meleeApproachMinimumTicks = meleeApproachMinimumTicks,
                targetReached = reachedTarget,
                hasMovementEnergy = HasMovementEnergy(),
                meleeApproachFollowupActionId = meleeApproachFollowupAction,
                meleeApproachFollowupUsable = HasUsableAction(meleeApproachFollowupAction),
                heldLocomotionActionId = heldAction,
                heldActionIsLocomotionCancel = heldAction == boostAction || IsStepAction(heldAction)
            });
        if (!decision.handled)
            return false;

        bool comboWasPending = meleeComboInputPending;
        meleeComboInputPending = decision.comboInputPending;
        if (!comboWasPending && meleeComboInputPending)
        {
            RecordCombatEvent(new TestPlayCombatTraceEvent
            {
                type = TestPlayCombatTraceEventType.ComboQueued,
                actionId = currentAnimationIndex,
                source = "C",
                reason = TestPlayCombatDecisionReason.ComboQueued
            });
        }
        if (decision.clearSequence) attackSequenceActive = false;
        if (decision.clearMeleeApproach) meleeApproachActive = false;
        if (decision.clearSwordCancel) swordCancelAction = -1;
        if (decision.transitionActionId >= 0)
        {
            RecordCombatTransition(decision.transitionActionId, decision.reason);
            ChangeAnimation(decision.transitionActionId);
        }
        return true;
    }

    void FinishNormalAttackSequence()
    {
        TestPlayCombatFinishDecision decision = TestPlayCombatCore.ResolveFinish(
            meleeApproachActive,
            currentAnimationIndex,
            meleeAction,
            meleeApproachFollowupAction,
            HasUsableAction(meleeApproachFollowupAction),
            airborneFlag,
            airIdleAction,
            stepLandingAction);
        if (decision.clearSequence) attackSequenceActive = false;
        if (decision.clearMeleeApproach) meleeApproachActive = false;
        if (decision.clearComboInput) meleeComboInputPending = false;
        if (decision.clearSwordCancel) swordCancelAction = -1;
        RecordCombatTransition(decision.transitionActionId, decision.reason);
        if (decision.reason == TestPlayCombatDecisionReason.MeleeApproachFollowup)
            ChangeAnimation(decision.transitionActionId);
        else if (airborneFlag)
            StartAirborneLocomotionSequence(decision.transitionActionId, false);
        else
            StartGroundRecoverySequence(decision.transitionActionId);
    }

    int GetHeldActionFromInput()
    {
        if (state.GetInt(194) != 0) return guardAction;
        if (IsBoostInputHeld() && HasMovementEnergy()) return boostAction;
        if (riseKeyHeld)
            return airborneFlag || riseSequenceActive || IsAirborneLocomotionAction(currentAnimationIndex)
                ? riseAction
                : riseStartAction;

        int stepAction = PrepareStepFromDirection(stepDirection);
        if (stepAction >= 0) return stepAction;

        if (airborneFlag)
        {
            if (stepRecoveryActive)
                return airIdleAction;
            return GetAirborneLocomotionAction();
        }

        if (IsMoveInputHeld())
            return moveAction;

        return -1;
    }

    public void SetAirborneFlag(bool value)
    {
        airborneFlag = value;
        // State-table int 150 points to original +0xBA8. The attack update
        // branches use 0 for grounded and 1 for airborne.
        if (state != null)
            state.SetInt(150, value ? 1 : 0);
    }

    bool IsBoostInputHeld()
    {
        return riseKeyHeld && state != null && state.GetInt(191) != 0;
    }

    static bool IsTapWithinWindow(float previousTapTime, float currentTapTime, float windowSeconds)
    {
        return previousTapTime >= 0f &&
               currentTapTime >= previousTapTime &&
               currentTapTime - previousTapTime <= Mathf.Max(0f, windowSeconds);
    }

    bool ShouldEndOriginalStyleBoost()
    {
        int direction = state != null ? state.GetInt(190) : 0;
        return TestPlayLocomotionCore.ShouldEndBoost(
            boostMotionActive,
            HasMovementEnergy(),
            riseKeyHeld,
            IsDirectionInput(direction),
            actionTick,
            boostMinimumReleaseTicks);
    }

    bool ShouldEndOriginalStyleRise()
    {
        return TestPlayLocomotionCore.ShouldEndRise(
            riseSequenceActive,
            currentAnimationIndex,
            riseKeyHeld,
            HasMovementEnergy(),
            actionTick,
            riseMinimumReleaseTicks,
            GetLocomotionActions());
    }

    bool CanStartOriginalAirRise()
    {
        // The original action-8 callback starts at c38=0 and checks c38 > 10.
        return TestPlayLocomotionCore.CanStartAirRise(actionTick, airRiseMinimumIdleTicks);
    }

    bool HasMovementEnergy()
    {
        return currentEnergy > 0.0001f;
    }

    bool TrySpendMovementEnergy(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (currentEnergy + 0.0001f < amount)
            return false;

        SetMovementEnergy(currentEnergy - amount);
        return true;
    }

    void InitializeOriginalSptStatus()
    {
        SptRuntimeData data = sptSource != null ? sptSource.LastSptData : null;

        maximumHP = Mathf.Max(1f, data != null && data.HP > 0 ? data.HP : fallbackMaximumHP);
        currentHP = maximumHP;

        // FUN_00499d50 transfers Script.spt Generator to +0xD1C/+0xD20.
        // This is the gauge consumed by rise, boost and step. Script.spt Energy
        // is a separate +0xD2C/+0xD30 resource and must not replace it.
        maximumEnergy = Mathf.Max(
            1f,
            data != null && data.Generator > 0 ? data.Generator : fallbackMaximumEnergy);
        SetMovementEnergy(maximumEnergy);

        maximumAuxiliaryEnergy = Mathf.Max(
            1f,
            data != null && data.Energy > 0 ? data.Energy : fallbackMaximumAuxiliaryEnergy);
        SetAuxiliaryEnergy(maximumAuxiliaryEnergy);

        configuredScore = data != null ? data.Score : 0;
        configuredRestBody = data != null ? data.RestBody : 0;
    }

    void UpdateOriginalMovementEnergy()
    {
        ReconcileMovementEnergyState();
        ReconcileAuxiliaryEnergyState();

        float delta = 0f;
        if (meleeApproachActive && currentAnimationIndex == meleeAction)
            delta = -Mathf.Max(0f, meleeApproachEnergyPerTick);
        else if (stepSequenceActive && IsStepAction(currentAnimationIndex))
            delta = -Mathf.Max(0f, stepEnergyPerTick);
        else if (IsCurrentAction(riseAction) && riseSequenceActive)
            delta = -Mathf.Max(0f, riseEnergyPerTick);
        else if (IsCurrentAction(boostAction) && boostMotionActive)
            delta = -Mathf.Max(0f, boostEnergyPerTick);
        else if (!airborneFlag)
            // Character initialization sets +0xD24 to 24 independently from
            // the Script.spt Generator maximum copied into +0xD1C.
            delta = Mathf.Max(0f, groundedEnergyRecoveryPerTick);

        if (!Mathf.Approximately(delta, 0f))
            SetMovementEnergy(currentEnergy + delta);
    }

    void SetMovementEnergy(float value)
    {
        float max = Mathf.Max(1f, maximumEnergy > 0f ? maximumEnergy : fallbackMaximumEnergy);
        currentEnergy = Mathf.Clamp(value, 0f, max);
        if (state == null)
            return;

        lastSyncedMovementEnergyInt = Mathf.RoundToInt(currentEnergy);
        lastSyncedMovementEnergyFloat = currentEnergy;
        state.SetInt(100, lastSyncedMovementEnergyInt);
        state.SetInt(101, Mathf.RoundToInt(max));
        state.SetFloat(100, currentEnergy); // Compatibility with existing Unity-side MOD previews.
        state.SetFloat(101, max);
    }

    void ReconcileMovementEnergyState()
    {
        if (state == null)
            return;

        int intValue = state.GetInt(100);
        float floatValue = state.GetFloat(100);
        bool originalIntChanged = intValue != lastSyncedMovementEnergyInt;
        bool compatibilityFloatChanged = !Mathf.Approximately(floatValue, lastSyncedMovementEnergyFloat);

        if (originalIntChanged || compatibilityFloatChanged)
        {
            // +0xD20 is an integer in the original pointer table. Prefer it if
            // both aliases were changed during the same Script.ani block.
            SetMovementEnergy(originalIntChanged ? intValue : floatValue);
        }
    }

    void SetAuxiliaryEnergy(float value)
    {
        float max = Mathf.Max(
            1f,
            maximumAuxiliaryEnergy > 0f ? maximumAuxiliaryEnergy : fallbackMaximumAuxiliaryEnergy);
        currentAuxiliaryEnergy = Mathf.Clamp(value, 0f, max);
        if (state == null)
            return;

        lastSyncedAuxiliaryEnergyInt = Mathf.RoundToInt(currentAuxiliaryEnergy);
        lastSyncedAuxiliaryEnergyFloat = currentAuxiliaryEnergy;
        state.SetInt(102, Mathf.RoundToInt(max));
        state.SetInt(103, lastSyncedAuxiliaryEnergyInt);
        state.SetFloat(102, max);
        state.SetFloat(103, currentAuxiliaryEnergy);
    }

    void ReconcileAuxiliaryEnergyState()
    {
        if (state == null)
            return;

        int intValue = state.GetInt(103);
        float floatValue = state.GetFloat(103);
        bool originalIntChanged = intValue != lastSyncedAuxiliaryEnergyInt;
        bool compatibilityFloatChanged = !Mathf.Approximately(floatValue, lastSyncedAuxiliaryEnergyFloat);
        if (originalIntChanged || compatibilityFloatChanged)
            SetAuxiliaryEnergy(originalIntChanged ? intValue : floatValue);
    }

    public void ApplySelfDamage(float damage)
    {
        currentHP = Mathf.Clamp(currentHP - Mathf.Max(0f, damage), 0f, Mathf.Max(1f, maximumHP));
    }

    public void RestoreSelfHP(float amount)
    {
        currentHP = Mathf.Clamp(currentHP + Mathf.Max(0f, amount), 0f, Mathf.Max(1f, maximumHP));
    }

    public bool TryGetSubLockDistance(int index, out float distance)
    {
        distance = 0f;
        SptRuntimeData data = sptSource != null ? sptSource.LastSptData : null;
        if (data == null || !data.SubLockDistances.TryGetValue(index, out int configuredDistance))
            return false;

        distance = configuredDistance;
        return true;
    }

    bool ShouldForceAirborneByAction()
    {
        if (IsCurrentAction(boostAction) && boostMotionActive)
            return true;
        if ((IsCurrentAction(riseStartAction) || IsCurrentAction(riseAction)) && riseSequenceActive)
            return true;
        return false;
    }

    bool ShouldRedirectBoostToAirIdle(int actionId)
    {
        return actionId == boostAction &&
               actionId != airMoveAction &&
               airborneFlag &&
               !boostMotionActive &&
               !IsBoostInputHeld();
    }

    void NormalizeActionIds()
    {
        if (idleAction == moveAction)
            idleAction = 0;
        if (airMoveAction == boostAction)
            airMoveAction = 4;
        if (airIdleAction == airMoveAction || airIdleAction == boostAction)
            airIdleAction = 8;
        // Earlier test-play scenes serialized action 3 into both jump start and
        // landing. The original dispatch table and real ANI identify landing as 5.
        if (landingAction == riseStartAction)
            landingAction = 5;
        if (stepLandingAction <= 0 || stepLandingAction == riseStartAction || stepLandingAction == landingAction)
            stepLandingAction = 6;
    }

    bool IsSwordEquipped()
    {
        return state != null
            ? state.GetInt(152) != 0
            : string.Equals(heldWeapon, "SWORD", StringComparison.OrdinalIgnoreCase);
    }

    int ResolveActionForWeaponMode(int actionId)
    {
        return ResolveActionSelection(actionId).poseActionId;
    }

    TestPlayActionSelection ResolveActionSelection(int actionId)
    {
        return TestPlayActionCore.Resolve(
            actionId,
            IsSwordEquipped() ? TestPlayWeaponMode.Sword : TestPlayWeaponMode.Gun,
            HasUsableAction,
            HasActionScript);
    }

    TestPlayActionSelection ResolveShotActionSelection(int selectedActionId)
    {
        if (selectedActionId == shotAction || selectedActionId == boostShotAction)
            return ResolveActionSelection(selectedActionId);

        int baseActionId = selectedActionId == 107 || selectedActionId == 108 ||
                           (selectedActionId == 103 && IsCurrentAction(boostAction))
            ? boostShotAction
            : shotAction;
        bool rearVariant = selectedActionId == 103;
        return new TestPlayActionSelection
        {
            requestedActionId = baseActionId,
            logicalActionId = rearVariant ? 103 : baseActionId,
            poseActionId = selectedActionId,
            scriptActionId = rearVariant ? 103 : baseActionId,
            weaponMode = TestPlayWeaponMode.Gun,
            primaryChannel = 0,
            secondaryChannel = 1
        };
    }

    bool HasActionScript(int actionId)
    {
        if (robo == null || robo.ani == null || robo.ani.animations == null ||
            actionId < 0 || actionId >= robo.ani.animations.Count)
            return false;

        animation candidate = robo.ani.animations[actionId];
        return candidate != null &&
               (!string.IsNullOrWhiteSpace(candidate.squirrelInit) ||
                (candidate.scripts != null && candidate.scripts.Count > 0));
    }

    animation ResolveScriptAnimation(int resolvedActionId, int logicalActionId, animation resolvedAnimation)
    {
        if (resolvedAnimation == null || resolvedActionId < 50 || resolvedActionId >= 100)
            return resolvedAnimation;

        bool resolvedHasScripts =
            !string.IsNullOrWhiteSpace(resolvedAnimation.squirrelInit) ||
            (resolvedAnimation.scripts != null && resolvedAnimation.scripts.Count > 0);
        if (resolvedHasScripts)
            return resolvedAnimation;

        if (robo == null || robo.ani == null || robo.ani.animations == null ||
            logicalActionId < 0 || logicalActionId >= robo.ani.animations.Count)
            return resolvedAnimation;

        animation baseAnimation = robo.ani.animations[logicalActionId];
        if (baseAnimation == null)
            return resolvedAnimation;

        // FUN_004d2030 selects +50 for the displayed ANI while retaining the
        // base 0..49 ANI as the script source when the +50 entry has no script
        // blocks. This is why sword locomotion poses still advance in-game.
        bool baseHasScripts =
            !string.IsNullOrWhiteSpace(baseAnimation.squirrelInit) ||
            (baseAnimation.scripts != null && baseAnimation.scripts.Count > 0);
        return baseHasScripts ? baseAnimation : resolvedAnimation;
    }

    static int GetLogicalActionId(int actionId)
    {
        return TestPlayActionCore.GetLogicalActionId(actionId);
    }

    bool IsCurrentAction(int actionId)
    {
        return GetLogicalActionId(currentAnimationIndex) == actionId;
    }

    bool HasUsableAction(int actionId)
    {
        if (robo == null || robo.ani == null || robo.ani.animations == null ||
            actionId < 0 || actionId >= robo.ani.animations.Count)
            return false;

        animation candidate = robo.ani.animations[actionId];
        if (candidate == null)
            return false;

        return !string.IsNullOrWhiteSpace(candidate.squirrelInit) ||
               (candidate.scripts != null && candidate.scripts.Count > 0) ||
               (candidate.frames != null && candidate.frames.Count > 0);
    }

    void UpdateOriginalAttackCooldowns()
    {
        TestPlayCombatCore.TickCooldowns(attackCooldownTicks);
    }

    void HandleAttackDelay(List<TestPlayScriptValue> args)
    {
        if (args == null || args.Count < 2)
        {
            LogUnhandled("AttackDelay requires slot and ticks.");
            return;
        }

        int slot = args[0].AsInt(-1);
        int cooldownTicks = args[1].AsInt();
        if (!TestPlayCombatCore.TrySetCooldown(attackCooldownTicks, slot, cooldownTicks))
        {
            LogUnhandled("AttackDelay slot out of original range 0..4: " + slot);
            return;
        }
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = TestPlayCombatTraceEventType.CooldownSet,
            actionId = currentAnimationIndex,
            slot = slot,
            cooldownTicks = Mathf.Max(0, cooldownTicks),
            source = "AttackDelay"
        });
    }

    public int GetAttackCooldownTicks(int slot)
    {
        return TestPlayCombatCore.GetCooldown(attackCooldownTicks, slot);
    }

    void ChangeToAirMoveOrLanding()
    {
        boostMotionActive = false;
        ResetBoostRuntimeState();
        if (airborneFlag)
        {
            StartAirIdleSequence();
            return;
        }

        StartLandingSequence();
    }

    void StartAirIdleSequence()
    {
        StartAirborneLocomotionSequence(GetAirborneLocomotionAction(), false);
    }

    void StartStepRecoverySequence()
    {
        int recoveryDirection = activeStepInputDirection;
        StartAirborneLocomotionSequence(airIdleAction, true);
        stepRecoveryDirection = recoveryDirection;
    }

    void StartAirborneLocomotionSequence(int airborneAction, bool fromStep)
    {
        landingSequenceActive = false;
        riseSequenceActive = false;
        stepSequenceActive = false;
        stepRecoveryActive = fromStep;
        if (!fromStep)
            stepRecoveryDirection = 0;
        boostFromRiseActive = false;
        boostMotionActive = false;
        ResetBoostRuntimeState();
        moveAnimationActive = false;
        stepMovedDistance = 0f;
        ClearHeldMotionState();
        SetAirborneFlag(true);
        previousAirborneFlag = true;
        ChangeAnimation(airborneAction, GetLogicalActionId(currentAnimationIndex) == airborneAction);
    }

    void FinishOriginalStyleStep()
    {
        // FUN_004d77e0 does not make a step airborne by itself. It branches to
        // air stop (8) only when the character is already airborne; grounded
        // steps enter the landing/ground recovery path instead.
        int exitAction = GetOriginalStepExitAction();
        if (exitAction == airIdleAction)
            StartStepRecoverySequence();
        else
            StartGroundRecoverySequence(exitAction);
    }

    int GetOriginalStepExitAction()
    {
        return TestPlayLocomotionCore.ResolveStepExit(
            airborneFlag,
            groundedFlag,
            GetLocomotionActions());
    }

    void UpdateStepRecoveryInputGate()
    {
        if (!stepRecoveryActive)
            return;

        int direction = state != null ? state.GetInt(190) : 0;
        if (!IsDirectionInput(direction) || direction != stepRecoveryDirection)
            ClearStepRecovery();
    }

    void ClearStepRecovery()
    {
        stepRecoveryActive = false;
        stepRecoveryDirection = 0;
    }

    void StartBoostAction()
    {
        if (!HasMovementEnergy())
            return;

        // FUN_004d5ec0/FUN_004d68e0 charge -Generator/5 once when the
        // airborne boost branch enters action 22. This is separate from the
        // five-unit per-tick action-22 drain.
        float boostEntryCost = Mathf.Floor(Mathf.Max(0f, maximumEnergy) / 5f);
        if (boostEntryCost > 0f)
            SetMovementEnergy(currentEnergy - boostEntryCost);

        boostMotionActive = true;
        ClearStepRecovery();
        ResetBoostRuntimeState();
        SetAirborneFlag(true);
        ChangeAnimation(boostAction, IsCurrentAction(boostAction));
    }

    void StartAirRiseAction()
    {
        landingSequenceActive = false;
        riseSequenceActive = true;
        ClearStepRecovery();
        boostMotionActive = false;
        ResetBoostRuntimeState();
        SetAirborneFlag(true);
        ApplyPendingDrivenHorizontalInertia();
        ChangeAnimation(riseAction, IsCurrentAction(riseAction));
    }

    void StartLandingSequence()
    {
        StartGroundRecoverySequence(landingAction);
    }

    void StartGroundRecoverySequence(int recoveryAction)
    {
        landingSequenceActive = true;
        groundRecoveryElapsedTicks = 0;
        groundRecoveryDurationTicks = 1;
        groundRecoveryCompletedThisTick = false;
        riseSequenceActive = false;
        stepSequenceActive = false;
        ClearStepRecovery();
        boostFromRiseActive = false;
        boostMotionActive = false;
        ResetBoostRuntimeState();
        moveAnimationActive = false;
        stepMovedDistance = 0f;
        pendingDrivenHorizontalVelocity = Vector3.zero;
        hasPendingDrivenHorizontalVelocity = false;
        ClearHeldMotionState();
        // Sword-side recovery 56 commonly has no script blocks. Clear the
        // preceding attack block's transient aim/Force/attack state here,
        // because a scriptless action never enters the regular block reset.
        ResetOriginalBlockState();
        SetAirborneFlag(false);
        groundedFlag = true;
        previousAirborneFlag = false;
        ChangeAnimation(recoveryAction, true);
        groundRecoveryDurationTicks = GetFiniteActionDurationTicks();
    }

    bool CanStartActionFromCurrent()
    {
        int action = GetLogicalActionId(currentAnimationIndex);
        return currentAnimation == null ||
               action == 0 ||
               action == idleAction ||
               action == moveAction ||
               moveAnimationActive;
    }

    bool IsMoveInputHeld()
    {
        return state.GetInt(190) != 0 && stepDirection == 0;
    }

    bool TryGetInputMoveVector(out Vector3 localMove)
    {
        localMove = Vector3.zero;
        if (!IsCurrentAction(moveAction) || !moveAnimationActive || !IsMoveInputHeld())
            return false;

        float amount = Mathf.Max(0f, inputMoveMagnitude);
        if (!IsDirectionInput(state.GetInt(190)) || amount <= 0f)
            return false;

        // In the original runtime, the direction input turns the movement heading
        // and the animation's forward Move value advances along that heading.
        localMove.z = amount;
        return true;
    }

    bool TryGetOriginalStyleInputWorldMove(Transform root, Vector3 scriptedLocalMove, out Vector3 worldMove)
    {
        worldMove = Vector3.zero;
        bool groundMoveActive = IsCurrentAction(moveAction) && moveAnimationActive && IsMoveInputHeld();
        bool airMoveActive = IsCurrentAction(airMoveAction) && airborneFlag && IsDirectionInput(state.GetInt(190));
        if (root == null || (!groundMoveActive && !airMoveActive))
            return false;

        if (!TryGetInputReferenceBasis(root, out Vector3 referenceForward, out Vector3 referenceRight))
            return false;

        Vector3 desiredHeading = GetDirectionVector(state.GetInt(190), referenceForward, referenceRight);
        if (desiredHeading.sqrMagnitude < 0.000001f)
            return false;

        if (!hasInputMoveHeading)
        {
            inputMoveHeading = FlattenDirection(root.forward, referenceForward);
            hasInputMoveHeading = true;
        }

        float turnDegrees = Mathf.Max(0f, inputTurnDegreesPerTick);
        inputMoveHeading = turnDegrees <= 0f
            ? desiredHeading
            : Vector3.RotateTowards(inputMoveHeading, desiredHeading, turnDegrees * Mathf.Deg2Rad, 0f).normalized;

        root.rotation = Quaternion.LookRotation(inputMoveHeading, Vector3.up);

        float horizontalAmount = new Vector2(scriptedLocalMove.x, scriptedLocalMove.z).magnitude;
        if (horizontalAmount <= 0.000001f)
            horizontalAmount = Mathf.Max(0f, inputMoveMagnitude);

        worldMove = inputMoveHeading * horizontalAmount + Vector3.up * scriptedLocalMove.y;
        return worldMove.sqrMagnitude > 0.000001f;
    }

    bool TryGetLiveMovementReferenceBasis(Transform root, out Vector3 referenceForward, out Vector3 referenceRight)
    {
        referenceForward = Vector3.zero;
        referenceRight = Vector3.zero;
        if (root == null)
            return false;

        Transform activeLockTarget = GetLockedTargetTransform();
        if (useTargetRelativeMovement && activeLockTarget != null)
        {
            Vector3 toTarget = activeLockTarget.position - root.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.000001f)
            {
                referenceForward = toTarget.normalized;
                referenceRight = Vector3.Cross(Vector3.up, referenceForward).normalized;
                return referenceRight.sqrMagnitude > 0.000001f;
            }
        }

        if (useCameraRelativeMovement && cameraController != null &&
            cameraController.TryGetPlanarMovementBasis(out referenceForward, out referenceRight))
            return true;

        referenceForward = Vector3.zero;
        referenceRight = Vector3.zero;
        return false;
    }

    bool TryGetInputReferenceBasis(Transform root, out Vector3 referenceForward, out Vector3 referenceRight)
    {
        // Target-relative movement continues tracking the live target. An unlocked
        // camera basis is instead captured at the start of one continuous direction
        // input: re-reading it after the mech has turned makes DOWN alternate between
        // root.forward and -root.forward every tick, producing shake/forward drift.
        bool refreshTargetRelativeBasis =
            useTargetRelativeMovement && GetLockedTargetTransform() != null;
        if ((!hasInputMoveReference || refreshTargetRelativeBasis) &&
            TryGetLiveMovementReferenceBasis(root, out Vector3 liveForward, out Vector3 liveRight))
        {
            inputMoveReferenceForward = liveForward;
            hasInputMoveReference = true;
            referenceForward = liveForward;
            referenceRight = liveRight;
            return true;
        }

        if (!hasInputMoveReference)
        {
            inputMoveReferenceForward = FlattenDirection(root.forward, Vector3.forward);
            hasInputMoveReference = true;
        }

        referenceForward = inputMoveReferenceForward;
        referenceRight = Vector3.Cross(Vector3.up, referenceForward).normalized;
        return referenceRight.sqrMagnitude > 0.000001f;
    }

    void ResetInputMoveHeadingIfInactive()
    {
        bool groundMoveActive = IsCurrentAction(moveAction) && moveAnimationActive && IsMoveInputHeld();
        bool airMoveActive = IsCurrentAction(airMoveAction) && airborneFlag && IsDirectionInput(state.GetInt(190));
        if (groundMoveActive || airMoveActive)
            return;

        ResetInputMoveHeading();
    }

    void ResetInputMoveHeading()
    {
        inputMoveReferenceForward = Vector3.zero;
        inputMoveHeading = Vector3.zero;
        hasInputMoveReference = false;
        hasInputMoveHeading = false;
    }

    bool TryGetOriginalStyleBoostWorldMove(Transform root, Vector3 scriptedLocalMove, out Vector3 worldMove)
    {
        worldMove = Vector3.zero;
        if (root == null || !IsCurrentAction(boostAction) || !boostMotionActive)
            return false;

        if (!TryGetBoostReferenceBasis(root, out Vector3 referenceForward, out Vector3 referenceRight))
            return false;

        if (!hasBoostMoveHeading)
        {
            boostMoveHeading = FlattenDirection(root.forward, referenceForward);
            hasBoostMoveHeading = true;
        }

        int direction = state != null ? state.GetInt(190) : 0;
        Vector3 desiredHeading = IsDirectionInput(direction)
            ? GetDirectionVector(direction, referenceForward, referenceRight)
            : boostMoveHeading;

        if (desiredHeading.sqrMagnitude > 0.000001f)
        {
            float turnDegrees;
            if (actionTick < 10)
            {
                turnDegrees = Mathf.Max(0f, boostInitialTurnDegreesPerTick);
            }
            else
            {
                float dot = Mathf.Clamp(Vector3.Dot(boostMoveHeading, desiredHeading), -1f, 1f);
                // FUN_004de120: (dot - 1) * -2 + 0.5 degrees/tick.
                turnDegrees = (dot - 1f) * -2f + 0.5f;
            }

            boostMoveHeading = Vector3.RotateTowards(
                boostMoveHeading,
                desiredHeading,
                turnDegrees * Mathf.Deg2Rad,
                0f).normalized;
            root.rotation = Quaternion.LookRotation(boostMoveHeading, Vector3.up);
        }

        float horizontalAmount = new Vector2(scriptedLocalMove.x, scriptedLocalMove.z).magnitude;
        worldMove = boostMoveHeading * horizontalAmount + Vector3.up * scriptedLocalMove.y;
        return true;
    }

    bool TryGetBoostReferenceBasis(Transform root, out Vector3 referenceForward, out Vector3 referenceRight)
    {
        if (TryGetLiveMovementReferenceBasis(root, out Vector3 liveForward, out Vector3 liveRight))
        {
            boostReferenceForward = liveForward;
            hasBoostReference = true;
            referenceForward = liveForward;
            referenceRight = liveRight;
            return true;
        }

        if (!hasBoostReference)
        {
            boostReferenceForward = FlattenDirection(root.forward, Vector3.forward);
            hasBoostReference = true;
        }

        referenceForward = boostReferenceForward;
        referenceRight = Vector3.Cross(Vector3.up, referenceForward).normalized;
        return referenceRight.sqrMagnitude > 0.000001f;
    }

    void ApplyOriginalRiseSteering(Transform root)
    {
        if (root == null || !IsCurrentAction(riseAction) || !riseSequenceActive || !riseKeyHeld)
            return;

        int direction = state != null ? state.GetInt(190) : 0;
        if (!IsDirectionInput(direction))
            return;

        Vector3 referenceForward;
        Vector3 referenceRight;
        if (!TryGetLiveMovementReferenceBasis(root, out referenceForward, out referenceRight))
        {
            referenceForward = FlattenDirection(root.forward, Vector3.forward);
            referenceRight = Vector3.Cross(Vector3.up, referenceForward).normalized;
        }

        Vector3 desiredHeading = GetDirectionVector(direction, referenceForward, referenceRight);
        Vector3 currentHeading = FlattenDirection(root.forward, referenceForward);
        Vector3 heading = Vector3.RotateTowards(
            currentHeading,
            desiredHeading,
            Mathf.Max(0f, riseTurnDegreesPerTick) * Mathf.Deg2Rad,
            0f).normalized;
        root.rotation = Quaternion.LookRotation(heading, Vector3.up);
    }

    void ApplyOriginalShotSteering(Transform root)
    {
        if (root == null || !attackSequenceActive ||
            !IsShotAttackAction(currentAnimationIndex))
            return;

        int direction = state != null ? state.GetInt(190) : 0;
        float yaw = 0f;
        if (direction == 4)
            yaw = -Mathf.Abs(shotTurnAng);
        else if (direction == 6)
            yaw = Mathf.Abs(shotTurnAng);

        if (!Mathf.Approximately(yaw, 0f))
            root.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * root.rotation;
    }

    void ResetBoostRuntimeState()
    {
        boostReferenceForward = Vector3.zero;
        boostMoveHeading = Vector3.zero;
        hasBoostReference = false;
        hasBoostMoveHeading = false;
    }

    static int EncodeDirectionInput(int horizontal, int vertical)
    {
        horizontal = Math.Sign(horizontal);
        vertical = Math.Sign(vertical);
        if (vertical > 0)
            return horizontal < 0 ? 7 : horizontal > 0 ? 9 : 8;
        if (vertical < 0)
            return horizontal < 0 ? 1 : horizontal > 0 ? 3 : 2;
        return horizontal < 0 ? 4 : horizontal > 0 ? 6 : 0;
    }

    static bool IsDirectionInput(int direction)
    {
        return direction >= 1 && direction <= 9 && direction != 5;
    }

    static Vector3 GetDirectionVector(int direction, Vector3 forward, Vector3 right)
    {
        Vector3 result = GetRawDirectionVector(direction, forward, right);
        return result.sqrMagnitude > 0.000001f ? result.normalized : Vector3.zero;
    }

    static Vector3 GetRawDirectionVector(int direction, Vector3 forward, Vector3 right)
    {
        switch (direction)
        {
            case 1: return -forward - right;
            case 2: return -forward;
            case 3: return -forward + right;
            case 4: return -right;
            case 6: return right;
            case 7: return forward - right;
            case 8: return forward;
            case 9: return forward + right;
            default: return Vector3.zero;
        }
    }

    static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.000001f)
        {
            fallback.y = 0f;
            direction = fallback.sqrMagnitude > 0.000001f ? fallback : Vector3.forward;
        }
        return direction.normalized;
    }

    int PrepareStepFromDirection(int direction)
    {
        if (!IsDirectionInput(direction) || robo == null || robo.root == null)
            return -1;

        Transform root = robo.root.transform;
        if (!TryGetInputReferenceBasis(root, out Vector3 referenceForward, out Vector3 referenceRight))
            return -1;

        Vector3 inputHeading = GetRawDirectionVector(direction, referenceForward, referenceRight);
        if (inputHeading.sqrMagnitude < 0.000001f)
            return -1;

        Vector3 currentHeading = FlattenDirection(root.forward, referenceForward);
        Vector3 currentRight = Vector3.Cross(Vector3.up, currentHeading).normalized;
        float forwardDot = Vector3.Dot(inputHeading, currentHeading);
        float rightDot = Vector3.Dot(inputHeading, currentRight);
        int actionId;
        if (Mathf.Abs(forwardDot) >= Mathf.Abs(rightDot))
            actionId = forwardDot >= 0f ? forwardStepAction : backStepAction;
        else
            actionId = rightDot >= 0f ? rightStepAction : leftStepAction;

        SetStepRuntimeState(direction, actionId, referenceForward, inputHeading);
        return actionId;
    }

    void EnsureStepRuntimeState(int actionId)
    {
        if (hasStepRuntimeState)
            return;

        if (robo == null || robo.root == null)
            return;

        Transform root = robo.root.transform;
        Vector3 referenceForward = FlattenDirection(root.forward, Vector3.forward);
        Vector3 referenceRight = Vector3.Cross(Vector3.up, referenceForward).normalized;
        int direction;
        Vector3 inputHeading;
        if (actionId == backStepAction)
        {
            direction = 2;
            inputHeading = -referenceForward;
        }
        else if (actionId == leftStepAction)
        {
            direction = 4;
            inputHeading = -referenceRight;
        }
        else if (actionId == rightStepAction)
        {
            direction = 6;
            inputHeading = referenceRight;
        }
        else
        {
            direction = 8;
            inputHeading = referenceForward;
        }

        SetStepRuntimeState(direction, actionId, referenceForward, inputHeading);
    }

    void SetStepRuntimeState(int direction, int actionId, Vector3 referenceForward, Vector3 rawInputHeading)
    {
        activeStepInputDirection = direction;
        stepReferenceForward = FlattenDirection(referenceForward, Vector3.forward);
        Vector3 inputHeading = FlattenDirection(rawInputHeading, stepReferenceForward);
        stepMoveHeading = FlattenDirection(
            rawInputHeading + stepReferenceForward * Mathf.Max(0f, stepTargetForwardBias),
            inputHeading);

        if (useOriginalStepFacing)
        {
            // FUN_004d0b70 chooses 9..12 from the input direction relative to
            // the pre-step body facing. FUN_004d77e0 then turns toward the
            // heading that makes that animation's local left/right/front/back
            // direction coincide with the requested world movement.
            stepFacingHeading = GetOriginalStepFacingHeading(actionId, inputHeading);
        }
        else if (preserveStepReferenceFacing)
        {
            stepFacingHeading = stepReferenceForward;
        }
        else if (actionId == backStepAction)
        {
            stepFacingHeading = -inputHeading;
        }
        else if (actionId == leftStepAction)
        {
            stepFacingHeading = Vector3.Cross(Vector3.up, inputHeading).normalized;
        }
        else if (actionId == rightStepAction)
        {
            stepFacingHeading = Vector3.Cross(inputHeading, Vector3.up).normalized;
        }
        else
        {
            stepFacingHeading = inputHeading;
        }

        stepFacingHeading = FlattenDirection(stepFacingHeading, stepReferenceForward);
        hasStepRuntimeState = true;
    }

    Vector3 GetOriginalStepFacingHeading(int actionId, Vector3 inputHeading)
    {
        if (actionId == backStepAction)
            return -inputHeading;
        if (actionId == leftStepAction)
            return Vector3.Cross(Vector3.up, inputHeading).normalized;
        if (actionId == rightStepAction)
            return Vector3.Cross(inputHeading, Vector3.up).normalized;
        return inputHeading;
    }

    bool TryGetOriginalStyleStepWorldMove(
        Transform root,
        Vector3 scriptedLocalMove,
        out Vector3 worldMove,
        out bool usingFallbackMove)
    {
        worldMove = Vector3.zero;
        usingFallbackMove = false;
        if (root == null || !stepSequenceActive || !IsStepAction(currentAnimationIndex))
            return false;

        EnsureStepRuntimeState(currentAnimationIndex);
        if (!hasStepRuntimeState)
            return false;

        float horizontalAmount = new Vector2(scriptedLocalMove.x, scriptedLocalMove.z).magnitude;
        if (horizontalAmount <= 0.000001f)
        {
            horizontalAmount = Mathf.Max(0f, stepFallbackMoveMagnitude);
            usingFallbackMove = horizontalAmount > 0f;
        }

        float turnDegrees = Mathf.Max(0f, stepTurnDegreesPerTick);
        Vector3 currentFacing = FlattenDirection(root.forward, stepFacingHeading);
        Vector3 nextFacing = turnDegrees <= 0f
            ? stepFacingHeading
            : Vector3.RotateTowards(currentFacing, stepFacingHeading, turnDegrees * Mathf.Deg2Rad, 0f).normalized;
        root.rotation = Quaternion.LookRotation(nextFacing, Vector3.up);

        worldMove = stepMoveHeading * horizontalAmount + Vector3.up * scriptedLocalMove.y;
        return worldMove.sqrMagnitude > 0.000001f;
    }

    bool ShouldFinishOriginalStyleStep()
    {
        if (!stepSequenceActive || !IsStepAction(currentAnimationIndex))
            return false;

        // FUN_004d77e0 subtracts four energy units first and forces its counter
        // to the terminal value when the pool is exhausted.
        if (maximumEnergy > 0f && !HasMovementEnergy())
            return true;

        int maximumTicks = GetOriginalStepMaximumTicks();
        if (actionTick >= maximumTicks)
            return true;

        int minimumTicks = Mathf.Clamp(stepMinimumTicks, 1, maximumTicks);
        if (actionTick < minimumTicks)
            return false;

        int currentDirection = state != null ? state.GetInt(190) : 0;
        return currentDirection != activeStepInputDirection;
    }

    int GetOriginalStepMaximumTicks()
    {
        return Mathf.Max(1, stepMaximumTicks);
    }

    void ResetStepRuntimeState()
    {
        activeStepInputDirection = 0;
        stepReferenceForward = Vector3.zero;
        stepMoveHeading = Vector3.zero;
        stepFacingHeading = Vector3.zero;
        hasStepRuntimeState = false;
    }

    float GetActiveStepMoveTargetDistance()
    {
        if (!stepSequenceActive || !IsStepAction(currentAnimationIndex))
            return 0f;

        return 0f;
    }

    int GetRuntimeScriptLengthTicks(script scriptBlock)
    {
        return Mathf.Max(1, scriptBlock.unk);
    }

    int ResolveStepScriptTicks(script scriptBlock)
    {
        return Mathf.Max(1, scriptBlock.unk);
    }

    bool IsStepAction(int actionId)
    {
        return TestPlayLocomotionCore.IsStep(actionId, GetLocomotionActions());
    }

    bool IsAirborneLocomotionAction(int actionId)
    {
        TestPlayLocomotionState locomotionState =
            TestPlayLocomotionCore.Classify(actionId, GetLocomotionActions());
        return locomotionState == TestPlayLocomotionState.AirMove ||
               locomotionState == TestPlayLocomotionState.AirIdle;
    }

    bool IsGroundRecoveryAction(int actionId)
    {
        TestPlayLocomotionState locomotionState =
            TestPlayLocomotionCore.Classify(actionId, GetLocomotionActions());
        return locomotionState == TestPlayLocomotionState.Landing ||
               locomotionState == TestPlayLocomotionState.StepLanding;
    }

    int GetAirborneLocomotionAction()
    {
        int direction = state != null ? state.GetInt(190) : 0;
        return TestPlayLocomotionCore.ResolveAirborneAction(
            IsDirectionInput(direction),
            GetLocomotionActions());
    }

    TestPlayLocomotionActions GetLocomotionActions()
    {
        return new TestPlayLocomotionActions
        {
            idle = idleAction,
            move = moveAction,
            jumpStart = riseStartAction,
            rise = riseAction,
            airMove = airMoveAction,
            airIdle = airIdleAction,
            landing = landingAction,
            stepLanding = stepLandingAction,
            forwardStep = forwardStepAction,
            backStep = backStepAction,
            leftStep = leftStepAction,
            rightStep = rightStepAction,
            boost = boostAction,
            guard = guardAction
        };
    }

    public TestPlayActionSelection CurrentActionSelection => currentActionSelection;
    public TestPlayMotionStep LastMotionStep => lastMotionStep;
    public TestPlayLocomotionState CurrentLocomotionState =>
        TestPlayLocomotionCore.Classify(currentAnimationIndex, GetLocomotionActions());

    public string CapturePhase2TickTrace()
    {
        return TestPlayPhase2TickTrace.Serialize(
            tick,
            currentActionSelection,
            CurrentLocomotionState,
            lastMotionStep);
    }

    public string CapturePhase3TickTrace()
    {
        return TestPlayPhase3TickTrace.Serialize(
            CapturePhase2TickTrace(),
            CaptureCombatSnapshot());
    }

    public string CapturePhase4TickTrace()
    {
        return TestPlayPhase4TickTrace.Serialize(
            CapturePhase3TickTrace(),
            new TestPlayPresentationSnapshot { events = currentPresentationTraceEvents });
    }

    public string CapturePhase5TickTrace()
    {
        Transform root = robo != null && robo.root != null ? robo.root.transform : null;
        return TestPlayPhase5TickTrace.Serialize(
            CapturePhase4TickTrace(),
            new TestPlayGoldenTickSnapshot
            {
                direction = state != null ? state.GetInt(190) : 0,
                rise = state != null && state.GetInt(199) != 0,
                boost = state != null && state.GetInt(191) != 0,
                shot = state != null && state.GetInt(192) != 0,
                melee = state != null && state.GetInt(193) != 0,
                guard = state != null && state.GetInt(194) != 0,
                lockInput = state != null && state.GetInt(195) != 0,
                hp = currentHP,
                movementEnergy = currentEnergy,
                auxiliaryEnergy = currentAuxiliaryEnergy,
                frameIndex = frameIndex,
                scriptIndex = scriptIndex,
                scriptTick = scriptTick,
                actionTick = actionTick,
                poseHeldAtEnd = animationPoseHeldAtEnd,
                airborne = airborneFlag,
                grounded = groundedFlag,
                targetLocked = targetLockActive,
                rootPosition = root != null ? root.position : Vector3.zero,
                rootRotation = root != null ? root.rotation : Quaternion.identity
            });
    }

    /// <summary>
    /// Starts the same 60 Hz runtime without reading hardware input or creating HUD/camera objects.
    /// This is used by golden-trace replay and does not replace StartTestPlay().
    /// </summary>
    public void BeginDeterministicTraceSession(TestPlayGoldenSetupKind setup)
    {
        NormalizeActionIds();
        if (state == null)
            state = new TestPlayStateTable();
        if (vm == null)
        {
            vm = new TestPlayScriptVM(state);
            vm.commandHandler = HandleCommand;
            vm.assignmentHandler = HandleAssignment;
            vm.unhandledLineHandler = LogUnhandled;
            vm.shouldStopExecution = () => abortScriptExecution;
        }

        state.ResetDefaults();
        ResetRuntimeFlags();
        ResetInputSamplingState();
        InitializeOriginalSptStatus();
        playModeActive = true;
        bool sword = setup == TestPlayGoldenSetupKind.GroundedSword;
        bool airborne = setup == TestPlayGoldenSetupKind.AirborneGun;
        deterministicGroundPlaneEnabled = !airborne;
        SetHeldWeapon(sword ? "SWORD" : "GUN");
        SetAirborneFlag(airborne);
        groundedFlag = !airborne;
        ChangeAnimation(airborne ? airIdleAction : idleAction);
    }

    /// <summary>
    /// Injects one explicit input state and advances exactly one original 60 Hz tick.
    /// Unity's Input and render-frame delta time are not read by this path.
    /// </summary>
    public string SimulateDeterministicTraceTick(TestPlayGoldenInputFrame input)
    {
        sampledDirectionInput = input.direction;
        sampledRiseKeyHeld = input.rise;
        sampledShotKeyHeld = input.shot;
        sampledMeleeKeyHeld = input.melee;
        sampledGuardKeyHeld = input.guard;
        sampledLockKeyHeld = input.lockTarget;
        sampledSpecial1KeyHeld = input.special1;
        sampledSpecial2KeyHeld = input.special2;
        sampledSpecial3KeyHeld = input.special3;
        SimulateOriginalTick();
        return CapturePhase5TickTrace();
    }

    public void EndDeterministicTraceSession()
    {
        playModeActive = false;
        deterministicGroundPlaneEnabled = false;
        StopAllBurnerEffects();
        DestroyTransientObjects();
    }

    TestPlayCombatSnapshot CaptureCombatSnapshot()
    {
        EnsureAttackProfile();
        int[] cooldowns = new int[attackCooldownTicks.Length];
        Array.Copy(attackCooldownTicks, cooldowns, cooldowns.Length);
        return new TestPlayCombatSnapshot
        {
            power = attackProfile.power,
            down = attackProfile.down,
            force = attackProfile.force,
            forceY = attackProfile.forceY,
            attackFlag = attackFlag,
            swordCancelActionId = swordCancelAction,
            cooldownTicks = cooldowns,
            sequenceActive = attackSequenceActive,
            meleeApproachActive = meleeApproachActive,
            comboInputPending = meleeComboInputPending,
            events = currentCombatTraceEvents
        };
    }

    void BeginCombatTraceTick()
    {
        currentCombatTraceEvents.Clear();
        if (pendingCombatTraceEvents.Count > 0)
        {
            currentCombatTraceEvents.AddRange(pendingCombatTraceEvents);
            pendingCombatTraceEvents.Clear();
        }
        simulatingCombatTick = true;
    }

    void RecordCombatEvent(TestPlayCombatTraceEvent combatEvent)
    {
        combatEvent.tick = tick;
        if (simulatingCombatTick)
            currentCombatTraceEvents.Add(combatEvent);
        else
            pendingCombatTraceEvents.Add(combatEvent);
    }

    void BeginPresentationTraceTick()
    {
        currentPresentationTraceEvents.Clear();
        if (pendingPresentationTraceEvents.Count > 0)
        {
            currentPresentationTraceEvents.AddRange(pendingPresentationTraceEvents);
            pendingPresentationTraceEvents.Clear();
        }
        simulatingPresentationTick = true;
    }

    void RaisePresentationEvent(TestPlayPresentationEvent presentationEvent)
    {
        presentationEvent.tick = tick;
        presentationEvent.actionIndex = currentAnimationIndex;
        presentationEvent.scriptIndex = scriptIndex;
        if (simulatingPresentationTick)
            currentPresentationTraceEvents.Add(presentationEvent);
        else
            pendingPresentationTraceEvents.Add(presentationEvent);

        Action<TestPlayPresentationEvent> handler = PresentationEventRaised;
        if (handler != null)
            handler(presentationEvent);
    }

    void RecordCombatTransition(int targetActionId, TestPlayCombatDecisionReason reason)
    {
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = reason == TestPlayCombatDecisionReason.AttackFinished
                ? TestPlayCombatTraceEventType.AttackFinished
                : TestPlayCombatTraceEventType.ActionTransition,
            actionId = currentAnimationIndex,
            targetActionId = targetActionId,
            source = "CombatSequence",
            reason = reason
        });
    }

    void RecordAttackProfileChanged(string source)
    {
        EnsureAttackProfile();
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = TestPlayCombatTraceEventType.ProfileChanged,
            actionId = currentAnimationIndex,
            source = source,
            damage = attackProfile.power,
            down = attackProfile.down,
            force = attackProfile.force,
            forceY = attackProfile.forceY,
            attackFlag = attackFlag,
            valueSource = TestPlayCombatValueSource.OriginalScriptProfile
        });
    }

    void RecordCombatHit(TestPlayCombatHitResult hit)
    {
        Vector3 horizontalImpact = Vector3.ProjectOnPlane(hit.impactForce, Vector3.up);
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = TestPlayCombatTraceEventType.Hit,
            actionId = currentAnimationIndex,
            source = hit.source,
            damage = hit.damage,
            down = hit.down,
            force = horizontalImpact.magnitude,
            forceY = hit.impactForce.y,
            attackFlag = hit.attackFlag,
            hitDecision = hit.decision,
            reactionState = hit.reactionState,
            guardHitTimerTicks = hit.guardHitTimerTicks,
            hitStopTicks = hit.hitStopTicks,
            valueSource = hit.valueSource
        });
    }

    public void NotifyProjectileHit(TestPlayCombatHitResult hit)
    {
        RecordCombatHit(hit);
        RaiseRuntimeEvent(
            TestPlayRuntimeEventType.AttackHit,
            hit.source,
            null,
            "",
            Mathf.RoundToInt(hit.damage));
    }

    bool IsHeldAction(int actionId)
    {
        actionId = GetLogicalActionId(actionId);
        return actionId == guardAction ||
               actionId == boostAction ||
               actionId == riseStartAction ||
               actionId == riseAction ||
               actionId == airMoveAction ||
               actionId == airIdleAction ||
               IsStepAction(actionId);
    }

    bool ShouldLoopHeldAction()
    {
        if (IsCurrentAction(moveAction) && moveAnimationActive && IsMoveInputHeld())
            return true;
        if (IsCurrentAction(boostAction) && boostMotionActive)
            return !ShouldEndOriginalStyleBoost();
        if (IsCurrentAction(airIdleAction) && stepRecoveryActive && airborneFlag)
            return true;
        if (IsAirborneLocomotionAction(currentAnimationIndex) && airborneFlag)
            return GetAirborneLocomotionAction() == GetLogicalActionId(currentAnimationIndex);
        if (IsCurrentAction(boostAction))
            return false;
        return IsHeldAction(currentAnimationIndex) && GetHeldActionFromInput() == GetLogicalActionId(currentAnimationIndex);
    }

    void RestartCurrentAnimationLoop()
    {
        animationPoseHeldAtEnd = false;
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        actionTick = 0;
    }

    void HoldCurrentAnimationAtLastPose()
    {
        animationPoseHeldAtEnd = true;
        int frameCount = currentAnimation != null && currentAnimation.frames != null
            ? currentAnimation.frames.Count
            : 0;
        frameIndex = Mathf.Max(0, frameCount - 1);
        frameTime = 0f;
        ApplyPose();
    }

    bool TryFinishFiniteActionByTicks()
    {
        bool stepAction = stepSequenceActive && IsStepAction(currentAnimationIndex);
        bool landingActionActive = landingSequenceActive && IsGroundRecoveryAction(currentAnimationIndex);
        if (!stepAction && !landingActionActive)
            return false;

        // FUN_004d77e0 owns step completion. ANI block length and traveled
        // distance are not the original step's termination conditions.
        if (stepAction)
            return false;

        int durationTicks = GetFiniteActionDurationTicks();
        ApplyFiniteActionPoseProgress(durationTicks);

        if (actionTick < durationTicks)
            return false;

        CompleteGroundRecoverySequence();
        return true;
    }

    bool TryFinishGroundRecoveryByWatchdog()
    {
        if (!landingSequenceActive)
        {
            groundRecoveryElapsedTicks = 0;
            groundRecoveryDurationTicks = 0;
            return false;
        }

        groundRecoveryElapsedTicks++;
        int durationTicks = Mathf.Max(1, groundRecoveryDurationTicks);
        if (groundRecoveryElapsedTicks <= durationTicks)
            return false;

        // Original landing callbacks consume the finite-action completion flag
        // and explicitly return to action 0. A malformed or self-redirecting
        // MOD recovery script can interrupt the normal block-end path forever,
        // so keep an entry-scoped clock that still guarantees that transition.
        CompleteGroundRecoverySequence();
        return true;
    }

    void CompleteGroundRecoverySequence()
    {
        landingSequenceActive = false;
        groundRecoveryElapsedTicks = 0;
        groundRecoveryDurationTicks = 0;
        groundRecoveryCompletedThisTick = true;
        ResetOriginalBlockState();
        ChangeAnimation(idleAction);
        // Apply the standing HOD immediately. This prevents the recovery pose
        // from remaining visible until another simulation tick when the idle
        // action itself contains no script blocks.
        ClearPoseTransition();
        ApplyPose();
    }

    bool TryTickScriptlessNormalAttack()
    {
        if (!attackSequenceActive)
            return false;

        int frameCount = currentAnimation != null && currentAnimation.frames != null
            ? currentAnimation.frames.Count
            : 0;
        if (frameCount <= 0)
        {
            FinishCurrentAnimation();
            return true;
        }

        // Some MOD ANI slots contain poses but no timing script. The original
        // data then provides no block duration to port, so use one 60 Hz tick
        // per HOD frame as a finite fallback instead of holding the final pose.
        frameIndex = Mathf.Clamp(actionTick - 1, 0, frameCount - 1);
        frameTime = 0f;
        ApplyPose();
        if (actionTick >= frameCount)
            FinishCurrentAnimation();
        return true;
    }

    int GetFiniteActionDurationTicks()
    {
        if (currentScriptAnimation != null && currentScriptAnimation.scripts != null && currentScriptAnimation.scripts.Count > 0)
        {
            int ticks = 0;
            for (int i = 0; i < currentScriptAnimation.scripts.Count; i++)
                ticks += GetRuntimeScriptLengthTicks(currentScriptAnimation.scripts[i]);
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

    void ClearHeldMotionState(bool preserveScriptedMove = false)
    {
        if (!preserveScriptedMove)
            moveCommand = Vector3.zero;
        forceCommand = Vector3.zero;
        moveLocked = false;
        shieldGuard = 0;
        ResetInputMoveHeading();
    }

    bool ExecuteScript(string text)
    {
        abortScriptExecution = false;
        if (string.IsNullOrEmpty(text) || vm == null)
            return false;
        vm.Execute(text);
        return abortScriptExecution;
    }

    void CompileAnimationScripts(animation targetAnimation)
    {
        if (vm == null || targetAnimation == null)
            return;

        vm.Compile(targetAnimation.squirrelInit);
        if (targetAnimation.scripts == null)
            return;

        for (int i = 0; i < targetAnimation.scripts.Count; i++)
            vm.Compile(targetAnimation.scripts[i].squirrel);
    }

    void HandleCommand(string name, List<TestPlayScriptValue> args, string rawLine)
    {
        if (logCommands)
            Debug.Log($"[TestPlayCommand] A{currentAnimationIndex} S{scriptIndex}: {rawLine.Trim()}");

        string key = NormalizeName(name);
        RaiseRuntimeEvent(TestPlayRuntimeEventType.Command, name, args);
        switch (key)
        {
            case "move":
                SetMoveVector(ref moveCommand, args);
                LogMotionAssignment("Move", rawLine);
                break;
            case "force":
                SetForceVector(ref forceCommand, args);
                LogMotionAssignment("Force", rawLine);
                break;
            case "movelock":
                moveLocked = true;
                LogMotionAssignment("MoveLock", rawLine);
                break;
            case "lockbodyuptarget":
                bodyUpAimRequested = true;
                break;
            case "lockbodydowntarget":
                bodyDownAimRequested = true;
                break;
            case "lockbodytarget":
                bodyUpAimRequested = true;
                bodyDownAimRequested = true;
                break;
            case "lockarm1target":
                arm1AimRequested = true;
                break;
            case "lockarm2target":
                arm2AimRequested = true;
                break;
            case "lockarmtarget":
                arm1AimRequested = true;
                arm2AimRequested = true;
                break;
            case "attack": HandleAttack(args); break;
            case "attackpow": SetAttackPower(args); break;
            case "attackdownf": SetAttackDown(args); break;
            case "attackforce": SetAttackForce(args); break;
            case "weaponattack": SpawnWeapon(args, "WeaponAttack"); break;
            case "weaponattack2": SpawnWeapon(args, "WeaponAttack2"); break;
            case "runproc": SpawnRunProc(args, false); break;
            case "runproc2": SpawnRunProc(args, true); break;
            case "burner": HandleBurner(args); break;
            case "burner2":
                LogUnhandled("BURNER2 is recognized but disabled by the original runtime.");
                RaisePresentationEvent(TestPlayPresentationCore.CreateUnsupported(
                    "BURNER2", args, "OriginalParserRejectsCommandObject"));
                RaiseRuntimeEvent(TestPlayRuntimeEventType.Warning, "BURNER2", args);
                break;
            case "snd":
            {
                string symbol = args.Count > 0 ? args[0].ToString() : "";
                TestPlayPresentationAdapterKind adapter = presentationRuntime != null
                    ? presentationRuntime.ResolveAudioAdapter(TestPlayPresentationEventType.Sound, symbol)
                    : TestPlayPresentationAdapterKind.None;
                RaisePresentationEvent(TestPlayPresentationCore.CreateSound(args, adapter));
                RaiseRuntimeEvent(TestPlayRuntimeEventType.Sound, "Snd", args, symbol);
                break;
            }
            case "voice":
            {
                string symbol = args.Count > 0 ? args[0].ToString() : "";
                TestPlayPresentationAdapterKind adapter = presentationRuntime != null
                    ? presentationRuntime.ResolveAudioAdapter(TestPlayPresentationEventType.Voice, symbol)
                    : TestPlayPresentationAdapterKind.None;
                RaisePresentationEvent(TestPlayPresentationCore.CreateVoice(args, adapter));
                RaiseRuntimeEvent(TestPlayRuntimeEventType.Voice, "Voice", args, symbol);
                break;
            }
            case "cameffect":
                SetCameraEffect(args.Count > 0 ? args[0].AsFloat() : 0f, args);
                break;
            case "changescript":
            case "goscriptindex":
                if (args.Count > 0)
                    scriptIndex = Mathf.Max(0, args[0].AsInt());
                scriptTick = 0;
                ResetScriptRepeatState();
                abortScriptExecution = true;
                break;
            case "goposeindex": if (args.Count > 0) frameIndex = Mathf.Max(0, args[0].AsInt()); frameTime = 0f; break;
            case "changeanime":
                if (args.Count > 0)
                    ChangeAnimation(args[0].AsInt());
                abortScriptExecution = true;
                break;
            case "changewapon":
            case "changeweapon": if (args.Count > 0) SetHeldWeapon(args[0].ToString()); break;
            case "attackdelay": HandleAttackDelay(args); break;
            case "execscripteverytime":
                scriptRepeatInterval = args.Count > 0 ? Mathf.Max(0, args[0].AsInt()) : 0;
                scriptRepeatCounter = scriptRepeatInterval;
                executeScriptRepeatedly = true;
                break;
            case "addenergy": if (args.Count > 0) SetMovementEnergy(currentEnergy + args[0].AsFloat()); break;
            case "addexgauge": if (args.Count > 0) state.SetInt(155, state.GetInt(155) + args[0].AsInt()); break;
            case "catchlastchara":
            case "catchchara":
                // CatchLastChara is the original input token. CatchChara remains an alias
                // for MODs created against the earlier Unity implementation.
                break;
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
                SetMoveVector(ref moveCommand, values);
                LogMotionAssignment("Move", rawLine);
                break;
            case "force":
                SetForceVector(ref forceCommand, values);
                LogMotionAssignment("Force", rawLine);
                break;
            case "gvenable": gvEnable = values.Count > 0 && values[0].AsBool(); break;
            case "shotturnang": shotTurnAng = values.Count > 0 ? values[0].AsFloat() : 0f; break;
            case "turnmoveang": turnMoveAng = values.Count > 0 ? values[0].AsFloat() : 0f; break;
            case "shildguard": shieldGuard = values.Count > 0 ? values[0].AsInt() : 0; break;
            case "attackflag":
                attackFlag = values.Count > 0 ? values[0].AsInt() : 0;
                RecordAttackProfileChanged("AttackFlag");
                break;
            case "cameffect": SetCameraEffect(values.Count > 0 ? values[0].AsFloat() : 0f, values); break;
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

    static void SetMoveVector(ref Vector3 targetVector, List<TestPlayScriptValue> args)
    {
        if (args.Count > 0) targetVector.x = ApplyMoveComponent(targetVector.x, args[0]);
        if (args.Count > 1) targetVector.y = ApplyMoveComponent(targetVector.y, args[1]);
        if (args.Count > 2) targetVector.z = ApplyMoveComponent(targetVector.z, args[2]);
    }

    void SetForceVector(ref Vector3 targetVector, List<TestPlayScriptValue> args)
    {
        if (args.Count > 0)
        {
            targetVector.x = ApplyForceComponent(targetVector.x, args[0]);
            if (args[0].type == TestPlayScriptValueType.Stop) velocity.x = 0f;
        }
        if (args.Count > 1)
        {
            targetVector.y = ApplyForceComponent(targetVector.y, args[1]);
            if (args[1].type == TestPlayScriptValueType.Stop) velocity.y = 0f;
        }
        if (args.Count > 2)
        {
            targetVector.z = ApplyForceComponent(targetVector.z, args[2]);
            if (args[2].type == TestPlayScriptValueType.Stop) velocity.z = 0f;
        }
    }

    static float ApplyMoveComponent(float current, TestPlayScriptValue value)
    {
        if (value.type == TestPlayScriptValueType.Stop)
            return 0f;

        float next = value.AsFloat(current);
        return Mathf.Approximately(next, 0f) ? current : next;
    }

    static float ApplyForceComponent(float current, TestPlayScriptValue value)
    {
        if (value.type == TestPlayScriptValueType.Stop)
            return 0f;
        return value.AsFloat(current);
    }

    void ApplyQueuedAimCommands()
    {
        Transform activeLockTarget = GetLockedTargetTransform();
        if (activeLockTarget == null)
            return;

        if (bodyUpAimRequested)
            RotateAimTransform(bodyUpAimRoot, activeLockTarget);
        if (bodyDownAimRequested)
            RotateAimTransform(bodyDownAimRoot, activeLockTarget);
        if (arm1AimRequested)
            RotateAimTransform(ResolveArmAimRoot(0, arm1AimRoot), activeLockTarget);
        if (arm2AimRequested)
            RotateAimTransform(ResolveArmAimRoot(1, arm2AimRoot), activeLockTarget);
    }

    Transform ResolveArmAimRoot(int attackArmId, Transform inspectorOverride)
    {
        // Preserve explicit scene/Prefab configuration. SPT is the fallback that
        // mirrors FUN_004cd840's two +0xE28 attack-arm transform slots.
        if (inspectorOverride != null)
            return inspectorOverride;

        SptRuntimeData data = sptSource != null ? sptSource.LastSptData : null;
        if (data != null &&
            data.AttackArms.TryGetValue(attackArmId, out SptFrameBindingInfo info) &&
            info != null)
            return info.BoneTr;
        return null;
    }

    void RotateAimTransform(Transform aimRoot, Transform activeLockTarget)
    {
        if (aimRoot == null || activeLockTarget == null)
            return;

        Vector3 toTarget = activeLockTarget.position - aimRoot.position;
        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        aimRoot.rotation = Quaternion.RotateTowards(
            aimRoot.rotation,
            desired,
            Mathf.Max(0f, aimTurnSpeed) * GetOriginalTickDeltaTime());
    }

    void HandleAttack(List<TestPlayScriptValue> args)
    {
        EnsureAttackProfile();
        TestPlayCombatCore.ConfigureAttackProfile(
            attackProfile,
            args.Count > 0 ? args[0].AsInt(Mathf.RoundToInt(defaultProjectileDamage)) : Mathf.RoundToInt(defaultProjectileDamage),
            args.Count > 1 ? args[1].AsInt() : 0,
            args.Count > 2 ? args[2].AsFloat() : 0f,
            args.Count > 3 ? args[3].AsFloat() : 0f);
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = TestPlayCombatTraceEventType.ProfileChanged,
            actionId = currentAnimationIndex,
            source = "ATTACK",
            damage = attackProfile.power,
            down = attackProfile.down,
            force = attackProfile.force,
            forceY = attackProfile.forceY,
            valueSource = TestPlayCombatValueSource.OriginalScriptProfile
        });
        RaiseRuntimeEvent(TestPlayRuntimeEventType.AttackProfileChanged, "ATTACK", args, "", attackProfile.power);
    }

    void SetHeldWeapon(string weapon)
    {
        heldWeapon = string.Equals(weapon, "SWORD", StringComparison.OrdinalIgnoreCase) ? "SWORD" : "GUN";
        if (state != null)
            state.SetInt(152, heldWeapon == "SWORD" ? 1 : 0);

        // The original ANI compiler emits proc type 51 for ChangeWeapon(GUN)
        // and type 52 for ChangeWeapon(SWORD). Keep the source-command VM path
        // equivalent to those compiled handlers.
        ApplyOriginalWeaponModelVisibility(heldWeapon == "GUN");
    }

    void SetAttackPower(List<TestPlayScriptValue> args)
    {
        SetAttackPower(args != null && args.Count > 0 ? args[0].AsInt() : 0);
    }

    void SetAttackDown(List<TestPlayScriptValue> args)
    {
        SetAttackDown(args != null && args.Count > 0 ? args[0].AsInt() : 0);
    }

    void SetAttackForce(List<TestPlayScriptValue> args)
    {
        SetAttackForce(args != null && args.Count > 0 ? args[0].AsFloat() : 0f);
    }

    void SetAttackPower(int value)
    {
        EnsureAttackProfile();
        attackProfile.power = value;
        RecordAttackProfileChanged("AttackPow");
    }

    void SetAttackDown(int value)
    {
        EnsureAttackProfile();
        attackProfile.down = value;
        RecordAttackProfileChanged("AttackDownF");
    }

    void SetAttackForce(float value)
    {
        EnsureAttackProfile();
        attackProfile.force = value;
        RecordAttackProfileChanged("AttackForce");
    }

    void SetAttackForceY(float value)
    {
        EnsureAttackProfile();
        attackProfile.forceY = value;
        RecordAttackProfileChanged("AttackForceY");
    }

    void EnsureAttackProfile()
    {
        if (attackProfile == null)
            attackProfile = new TestPlayAttackProfile();
    }

    void HandleBurner(List<TestPlayScriptValue> args)
    {
        int requestedId = args != null && args.Count > 0 ? args[0].AsInt(-1) : -1;
        TestPlayPresentationEvent presentationEvent;
        if (!TestPlayPresentationCore.TryCreateBurner(
                args,
                ResolveBurnerAdapter(requestedId),
                out presentationEvent))
        {
            RaisePresentationEvent(presentationEvent);
            if (args == null || args.Count == 0)
                LogUnhandled("BURNER requires an id.");
            else
                LogUnhandled("BURNER id out of original range 0..19: " + requestedId);
            return;
        }

        // The original executable reads a second float. Keep the historical one-argument
        // Unity form as output=1 so existing MOD data remains previewable.
        burnerRequestedOutputs[presentationEvent.originalId] = presentationEvent.output;
        RaisePresentationEvent(presentationEvent);
        RaiseRuntimeEvent(TestPlayRuntimeEventType.BurnerOutput, "BURNER", args, "",
            presentationEvent.originalId, presentationEvent.output);
    }

    TestPlayPresentationAdapterKind ResolveBurnerAdapter(int id)
    {
        if (id < TestPlayPresentationCore.MinimumBurnerId || id > TestPlayPresentationCore.MaximumBurnerId ||
            sptSource == null || sptSource.LastSptData == null ||
            !sptSource.LastSptData.BurnerSets.ContainsKey(id))
            return TestPlayPresentationAdapterKind.None;

        return useConeBurnerEffects
            ? TestPlayPresentationAdapterKind.BurnerCone
            : TestPlayPresentationAdapterKind.ParticleSystem;
    }

    void SetCameraEffect(float value, List<TestPlayScriptValue> args)
    {
        camEffect = value;
        TestPlayPresentationAdapterKind adapter = cameraController != null && cameraController.approximateCameraEffects
            ? TestPlayPresentationAdapterKind.CameraShakeApproximation
            : TestPlayPresentationAdapterKind.None;
        RaisePresentationEvent(TestPlayPresentationCore.CreateCameraEffect(args, value, adapter));
        RaiseRuntimeEvent(TestPlayRuntimeEventType.CameraEffect, "CamEffect", args, "", Mathf.RoundToInt(value), value);
    }

    void SpawnWeapon(List<TestPlayScriptValue> args, string source)
    {
        int weaponType = args.Count > 1 ? args[1].AsInt() : 0;
        float energy = args.Count > 3 ? args[3].AsFloat() : 0f;
        if (energy > 0f)
            SetMovementEnergy(currentEnergy - energy);

        float damage = GetCurrentAttackDamage(EstimateDamageForWeapon(weaponType));
        float speed = EstimateSpeed(args, defaultProjectileSpeed);
        SpawnProjectile(
            source + ":" + weaponType,
            damage,
            speed,
            IsHomingWeapon(weaponType),
            TestPlayCombatCore.ResolveRunProcCollisionKind(weaponType));
    }

    void SpawnRunProc(List<TestPlayScriptValue> args, bool extended)
    {
        int procType = args.Count > 1 ? args[1].AsInt() : 0;
        RaisePresentationEvent(TestPlayPresentationCore.CreateProc(
            extended,
            args,
            ResolveRunProcPresentationAdapter(extended, args, procType)));
        // FUN_004b74a0 dispatches proc type 51/52 to FUN_004f97f0/
        // FUN_004f9930. Type 51 is ChangeWeapon(GUN); type 52 is
        // ChangeWeapon(SWORD). Each recursively switches the paired SPT sets.
        if (procType == 51 || procType == 52)
        {
            ApplyOriginalWeaponModelVisibility(procType == 51);
            return;
        }
        // FUN_004b74a0 dispatches RunProc type 53/54 to FUN_004f99f0/
        // FUN_004f9ba0.  The handlers create BB_WindLine x7 or
        // BB_WindRing2 x1 and do not receive the proc argument object.
        if (TestPlayPresentationCore.IsOriginalWindProc(extended, procType))
        {
            SpawnOriginalWindProc(procType);
            return;
        }
        if (TestPlayPresentationCore.IsOriginalSwordBeamProc(extended, procType))
        {
            SpawnOriginalSwordEffect(args, "RunProc2:55");
            return;
        }
        if (TestPlayPresentationCore.IsOriginalThunderEffectProc(extended, procType))
        {
            SpawnOriginalThunderEffect(args, "RunProc2:60");
            return;
        }
        if (procType == TestPlayPresentationCore.SwordBeamProcType)
        {
            return;
        }

        if (procType == 57)
        {
            SpawnMeleeAttack(args, extended ? "RunProc2:57" : "RunProc:57", EstimateDamageForWeapon(procType));
            return;
        }

        if (procType == 62)
        {
            int subtype = args.Count > 3 ? args[3].AsInt() : 0;
            if (extended && TrySpawnOriginalSpecialEffect(args, subtype))
                return;
            if (subtype == 3)
                SpawnSimpleEffect((extended ? "RunProc2:" : "RunProc:") + procType + ":" + subtype, Color.cyan, 0.35f, 0.3f);
            return;
        }

        int textureId = extended ? GetRunProcTextureId(args, procType) : -1;
        Vector2 visualSize = GetRunProcVisualSize(args, procType);
        SpawnProjectile((extended ? "RunProc2:" : "RunProc:") + procType,
            GetCurrentAttackDamage(EstimateDamageForWeapon(procType)), EstimateSpeed(args, defaultProjectileSpeed),
            IsHomingWeapon(procType), textureId, visualSize,
            TestPlayCombatCore.ResolveRunProcCollisionKind(procType));
    }

    TestPlayPresentationAdapterKind ResolveRunProcPresentationAdapter(
        bool extended,
        List<TestPlayScriptValue> args,
        int procType)
    {
        if (procType == 57)
            return TestPlayPresentationAdapterKind.CombatOnly;
        if (TestPlayPresentationCore.IsOriginalSwordBeamProc(extended, procType))
        {
            if (!TestPlayPresentationCore.TryCreateOriginalSwordBeamParameters(
                    extended,
                    args,
                    out TestPlaySwordBeamParameters parameters) ||
                presentationRuntime == null)
                return TestPlayPresentationAdapterKind.None;
            return presentationRuntime.HasOriginalTexture(parameters.primaryTextureId) ||
                   presentationRuntime.HasOriginalTexture(parameters.lineTextureId)
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None;
        }
        if (TestPlayPresentationCore.IsOriginalThunderEffectProc(extended, procType))
        {
            if (!TestPlayPresentationCore.TryCreateOriginalThunderEffectParameters(
                    extended,
                    args,
                    out TestPlayThunderEffectParameters parameters) ||
                presentationRuntime == null)
                return TestPlayPresentationAdapterKind.None;
            return presentationRuntime.HasOriginalTexture(parameters.textureId)
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None;
        }
        if (!TestPlayPresentationCore.IsOriginalWindProc(extended, procType))
            return TestPlayPresentationAdapterKind.None;

        string key = "RunProc:" + procType;
        return presentationRuntime != null && presentationRuntime.HasMappedEffect(key)
            ? TestPlayPresentationAdapterKind.MappedPrefab
            : TestPlayPresentationAdapterKind.PrimitiveFallback;
    }

    void SpawnOriginalWindProc(int procType)
    {
        if (robo == null || robo.root == null)
            return;

        Transform root = robo.root.transform;
        if (procType == TestPlayPresentationCore.WindLineProcType)
        {
            Quaternion rotation = Quaternion.LookRotation(-root.forward, root.up);
            for (int i = 0; i < TestPlayPresentationCore.OriginalWindLineCount; i++)
            {
                Vector3 position = root.position + root.rotation * DeterministicWindLineOffsets[i];
                SpawnOriginalWindVisual(procType, position, rotation, TestPlayWindEffectKind.WindLine);
            }
            return;
        }

        if (procType == TestPlayPresentationCore.WindRingProcType)
            SpawnOriginalWindVisual(procType, root.position, root.rotation, TestPlayWindEffectKind.WindRing);
    }

    void SpawnOriginalWindVisual(
        int procType,
        Vector3 position,
        Quaternion rotation,
        TestPlayWindEffectKind kind)
    {
        string key = "RunProc:" + procType;
        GameObject go = presentationRuntime != null
            ? presentationRuntime.CreateMappedEffect(key, position, rotation)
            : null;
        bool mappedEffect = go != null;
        if (go == null)
        {
            go = new GameObject("TestPlayEffect_" + key);
            go.transform.SetPositionAndRotation(position, rotation);
            TestPlayWindEffect effect = go.AddComponent<TestPlayWindEffect>();
            Shader shader = presentationRuntime != null ? presentationRuntime.originalEffectShader : null;
            if (kind == TestPlayWindEffectKind.WindLine)
            {
                effect.ConfigureWindLine(
                    TestPlayPresentationCore.OriginalWindLineWidth,
                    TestPlayPresentationCore.OriginalWindLineLength,
                    windEffectLifeSeconds,
                    windEffectColor,
                    shader);
            }
            else
            {
                effect.ConfigureWindRing(
                    TestPlayPresentationCore.OriginalWindRingRadius,
                    windEffectLifeSeconds,
                    windRingExpansionPerSecond,
                    windEffectColor,
                    shader);
            }
        }

        TestPlayPresentationAdapterKind adapter = mappedEffect
            ? TestPlayPresentationAdapterKind.MappedPrefab
            : TestPlayPresentationAdapterKind.PrimitiveFallback;
        RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(
            key,
            -1,
            adapter,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            "OriginalCountAndDimensionsWithUnityLifetimeAndDeterministicScatter"));
        spawnedTransientObjects.Add(go);
        RaiseRuntimeEvent(TestPlayRuntimeEventType.EffectSpawned, key, null, key, procType, windEffectLifeSeconds);
        if (mappedEffect && Application.isPlaying && windEffectLifeSeconds > 0f)
            Destroy(go, windEffectLifeSeconds);
    }

    void ApplyOriginalWeaponModelVisibility(bool gunVisible)
    {
        SptRuntimeData data = sptSource != null ? sptSource.LastSptData : null;
        if (data == null)
            return;

        SetFrameBindingsActive(data.GunModels, gunVisible);
        SetFrameBindingsActive(data.SwordModels, !gunVisible);
    }

    static void SetFrameBindingsActive(Dictionary<int, SptFrameBindingInfo> bindings, bool active)
    {
        if (bindings == null)
            return;

        foreach (SptFrameBindingInfo info in bindings.Values)
        {
            if (info != null && info.BoneTr != null)
                info.BoneTr.gameObject.SetActive(active);
        }
    }

    int GetCurrentAttackDamage(float fallback)
    {
        EnsureAttackProfile();
        return Mathf.RoundToInt(TestPlayCombatCore.CreateProjectilePayload(
            attackProfile, fallback, 0f, false, "Damage").damage);
    }

    void SpawnMeleeAttack(List<TestPlayScriptValue> args, string source, float fallbackDamage)
    {
        // FUN_004fa150: [2]=WEAPONPOINT, [3]/100=長さ, [5]/[6]=原作未命名値,
        // [11]=存続tick。FUN_00502e60はATTACKテーブルを生成時に複製する。
        if (args == null || args.Count < 12 || sptSource == null || sptSource.LastSptData == null)
        {
            LogUnhandled(source + " requires the original 12 arguments and parsed Script.spt WEAPONPOINT data.");
            return;
        }

        int weaponPointId = args[2].AsInt(-1);
        if (!sptSource.LastSptData.WeaponPoints.TryGetValue(weaponPointId, out WeaponPointInfo weaponPoint) ||
            weaponPoint == null || weaponPoint.BoneTr == null)
        {
            LogUnhandled(source + " WEAPONPOINT " + weaponPointId + " is not bound; original type 57 creates no hit object.");
            return;
        }

        EnsureAttackProfile();
        activeMeleeAttacks.Add(TestPlayCombatCore.CreateMeleeAttackState(
            attackProfile,
            fallbackDamage,
            weaponPointId,
            args[3].AsFloat() / 100f,
            args[11].AsInt(),
            args[5].AsInt(),
            args[6].AsInt(),
            weaponPoint.BoneTr.position,
            weaponPoint.WorldForward,
            source,
            attackFlag));
    }

    void TickActiveMeleeAttacks()
    {
        if (activeMeleeAttacks.Count == 0)
            return;

        SptRuntimeData sptData = sptSource != null ? sptSource.LastSptData : null;
        for (int i = activeMeleeAttacks.Count - 1; i >= 0; i--)
        {
            TestPlayMeleeAttackState attack = activeMeleeAttacks[i];
            if (sptData == null ||
                !sptData.WeaponPoints.TryGetValue(attack.weaponPointId, out WeaponPointInfo weaponPoint) ||
                weaponPoint == null || weaponPoint.BoneTr == null)
            {
                activeMeleeAttacks.RemoveAt(i);
                continue;
            }

            TestPlayMeleeTickResult tickResult = TestPlayCombatCore.TickMeleeAttack(
                attack,
                new TestPlayMeleeTickInput
                {
                    origin = weaponPoint.BoneTr.position,
                    forward = weaponPoint.WorldForward,
                    targetAlive = target != null && target.IsAlive,
                    targetId = target != null
                        ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)
                        : 0,
                    targetPosition = target != null ? target.transform.position : Vector3.zero,
                    targetRadius = target != null ? target.hitRadius : 0f
                });

            if (tickResult.hit && target != null)
            {
                Vector3 impactDirection = target.transform.position - tickResult.currentOrigin;
                TestPlayCombatHitResult hit = TestPlayCombatCore.CreateHitResult(
                    attack.payload, impactDirection);
                Vector3 attackerPosition = robo != null && robo.root != null
                    ? robo.root.transform.position
                    : tickResult.currentOrigin;
                hit = target.ResolveImpact(
                    hit,
                    attackerPosition,
                    false,
                    true,
                    attack.originalP2);
                ApplyMeleeDefenseFeedback(hit);
                RecordCombatHit(hit);
                RaiseRuntimeEvent(
                    TestPlayRuntimeEventType.AttackHit,
                    attack.source,
                    null,
                    "WEAPONPOINT:" + attack.weaponPointId,
                    Mathf.RoundToInt(hit.damage),
                    attack.remainingTicks);
            }

            if (tickResult.expired)
                activeMeleeAttacks.RemoveAt(i);
        }
    }

    void ApplyMeleeDefenseFeedback(TestPlayCombatHitResult hit)
    {
        if (hit.decision == TestPlayCombatHitDecision.Damaged && hit.hitStopTicks > 0)
            hitStopTicks = Mathf.Max(hitStopTicks, hit.hitStopTicks);

        if (hit.attackerGuardReactionTicks > 0)
            meleeGuardFeedbackTicks = Mathf.Max(
                meleeGuardFeedbackTicks,
                hit.attackerGuardReactionTicks);
        if (hit.applyAttackerGuardRecoil && target != null)
            velocity += target.transform.forward * 0.05f;
    }

    void SpawnProjectile(string source, float damage, float speed, bool homing)
    {
        SpawnProjectile(
            source,
            damage,
            speed,
            homing,
            -1,
            Vector2.zero,
            TestPlayAttackCollisionKind.Unspecified);
    }

    void SpawnProjectile(
        string source,
        float damage,
        float speed,
        bool homing,
        TestPlayAttackCollisionKind collisionKind)
    {
        SpawnProjectile(source, damage, speed, homing, -1, Vector2.zero, collisionKind);
    }

    void SpawnProjectile(
        string source,
        float damage,
        float speed,
        bool homing,
        int textureId,
        Vector2 visualSize,
        TestPlayAttackCollisionKind collisionKind)
    {
        if (robo == null || robo.root == null)
            return;

        EnsureAttackProfile();
        TestPlayProjectilePayload payload = TestPlayCombatCore.CreateProjectilePayload(
            attackProfile, damage, speed, homing, source, attackFlag, collisionKind);

        Vector3 spawnPosition = robo.root.transform.position + robo.root.transform.forward * 1.5f + Vector3.up * 1.2f;
        Quaternion spawnRotation = robo.root.transform.rotation;
        GameObject go = presentationRuntime != null
            ? presentationRuntime.CreateMappedEffect(source, spawnPosition, spawnRotation)
            : null;
        bool mappedEffect = go != null;
        TestPlayPresentationAdapterKind visualAdapter = mappedEffect
            ? TestPlayPresentationAdapterKind.MappedPrefab
            : TestPlayPresentationAdapterKind.None;
        if (go == null && presentationRuntime != null && textureId >= 0)
        {
            Vector2 size = visualSize.sqrMagnitude > 0.0001f
                ? visualSize
                : Vector2.one * Mathf.Max(0.1f, projectileRadius * 4f);
            GameObject visual = presentationRuntime.CreateOriginalTextureEffect(textureId, spawnPosition, spawnRotation, size, 0f, Color.white);
            if (visual != null)
            {
                go = new GameObject("TestPlayProjectileRoot_" + source);
                go.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                visual.transform.SetParent(go.transform, true);
                mappedEffect = true;
                visualAdapter = TestPlayPresentationAdapterKind.OriginalTextureQuad;
            }
        }
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualAdapter = TestPlayPresentationAdapterKind.PrimitiveFallback;
        }
        RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(source, textureId, visualAdapter));
        go.name = "TestPlayProjectile_" + source;
        go.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (!mappedEffect)
            go.transform.localScale = Vector3.one * projectileRadius;
        TestPlayProjectile projectile = go.GetComponent<TestPlayProjectile>();
        if (projectile == null)
            projectile = go.AddComponent<TestPlayProjectile>();
        projectile.owner = this;
        projectile.target = target;
        projectile.damage = payload.damage;
        projectile.downValue = payload.down;
        projectile.horizontalImpactForce = payload.horizontalImpactForce;
        projectile.verticalImpactForce = payload.verticalImpactForce;
        projectile.attackFlag = payload.attackFlag;
        projectile.collisionKind = payload.collisionKind;
        projectile.speed = payload.speed;
        projectile.hitRadius = projectileRadius;
        projectile.homingTurnRate = payload.homing ? 180f : 0f;
        projectile.sourceCommand = payload.source;
        projectile.valueSource = payload.valueSource;
        spawnedTransientObjects.Add(go);
        RecordCombatEvent(new TestPlayCombatTraceEvent
        {
            type = TestPlayCombatTraceEventType.ProjectileSpawned,
            actionId = currentAnimationIndex,
            source = payload.source,
            damage = payload.damage,
            down = payload.down,
            force = payload.horizontalImpactForce,
            forceY = payload.verticalImpactForce,
            attackFlag = payload.attackFlag,
            valueSource = payload.valueSource
        });
        RaiseRuntimeEvent(TestPlayRuntimeEventType.WeaponSpawned, source, null, source, 0, payload.damage);
    }

    bool TrySpawnOriginalSpecialEffect(List<TestPlayScriptValue> args, int subtype)
    {
        if (presentationRuntime == null || robo == null || robo.root == null)
            return false;

        string key = "RunProc2:62:" + subtype;
        Vector3 basePosition = robo.root.transform.position - robo.root.transform.forward * 0.5f + Vector3.up;
        switch (subtype)
        {
            case 3:
            {
                float sphereSize = Mathf.Clamp(Mathf.Abs(GetArg(args, 4, 35f)) * 0.01f, 0.2f, 6f);
                float length = Mathf.Clamp(Mathf.Abs(GetArg(args, 5, 35f)) * 0.01f, 0.2f, 10f);
                int variant = Mathf.RoundToInt(GetArg(args, 6, 0f));
                string textureName = variant == 1 ? "burner4.png" : "burner3.png";
                return SpawnOriginalNamedEffect(key, textureName, basePosition, new Vector2(sphereSize, length), 0.3f);
            }
            case 4:
            {
                int textureId = Mathf.RoundToInt(GetArg(args, 7, -1f));
                float thickness = Mathf.Clamp(Mathf.Abs(GetArg(args, 5, 10f)) * 0.1f, 0.1f, 5f);
                return SpawnOriginalTextureEffect(key, textureId, basePosition, Vector2.one * thickness, 0.4f);
            }
            case 7:
            {
                int textureId = Mathf.RoundToInt(GetArg(args, 6, -1f));
                float size = Mathf.Clamp(Mathf.Abs(GetArg(args, 5, 50f)) * 0.01f, 0.1f, 8f);
                float life = Mathf.Clamp(Mathf.Abs(GetArg(args, 10, 30f)) * 0.02f, 0.1f, 5f);
                return SpawnOriginalTextureEffect(key, textureId, basePosition, Vector2.one * size, life);
            }
            case 10:
            {
                float size = Mathf.Clamp(Mathf.Abs(GetArg(args, 5, 100f)) * 0.01f, 0.1f, 8f);
                return SpawnOriginalTextureEffect(key, 35, basePosition, Vector2.one * size, 0.8f);
            }
            case 11:
                return SpawnOriginalTextureEffect(key, 40, basePosition, Vector2.one * 0.35f, 0.4f);
        }
        return false;
    }

    void SpawnOriginalSwordEffect(List<TestPlayScriptValue> args, string key)
    {
        if (!TestPlayPresentationCore.TryCreateOriginalSwordBeamParameters(
                true,
                args,
                out TestPlaySwordBeamParameters parameters))
        {
            LogUnhandled(key + " requires the original 12 arguments.");
            return;
        }

        SptRuntimeData sptData = sptSource != null ? sptSource.LastSptData : null;
        if (sptData == null ||
            !sptData.WeaponPoints.TryGetValue(parameters.weaponPointId, out WeaponPointInfo weaponPoint) ||
            weaponPoint == null || weaponPoint.BoneTr == null)
        {
            LogUnhandled(key + " WEAPONPOINT " + parameters.weaponPointId +
                " is not bound; original BB_SwordBeam is not created.");
            return;
        }

        if (parameters.replaceManagedBeam && managedSwordBeam != null)
            RemoveActiveSwordBeam(managedSwordBeam);

        GameObject visualRoot = null;
        TestPlaySwordBeamEffect visual = null;
        bool primaryLayerCreated = false;
        bool lineLayerCreated = false;
        if (presentationRuntime != null)
        {
            // Real HOD weapon-point bones already contain the outward basis for both
            // UP and DOWN labels. Reversing the display child swaps the saber texture's
            // root and tip, so type 55 follows the bone's local Z+ without another turn.
            visualRoot = presentationRuntime.CreateOriginalSwordBeamEffect(
                parameters,
                weaponPoint.BoneTr,
                false,
                out visual,
                out primaryLayerCreated,
                out lineLayerCreated);
        }

        ActiveSwordBeam active = new ActiveSwordBeam
        {
            parameters = parameters,
            anchor = weaponPoint.BoneTr,
            visualRoot = visualRoot,
            visual = visual,
            currentLength = parameters.initialLength,
            remainingTicks = parameters.lifetimeTicks
        };
        activeSwordBeams.Add(active);
        if (parameters.replaceManagedBeam)
            managedSwordBeam = active;
        if (visualRoot != null)
        {
            visualRoot.name = "TestPlayEffect_" + key + "_WEAPONPOINT" + parameters.weaponPointId;
            spawnedTransientObjects.Add(visualRoot);
            RaiseRuntimeEvent(TestPlayRuntimeEventType.EffectSpawned, key, null, key, 0, parameters.lifetimeTicks);
        }

        if (parameters.primaryTextureId >= 0)
        {
            RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(
                key + ":Primary",
                parameters.primaryTextureId,
                primaryLayerCreated
                    ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                    : TestPlayPresentationAdapterKind.None,
                TestPlayPresentationEvidence.OriginalExecutableConfirmed,
                primaryLayerCreated ? "" : "OriginalPrimaryTextureUnavailable"));
        }
        RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(
            key + ":Line",
            parameters.lineTextureId,
            lineLayerCreated
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            lineLayerCreated ? "" : "OriginalLineTextureUnavailable"));
    }

    void TickActiveSwordBeams()
    {
        for (int i = activeSwordBeams.Count - 1; i >= 0; i--)
        {
            ActiveSwordBeam active = activeSwordBeams[i];
            if (active == null || active.anchor == null ||
                !TestPlayPresentationCore.AdvanceOriginalSwordBeam(
                    ref active.currentLength,
                    active.parameters.targetLength,
                    ref active.remainingTicks))
            {
                RemoveActiveSwordBeamAt(i);
                continue;
            }

            if (active.visual != null)
                active.visual.SetLength(active.currentLength);
        }
    }

    void SpawnOriginalThunderEffect(List<TestPlayScriptValue> args, string key)
    {
        if (!TestPlayPresentationCore.TryCreateOriginalThunderEffectParameters(
                true,
                args,
                out TestPlayThunderEffectParameters parameters))
        {
            LogUnhandled(key + " requires the original 12 arguments.");
            return;
        }

        SptRuntimeData sptData = sptSource != null ? sptSource.LastSptData : null;
        if (sptData == null ||
            !sptData.WeaponPoints.TryGetValue(parameters.weaponPointId, out WeaponPointInfo weaponPoint) ||
            weaponPoint == null || weaponPoint.BoneTr == null)
        {
            LogUnhandled(key + " WEAPONPOINT " + parameters.weaponPointId +
                " is not bound; original LZ_ThunderEffect is not created.");
            return;
        }

        GameObject visualRoot = null;
        TestPlayThunderEffect visual = null;
        if (presentationRuntime != null)
        {
            visualRoot = presentationRuntime.CreateOriginalThunderEffect(
                parameters,
                weaponPoint.BoneTr.position,
                weaponPoint.BoneTr.rotation,
                weaponPoint.WorldForward,
                out visual);
        }

        ActiveThunderEffect active = new ActiveThunderEffect
        {
            parameters = parameters,
            visualRoot = visualRoot,
            visual = visual,
            currentWidth = parameters.width,
            remainingActiveTicks = parameters.activeTicks,
            travelDistance = 0f,
            elapsedTicks = 0
        };
        activeThunderEffects.Add(active);
        if (visualRoot != null)
        {
            visualRoot.name = "TestPlayEffect_" + key + "_WEAPONPOINT" + parameters.weaponPointId;
            spawnedTransientObjects.Add(visualRoot);
            RaiseRuntimeEvent(TestPlayRuntimeEventType.EffectSpawned, key, null, key, 0, parameters.activeTicks);
        }

        RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(
            key,
            parameters.textureId,
            visual != null
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None,
            TestPlayPresentationEvidence.OriginalExecutableConfirmed,
            visual != null
                ? "OriginalParametersAndTimingWithDeterministicUnityScatter"
                : "OriginalThunderTextureUnavailable"));
    }

    void TickActiveThunderEffects()
    {
        for (int i = activeThunderEffects.Count - 1; i >= 0; i--)
        {
            ActiveThunderEffect active = activeThunderEffects[i];
            if (active == null)
            {
                RemoveActiveThunderEffectAt(i);
                continue;
            }

            active.elapsedTicks++;
            if (!TestPlayPresentationCore.AdvanceOriginalThunderEffect(
                    active.parameters.width,
                    active.parameters.forwardSpeedPerTick,
                    ref active.currentWidth,
                    ref active.remainingActiveTicks,
                    ref active.travelDistance))
            {
                RemoveActiveThunderEffectAt(i);
                continue;
            }

            if (active.visual != null)
            {
                active.visual.ApplyOriginalTickState(
                    active.currentWidth,
                    active.travelDistance,
                    active.elapsedTicks);
            }
        }
    }

    void RemoveActiveThunderEffectAt(int index)
    {
        ActiveThunderEffect active = activeThunderEffects[index];
        activeThunderEffects.RemoveAt(index);
        GameObject visualRoot = active != null ? active.visualRoot : null;
        if (visualRoot == null)
            return;

        spawnedTransientObjects.Remove(visualRoot);
        if (Application.isPlaying)
            Destroy(visualRoot);
        else
            DestroyImmediate(visualRoot);
    }

    void RemoveActiveSwordBeam(ActiveSwordBeam active)
    {
        int index = activeSwordBeams.IndexOf(active);
        if (index >= 0)
            RemoveActiveSwordBeamAt(index);
    }

    void RemoveActiveSwordBeamAt(int index)
    {
        ActiveSwordBeam active = activeSwordBeams[index];
        if (ReferenceEquals(managedSwordBeam, active))
            managedSwordBeam = null;
        activeSwordBeams.RemoveAt(index);

        GameObject visualRoot = active != null ? active.visualRoot : null;
        if (visualRoot == null)
            return;
        spawnedTransientObjects.Remove(visualRoot);
        if (Application.isPlaying)
            Destroy(visualRoot);
        else
            DestroyImmediate(visualRoot);
    }

    bool SpawnOriginalTextureEffect(string key, int textureId, Vector3 position, Vector2 size, float life)
    {
        if (presentationRuntime == null || textureId < 0)
        {
            RaisePresentationEvent(TestPlayPresentationCore.CreateTexture(
                key, textureId, "", TestPlayPresentationAdapterKind.None));
            return false;
        }
        GameObject go = presentationRuntime.CreateOriginalTextureEffect(textureId, position, robo.root.transform.rotation, size, life, Color.white);
        RaisePresentationEvent(TestPlayPresentationCore.CreateTexture(
            key,
            textureId,
            "",
            go != null
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None));
        return TrackOriginalEffect(go, key, life);
    }

    bool SpawnOriginalNamedEffect(string key, string textureName, Vector3 position, Vector2 size, float life)
    {
        if (presentationRuntime == null)
        {
            RaisePresentationEvent(TestPlayPresentationCore.CreateTexture(
                key, -1, textureName, TestPlayPresentationAdapterKind.None));
            return false;
        }
        GameObject go = presentationRuntime.CreateOriginalNamedTextureEffect(textureName, position, robo.root.transform.rotation, size, life, Color.white);
        RaisePresentationEvent(TestPlayPresentationCore.CreateTexture(
            key,
            -1,
            textureName,
            go != null
                ? TestPlayPresentationAdapterKind.OriginalTextureQuad
                : TestPlayPresentationAdapterKind.None));
        return TrackOriginalEffect(go, key, life);
    }

    bool TrackOriginalEffect(GameObject go, string key, float life)
    {
        if (go == null)
            return false;
        go.name = "TestPlayEffect_" + key;
        spawnedTransientObjects.Add(go);
        RaiseRuntimeEvent(TestPlayRuntimeEventType.EffectSpawned, key, null, key, 0, life);
        return true;
    }

    static int GetRunProcTextureId(List<TestPlayScriptValue> args, int procType)
    {
        switch (procType)
        {
            case 1: return Mathf.RoundToInt(GetArg(args, 8, -1f));
            case 24:
            case 25:
            case 28: return Mathf.RoundToInt(GetArg(args, 7, -1f));
            default: return -1;
        }
    }

    static Vector2 GetRunProcVisualSize(List<TestPlayScriptValue> args, int procType)
    {
        float thickness = 0.5f;
        float length = 1f;
        switch (procType)
        {
            case 1:
                length = Mathf.Abs(GetArg(args, 4, 100f)) * 0.01f;
                thickness = Mathf.Abs(GetArg(args, 6, 20f)) * 0.01f;
                break;
            case 24:
            case 25:
                thickness = Mathf.Abs(GetArg(args, 4, 80f)) * 0.01f;
                length = Mathf.Abs(GetArg(args, 6, 80f)) * 0.01f;
                break;
            case 28:
                thickness = Mathf.Abs(GetArg(args, 4, 80f)) * 0.01f;
                length = Mathf.Abs(GetArg(args, 5, 80f)) * 0.01f;
                break;
        }
        return new Vector2(Mathf.Clamp(thickness, 0.05f, 5f), Mathf.Clamp(length, 0.1f, 12f));
    }

    static float GetArg(List<TestPlayScriptValue> args, int index, float fallback)
    {
        return args != null && index >= 0 && index < args.Count ? args[index].AsFloat() : fallback;
    }

    void SpawnSimpleEffect(string effectKey, Color color, float scale, float life)
    {
        if (robo == null || robo.root == null)
            return;

        Vector3 position = robo.root.transform.position - robo.root.transform.forward * 0.5f;
        GameObject go = presentationRuntime != null
            ? presentationRuntime.CreateMappedEffect(effectKey, position, robo.root.transform.rotation)
            : null;
        bool mappedEffect = go != null;
        if (go == null)
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        RaisePresentationEvent(TestPlayPresentationCore.CreateVisual(
            effectKey,
            -1,
            mappedEffect
                ? TestPlayPresentationAdapterKind.MappedPrefab
                : TestPlayPresentationAdapterKind.PrimitiveFallback));
        go.name = "TestPlayEffect";
        go.transform.position = position;
        if (!mappedEffect)
            go.transform.localScale = Vector3.one * scale;
        Renderer renderer = go.GetComponent<Renderer>();
        // renderer.material instantiates a scene material and delayed Destroy
        // is invalid while editor verification runs outside Play Mode. Golden
        // Trace does not need the fallback visual; EndDeterministicTraceSession
        // removes every tracked transient immediately.
        if (!mappedEffect && renderer != null && Application.isPlaying)
            renderer.material.color = color;
        spawnedTransientObjects.Add(go);
        RaiseRuntimeEvent(TestPlayRuntimeEventType.EffectSpawned, effectKey, null, effectKey, 0, life);
        if (Application.isPlaying)
            Destroy(go, life);
    }

    void ApplyBurners()
    {
        UpdatePropulsionLoopFromBurnerOutputs();

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

            float output;
            bool requested = burnerRequestedOutputs.TryGetValue(info.Id, out output) && output > 0f;
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

            float output;
            bool requested = burnerRequestedOutputs.TryGetValue(info.Id, out output) && output > 0f && info.Scale > 0f;
            float outputScale = Mathf.Max(0f, output);
            float length = Mathf.Max(0f, info.Scale * burnerLengthMultiplier * outputScale);
            float radius = Mathf.Max(0.001f, length * burnerRadiusRatio);
            Color outputColor = burnerConeColor;
            outputColor.a *= Mathf.Clamp01(outputScale);
            cone.SetTarget(requested, length, radius, outputColor, burnerFadeSpeed);
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

        Texture2D originalTexture = useOriginalBurnerTexture && presentationRuntime != null
            ? presentationRuntime.GetOriginalTexture(originalBurnerTextureId)
            : null;
        cone.ConfigureVisual(
            originalTexture,
            originalTexture != null && presentationRuntime != null ? presentationRuntime.originalEffectShader : null);
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
        burnerRequestedOutputs.Clear();
        presentationRuntime?.SetPropulsionLoopActive(false);
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

    void UpdatePropulsionLoopFromBurnerOutputs()
    {
        bool active = false;
        foreach (KeyValuePair<int, float> output in burnerRequestedOutputs)
        {
            if (output.Value > 0f)
            {
                active = true;
                break;
            }
        }
        presentationRuntime?.SetPropulsionLoopActive(active);
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
            "[TestPlay][MotionAssign] tick={0} action={1}:{2} script={3} root={4} label={5} raw=\"{6}\" move={7} force={8} moveLocked={9} vF={10:F3} aniScale={11:F3}",
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
            aniUnitsToUnityScale));
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
            "[TestPlay][RootMotion] tick={0} action={1}:{2} script={3}/{4} root={5} path={6} pos={7}->{8} delta={9} localMove={10} worldMove={11} scriptedMove={12} appliedMove={13} force={14} appliedForce={15} velocity={16} moveLocked={17} inputMove={18} stepFallbackMove={19} vF={20:F3} aniScale={21:F3} tickDt={22:F4} step={23} stepDist={24:F4}/{25:F4}",
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
            FormatVector(lastMotionStep.forcePerTick),
            FormatVector(velocity),
            moveLocked,
            usingInputMove,
            usingStepFallbackMove,
            vFMulti,
            aniUnitsToUnityScale,
            GetOriginalTickDeltaTime(),
            stepSequenceActive && IsStepAction(currentAnimationIndex),
            debugStepMovedDistance,
            debugStepTargetDistance));
    }

    void LogGroundingRootDebug(bool forceAirborne, bool gravityEnabled, Vector3 requestedMove, Vector3 controllerMove, Vector3 actualMove)
    {
        if (!logGroundingDebug)
            return;

        int interval = Mathf.Max(1, motionDebugIntervalTicks);
        bool hasVerticalMotion = Mathf.Abs(controllerMove.y) > 0.000001f || Mathf.Abs(verticalFallSpeed) > 0.000001f;
        bool shouldLogThisTick = hasVerticalMotion || (tick % interval) == 0;
        if (!shouldLogThisTick)
            return;

        Debug.Log(string.Format(
            "[TestPlay][Grounding] tick={0} action={1}:{2} grounded={3} airborne={4} forceAir={5} gvEnable={6} fallSpeed={7:F4} flags={8} requested={9} controllerMove={10} actual={11}",
            tick,
            currentAnimationIndex,
            currentAnimationName,
            groundedFlag,
            airborneFlag,
            forceAirborne,
            gravityEnabled,
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
        currentScriptAnimation = null;
        currentActionSelection = new TestPlayActionSelection();
        lastMotionStep = new TestPlayMotionStep();
        currentAnimationIndex = 0;
        scriptIndex = 0;
        scriptTick = 0;
        frameIndex = 0;
        frameTime = 0f;
        tick = 0;
        initFired = false;
        ResetScriptRepeatState();
        abortScriptExecution = false;
        animeLoop = false;
        animationPoseHeldAtEnd = false;
        moveLocked = false;
        shieldGuard = 0;
        gvEnable = true;
        attackFlag = 0;
        hitStopTicks = 0;
        meleeGuardFeedbackTicks = 0;
        swordCancelAction = -1;
        Array.Clear(attackCooldownTicks, 0, attackCooldownTicks.Length);
        attackSequenceActive = false;
        meleeApproachActive = false;
        meleeComboInputPending = false;
        activeMeleeAttacks.Clear();
        currentCombatTraceEvents.Clear();
        pendingCombatTraceEvents.Clear();
        simulatingCombatTick = false;
        currentPresentationTraceEvents.Clear();
        pendingPresentationTraceEvents.Clear();
        simulatingPresentationTick = false;
        heldWeapon = "GUN";
        shotTurnAng = 0f;
        turnMoveAng = 0f;
        camEffect = 0f;
        vFMulti = 1f;
        moveCommand = Vector3.zero;
        scriptedMoveRetention = 1f;
        forceCommand = Vector3.zero;
        velocity = Vector3.zero;
        pendingDrivenHorizontalVelocity = Vector3.zero;
        hasPendingDrivenHorizontalVelocity = false;
        verticalFallSpeed = 0f;
        groundedFlag = false;
        groundingCollisionFlags = CollisionFlags.None;
        riseKeyHeld = false;
        previousRiseKeyHeld = false;
        lastRiseTapTick = int.MinValue;
        riseSequenceActive = false;
        landingSequenceActive = false;
        groundRecoveryElapsedTicks = 0;
        groundRecoveryDurationTicks = 0;
        groundRecoveryCompletedThisTick = false;
        deterministicGroundPlaneEnabled = false;
        stepSequenceActive = false;
        ClearStepRecovery();
        previousAirborneFlag = false;
        boostFromRiseActive = false;
        boostMotionActive = false;
        ResetBoostRuntimeState();
        moveAnimationActive = false;
        actionTick = 0;
        stepMovedDistance = 0f;
        ResetStepRuntimeState();
        airborneFlag = false;
        currentHP = 0f;
        maximumHP = 0f;
        currentEnergy = 0f;
        maximumEnergy = 0f;
        currentAuxiliaryEnergy = 0f;
        maximumAuxiliaryEnergy = 0f;
        configuredScore = 0;
        configuredRestBody = 0;
        lastSyncedMovementEnergyInt = 0;
        lastSyncedMovementEnergyFloat = 0f;
        lastSyncedAuxiliaryEnergyInt = 0;
        lastSyncedAuxiliaryEnergyFloat = 0f;
        if (attackProfile == null)
            attackProfile = new TestPlayAttackProfile();
        attackProfile.Reset();
        previousDirectionInput = 0;
        lastDirectionTap = 0;
        stepDirection = 0;
        lastDirectionTapTick = int.MinValue;
        ClearTargetLock();
        bodyUpAimRequested = false;
        bodyDownAimRequested = false;
        arm1AimRequested = false;
        arm2AimRequested = false;
        burnerRequestedOutputs.Clear();
        HideAllBurnerCones();
        ClearPoseTransition();
    }

    void RaiseRuntimeEvent(TestPlayRuntimeEventType type, string command, List<TestPlayScriptValue> args, string symbol = "", int intValue = 0, float floatValue = 0f)
    {
        Action<TestPlayRuntimeEvent> handler = RuntimeEventRaised;
        if (handler == null)
            return;

        handler(new TestPlayRuntimeEvent
        {
            type = type,
            command = command ?? "",
            symbol = symbol ?? "",
            actionIndex = currentAnimationIndex,
            scriptIndex = scriptIndex,
            tick = tick,
            intValue = intValue,
            floatValue = floatValue,
            arguments = args != null ? args.ToArray() : new TestPlayScriptValue[0]
        });
    }

    void DestroyTransientObjects()
    {
        activeSwordBeams.Clear();
        managedSwordBeam = null;
        activeThunderEffects.Clear();
        for (int i = 0; i < spawnedTransientObjects.Count; i++)
        {
            GameObject transient = spawnedTransientObjects[i];
            if (transient != null)
            {
                if (Application.isPlaying)
                    Destroy(transient);
                else
                    DestroyImmediate(transient);
            }
        }
        spawnedTransientObjects.Clear();
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
        transitionTickTotal = Mathf.Max(1, Mathf.RoundToInt(seconds / GetOriginalTickDeltaTime()));
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
            // The plume mesh extends along local Z+. Real SPT/HOD output markers
            // already encode the outward basis for both UP and DOWN; applying an
            // extra 180-degree turn to DOWN makes those burners point into/through
            // the mech. Keep TestPlay consistent with SptParser.BuildBurnerEffects.
            case SptDirection.UP: return Quaternion.identity;
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
