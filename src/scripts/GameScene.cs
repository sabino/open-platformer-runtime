using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

public partial class GameScene : Node2D
{
    private const float LevelVisualYOffset = -64.0f;
    private const int Map16TileSize = 16;
    private const int Map16AtlasColumns = 16;
    private const int PlayerOamSpriteSlots = 8;
    private const int SnesSpriteTileSize = 8;
    private const int SnesSpriteAtlasColumns = 16;
    private const float LogicalViewportWidth = 256.0f;
    private const float LogicalViewportHeight = 224.0f;
    private const float CameraHorizontalInitialFocus = 0x80;
    private const float CameraHorizontalRightFocus = 0x60;
    private const float CameraHorizontalLeftFocus = 0x90;
    private const float CameraHorizontalFocusStep = 2.0f;
    private const float CameraHorizontalBand = 12.0f;
    private const float CameraVerticalUpper = 0x64;
    private const float CameraVerticalLower = 0x7C;
    private const float CameraMaxScrollUpPerFrame = 3.0f;
    private const float CameraMaxScrollDownPerFrame = 5.0f;
    private const int SpriteActorWidth = 16;
    private const int SpriteActorHeight = 16;
    private const float SpriteActorWakeLeftMargin = 48.0f;
    private const float SpriteActorWakeRightMargin = 32.0f;
    private const float SpriteActorSleepMargin = 160.0f;
    private const float SpriteActorVerticalWakeMargin = 80.0f;
    private const float SpriteActorVerticalSleepMargin = 128.0f;
    private const int SpriteActorNativeWakeDelayFrames = 2;
    private const float SpriteActorGravity = 0.42f;
    private const float SpriteActorMaxFall = 4.0f;
    private const float RexStompMinimumTopPenetration = 5.0f;
    private const float BigRexStompMinimumTopPenetration = 3.0f;
    private const float SquishedRexStompMinimumTopPenetration = 8.0f;
    private const float BigSquishedRexStompMinimumTopPenetration = 9.0f;
    private const float PostBanzaiSquishedRexStompMinimumTopPenetration = 3.0f;
    private const float PostBanzaiRexHorizontalStompSlack = 0.5f;
    private const float BanzaiBillStompMinimumTopPenetration = 2.5f;
    private const float CarriedShellBanzaiTopBandSlack = 0.5f;
    private const int NativeRexInteractionCooldownFrames = 7;
    private const int NativeRexPostStompMotionFreezeFrames = 12;
    private const float NativeKickedShellXSpeed = 0.79f;
    private const int NativeTossedShellUpState = 0x09;
    private const int NativeKickedShellState = 0x0A;
    private const int NativeCarriedShellState = 0x0B;
    private const float NativeCarriedShellXOffset = 9.0f;
    private const float NativeCarriedShellYOffset = 13.0f;
    private const float NativeShellCarryMinimumOverlapX = 3.0f;
    private const float NativeTossedShellUpYSpeed = -7.0f;
    private const float NativeTossedShellGravity = 3.0f / 16.0f;
    private const int DefaultSpriteStompYSpeed = -48;
    private const int NativeSpriteStompYSpeed = -88;
    private const int NativeHeldJumpGravity = 3;
    private const float SlidingKoopaGroundDeceleration = 0.03125f;
    private const int NativeLevelStartSpriteWarmupFrames = 22;
    private const int NativePlayerHurtAnimationFrames = 0x2F;
    private const int NativePlayerPostPowerdownInvulnerabilityFrames = 0x7F;
    private const int NativePlayerHurtBlinkFrameShift = 2;
    private const int NativeSpinTurnBlockBreakYSpeed = -42;
    private const int NativeSpinTurnBlockBreakYOffset = 1;
    private const int NativeSpinTurnBlockSideFallbackMinYSpeed = 0x30;
    private const int NativeSpinTurnBlockSideFallbackXOffset = 1;
    private const float NativePostSpinPipeCornerRestInset = 2.625f;
    private const int NativePipeTransitionDelayFrames = 33;
    private const int NativeVerticalPipeExitHoldFrames = 30;
    private const int NativePipeExitPassThroughFrames = 24;
    private const int NativeVerticalPipeExitReleaseYOffset = -1;
    private const int NativeHorizontalPipeEntryFrames = 58;
    private const int NativeHorizontalPipeTransitionDelayFrames = 34;
    private const int NativeHorizontalPipeEntrySettleFrame = NativeHorizontalPipeEntryFrames - 1;
    private const int NativePipeExitJumpSubYFrames = 180;
    private const int NativePipeExitJumpTakeoffSubY = 0xE0;
    private const int NativeGroundedYSubDelta = 0x60;
    private const int NativeNormalCoinPickupCooldownFrames = 17;
    private const int NativeUpperCoinPickupCooldownFrames = 7;
    private const float NativeUnderworldRightWallAirContactX = 370.4375f;
    private const int JumpingPiranhaCycleFrames = 192;
    private const int JumpingPiranhaHiddenFrames = 48;
    private const int JumpingPiranhaRiseFrames = 24;
    private const int JumpingPiranhaExtendedFrames = 48;
    private const int JumpingPiranhaFallFrames = 24;
    private const float JumpingPiranhaTravelPixels = 32.0f;
    private const int WingedQuestionBlockCycleFrames = 64;
    private const float SolidBlockActorSideProbeDepth = 4.0f;
    private const float SolidBlockActorSideSnapInset = 1.5625f;
    private const int SolidBlockActorSideCooldownFrames = 12;
    private const float ExtendedQuestionBlockHitPenetrationPixels = 8.75f;
    private const int PowerupItemEmergingState = 1;
    private const int PowerupItemActiveState = 2;
    private const int PowerupItemEmergingFrames = 64;
    private const int PowerupItemEmergingCollectFrame = 56;
    private const int NativePowerupAnimationFrames = 32;
    private const int NativeGrowthPowerupAnimationFrames = 47;
    private const int NativePowerupSettleFrames = 1;
    private const float PowerupItemEmergingPixels = 16.0f;
    private const float PowerupItemCollectionMinOverlapX = 3.25f;
    private const float PowerupItemWalkSpeed = 1.0f;
    private const int InvisibleMushroomRevealCooldownFrames = 32;
    private const float InvisibleMushroomRevealYOffset = -15.0f;
    private const float InvisibleMushroomRevealYSpeed = -4.0f;
    private const int GoalTapeSpriteId = 0x7B;
    private const int GoalTapeCycleFrames = 124;
    private const float GoalTapeDownSpeed = 1.0f;
    private const float GoalTapeUpSpeed = -1.0f;
    private const int CourseClearWalkoutMaxFrames = 431;
    private const int CourseClearAirborneWalkoutIntegrationXSpeed = 4;
    private const int CourseClearGroundedWalkoutIntegrationXSpeed = 5;
    private const int CourseClearGoalCoinAwardFrame = 56;
    private const int CourseClearPostWalkPauseFrames = 64;
    private const int CourseClearExitTransitionFrame = 60;
    private const int CourseClearExitTransitionX = 13;
    private const int CourseClearExitTransitionY = 65438;
    private const int CourseClearExitVisibleYFrame = 318;
    private const int CourseClearExitVisibleY = 150;
    private const int CourseClearExitFinalYFrame = 433;
    private const int CourseClearExitFinalY = 94;
    private const int DefaultPlayerPowerup = SmwPhysics.SmallPowerup;
    private const int StartingLives = 5;
    private const int MaxLives = 99;
    private const int DefaultLevelTimerSeconds = 300;
    private const int NativeFramesPerSecond = 60;
    private const int CoinScore = 100;
    private const int DragonCoinScore = 1000;
    private const int PowerupRewardScore = 1000;
    private const int NativeStarPowerTimerInitial = 0xFF;
    private const int DebugCheckpointSchemaVersion = 1;
    private const string DebugCheckpointDirectory = "generated/smw/checkpoints";
    private const int CoinLifeThreshold = 100;
    private const int DragonCoinLifeThreshold = 5;
    private const int MaxPlayerFireballs = 2;
    private const int FireballShootPoseFrames = 10;
    private const int FireballInitialYSpeed = 48;
    private const int FireballGravity = 4;
    private const int FireballMaxYSpeed = 48;
    private const int FireballBounceLimit = 2;
    private const float FireballXSpeed = 3.0f;
    private const float FireballLevelCollisionProbe = 4.0f;
    private const int AutoplayExploreJumpPeriod = 48;
    private const int AutoplayExploreJumpHeldFrames = 8;
    private const int AutoplayExploreStuckJumpThreshold = 75;
    private const int AutoplayExploreStuckJumpPeriod = 40;
    private const int AutoplayExploreStuckJumpHeldFrames = 12;
    private const float AutoplayExploreActorJumpAheadPixels = 96.0f;
    private const float AutoplayExploreActorJumpBehindPixels = 8.0f;
    private const float AutoplayExploreActorVerticalRangePixels = 72.0f;
    private const float AutoplayExploreActorPeriodicSuppressAheadPixels = 128.0f;
    private const float AutoplayExploreActorPeriodicSuppressBehindPixels = 16.0f;
    private const float AutoplayExploreActorAirBrakeAheadPixels = 96.0f;
    private const float AutoplayExploreActorAirBrakeBehindPixels = 24.0f;
    private const float AutoplayExploreStompableAirBrakeAheadPixels = 48.0f;
    private const float AutoplayExploreStompableAirBrakeBehindPixels = 16.0f;
    private const float AutoplayExploreStompableAirBrakeBodyOverlapPixels = 8.0f;
    private const float AutoplayExploreActorDuckAheadPixels = 160.0f;
    private const float AutoplayExploreActorDuckBehindPixels = 24.0f;
    private const float AutoplayExploreTerrainJumpAheadPixels = 24.0f;
    private const float AutoplayExploreTerrainJumpBehindPixels = 4.0f;
    private const float AutoplayExploreTerrainJumpMaxHeightPixels = 32.0f;
    private static readonly int[] StompScoreByNativeGivePointsIndex = [100, 200, 400, 800, 1000, 2000, 4000, 8000];
    private static readonly int[] FireballBounceYSpeedBySlopeType = [0, -72, -64, -56, -48, -40, -32, -24, -16];
    private static readonly int[] CourseClearExitAdjustedYByFrame404 =
    [
        118, 117, 116, 116, 115, 114, 113, 112, 112, 111,
        110, 109, 108, 107, 107, 106, 105, 104, 103, 103,
        102, 101, 100, 99, 99, 98, 97, 96, 95,
    ];
    private const float FallDeathMarginPixels = 96.0f;
    private static readonly int[] SpriteAtlasTileStartByLmuBank = [0, 128, 256, 384];
    private static readonly int[] LoadLevelYLowTable =
    [
        0x00, 0x30, 0x60, 0x80, 0xA0, 0xB0, 0xC0, 0xE0,
        0x10, 0x30, 0x50, 0x60, 0x70, 0x90, 0x00, 0x00,
    ];
    private static readonly int[] LoadLevelYHighTable =
    [
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
    ];
    private static readonly int[] LoadLevelXLowTable =
    [
        0x10, 0x80, 0x00, 0xE0, 0x10, 0x70, 0x00, 0xE0,
    ];
    private static readonly int[] LoadLevelXHighTable =
    [
        0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x01, 0x01,
    ];
    private static readonly int[] NativePlayerAnimationSpeedFallback =
    [
        0x0A, 0x08, 0x06, 0x04, 0x03, 0x02, 0x01, 0x01,
        0x0A, 0x08, 0x06, 0x04, 0x03, 0x02, 0x01, 0x01,
        0x0A, 0x08, 0x06, 0x04, 0x03, 0x02, 0x01, 0x01,
        0x08, 0x06, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01,
        0x08, 0x06, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x05, 0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01,
        0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x04, 0x03, 0x02, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
    ];
    private static readonly int[] NativeSpinJumpPoseTable =
    [
        0x00, 0x00, 0x25, 0x44, 0x00, 0x00, 0x0F, 0x45,
    ];
    private static readonly int[] NativeSpinJumpFacingTable =
    [
        0, 0, 0, 0, 1, 1, 1, 1,
    ];

    private readonly SmwPhysics _physics = new();
    private readonly List<Rect2> _solids = [];
    private readonly List<bool> _solidStepUpEnabled = [];
    private readonly List<bool> _solidVerticalEnabled = [];
    private readonly List<int> _solidSupportModes = [];
    private readonly List<Rect2> _frameSolids = [];
    private readonly List<bool> _frameSolidStepUpEnabled = [];
    private readonly List<bool> _frameSolidVerticalEnabled = [];
    private readonly List<int> _frameSolidSupportModes = [];
    private readonly List<SmwPhysics.SlopeSurface> _slopes = [];
    private readonly List<SmwPhysics.SlopeSurface> _spriteSlopes = [];
    private readonly List<Godot.Collections.Dictionary> _screenExits = [];
    private readonly List<Godot.Collections.Dictionary> _levelObjects = [];
    private readonly List<Godot.Collections.Dictionary> _layer2Objects = [];
    private readonly List<SpriteSpawn> _levelSprites = [];
    private readonly List<PlacedMap16Tile> _placedTiles = [];
    private readonly List<PlacedMap16Tile> _rawPlacedTiles = [];
    private readonly List<PipeEntrance> _pipeEntrances = [];
    private readonly List<int> _headTilePointers = [];
    private readonly List<int> _bodyTilePointers = [];
    private readonly List<int> _playerWalkingPoseCounts = [];
    private readonly List<int> _playerAnimationSpeedTable = [];
    private readonly List<int> _playerXYDispIndexIndex = [];
    private readonly List<int> _playerXYDispIndex = [];
    private readonly List<int> _playerXDisp = [];
    private readonly List<int> _playerYDisp = [];
    private readonly List<int> _playerPowerupTilesetIndex = [];
    private readonly List<int> _playerTileDescriptors = [];
    private readonly List<int> _playerTilesIndex = [];
    private readonly List<int> _playerTileXFlip = [];
    private readonly List<Sprite2D> _playerTileSprites = [];
    private readonly List<RuntimeSpriteActor> _spriteActors = [];
    private readonly List<PlayerFireball> _playerFireballs = [];
    private readonly List<ScriptedInputSegment> _inputScript = [];
    private readonly List<Rect2> _goalTapeTriggers = [];
    private readonly List<GoalTapeRuntime> _goalTapes = [];
    private readonly List<CoinPickup> _coinPickups = [];
    private readonly Dictionary<(int X, int Y), PlacedMap16Tile> _map16TilesByCoord = [];
    private readonly HashSet<(int X, int Y)> _diagonalPipeFloorCells = [];
    private readonly HashSet<(int X, int Y)> _diagonalPipeBodyCells = [];
    private readonly HashSet<(int X, int Y)> _diagonalPipeCeilingCells = [];
    private readonly Dictionary<string, DebugCheckpointData> _debugCheckpoints = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions DebugCheckpointJsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    private SmwPhysics.PlayerState _state;
    private Node2D? _player;
    private Line2D? _playerHitboxGizmo;
    private ColorRect? _playerFootGizmo;
    private readonly List<ColorRect> _playerSensorGizmos = [];
    private Label? _playerDebugLabel;
    private Line2D? _cameraGizmo;
    private Label? _statusHud;
    private Label? _hud;
    private Label? _courseClearLabel;
    private CanvasLayer? _hudLayer;
    private CanvasLayer? _courseClearLayer;
    private Node2D? _worldRoot;
    private Label? _pauseLabel;
    private Label? _gameOverLabel;
    private SmwAudio? _audio;
    private readonly ImageTexture?[] _playerTextures = new ImageTexture?[4];
    private ImageTexture? _spriteTexture;
    private readonly ImageTexture?[] _spritePaletteTextures = new ImageTexture?[8];
    private ImageTexture? _map16Texture;
    private Map16TileLayer? _map16Layer;
    private Godot.Collections.Dictionary? _entranceTables;
    private string _currentLevelId = "105";
    private string _levelGfxAtlasPath = "res://generated/smw/tilesets/level_105_tileset7_8x8.png";
    private string _levelMap16AtlasPath = "res://generated/smw/tilesets/level_105_tileset7_map16_preview.png";
    private string _levelSpriteAtlasPath = "res://generated/smw/spritesets/level_105_spritegfx8_8x8.png";
    private string _levelSpriteVramPath = "res://generated/smw/spritesets/level_105_spritegfx8_vram.bin";
    private string _levelPalettePath = "res://generated/smw/palettes/level_105_palette.json";
    private string _levelLayoutPreviewPath = "res://generated/smw/levels/level_105_partial_layout.png";
    private string _levelTilemapPath = "res://generated/smw/levels/level_105_partial_tilemap.json";
    private string _levelLayer2BackgroundPath = "res://generated/smw/levels/level_105_layer2_background.png";
    private int _currentLevelTileset = 7;
    private int _currentLevelMusicIndex;
    private string _currentLevelMusicPreview = "Level";
    private float _cameraX;
    private float _cameraY;
    private float _cameraHorizontalFocus = CameraHorizontalInitialFocus;
    private bool _cameraInitialized;
    private bool _debugCameraLocked;
    private Vector2 _debugCameraLockPosition;
    private int _lastPlayerPose = -1;
    private int _lastPlayerFacing = -1;
    private int _lastPlayerPowerup = -1;
    private bool _lastPlayerDucking;
    private bool _lastPlayerBlinkHidden;
    private bool _pipeTransitionLatch;
    private int _playerHurtCooldown;
    private string _lastActorEvent = "none";
    private string _lastActorContact = "none";
    private bool _courseClear;
    private bool _courseClearGoalCoinAwarded;
    private bool _gamePaused;
    private bool _gameOver;
    private string? _queuedPlayerDeathCause;
    private string _queuedPlayerDeathEvent = "death:hurt";
    private int _courseClearWalkoutFrames;
    private int _courseClearPostWalkPauseFrames = -1;
    private int _courseClearExitWalkFrames;
    private int _courseClearExitTransitionFrames;
    private bool _courseClearInitialWalkoutInputFrame;
    private bool _courseClearExitTransition;
    private int _entranceMotionFrames;
    private int _entranceMotionAction;
    private Vector2 _entranceMotionPixelsPerFrame;
    private int _entranceMotionDelayFrames;
    private int _entranceReleaseHoldFrames;
    private int _deferredEntranceMotionFrames;
    private Vector2 _deferredEntranceMotionPixelsPerFrame;
    private int _pipeExitPassThroughFrames;
    private int _pipeExitJumpSubYFrames;
    private int _pipeExitSyntheticGroundSubY = -1;
    private bool _pipeEntryHorizontal;
    private int _pipeEntryInitialFrames;
    private Vector2 _pipeEntryPixelsPerFrame;
    private int _pipeTransitionDelayAfterEntryFrames;
    private int _postPipeShootoutPMeterFloorFrames;
    private int _coinCount;
    private int _dragonCoinCount;
    private int _pendingNormalCoinIncrements;
    private int _pendingDragonCoinNormalCoins;
    private int _normalCoinPickupCooldownFrames;
    private int _oneUpCount;
    private int _score;
    private int _stompChainCounter;
    private int _starPowerTimer;
    private int _fireballShootPoseTimer;
    private int _spinJumpFireballTimer;
    private int _lives = StartingLives;
    private int _levelTimerFrames = DefaultLevelTimerSeconds * NativeFramesPerSecond;
    private int _blockBreakCount;
    private int _deathCount;
    private int _powerupAnimationFrames;
    private int _powerupSettleFrames;
    private int _pendingPowerup = -1;
    private bool _suppressNextPipeCornerLeft;
    private int _pipeEntryMotionFrames;
    private int _pipeTransitionDelayFrames;
    private LevelEntrance? _pendingPipeTransitionEntrance;
    private int _pendingEntrancePowerup = -1;
    private int _inputScriptIndex;
    private int _inputScriptFrame;
    private int _inputScriptElapsedFrames;
    private string _inputScriptName = "";
    private bool _inputScriptDoneLogged;
    private SmwPhysics.FrameInput _lastFrameInput;
    private int _playerWalkingFrame;
    private int _playerAnimTimer;
    private string? _debugCommandPath;
    private long _debugCommandOffset;
    private bool _debugPaused;
    private int _debugStepFrames;
    private int _debugFrameCounter;
    private int _debugCommandInputFrames;
    private int _debugCommandInputFrame;
    private SmwPhysics.FrameInput _debugCommandInput;
    private SmwPhysics.FrameInput _debugHeldInput;
    private bool _debugHeldInputActive;
    private bool _debugHeldJumpPressed;
    private bool _debugHeldSpinPressed;
    private bool _debugHeldRunPressed;
    private float _debugMaxPlayerX;
    private DebugAutoplayMode _autoplayMode;
    private int _autoplayFrame;
    private int _autoplayLastPlayerX;
    private int _autoplayStuckFrames;
    private bool _autoplayJumpHeld;
    private int _debugTraceFrames;
    private int _debugTraceTotalFrames;
    private int _debugTraceFrame;
    private string _debugTraceTag = "trace";
    private bool _debugTraceOam;
    private bool _debugTraceSensors;
    private bool _debugTraceQuitWhenDone;
    private int _debugTraceCheckpointFrame = -1;
    private string _debugTraceCheckpointSlot = "";
    private bool _debugTraceCheckpointFile;
    private bool _debugActorsEnabled = true;
    private bool _debugActorVisualsEnabled = true;
    private bool _debugInvincible;
    private TcpListener? _debugRconListener;
    private readonly List<TcpClient> _debugRconClients = [];
    private readonly Dictionary<TcpClient, StringBuilder> _debugRconBuffers = [];
    private readonly byte[] _debugRconReadBuffer = new byte[4096];

    public bool DebugOverlays { get; set; }
    public bool ActorsEnabled { get; set; } = true;
    public bool ActorVisualsEnabled { get; set; } = true;
    public SmwAudio? Audio { get; set; }
    public bool AudioEnabled { get; set; } = true;

    public override void _Ready()
    {
        GetViewport().TransparentBg = false;
        RenderingServer.SetDefaultClearColor(new Color(0.0f, 0.39f, 0.74f, 1.0f));
        _debugActorsEnabled = ActorsEnabled;
        _debugActorVisualsEnabled = ActorVisualsEnabled;
        _audio = Audio;
        if (_audio == null && AudioEnabled)
        {
            _audio = new SmwAudio { Name = "SmwAudio" };
            AddChild(_audio);
        }
        LoadAssetPack();
        _state = MakeInitialPlayerState();
        ResetPowerupAnimationState();
        ResetDebugMaxPlayerX();
        ResetPlayerAnimationState();
        BuildWorld();
        BuildPlayer();
        BuildHud();
        PrintRuntimeState();
        StartLevelMusic();
    }

    public override void _PhysicsProcess(double delta)
    {
        PollDebugCommands();
        PollDebugRcon();
        var isDebugStep = _debugStepFrames > 0;
        if (_gameOver)
        {
            HandleGameOverFrame(isDebugStep);
            return;
        }

        if (!_gamePaused && !_courseClear && Input.IsActionJustPressed("smw_start"))
        {
            ToggleGameplayPause("start");
        }

        if (_debugPaused && _debugStepFrames <= 0)
        {
            UpdateHud();
            UpdateDebugGizmos();
            return;
        }

        if (_gamePaused)
        {
            HandleGameplayPauseFrame(isDebugStep);
            return;
        }

        var frameInput = _courseClear
            ? ReadCourseClearInput()
            : ReadFrameInput();
        if (_suppressNextPipeCornerLeft && frameInput.Left)
        {
            frameInput.Left = false;
            frameInput.RunPressed = false;
            _suppressNextPipeCornerLeft = false;
        }
        else
        {
            _suppressNextPipeCornerLeft = false;
        }
        _lastFrameInput = frameInput;
        ReleaseCarriedShellsIfNeeded(frameInput);
        if (_powerupAnimationFrames > 0)
        {
            HandlePowerupAnimationFrame(frameInput, isDebugStep);
            return;
        }
        if (_powerupSettleFrames > 0)
        {
            HandlePowerupSettleFrame(frameInput, isDebugStep);
            return;
        }

        var previousStateForActors = _state;

        var entranceLocked = _entranceMotionFrames > 0 ||
            _entranceMotionDelayFrames > 0 ||
            _entranceReleaseHoldFrames > 0 ||
            _pipeEntryMotionFrames > 0 ||
            _pipeTransitionDelayFrames > 0;
        if (!entranceLocked && _state.OnGround && frameInput.SpinPressed)
        {
            _audio?.PlaySpinJump();
        }
        else if (!entranceLocked && _state.OnGround && frameInput.JumpPressed)
        {
            _audio?.PlayJump();
        }

        if (_pipeEntryMotionFrames > 0)
        {
            ApplyPipeEntryMotion();
        }
        else if (_pipeTransitionDelayFrames > 0)
        {
            ApplyPipeTransitionDelay();
        }
        else if (_entranceMotionFrames > 0)
        {
            ApplyEntranceMotion();
        }
        else if (_entranceMotionDelayFrames > 0)
        {
            ApplyEntranceMotionDelay();
        }
        else if (_entranceReleaseHoldFrames > 0)
        {
            ApplyEntranceReleaseHold();
        }
        else if (_courseClearExitTransition)
        {
            ApplyCourseClearExitTransitionFrame();
        }
        else
        {
            var previousState = _state;
            var solids = _solids;
            var solidStepUpEnabled = _solidStepUpEnabled;
            var solidVerticalEnabled = _solidVerticalEnabled;
            var solidSupportModes = _solidSupportModes;
            if (_pipeExitPassThroughFrames > 0)
            {
                BuildPipeExitFilteredSolids();
                solids = _frameSolids;
                solidStepUpEnabled = _frameSolidStepUpEnabled;
                solidVerticalEnabled = _frameSolidVerticalEnabled;
                solidSupportModes = _frameSolidSupportModes;
            }

            ApplyCourseClearWalkoutIntegrationSpeed();
            _physics.Step(
                ref _state,
                frameInput,
                solids,
                solidStepUpEnabled,
                solidVerticalEnabled,
                solidSupportModes,
                _slopes,
                0,
                (int)MathF.Round(GetLevelPixelRight()));
            ApplyCourseClearWalkoutSpeedCap();
            UpdateCourseClearPostWalkPhase();
            ApplyPostPipeShootoutPMeterFloor(frameInput);
            ApplyPipeExitGroundSubpixelCarry(previousState, frameInput);
            ClampUnderworldRightWallAirContact(previousState);
            if (_pipeExitPassThroughFrames > 0)
            {
                _pipeExitPassThroughFrames--;
            }
            ResolveDiagonalPipeTileContacts(previousState);
            TryBreakSpinJumpTurnBlocks(previousState);
            TryHitStaticBlockFromBelow(previousState);
            ClampPostSpinPipeCornerContact();
            if (TryHandlePlayerFallDeath())
            {
                previousStateForActors = _state;
            }
        }
        UpdateCamera();

        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }

        TrySpawnPlayerFireball(frameInput);
        UpdatePlayerGraphic();
        if (UpdateSpriteActors(previousStateForActors))
        {
            if (_player != null)
            {
                _player.Position = PlayerRenderPosition();
            }
            UpdatePlayerGraphic(force: true);
        }
        UpdateDebugMaxPlayerX();
        TryHandleQueuedPlayerDeath();
        UpdatePlayerFireballs();
        UpdateGoalTapes();
        CheckCoinPickups();
        CheckGoalTape();
        ApplyCourseClearGoalCoinAward();
        ResetStompChainIfGrounded();
        TickStarPowerTimer();
        TickLevelTimer();
        UpdateHud();
        UpdateDebugGizmos();
        if (!entranceLocked && !_courseClear)
        {
            CheckPipeDebug(frameInput);
        }
        PrintQueuedDebugTrace(frameInput);

        _debugFrameCounter++;
        if (isDebugStep)
        {
            _debugStepFrames--;
            if (_debugStepFrames <= 0 && _debugPaused)
            {
                PrintDebugState("step_done");
            }
        }
    }

    private void ApplyPipeExitGroundSubpixelCarry(
        SmwPhysics.PlayerState previousState,
        SmwPhysics.FrameInput frameInput)
    {
        if (_pipeExitJumpSubYFrames <= 0)
        {
            return;
        }

        var jumpStarted = previousState.OnGround &&
            !_state.OnGround &&
            (frameInput.JumpPressed || frameInput.SpinPressed) &&
            _state.InAirState is SmwPhysics.NativeNormalJumpInAirState or SmwPhysics.NativeRunningJumpInAirState;
        if (jumpStarted)
        {
            var takeoffSubY = _pipeExitSyntheticGroundSubY >= 0
                ? (_pipeExitSyntheticGroundSubY + NativeGroundedYSubDelta) & 0xFF
                : NativePipeExitJumpTakeoffSubY;
            _state.SubY = takeoffSubY;
            _pipeExitSyntheticGroundSubY = takeoffSubY;
            _pipeExitJumpSubYFrames--;
            return;
        }

        if (!previousState.OnGround && _state.OnGround)
        {
            var landingSubY = (previousState.SubY + ((previousState.YSpeed * 16) & 0xFF)) & 0xFF;
            _state.SubY = landingSubY;
            _pipeExitSyntheticGroundSubY = landingSubY;
            _pipeExitJumpSubYFrames--;
            return;
        }

        if (_state.OnGround)
        {
            if (_pipeExitSyntheticGroundSubY >= 0)
            {
                _pipeExitSyntheticGroundSubY = (_pipeExitSyntheticGroundSubY + NativeGroundedYSubDelta) & 0xFF;
                _state.SubY = _pipeExitSyntheticGroundSubY;
            }
            _pipeExitJumpSubYFrames--;
            return;
        }

        _pipeExitJumpSubYFrames--;
        if (_pipeExitJumpSubYFrames <= 0)
        {
            _pipeExitSyntheticGroundSubY = -1;
        }
    }

    private void ClampUnderworldRightWallAirContact(SmwPhysics.PlayerState previousState)
    {
        if (_currentLevelId != "1CB" ||
            previousState.XSpeed < 0x20 ||
            _state.XSpeed != 1 ||
            _state.OnGround ||
            _state.XFloat is < 370.75f or > 371.25f ||
            _state.YFloat is < 235.0f or > 237.0f)
        {
            return;
        }

        SetPlayerXFloat(NativeUnderworldRightWallAirContactX);
    }

    private void HandleGameplayPauseFrame(bool isDebugStep)
    {
        if (Input.IsActionJustPressed("smw_start"))
        {
            ToggleGameplayPause("start");
            return;
        }

        UpdateHud();
        UpdateDebugGizmos();
        PrintQueuedDebugTrace(new SmwPhysics.FrameInput());

        _debugFrameCounter++;
        if (isDebugStep)
        {
            _debugStepFrames--;
            if (_debugStepFrames <= 0 && _debugPaused)
            {
                PrintDebugState("step_done");
            }
        }
    }

    private void HandleGameOverFrame(bool isDebugStep)
    {
        if (Input.IsActionJustPressed("smw_start") || Input.IsActionJustPressed("smw_jump"))
        {
            ContinueAfterGameOver();
        }

        UpdateHud();
        UpdateDebugGizmos();
        PrintQueuedDebugTrace(new SmwPhysics.FrameInput());

        _debugFrameCounter++;
        if (isDebugStep)
        {
            _debugStepFrames--;
            if (_debugStepFrames <= 0 && _debugPaused)
            {
                PrintDebugState("step_done");
            }
        }
    }

    private void HandlePowerupAnimationFrame(SmwPhysics.FrameInput frameInput, bool isDebugStep)
    {
        UpdateCamera();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }
        UpdatePlayerGraphic();
        UpdateHud();
        UpdateDebugGizmos();
        PrintQueuedDebugTrace(frameInput);

        _debugFrameCounter++;
        _powerupAnimationFrames--;
        if (_powerupAnimationFrames <= 0 && _pendingPowerup >= 0)
        {
            ApplyPendingPowerup();
            _powerupSettleFrames = NativePowerupSettleFrames;
        }

        if (isDebugStep)
        {
            _debugStepFrames--;
            if (_debugStepFrames <= 0 && _debugPaused)
            {
                PrintDebugState("step_done");
            }
        }
    }

    private void HandlePowerupSettleFrame(SmwPhysics.FrameInput frameInput, bool isDebugStep)
    {
        UpdateCamera();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }
        UpdatePlayerGraphic();
        UpdateHud();
        UpdateDebugGizmos();
        PrintQueuedDebugTrace(frameInput);

        _debugFrameCounter++;
        _powerupSettleFrames--;
        if (isDebugStep)
        {
            _debugStepFrames--;
            if (_debugStepFrames <= 0 && _debugPaused)
            {
                PrintDebugState("step_done");
            }
        }
    }

    private void ApplyPendingPowerup()
    {
        var powerup = Math.Clamp(_pendingPowerup, SmwPhysics.SmallPowerup, SmwPhysics.FirePowerup);
        _pendingPowerup = -1;
        ApplyPowerupState(powerup);
        _state.X += 2;
        _state.XSpeed = 0;
        _state.SubXSpeed = 0;
    }

    private void ApplyPowerupState(int powerup)
    {
        _state.Powerup = powerup;
        if (_state.Powerup == SmwPhysics.SmallPowerup)
        {
            _state.Ducking = false;
        }
        if (_state.Powerup != SmwPhysics.CapePowerup)
        {
            _state.CapeFloatFrames = 0;
        }

        _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
        UpdatePlayerGraphic(force: true);
    }

    private void ResetPowerupAnimationState()
    {
        _powerupAnimationFrames = 0;
        _powerupSettleFrames = 0;
        _pendingPowerup = -1;
    }

    private readonly record struct PlacedMap16Tile(int X, int Y, int Map16, string Source);
    private readonly record struct SpriteSpawn(int X, int Y, int Screen, int SpriteId, int ExtraBits, int Offset);
    private readonly record struct SpriteOamTile(int Dx, int Dy, int Tile, int Prop, int Bank, bool Large);
    private readonly record struct SpriteActorBehavior(
        Rect2 Hitbox,
        bool CanInteract,
        bool Stompable,
        bool TerrainCollision,
        bool Gravity,
        float InitialXSpeed,
        Rect2? TerrainHitbox = null);
    private readonly record struct PipeEntrance(
        Rect2 Rect,
        int Screen,
        bool Horizontal,
        string Kind,
        int SourceX,
        int SourceY,
        string Source);
    private readonly record struct PipeEntranceCandidate(
        int Score,
        int ObjectId,
        int Screen,
        int TileX,
        int TileY);
    private readonly record struct ScriptedInputSegment(int Frames, SmwPhysics.FrameInput Input);
    private readonly record struct LevelEntrance(
        string LevelId,
        Vector2 Position,
        int EntranceSettings,
        bool Secondary,
        int SourceId);

    private sealed class DebugCheckpointData
    {
        public int Version = DebugCheckpointSchemaVersion;
        public string LevelId = "105";
        public SmwPhysics.PlayerState State;
        public int Frame;
        public float CameraX;
        public float CameraY;
        public float CameraHorizontalFocus;
        public bool CameraInitialized;
        public bool DebugCameraLocked;
        public float DebugCameraLockX;
        public float DebugCameraLockY;
        public int LastPlayerPose;
        public int LastPlayerFacing;
        public int LastPlayerPowerup;
        public bool LastPlayerDucking;
        public bool LastPlayerBlinkHidden;
        public bool CourseClear;
        public bool CourseClearGoalCoinAwarded;
        public bool GamePaused;
        public bool GameOver;
        public string? QueuedPlayerDeathCause;
        public string QueuedPlayerDeathEvent = "death:hurt";
        public int CourseClearWalkoutFrames;
        public int CourseClearPostWalkPauseFrames;
        public int CourseClearExitWalkFrames;
        public int CourseClearExitTransitionFrames;
        public bool CourseClearInitialWalkoutInputFrame;
        public bool CourseClearExitTransition;
        public int EntranceMotionFrames;
        public int EntranceMotionAction;
        public float EntranceMotionX;
        public float EntranceMotionY;
        public int EntranceMotionDelayFrames;
        public int EntranceReleaseHoldFrames;
        public int DeferredEntranceMotionFrames;
        public float DeferredEntranceMotionX;
        public float DeferredEntranceMotionY;
        public int PipeExitPassThroughFrames;
        public int PipeExitJumpSubYFrames;
        public int PipeExitSyntheticGroundSubY;
        public bool PipeEntryHorizontal;
        public int PipeEntryInitialFrames;
        public float PipeEntryMotionX;
        public float PipeEntryMotionY;
        public int PipeTransitionDelayAfterEntryFrames;
        public int PostPipeShootoutPMeterFloorFrames;
        public int CoinCount;
        public int DragonCoinCount;
        public int PendingNormalCoinIncrements;
        public int PendingDragonCoinNormalCoins;
        public int NormalCoinPickupCooldownFrames;
        public int OneUpCount;
        public int Score;
        public int StompChainCounter;
        public int StarPowerTimer;
        public int FireballShootPoseTimer;
        public int SpinJumpFireballTimer;
        public int Lives;
        public int LevelTimerFrames;
        public int BlockBreakCount;
        public int DeathCount;
        public int PowerupAnimationFrames;
        public int PowerupSettleFrames;
        public int PendingPowerup;
        public bool SuppressNextPipeCornerLeft;
        public int PipeEntryMotionFrames;
        public int PipeTransitionDelayFrames;
        public LevelEntranceCheckpoint? PendingPipeTransitionEntrance;
        public int PendingEntrancePowerup;
        public int InputScriptIndex;
        public int InputScriptFrame;
        public int InputScriptElapsedFrames;
        public string InputScriptName = "";
        public bool InputScriptDoneLogged;
        public SmwPhysics.FrameInput LastFrameInput;
        public int PlayerWalkingFrame;
        public int PlayerAnimTimer;
        public int PlayerHurtCooldown;
        public float DebugMaxPlayerX;
        public int AutoplayFrame;
        public int AutoplayLastPlayerX;
        public int AutoplayStuckFrames;
        public bool AutoplayJumpHeld;
        public string LastActorEvent = "none";
        public string LastActorContact = "none";
        public List<TileCheckpoint> PlacedTiles = [];
        public List<ActorCheckpoint> Actors = [];
        public List<FireballCheckpoint> Fireballs = [];
        public List<GoalTapeCheckpoint> GoalTapes = [];
        public List<bool> CoinPickupCollected = [];
    }

    private sealed class LevelEntranceCheckpoint
    {
        public string LevelId = "";
        public float X;
        public float Y;
        public int EntranceSettings;
        public bool Secondary;
        public int SourceId;
    }

    private sealed class TileCheckpoint
    {
        public int X;
        public int Y;
        public int Map16;
        public string Source = "";
    }

    private sealed class ActorCheckpoint
    {
        public int SpriteId;
        public float X;
        public float Y;
        public float PreviousX;
        public float PreviousY;
        public float HomeY;
        public float XSpeed;
        public float YSpeed;
        public int MotionFrame;
        public int InteractionCooldownFrames;
        public int MotionFreezeFrames;
        public int SolidSideCooldownFrames;
        public bool Used;
        public bool Alive;
        public bool Active;
        public bool AlwaysActive;
        public bool OnGround;
        public int WakeDelayFrames;
        public int WakeScreen;
        public int ContentIndex;
        public int SpawnOffset;
        public int State;
    }

    private sealed class FireballCheckpoint
    {
        public float X;
        public float Y;
        public float XSpeed;
        public int YSpeed;
        public int SubY;
        public int MotionFrame;
        public int BounceCount;
        public bool Alive;
    }

    private sealed class GoalTapeCheckpoint
    {
        public float Y;
        public int Timer;
        public int Direction;
    }

    private enum DebugAutoplayMode
    {
        Off,
        TitleStart,
        Explore,
    }

    private sealed class RuntimeSpriteActor
    {
        public required Node2D Node { get; init; }
        public required ColorRect Body { get; init; }
        public int SpriteId { get; init; }
        public float X { get; set; }
        public float Y { get; set; }
        public float PreviousX { get; set; }
        public float PreviousY { get; set; }
        public float HomeY { get; init; }
        public float XSpeed { get; set; }
        public float YSpeed { get; set; }
        public int MotionFrame { get; set; }
        public int InteractionCooldownFrames { get; set; }
        public int MotionFreezeFrames { get; set; }
        public int SolidSideCooldownFrames { get; set; }
        public bool Used { get; set; }
        public bool Alive { get; set; } = true;
        public bool Active { get; set; }
        public bool AlwaysActive { get; init; }
        public bool OnGround { get; set; }
        public int WakeDelayFrames { get; set; }
        public int WakeScreen { get; init; }
        public int ContentIndex { get; init; }
        public int SpawnOffset { get; init; }
        public required List<Node> Visuals { get; init; }
        public required SpriteActorBehavior Behavior { get; set; }
        public int State { get; set; }
        public Rect2 Rect => new(X + Behavior.Hitbox.Position.X, Y + Behavior.Hitbox.Position.Y, Behavior.Hitbox.Size.X, Behavior.Hitbox.Size.Y);
        public Rect2 TerrainRect => Behavior.TerrainHitbox is { } terrainHitbox
            ? new Rect2(X + terrainHitbox.Position.X, Y + terrainHitbox.Position.Y, terrainHitbox.Size.X, terrainHitbox.Size.Y)
            : Rect;
    }

    private sealed class PlayerFireball
    {
        public required Node2D Node { get; init; }
        public float X { get; set; }
        public float Y { get; set; }
        public float XSpeed { get; init; }
        public int YSpeed { get; set; }
        public int SubY { get; set; }
        public int MotionFrame { get; set; }
        public int BounceCount { get; set; }
        public bool Alive { get; set; } = true;
        public Rect2 Rect => new(X, Y, 8.0f, 8.0f);
    }

    private sealed class GoalTapeRuntime
    {
        public required Node2D Node { get; init; }
        public required Rect2 GateRect { get; init; }
        public float X { get; init; }
        public float Y { get; set; }
        public float MinY { get; init; }
        public float MaxY { get; init; }
        public int Timer { get; set; } = GoalTapeCycleFrames;
        public int Direction { get; set; } = 1;
        public float YSpeed => Direction > 0 ? GoalTapeDownSpeed : GoalTapeUpSpeed;
        public Rect2 TapeRect => new(X - 8.0f, Y + 8.0f, 24.0f, 8.0f);
    }

    private sealed class CoinPickup
    {
        public required Rect2 Rect { get; init; }
        public required List<(int X, int Y)> Tiles { get; init; }
        public bool DragonCoin { get; init; }
        public bool Collected { get; set; }
    }

    private sealed partial class Map16TileLayer : Node2D
    {
        private ImageTexture? _texture;
        private readonly List<PlacedMap16Tile> _tiles = [];
        private readonly HashSet<(int X, int Y)> _hiddenTiles = [];

        public void Configure(ImageTexture texture, IEnumerable<PlacedMap16Tile> tiles)
        {
            _texture = texture;
            _tiles.Clear();
            _tiles.AddRange(tiles);
            _hiddenTiles.Clear();
            QueueRedraw();
        }

        public void HideTile(int x, int y)
        {
            if (_hiddenTiles.Add((x, y)))
            {
                QueueRedraw();
            }
        }

        public void ReplaceTile(PlacedMap16Tile replacement)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].X != replacement.X || _tiles[i].Y != replacement.Y)
                {
                    continue;
                }

                _tiles[i] = replacement;
                _hiddenTiles.Remove((replacement.X, replacement.Y));
                QueueRedraw();
                return;
            }

            _tiles.Add(replacement);
            _hiddenTiles.Remove((replacement.X, replacement.Y));
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_texture == null)
            {
                return;
            }

            foreach (var tile in _tiles)
            {
                if (_hiddenTiles.Contains((tile.X, tile.Y)) || tile.Map16 < 0)
                {
                    continue;
                }

                var region = new Rect2(
                    (tile.Map16 % Map16AtlasColumns) * Map16TileSize,
                    (tile.Map16 / Map16AtlasColumns) * Map16TileSize,
                    Map16TileSize,
                    Map16TileSize);
                if (region.Position.Y + Map16TileSize > _texture.GetHeight())
                {
                    continue;
                }

                DrawTextureRectRegion(
                    _texture,
                    new Rect2(TileToWorld(tile.X, tile.Y), new Vector2(Map16TileSize, Map16TileSize)),
                    region);
            }
        }
    }

    private SmwPhysics.FrameInput ReadFrameInput()
    {
        if (_debugCommandInputFrames > 0)
        {
            var input = _debugCommandInput;
            if (_debugCommandInputFrame > 0)
            {
                input.JumpPressed = false;
                input.SpinPressed = false;
                input.RunPressed = false;
            }

            _debugCommandInputFrame++;
            _debugCommandInputFrames--;
            if (_debugCommandInputFrames <= 0)
            {
                _debugCommandInputFrame = 0;
                _debugCommandInput = default;
            }

            return input;
        }

        if (_debugHeldInputActive)
        {
            var input = _debugHeldInput;
            input.JumpPressed = _debugHeldJumpPressed;
            input.SpinPressed = _debugHeldSpinPressed;
            input.RunPressed = _debugHeldRunPressed;
            _debugHeldJumpPressed = false;
            _debugHeldSpinPressed = false;
            _debugHeldRunPressed = false;
            return input;
        }

        if (_inputScript.Count > 0)
        {
            return ReadScriptedFrameInput();
        }

        var liveInput = new SmwPhysics.FrameInput
        {
            Left = Input.IsActionPressed("smw_left"),
            Right = Input.IsActionPressed("smw_right"),
            Up = Input.IsActionPressed("smw_up"),
            Down = Input.IsActionPressed("smw_down"),
            Jump = Input.IsActionPressed("smw_jump"),
            JumpPressed = Input.IsActionJustPressed("smw_jump"),
            Spin = Input.IsActionPressed("smw_spin"),
            SpinPressed = Input.IsActionJustPressed("smw_spin"),
            Run = Input.IsActionPressed("smw_run"),
            RunPressed = Input.IsActionJustPressed("smw_run"),
        };
        return HasAnyFrameInput(liveInput) ? liveInput : ReadAutoplayFrameInput();
    }

    private SmwPhysics.FrameInput ReadAutoplayFrameInput()
    {
        if (_autoplayMode == DebugAutoplayMode.Off || _gamePaused || _gameOver || _courseClear)
        {
            _autoplayJumpHeld = false;
            return new SmwPhysics.FrameInput();
        }

        _autoplayFrame++;
        if (_autoplayMode == DebugAutoplayMode.TitleStart)
        {
            _autoplayJumpHeld = false;
            return new SmwPhysics.FrameInput();
        }

        if (_state.X == _autoplayLastPlayerX)
        {
            if (_autoplayStuckFrames < int.MaxValue)
            {
                _autoplayStuckFrames++;
            }
        }
        else
        {
            _autoplayLastPlayerX = _state.X;
            _autoplayStuckFrames = 0;
        }

        var actorJump = _state.OnGround && ShouldAutoplayJumpForActorAhead();
        var terrainJump = _state.OnGround && ShouldAutoplayJumpForTerrainAhead();
        var duck = _state.OnGround && !actorJump && ShouldAutoplayDuckUnderActorAhead();
        var actorAhead = ShouldAutoplayDeferPeriodicJumpForActorAhead();
        var airBrake = ShouldAutoplayBrakeForAirborneActorAhead() ||
            ShouldAutoplayBrakeForRisingStompableActorAhead();
        var periodicJump = _state.OnGround &&
            !duck &&
            !actorAhead &&
            _autoplayFrame % AutoplayExploreJumpPeriod < AutoplayExploreJumpHeldFrames;
        var stuckJump = _autoplayStuckFrames > AutoplayExploreStuckJumpThreshold &&
            !duck &&
            _autoplayFrame % AutoplayExploreStuckJumpPeriod < AutoplayExploreStuckJumpHeldFrames;
        var jump = periodicJump || stuckJump || actorJump || terrainJump;
        var jumpPressed = jump && !_autoplayJumpHeld;
        _autoplayJumpHeld = jump;
        return new SmwPhysics.FrameInput
        {
            Right = !airBrake,
            Left = airBrake,
            Down = duck,
            Jump = jump,
            JumpPressed = jumpPressed,
            Run = !airBrake && (!actorAhead || jump),
        };
    }

    private bool ShouldAutoplayJumpForActorAhead()
    {
        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        foreach (var actor in _spriteActors)
        {
            if (!IsAutoplayAvoidanceActor(actor) || !actor.Behavior.Stompable)
            {
                continue;
            }

            var actorRect = actor.Rect;
            var ahead = actorRect.Position.X - playerRight;
            if (ahead < -AutoplayExploreActorJumpBehindPixels || ahead > AutoplayExploreActorJumpAheadPixels)
            {
                continue;
            }

            var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
            var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
            if (actorBottom < playerRect.Position.Y - AutoplayExploreActorVerticalRangePixels ||
                actorRect.Position.Y > playerBottom + AutoplayExploreActorVerticalRangePixels)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool ShouldAutoplayDeferPeriodicJumpForActorAhead()
    {
        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        foreach (var actor in _spriteActors)
        {
            if (!IsAutoplayAvoidanceActor(actor))
            {
                continue;
            }

            var actorRect = actor.Rect;
            var ahead = actorRect.Position.X - playerRight;
            if (ahead < -AutoplayExploreActorPeriodicSuppressBehindPixels ||
                ahead > AutoplayExploreActorPeriodicSuppressAheadPixels)
            {
                continue;
            }

            var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
            var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
            if (actorBottom < playerRect.Position.Y - AutoplayExploreActorVerticalRangePixels ||
                actorRect.Position.Y > playerBottom + AutoplayExploreActorVerticalRangePixels)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool ShouldAutoplayBrakeForAirborneActorAhead()
    {
        if (_state.OnGround || _state.YSpeed >= 0)
        {
            return false;
        }

        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        foreach (var actor in _spriteActors)
        {
            if (!IsAutoplayAvoidanceActor(actor) || actor.Behavior.Stompable)
            {
                continue;
            }

            var actorRect = actor.Rect;
            var ahead = actorRect.Position.X - playerRight;
            if (ahead < -AutoplayExploreActorAirBrakeBehindPixels ||
                ahead > AutoplayExploreActorAirBrakeAheadPixels)
            {
                continue;
            }

            var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
            if (actorBottom < playerRect.Position.Y - AutoplayExploreActorVerticalRangePixels ||
                actorRect.Position.Y > playerBottom + AutoplayExploreActorVerticalRangePixels)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool ShouldAutoplayBrakeForRisingStompableActorAhead()
    {
        if (_state.OnGround || _state.YSpeed >= 0)
        {
            return false;
        }

        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        foreach (var actor in _spriteActors)
        {
            if (!IsAutoplayAvoidanceActor(actor) ||
                !actor.Behavior.Stompable)
            {
                continue;
            }

            var actorRect = actor.Rect;
            var ahead = actorRect.Position.X - playerRight;
            if (ahead < -AutoplayExploreStompableAirBrakeBehindPixels ||
                ahead > AutoplayExploreStompableAirBrakeAheadPixels)
            {
                continue;
            }

            var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
            if (actorBottom < playerRect.Position.Y - AutoplayExploreActorVerticalRangePixels ||
                actorRect.Position.Y > playerBottom + AutoplayExploreActorVerticalRangePixels)
            {
                continue;
            }

            if (playerBottom >= actorRect.Position.Y + AutoplayExploreStompableAirBrakeBodyOverlapPixels)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldAutoplayDuckUnderActorAhead()
    {
        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        foreach (var actor in _spriteActors)
        {
            if (!IsAutoplayAvoidanceActor(actor) || actor.Behavior.Stompable)
            {
                continue;
            }

            var actorRect = actor.Rect;
            var ahead = actorRect.Position.X - playerRight;
            if (ahead < -AutoplayExploreActorDuckBehindPixels || ahead > AutoplayExploreActorDuckAheadPixels)
            {
                continue;
            }

            var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
            if (actorRect.Position.Y < playerRect.Position.Y &&
                actorBottom >= playerRect.Position.Y - 96.0f &&
                actorBottom <= playerRect.Position.Y + 12.0f)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldAutoplayJumpForTerrainAhead()
    {
        var playerRect = _physics.PlayerRect(_state);
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        var playerTop = playerRect.Position.Y;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        for (var solidIndex = 0; solidIndex < _solids.Count; solidIndex++)
        {
            var solid = _solids[solidIndex];
            if (solid.Size.Y > AutoplayExploreTerrainJumpMaxHeightPixels)
            {
                continue;
            }

            var ahead = solid.Position.X - playerRight;
            if (ahead < -AutoplayExploreTerrainJumpBehindPixels ||
                ahead > AutoplayExploreTerrainJumpAheadPixels)
            {
                continue;
            }

            var solidBottom = solid.Position.Y + solid.Size.Y;
            if (solid.Position.Y >= playerBottom - 2.0f ||
                solidBottom <= playerTop + 2.0f ||
                solid.Position.Y < playerTop - 4.0f)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsAutoplayAvoidanceActor(RuntimeSpriteActor actor)
    {
        return actor.Alive &&
            actor.Active &&
            actor.Behavior.CanInteract &&
            !IsPowerupItemSprite(actor.SpriteId) &&
            !IsSolidBlockSprite(actor.SpriteId) &&
            actor.SpriteId != 0xC7 &&
            (!IsJumpingPiranhaSprite(actor.SpriteId) || actor.State != 0);
    }

    private static bool HasAnyFrameInput(SmwPhysics.FrameInput input)
    {
        return input.Left ||
            input.Right ||
            input.Up ||
            input.Down ||
            input.Jump ||
            input.JumpPressed ||
            input.Spin ||
            input.SpinPressed ||
            input.Run ||
            input.RunPressed;
    }

    private SmwPhysics.FrameInput ReadCourseClearInput()
    {
        _courseClearInitialWalkoutInputFrame = false;

        if (_courseClearExitTransition)
        {
            return new SmwPhysics.FrameInput();
        }

        if (_courseClearExitWalkFrames >= CourseClearExitTransitionFrame)
        {
            BeginCourseClearExitTransition();
            return new SmwPhysics.FrameInput();
        }

        if (_courseClearExitWalkFrames > 0 || _courseClearPostWalkPauseFrames == 0)
        {
            _courseClearExitWalkFrames++;
            return new SmwPhysics.FrameInput
            {
                Right = true,
            };
        }

        if (_courseClearPostWalkPauseFrames > 0)
        {
            _courseClearPostWalkPauseFrames--;
            return new SmwPhysics.FrameInput();
        }

        if (_courseClearWalkoutFrames >= CourseClearWalkoutMaxFrames)
        {
            return new SmwPhysics.FrameInput();
        }

        _courseClearWalkoutFrames++;
        _courseClearInitialWalkoutInputFrame = true;
        return new SmwPhysics.FrameInput
        {
            Right = true,
        };
    }

    private SmwPhysics.FrameInput ReadScriptedFrameInput()
    {
        if (_inputScriptIndex >= _inputScript.Count)
        {
            if (!_inputScriptDoneLogged)
            {
                _inputScriptDoneLogged = true;
                GD.Print(
                    $"smw-input-script: done name={_inputScriptName} frames={_inputScriptElapsedFrames} " +
                    $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} coins={_coinCount} dragon_coins={_dragonCoinCount}");
            }

            return new SmwPhysics.FrameInput();
        }

        var segment = _inputScript[_inputScriptIndex];
        var input = segment.Input;
        if (_inputScriptFrame > 0)
        {
            input.JumpPressed = false;
            input.SpinPressed = false;
            input.RunPressed = false;
        }

        _inputScriptFrame++;
        _inputScriptElapsedFrames++;
        if (_inputScriptFrame >= segment.Frames)
        {
            _inputScriptIndex++;
            _inputScriptFrame = 0;
        }

        return input;
    }

    private void LoadAssetPack()
    {
        LoadPlayerGraphicsMetadata();
        LoadLevelData(_currentLevelId);
    }

    private bool LoadLevelData(string levelId)
    {
        _screenExits.Clear();
        _levelObjects.Clear();
        _layer2Objects.Clear();
        _levelSprites.Clear();
        _placedTiles.Clear();
        _rawPlacedTiles.Clear();

        if (!FileAccess.FileExists("res://generated/smw/manifest.json"))
        {
            return false;
        }

        using var file = FileAccess.Open("res://generated/smw/manifest.json", FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return false;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var manifest = parsed.AsGodotDictionary();
        if (!manifest.TryGetValue("levels", out var levelsVariant) || levelsVariant.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var levels = levelsVariant.AsGodotDictionary();
        if (!levels.TryGetValue(levelId, out var levelVariant) || levelVariant.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var level = levelVariant.AsGodotDictionary();
        _currentLevelId = levelId;
        ApplyLevelAssetPaths(level);

        if (level.TryGetValue("screen_exits", out var exitsVariant) && exitsVariant.VariantType == Variant.Type.Array)
        {
            foreach (var exitVariant in exitsVariant.AsGodotArray())
            {
                if (exitVariant.VariantType == Variant.Type.Dictionary)
                {
                    _screenExits.Add(exitVariant.AsGodotDictionary());
                }
            }
        }

        if (!level.TryGetValue("file", out var fileVariant))
        {
            return false;
        }

        var levelPath = $"res://generated/smw/{fileVariant.AsString()}";
        if (!FileAccess.FileExists(levelPath))
        {
            return false;
        }

        using var levelFile = FileAccess.Open(levelPath, FileAccess.ModeFlags.Read);
        if (levelFile == null)
        {
            return false;
        }

        var levelParsed = Json.ParseString(levelFile.GetAsText());
        if (levelParsed.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var levelDetails = levelParsed.AsGodotDictionary();
        ApplyLevelAssetPaths(levelDetails);
        ApplyLevelHeaderMetadata(levelDetails);
        if (!levelDetails.TryGetValue("layer1", out var layer1Variant) || layer1Variant.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var layer1 = layer1Variant.AsGodotDictionary();
        if (layer1.TryGetValue("objects", out var objectsVariant) && objectsVariant.VariantType == Variant.Type.Array)
        {
            foreach (var objectVariant in objectsVariant.AsGodotArray())
            {
                if (objectVariant.VariantType == Variant.Type.Dictionary)
                {
                    _levelObjects.Add(objectVariant.AsGodotDictionary());
                }
            }
        }

        if (levelDetails.TryGetValue("layer2", out var layer2Variant) && layer2Variant.VariantType == Variant.Type.Dictionary)
        {
            var layer2 = layer2Variant.AsGodotDictionary();
            if (layer2.TryGetValue("objects", out var layer2ObjectsVariant) &&
                layer2ObjectsVariant.VariantType == Variant.Type.Array)
            {
                foreach (var objectVariant in layer2ObjectsVariant.AsGodotArray())
                {
                    if (objectVariant.VariantType == Variant.Type.Dictionary)
                    {
                        _layer2Objects.Add(objectVariant.AsGodotDictionary());
                    }
                }
            }
        }

        LoadSpriteSpawns(levelDetails);
        LoadPlacedTiles(_levelTilemapPath);
        return true;
    }

    private void ApplyLevelHeaderMetadata(Godot.Collections.Dictionary levelDetails)
    {
        _currentLevelMusicIndex = 0;
        _currentLevelMusicPreview = "Level";
        if (!levelDetails.TryGetValue("header", out var headerVariant) ||
            headerVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var header = headerVariant.AsGodotDictionary();
        if (header.TryGetValue("tileset", out var tilesetVariant))
        {
            _currentLevelTileset = tilesetVariant.AsInt32();
        }

        if (header.TryGetValue("music_index", out var musicVariant))
        {
            _currentLevelMusicIndex = musicVariant.AsInt32();
        }

        _currentLevelMusicPreview = MusicPreviewForLevelHeader(_currentLevelMusicIndex);
    }

    private static string MusicPreviewForLevelHeader(int musicIndex)
    {
        return musicIndex switch
        {
            _ => "Level",
        };
    }

    private void StartLevelMusic()
    {
        if (_courseClear)
        {
            return;
        }

        if (AudioEnabled)
        {
            _audio?.PlayMusicPreview(_starPowerTimer > 0 ? "Star" : _currentLevelMusicPreview);
        }
        GD.Print($"smw-runtime: level_music level={_currentLevelId} music_index={_currentLevelMusicIndex} bank={(_starPowerTimer > 0 ? "Star" : _currentLevelMusicPreview)}");
    }

    private void LoadSpriteSpawns(Godot.Collections.Dictionary levelDetails)
    {
        if (!levelDetails.TryGetValue("sprite_layer", out var spriteLayerVariant) ||
            spriteLayerVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var spriteLayer = spriteLayerVariant.AsGodotDictionary();
        if (!spriteLayer.TryGetValue("sprites", out var spritesVariant) || spritesVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var spriteVariant in spritesVariant.AsGodotArray())
        {
            if (spriteVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var sprite = spriteVariant.AsGodotDictionary();
            var screenY = sprite.TryGetValue("screen_y", out var screenYVariant) ? screenYVariant.AsInt32() : 0;
            var xId = sprite.TryGetValue("x_id", out var xIdVariant) ? xIdVariant.AsInt32() : 0;
            var spriteId = sprite.TryGetValue("sprite_id", out var spriteIdVariant) ? spriteIdVariant.AsInt32() : 0;
            var extraBits = sprite.TryGetValue("extra_bits", out var extraBitsVariant) ? extraBitsVariant.AsInt32() : 0;
            var offset = sprite.TryGetValue("offset", out var offsetVariant) ? offsetVariant.AsInt32() : 0;
            var screen = sprite.TryGetValue("screen", out var screenVariant)
                ? screenVariant.AsInt32()
                : (((screenY << 3) & 0x10) | (xId & 0x0F));
            var x = sprite.TryGetValue("x_px", out var xVariant)
                ? xVariant.AsInt32()
                : screen * 256 + (xId & 0xF0);
            var y = sprite.TryGetValue("y_px", out var yVariant)
                ? yVariant.AsInt32() + (int)LevelVisualYOffset
                : (screenY & 0xF0) + (((screenY & 0x01) != 0) ? 0x100 : 0) + (int)LevelVisualYOffset;
            _levelSprites.Add(new SpriteSpawn(x, y, screen, spriteId, extraBits, offset));
        }
    }

    private void ApplyLevelAssetPaths(Godot.Collections.Dictionary level)
    {
        if (level.TryGetValue("tileset_assets", out var tilesetVariant) && tilesetVariant.VariantType == Variant.Type.Dictionary)
        {
            var tileset = tilesetVariant.AsGodotDictionary();
            if (tileset.TryGetValue("atlas_png", out var atlasVariant) &&
                TryReadAssetFile(atlasVariant, out var atlasFile))
            {
                _levelGfxAtlasPath = $"res://generated/smw/{atlasFile}";
            }
            if (tileset.TryGetValue("map16_preview_png", out var map16Variant) &&
                TryReadAssetFile(map16Variant, out var map16File))
            {
                _levelMap16AtlasPath = $"res://generated/smw/{map16File}";
            }
        }

        if (level.TryGetValue("sprite_tileset_assets", out var spriteTilesetVariant) &&
            spriteTilesetVariant.VariantType == Variant.Type.Dictionary)
        {
            var spriteTileset = spriteTilesetVariant.AsGodotDictionary();
            if (spriteTileset.TryGetValue("atlas_png", out var atlasVariant) &&
                TryReadAssetFile(atlasVariant, out var atlasFile))
            {
                _levelSpriteAtlasPath = $"res://generated/smw/{atlasFile}";
            }
            if (spriteTileset.TryGetValue("vram", out var vramVariant) &&
                TryReadAssetFile(vramVariant, out var vramFile))
            {
                _levelSpriteVramPath = $"res://generated/smw/{vramFile}";
            }
            if (spriteTileset.TryGetValue("palette_assets", out var spritePaletteVariant) &&
                TryReadAssetFile(spritePaletteVariant, out var spritePaletteFile))
            {
                _levelPalettePath = $"res://generated/smw/{spritePaletteFile}";
            }

            ApplySpriteUploadTileStarts(spriteTileset);
        }

        if (level.TryGetValue("palette_assets", out var paletteVariant) &&
            TryReadAssetFile(paletteVariant, out var paletteFile))
        {
            _levelPalettePath = $"res://generated/smw/{paletteFile}";
        }

        if (level.TryGetValue("layout_preview", out var layoutVariant) && layoutVariant.VariantType == Variant.Type.Dictionary)
        {
            var layout = layoutVariant.AsGodotDictionary();
            if (layout.TryGetValue("file", out var tilemapVariant) &&
                TryReadAssetFile(tilemapVariant, out var tilemapFile))
            {
                _levelTilemapPath = $"res://generated/smw/{tilemapFile}";
            }
            if (layout.TryGetValue("preview_png", out var previewVariant) &&
                TryReadAssetFile(previewVariant, out var previewFile))
            {
                _levelLayoutPreviewPath = $"res://generated/smw/{previewFile}";
            }
        }

        if (level.TryGetValue("layer2_background", out var layer2Variant) &&
            layer2Variant.VariantType == Variant.Type.Dictionary)
        {
            var layer2 = layer2Variant.AsGodotDictionary();
            if (layer2.TryGetValue("preview_png", out var previewVariant) &&
                TryReadAssetFile(previewVariant, out var previewFile))
            {
                _levelLayer2BackgroundPath = $"res://generated/smw/{previewFile}";
            }
        }
    }

    private static bool TryReadAssetFile(Variant value, out string file)
    {
        if (value.VariantType == Variant.Type.String)
        {
            file = value.AsString();
            return !string.IsNullOrWhiteSpace(file);
        }

        if (value.VariantType == Variant.Type.Dictionary)
        {
            var dictionary = value.AsGodotDictionary();
            if (dictionary.TryGetValue("file", out var fileVariant) &&
                fileVariant.VariantType == Variant.Type.String)
            {
                file = fileVariant.AsString();
                return !string.IsNullOrWhiteSpace(file);
            }
        }

        file = "";
        return false;
    }

    private static void ApplySpriteUploadTileStarts(Godot.Collections.Dictionary spriteTileset)
    {
        if (!spriteTileset.TryGetValue("uploads", out var uploadsVariant) ||
            uploadsVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var uploadVariant in uploadsVariant.AsGodotArray())
        {
            if (uploadVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var upload = uploadVariant.AsGodotDictionary();
            if (!upload.TryGetValue("slot", out var slotVariant) ||
                !upload.TryGetValue("tile_start", out var tileStartVariant))
            {
                continue;
            }

            var slot = slotVariant.AsInt32();
            var lmuBank = 3 - slot;
            if (lmuBank < 0 || lmuBank >= SpriteAtlasTileStartByLmuBank.Length)
            {
                continue;
            }

            SpriteAtlasTileStartByLmuBank[lmuBank] = tileStartVariant.AsInt32();
        }
    }

    private void LoadPlacedTiles(string tilemapPath)
    {
        if (!FileAccess.FileExists(tilemapPath))
        {
            return;
        }

        using var tilemapFile = FileAccess.Open(tilemapPath, FileAccess.ModeFlags.Read);
        if (tilemapFile == null)
        {
            return;
        }

        var tilemapParsed = Json.ParseString(tilemapFile.GetAsText());
        if (tilemapParsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var tilemap = tilemapParsed.AsGodotDictionary();
        if (!tilemap.TryGetValue("placed_tiles", out var placedTilesVariant) ||
            placedTilesVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        var finalTiles = new Dictionary<(int X, int Y), PlacedMap16Tile>();
        var finalTileOrder = new List<(int X, int Y)>();
        foreach (var placedVariant in placedTilesVariant.AsGodotArray())
        {
            if (placedVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var placed = placedVariant.AsGodotDictionary();
            var x = placed.TryGetValue("x", out var xVariant) ? xVariant.AsInt32() : 0;
            var y = placed.TryGetValue("y", out var yVariant) ? yVariant.AsInt32() : 0;
            var map16 = placed.TryGetValue("map16", out var map16Variant) ? map16Variant.AsInt32() : 0;
            var source = placed.TryGetValue("source", out var sourceVariant) ? sourceVariant.AsString() : "";
            var key = (x, y);
            if (!finalTiles.ContainsKey(key))
            {
                finalTileOrder.Add(key);
            }

            var candidate = new PlacedMap16Tile(x, y, map16, source);
            _rawPlacedTiles.Add(candidate);
            finalTiles[key] = finalTiles.TryGetValue(key, out var existing)
                ? ChooseVisibleMap16Tile(existing, candidate)
                : candidate;
        }

        foreach (var key in finalTileOrder)
        {
            _placedTiles.Add(finalTiles[key]);
        }
    }

    private static PlacedMap16Tile ChooseVisibleMap16Tile(PlacedMap16Tile current, PlacedMap16Tile incoming)
    {
        if (TerrainMasksPipeBody(current.Source, incoming.Source))
        {
            return current;
        }

        if (TerrainMasksPipeBody(incoming.Source, current.Source))
        {
            return incoming;
        }

        return incoming;
    }

    private static bool TerrainMasksPipeBody(string terrainSource, string pipeSource)
    {
        return IsTerrainMaskSource(terrainSource) && IsPipeBodySource(pipeSource);
    }

    private static bool IsTerrainMaskSource(string source)
    {
        return source.Contains("ledge", StringComparison.Ordinal) ||
            source.Contains("ground", StringComparison.Ordinal) ||
            source.StartsWith("std_generic_", StringComparison.Ordinal);
    }

    private static bool IsPipeBodySource(string source)
    {
        return source.Contains("pipe_shaft", StringComparison.Ordinal) ||
            source.Contains("diagonal_pipe", StringComparison.Ordinal);
    }

    private void LoadPlayerGraphicsMetadata()
    {
        const string playerGraphicsPath = "res://generated/smw/player/player_graphics.json";
        if (!FileAccess.FileExists(playerGraphicsPath))
        {
            return;
        }

        using var file = FileAccess.Open(playerGraphicsPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var metadata = parsed.AsGodotDictionary();
        if (!metadata.TryGetValue("tile_pointer_tables", out var tablesVariant) ||
            tablesVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var tables = tablesVariant.AsGodotDictionary();
        LoadTilePointerArray(tables, "head", _headTilePointers);
        LoadTilePointerArray(tables, "body", _bodyTilePointers);
        LoadTilePointerArray(tables, "walking_pose_count", _playerWalkingPoseCounts);
        LoadTilePointerArray(tables, "animation_speed_table", _playerAnimationSpeedTable);

        if (!metadata.TryGetValue("oam_tables", out var oamTablesVariant) ||
            oamTablesVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var oamTables = oamTablesVariant.AsGodotDictionary();
        LoadOamTableValues(oamTables, "player_xy_disp_index_index", _playerXYDispIndexIndex);
        LoadOamTableValues(oamTables, "player_xy_disp_index", _playerXYDispIndex);
        LoadOamTableValues(oamTables, "x_disp", _playerXDisp);
        LoadOamTableValues(oamTables, "y_disp", _playerYDisp);
        LoadOamTableValues(oamTables, "powerup_tileset_index", _playerPowerupTilesetIndex);
        LoadOamTableValues(oamTables, "tiles", _playerTileDescriptors);
        LoadOamTableValues(oamTables, "tiles_index", _playerTilesIndex);
        LoadOamTableValues(oamTables, "tile_x_flip", _playerTileXFlip);
    }

    private static void LoadTilePointerArray(Godot.Collections.Dictionary tables, string key, List<int> target)
    {
        target.Clear();
        if (!tables.TryGetValue(key, out var variant) || variant.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var entry in variant.AsGodotArray())
        {
            target.Add(entry.AsInt32());
        }
    }

    private static void LoadOamTableValues(Godot.Collections.Dictionary tables, string key, List<int> target)
    {
        target.Clear();
        if (!tables.TryGetValue(key, out var tableVariant) ||
            tableVariant.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var table = tableVariant.AsGodotDictionary();
        if (!table.TryGetValue("values", out var valuesVariant) ||
            valuesVariant.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var entry in valuesVariant.AsGodotArray())
        {
            target.Add(entry.AsInt32());
        }
    }

    private void StartWorldRoot()
    {
        _worldRoot?.QueueFree();
        _worldRoot = new Node2D
        {
            Name = $"World_{_currentLevelId}",
        };
        AddChild(_worldRoot);
    }

    private void AddWorldChild(Node node)
    {
        (_worldRoot ?? this).AddChild(node);
    }

    private void BuildWorld()
    {
        _solids.Clear();
        _solidStepUpEnabled.Clear();
        _solidVerticalEnabled.Clear();
        _solidSupportModes.Clear();
        _slopes.Clear();
        _spriteActors.Clear();
        _playerFireballs.Clear();
        _goalTapeTriggers.Clear();
        _goalTapes.Clear();
        _coinPickups.Clear();
        _map16TilesByCoord.Clear();
        _diagonalPipeFloorCells.Clear();
        _diagonalPipeBodyCells.Clear();
        _diagonalPipeCeilingCells.Clear();
        _cameraGizmo?.QueueFree();
        _cameraGizmo = null;
        _spriteTexture = null;
        Array.Clear(_spritePaletteTextures);
        _map16Texture = null;
        _map16Layer = null;
        StartWorldRoot();
        AddWorldBackground();
        AddLayer2BackgroundPreview();

        if (AddGeneratedMap16Tiles())
        {
            AddGeneratedCollision(DebugOverlays);
        }
        else
        {
            AddGeneratedLevelPreview();
            AddSolid(new Rect2(0, 192, 3584, 64), new Color(0.20f, 0.55f, 0.25f, 0.22f), DebugOverlays);
            AddSolid(new Rect2(240, 160, 48, 32), new Color(0.55f, 0.42f, 0.20f, 0.22f), DebugOverlays);
            AddSolid(new Rect2(368, 144, 64, 48), new Color(0.20f, 0.48f, 0.22f, 0.22f), DebugOverlays);
        }

        AddCoinPickups();
        RebuildPipeEntrances();
        LoadSpriteTexture();
        AddRuntimeSpriteActors();
        WarmUpRuntimeSpriteActors(NativeLevelStartSpriteWarmupFrames);
        AddGoalTapeTriggers();
        if (DebugOverlays)
        {
            AddPipeMarkers();
            AddGoalTapeMarkers();
            AddObjectMarkers();
            AddSpriteMarkers();
            AddTileSemanticMarkers();
            AddPickupDebugMarkers();
            BuildCameraGizmo();

            for (var i = 0; i < 20; i++)
            {
                AddScreenLine(i);
            }
        }
    }

    private void AddGoalTapeTriggers()
    {
        foreach (var spawn in _levelSprites)
        {
            if (spawn.SpriteId != GoalTapeSpriteId)
            {
                continue;
            }

            var gateBounds = GoalGateBoundsForSpawn(spawn);
            var rect = new Rect2(spawn.X - 8, gateBounds.Top, 24, gateBounds.Bottom - gateBounds.Top);
            _goalTapeTriggers.Add(rect);

            var tapeNode = new Node2D
            {
                Name = "GoalTape",
                Position = new Vector2(spawn.X, InitialGoalTapeY(spawn, gateBounds.Top, gateBounds.Bottom)),
                ZIndex = 5,
            };
            var visuals = AddSpriteActorVisuals(tapeNode, GoalTapeSpriteId, state: 0);
            if (visuals.Count == 0)
            {
                AddDebugRect(tapeNode, new Rect2(-8, 8, 24, 8), new Color(1.0f, 0.86f, 0.18f, 1.0f), 5);
            }
            AddWorldChild(tapeNode);
            _goalTapes.Add(new GoalTapeRuntime
            {
                Node = tapeNode,
                GateRect = rect,
                X = spawn.X,
                Y = tapeNode.Position.Y,
                MinY = gateBounds.Top + 8.0f,
                MaxY = gateBounds.Bottom - 24.0f,
            });
        }
    }

    private (float Top, float Bottom) GoalGateBoundsForSpawn(SpriteSpawn spawn)
    {
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var tile in _placedTiles)
        {
            if (!tile.Source.StartsWith("goal_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tileX = tile.X * Map16TileSize;
            if (MathF.Abs((tileX + Map16TileSize * 0.5f) - spawn.X) > 40.0f)
            {
                continue;
            }

            var tileTop = tile.Y * Map16TileSize + LevelVisualYOffset;
            top = MathF.Min(top, tileTop);
            bottom = MathF.Max(bottom, tileTop + Map16TileSize);
        }

        if (top < bottom)
        {
            return (top, bottom);
        }

        return (spawn.Y - 128.0f, spawn.Y + 32.0f);
    }

    private static float InitialGoalTapeY(SpriteSpawn spawn, float gateTop, float gateBottom)
    {
        return Math.Clamp(spawn.Y - 76.0f, gateTop + 8.0f, gateBottom - 24.0f);
    }

    private void AddRuntimeSpriteActors()
    {
        foreach (var spawn in _levelSprites)
        {
            if (!IsRuntimeEnemySprite(spawn.SpriteId))
            {
                continue;
            }

            var actor = CreateRuntimeSpriteActor(spawn, DebugOverlays);
            _spriteActors.Add(actor);
            AddWorldChild(actor.Node);
        }
    }

    private void WarmUpRuntimeSpriteActors(int frames)
    {
        if (frames <= 0 || _spriteActors.Count == 0)
        {
            return;
        }

        for (var frame = 0; frame < frames; frame++)
        {
            foreach (var actor in _spriteActors)
            {
                if (!actor.Alive)
                {
                    continue;
                }

                actor.Active = IsSpriteActorAwake(actor);
                if (!actor.Active)
                {
                    continue;
                }

                actor.PreviousX = actor.X;
                actor.PreviousY = actor.Y;
                UpdateSpriteActorMotion(actor);
                actor.Node.Position = new Vector2(actor.X, actor.Y);
            }
        }
    }

    private void LoadSpriteTexture()
    {
        var paletteTextureCount = LoadSpritePaletteTextures();

        if (!FileAccess.FileExists(_levelSpriteAtlasPath))
        {
            if (paletteTextureCount > 0)
            {
                GD.Print(
                    $"smw-runtime: sprite_palettes={paletteTextureCount} source=vram palette={_levelPalettePath} vram={_levelSpriteVramPath}");
            }
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(_levelSpriteAtlasPath));
        if (image != null && !image.IsEmpty())
        {
            _spriteTexture = ImageTexture.CreateFromImage(image);
        }

        GD.Print(
            $"smw-runtime: sprite_palettes={paletteTextureCount} source={(paletteTextureCount > 0 ? "vram" : "preview")} atlas={_levelSpriteAtlasPath}");
    }

    private int LoadSpritePaletteTextures()
    {
        Array.Clear(_spritePaletteTextures);
        if (!FileAccess.FileExists(_levelSpriteVramPath) ||
            !TryLoadLevelPalette(out var palette))
        {
            return 0;
        }

        byte[] vram;
        try
        {
            vram = IoFile.ReadAllBytes(ProjectSettings.GlobalizePath(_levelSpriteVramPath));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"smw-runtime: failed to read sprite VRAM {_levelSpriteVramPath}: {ex.Message}");
            return 0;
        }

        if (vram.Length == 0 || vram.Length % 32 != 0)
        {
            GD.PushWarning($"smw-runtime: invalid sprite VRAM length {vram.Length} for {_levelSpriteVramPath}");
            return 0;
        }

        var tileCount = vram.Length / 32;
        var rows = (tileCount + SnesSpriteAtlasColumns - 1) / SnesSpriteAtlasColumns;
        var width = SnesSpriteAtlasColumns * SnesSpriteTileSize;
        var height = rows * SnesSpriteTileSize;
        var built = 0;
        for (var oamPalette = 0; oamPalette < _spritePaletteTextures.Length; oamPalette++)
        {
            var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            var rowOffset = (8 + oamPalette) * 16;
            for (var tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                var tileOffset = tileIndex * 32;
                var tileX = (tileIndex % SnesSpriteAtlasColumns) * SnesSpriteTileSize;
                var tileY = (tileIndex / SnesSpriteAtlasColumns) * SnesSpriteTileSize;
                for (var y = 0; y < SnesSpriteTileSize; y++)
                {
                    var p0 = vram[tileOffset + y * 2];
                    var p1 = vram[tileOffset + y * 2 + 1];
                    var p2 = vram[tileOffset + 16 + y * 2];
                    var p3 = vram[tileOffset + 16 + y * 2 + 1];
                    for (var x = 0; x < SnesSpriteTileSize; x++)
                    {
                        var bit = 7 - x;
                        var colorIndex =
                            ((p0 >> bit) & 1) |
                            (((p1 >> bit) & 1) << 1) |
                            (((p2 >> bit) & 1) << 2) |
                            (((p3 >> bit) & 1) << 3);
                        var color = colorIndex == 0
                            ? new Color(0, 0, 0, 0)
                            : palette[rowOffset + colorIndex];
                        image.SetPixel(tileX + x, tileY + y, color);
                    }
                }
            }

            _spritePaletteTextures[oamPalette] = ImageTexture.CreateFromImage(image);
            built++;
        }

        return built;
    }

    private bool TryLoadLevelPalette(out Color[] palette)
    {
        palette = [];
        if (!FileAccess.FileExists(_levelPalettePath))
        {
            return false;
        }

        using var file = FileAccess.Open(_levelPalettePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return false;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        var dictionary = parsed.AsGodotDictionary();
        if (!dictionary.TryGetValue("rgb888", out var rgbVariant) ||
            rgbVariant.VariantType != Variant.Type.Array)
        {
            return false;
        }

        var rgbRows = rgbVariant.AsGodotArray();
        if (rgbRows.Count < 256)
        {
            return false;
        }

        palette = new Color[256];
        for (var i = 0; i < palette.Length; i++)
        {
            if (rgbRows[i].VariantType != Variant.Type.Array)
            {
                return false;
            }

            var rgb = rgbRows[i].AsGodotArray();
            if (rgb.Count < 3)
            {
                return false;
            }

            palette[i] = new Color(
                rgb[0].AsSingle() / 255.0f,
                rgb[1].AsSingle() / 255.0f,
                rgb[2].AsSingle() / 255.0f,
                1.0f);
        }

        return true;
    }

    private static bool IsRuntimeEnemySprite(int spriteId)
    {
        return spriteId is 0x4F or 0x83 or 0x95 or 0x9F or 0xAB or 0xB9 or 0xBD or 0xC7 or 0xDA or 0xDB or 0xDC or 0xDD or 0xDF;
    }

    private static bool IsJumpingPiranhaSprite(int spriteId)
    {
        return spriteId is 0x4F or 0x50;
    }

    private static bool IsSolidBlockSprite(int spriteId)
    {
        return spriteId is 0x83 or 0xB9;
    }

    private static bool IsPowerupItemSprite(int spriteId)
    {
        return spriteId is 0x74 or 0x75 or 0x76 or 0x77 or 0x78;
    }

    private RuntimeSpriteActor CreateRuntimeSpriteActor(SpriteSpawn spawn, bool debugOverlays)
    {
        var color = SpriteActorColor(spawn.SpriteId);
        var behavior = SpriteActorBehaviorFor(spawn.SpriteId);
        var actorY = spawn.Y + SpriteActorSpawnYOffsetFor(spawn.SpriteId);
        var node = new Node2D
        {
            Name = $"Sprite_{spawn.SpriteId:X2}_{spawn.Offset:X2}",
            Position = new Vector2(spawn.X, actorY),
            ZIndex = 6,
            Visible = _debugActorVisualsEnabled,
        };
        var visuals = AddSpriteActorVisuals(node, spawn.SpriteId, state: 0);
        var hasVisual = visuals.Count > 0;
        var body = new ColorRect
        {
            Name = hasVisual ? "ActorCollisionDebug" : "ActorPlaceholderDebug",
            Color = debugOverlays ? new Color(color.R, color.G, color.B, 0.20f) : color,
            Position = behavior.Hitbox.Position,
            Size = behavior.Hitbox.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        node.AddChild(body);
        if (debugOverlays)
        {
            AddRectOutline(
                node,
                behavior.Hitbox,
                new Color(1.0f, 0.10f, 0.10f, 0.88f),
                1.0f,
                60);
        }
        return new RuntimeSpriteActor
        {
            Node = node,
            Body = body,
            SpriteId = spawn.SpriteId,
            X = spawn.X,
            Y = actorY,
            PreviousX = spawn.X,
            PreviousY = actorY,
            HomeY = actorY,
            XSpeed = behavior.InitialXSpeed,
            WakeScreen = spawn.Screen,
            ContentIndex = WorldTileXNibble(spawn.X) & 0x03,
            SpawnOffset = spawn.Offset,
            MotionFrame = InitialSpriteMotionFrame(spawn),
            Visuals = visuals,
            Behavior = behavior,
        };
    }

    private static int SpriteActorSpawnYOffsetFor(int spriteId)
    {
        return spriteId switch
        {
            // These level 105 enemies use OAM and clipping data relative to the native sprite
            // origin, not to the top of their rendered sprite.
            0x9F or 0xAB => 0,
            _ => -SpriteActorVisualHeightFor(spriteId),
        };
    }

    private static int SpriteActorVisualHeightFor(int spriteId)
    {
        return spriteId switch
        {
            0x9F => 64,
            _ => SpriteActorHeight,
        };
    }

    private static int InitialSpriteMotionFrame(SpriteSpawn spawn)
    {
        if (IsJumpingPiranhaSprite(spawn.SpriteId))
        {
            return (spawn.Offset * 7) % JumpingPiranhaCycleFrames;
        }
        if (spawn.SpriteId == 0x83)
        {
            return (spawn.Offset * 5) % WingedQuestionBlockCycleFrames;
        }

        return 0;
    }

    private List<Node> AddSpriteActorVisuals(Node2D node, int spriteId, int state, bool used = false)
    {
        var visuals = new List<Node>();
        foreach (var tile in SpriteOamTilesFor(spriteId, state))
        {
            if (AddSpriteOamTile(node, tile, out var sprite))
            {
                visuals.Add(sprite);
            }
        }

        AddSpriteCommandVisual(node, spriteId, used, visuals);
        return visuals;
    }

    private bool AddSpriteOamTile(Node2D node, SpriteOamTile tile, out Sprite2D sprite)
    {
        sprite = null!;
        if (tile.Bank < 0 ||
            tile.Bank >= SpriteAtlasTileStartByLmuBank.Length)
        {
            return false;
        }

        var oamPalette = (tile.Prop >> 1) & 0x07;
        var texture = _spritePaletteTextures[oamPalette] ?? _spriteTexture;
        if (texture == null)
        {
            return false;
        }

        var size = tile.Large ? 16 : 8;
        var tileIndex = SpriteAtlasTileStartByLmuBank[tile.Bank] + (tile.Tile & 0x7F);
        var region = new Rect2(
            (tileIndex % SnesSpriteAtlasColumns) * SnesSpriteTileSize,
            (tileIndex / SnesSpriteAtlasColumns) * SnesSpriteTileSize,
            size,
            size);
        if (region.Position.X + size > texture.GetWidth() ||
            region.Position.Y + size > texture.GetHeight())
        {
            return false;
        }

        sprite = new Sprite2D
        {
            Texture = texture,
            RegionEnabled = true,
            RegionRect = region,
            Position = new Vector2(tile.Dx, tile.Dy),
            Centered = false,
            FlipH = (tile.Prop & 0x40) != 0,
            FlipV = (tile.Prop & 0x80) != 0,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 2,
        };
        node.AddChild(sprite);
        return true;
    }

    private void AddSpriteCommandVisual(Node2D node, int spriteId, bool used, List<Node> visuals)
    {
        switch (spriteId)
        {
            case 0x83:
                if (used)
                {
                    AddMap16SpriteVisual(node, 0x0125, Vector2.Zero, visuals);
                }
                else if (AddSpriteOamTile(node, new SpriteOamTile(0, -1, 0x2A, 0x00, 0, true), out var questionBlock))
                {
                    visuals.Add(questionBlock);
                }
                else
                {
                    AddMap16SpriteVisual(node, 0x0124, Vector2.Zero, visuals);
                }
                break;
            case 0xB9:
                if (!AddMap16SpriteVisual(node, 0x0125, Vector2.Zero, visuals))
                {
                    AddFallbackBlockVisual(node, "!", new Color(0.92f, 0.60f, 0.18f, 1.0f), visuals);
                }
                break;
            case 0xC7:
                if (DebugOverlays)
                {
                    AddBubbleVisual(node, visuals);
                }
                break;
        }
    }

    private bool AddMap16SpriteVisual(Node2D node, int map16Tile, Vector2 position, List<Node> visuals)
    {
        if (_map16Texture == null)
        {
            return false;
        }

        var region = new Rect2(
            (map16Tile % Map16AtlasColumns) * Map16TileSize,
            (map16Tile / Map16AtlasColumns) * Map16TileSize,
            Map16TileSize,
            Map16TileSize);
        if (region.Position.Y + Map16TileSize > _map16Texture.GetHeight())
        {
            return false;
        }

        var sprite = new Sprite2D
        {
            Texture = _map16Texture,
            RegionEnabled = true,
            RegionRect = region,
            Position = position,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 2,
        };
        node.AddChild(sprite);
        visuals.Add(sprite);
        return true;
    }

    private static void AddFallbackBlockVisual(Node2D node, string labelText, Color color, List<Node> visuals)
    {
        visuals.Add(AddDebugRect(node, new Rect2(0, 0, 16, 16), color, 2));
        var label = new Label
        {
            Text = labelText,
            Position = new Vector2(3, -2),
            ZIndex = 3,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        node.AddChild(label);
        visuals.Add(label);
    }

    private static void AddBubbleVisual(Node2D node, List<Node> visuals)
    {
        visuals.Add(AddDebugRect(node, new Rect2(1, 2, 14, 11), new Color(0.70f, 0.90f, 1.0f, 0.38f), 2));
        visuals.Add(AddDebugRect(node, new Rect2(5, 9, 6, 8), new Color(0.70f, 0.90f, 1.0f, 0.38f), 2));
    }

    private static ColorRect AddDebugRect(Node parent, Rect2 rect, Color color, int zIndex)
    {
        var body = new ColorRect
        {
            Color = color,
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zIndex,
        };
        parent.AddChild(body);
        return body;
    }

    private static IReadOnlyList<SpriteOamTile> SpriteOamTilesFor(int spriteId, int state)
    {
        return spriteId switch
        {
            0x9F => BanzaiBillOamTiles,
            0x95 => ClappinChuckOamTiles,
            0xAB => state == 1 ? SquishedRexOamTiles : RexOamTiles,
            0xBD => SlidingKoopaOamTiles,
            0x83 => WingOamTiles,
            0x74 or 0x75 or 0x76 or 0x77 or 0x78 => PowerupItemOamTilesFor(spriteId),
            0xDA or 0xDB or 0xDC or 0xDD or 0xDF => ShellOamTilesFor(spriteId),
            0x4F or 0x50 => JumpingPiranhaOamTiles,
            0x4B => PipeLakituOamTiles,
            0x52 => MovingLedgeHoleOamTiles,
            0x7B => GoalTapeOamTiles,
            0x87 => LakituCloudOamTiles,
            0x8F => ScalePlatformOamTiles,
            0x90 => GreenGasBubbleOamTiles,
            0x99 => VolcanoLotusOamTiles,
            _ => [],
        };
    }

    private static readonly SpriteOamTile[] BanzaiBillOamTiles =
    [
        new(0x00, 0x00, 0x80, 0x33, 3, true),
        new(0x10, 0x00, 0x82, 0x33, 3, true),
        new(0x20, 0x00, 0x84, 0x33, 3, true),
        new(0x30, 0x00, 0x86, 0x33, 3, true),
        new(0x00, 0x10, 0xA0, 0x33, 3, true),
        new(0x10, 0x10, 0x88, 0x33, 3, true),
        new(0x20, 0x10, 0xCE, 0x33, 3, true),
        new(0x30, 0x10, 0xEE, 0x33, 3, true),
        new(0x00, 0x20, 0xC0, 0x33, 3, true),
        new(0x10, 0x20, 0xC2, 0x33, 3, true),
        new(0x20, 0x20, 0xCE, 0x33, 3, true),
        new(0x30, 0x20, 0xEE, 0x33, 3, true),
        new(0x00, 0x30, 0x8E, 0x33, 3, true),
        new(0x10, 0x30, 0xAE, 0x33, 3, true),
        new(0x20, 0x30, 0x84, 0xB3, 3, true),
        new(0x30, 0x30, 0x86, 0xB3, 3, true),
    ];

    private static readonly SpriteOamTile[] RexOamTiles =
    [
        new(-4, -15, 0x8A, 0x07, 3, true),
        new(0, 0, 0xAA, 0x07, 3, true),
    ];

    private static readonly SpriteOamTile[] ClappinChuckOamTiles =
    [
        new(0, -12, 0x06, 0x4B, 2, true),
        new(4, 0, 0x40, 0x4B, 2, true),
        new(-4, 0, 0x40, 0x0B, 2, true),
        new(-6, -8, 0x0C, 0x0B, 2, false),
        new(14, -8, 0x0C, 0x4B, 2, false),
    ];

    private static readonly SpriteOamTile[] SquishedRexOamTiles =
    [
        new(0, 0, 0x8C, 0x07, 3, true),
    ];

    private static readonly SpriteOamTile[] SlidingKoopaOamTiles =
    [
        new(0, 0, 0x86, 0x06, 1, true),
    ];

    private static readonly SpriteOamTile[] WingOamTiles =
    [
        new(-11, -10, 0xC6, 0x46, 1, true),
        new(11, -10, 0xC6, 0x06, 1, true),
    ];

    private static SpriteOamTile[] PowerupItemOamTilesFor(int spriteId)
    {
        var index = Math.Clamp(spriteId - 0x74, 0, 4);
        int[] tiles = [0x24, 0x26, 0x48, 0x0E, 0x24];
        int[] props = [0x08, 0x0A, 0x00, 0x04, 0x0A];
        return [new SpriteOamTile(0, -1, tiles[index], props[index], 0, true)];
    }

    private static readonly SpriteOamTile[] JumpingPiranhaOamTiles =
    [
        new(8, -17, 0xAE, 0x58, 1, true),
        new(8, -9, 0x83, 0x0A, 1, false),
        new(16, -9, 0x83, 0x4A, 1, false),
        new(8, -1, 0xC4, 0x0A, 1, false),
        new(16, -1, 0xC4, 0x4A, 1, false),
    ];

    private static readonly SpriteOamTile[] PipeLakituOamTiles =
    [
        new(0, 0, 0xEC, 0x5B, 3, true),
        new(0, 16, 0xEE, 0x5B, 3, true),
    ];

    private static readonly SpriteOamTile[] MovingLedgeHoleOamTiles =
    [
        new(0, 0, 0xEB, 0x71, 3, true),
        new(8, 0, 0xEA, 0x31, 3, true),
        new(24, 0, 0xEA, 0x31, 3, true),
        new(32, 0, 0xEB, 0x31, 3, true),
    ];

    private static readonly SpriteOamTile[] GoalTapeOamTiles =
    [
        new(-8, 8, 0xD4, 0x32, 1, false),
        new(0, 8, 0xD5, 0x32, 1, false),
        new(8, 8, 0xD5, 0x32, 1, false),
    ];

    private static readonly SpriteOamTile[] LakituCloudOamTiles =
    [
        new(-4, 0, 0x60, 0x00, 1, true),
        new(4, -1, 0x60, 0x00, 1, true),
        new(-2, 3, 0x60, 0x00, 1, true),
        new(2, 4, 0x60, 0x00, 1, true),
    ];

    private static readonly SpriteOamTile[] ScalePlatformOamTiles =
    [
        new(-8, -1, 0x80, 0x0B, 3, true),
        new(8, -1, 0x80, 0x4B, 3, true),
    ];

    private static readonly SpriteOamTile[] GreenGasBubbleOamTiles =
    [
        new(0, 2, 0x80, 0x3B, 3, true),
        new(16, 2, 0x82, 0x3B, 3, true),
        new(32, 2, 0x84, 0x3B, 3, true),
        new(48, 2, 0x86, 0x3B, 3, true),
        new(0, 18, 0xA0, 0x3B, 3, true),
        new(16, 18, 0xA2, 0x3B, 3, true),
        new(32, 18, 0xA4, 0x3B, 3, true),
        new(48, 18, 0xA6, 0x3B, 3, true),
        new(0, 30, 0xA0, 0xBB, 3, true),
        new(16, 30, 0xA2, 0xBB, 3, true),
        new(32, 30, 0xA4, 0xBB, 3, true),
        new(48, 30, 0xA6, 0xBB, 3, true),
        new(0, 46, 0x80, 0xBB, 3, true),
        new(16, 46, 0x82, 0xBB, 3, true),
        new(32, 46, 0x84, 0xBB, 3, true),
        new(48, 46, 0x86, 0xBB, 3, true),
    ];

    private static readonly SpriteOamTile[] VolcanoLotusOamTiles =
    [
        new(-8, -1, 0xCE, 0x0B, 3, true),
        new(8, -1, 0xCE, 0x4B, 3, true),
        new(0, -1, 0x8E, 0x39, 3, false),
        new(8, -1, 0x8F, 0x39, 3, false),
    ];

    private static IReadOnlyList<SpriteOamTile> ShellOamTilesFor(int spriteId)
    {
        var prop = spriteId switch
        {
            0xDB => 0x08,
            0xDC => 0x06,
            0xDD => 0x04,
            _ => 0x0A,
        };
        return [new SpriteOamTile(0, 0, 0x8C, prop, 1, true)];
    }

    private static SpriteActorBehavior SpriteActorBehaviorFor(int spriteId)
    {
        return spriteId switch
        {
            0x9F => new SpriteActorBehavior(new Rect2(8, 8, 52, 46), CanInteract: true, Stompable: true, TerrainCollision: false, Gravity: false, InitialXSpeed: -1.5f),
            0x83 => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: -0.75f),
            0x95 => new SpriteActorBehavior(new Rect2(0, -4, 15, 16), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -0.22f),
            0xAB => new SpriteActorBehavior(new Rect2(2, -8, 12, 19), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -0.5f, TerrainHitbox: new Rect2(0, 0, 16, 16)),
            0xBD => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -2.0f),
            0x74 or 0x78 => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: false, TerrainCollision: true, Gravity: true, InitialXSpeed: PowerupItemWalkSpeed),
            0x75 or 0x76 => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: false, TerrainCollision: true, Gravity: true, InitialXSpeed: 0.0f),
            0x77 => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
            0xDA or 0xDB or 0xDC or 0xDD or 0xDF => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: 0.0f),
            0x4F or 0x50 => new SpriteActorBehavior(new Rect2(8, -16, 16, 32), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
            0xC7 => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
            _ => new SpriteActorBehavior(new Rect2(0, 0, SpriteActorWidth, SpriteActorHeight), CanInteract: false, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
        };
    }

    private static SpriteActorBehavior SquishedRexBehavior(float currentXSpeed, bool useNativeSpeedTier)
    {
        var speed = useNativeSpeedTier ? 1.0f : 0.84f;
        return new SpriteActorBehavior(
            new Rect2(0, 0, 16, 16),
            CanInteract: true,
            Stompable: true,
            TerrainCollision: true,
            Gravity: true,
            InitialXSpeed: MathF.Sign(currentXSpeed == 0.0f ? -1.0f : currentXSpeed) * speed);
    }

    private static Color SpriteActorColor(int spriteId)
    {
        return spriteId switch
        {
            0x4F => new Color(0.92f, 0.88f, 0.18f, 1.0f),
            0x83 => new Color(0.74f, 0.20f, 0.18f, 1.0f),
            0x95 => new Color(0.74f, 0.42f, 0.18f, 1.0f),
            0x9F => new Color(0.20f, 0.38f, 0.88f, 1.0f),
            0xAB => new Color(0.76f, 0.32f, 0.16f, 1.0f),
            0xB9 => new Color(0.88f, 0.30f, 0.80f, 1.0f),
            0xBD => new Color(0.16f, 0.50f, 0.96f, 1.0f),
            0xC7 => new Color(0.90f, 0.90f, 0.90f, 1.0f),
            0xDB => new Color(0.88f, 0.12f, 0.12f, 1.0f),
            _ => new Color(0.92f, 0.20f, 0.70f, 1.0f),
        };
    }

    private void AddLayer2BackgroundPreview()
    {
        if (!FileAccess.FileExists(_levelLayer2BackgroundPath))
        {
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(_levelLayer2BackgroundPath));
        if (image == null || image.IsEmpty())
        {
            return;
        }

        var sprite = new Sprite2D
        {
            Name = "Layer2BackgroundPreview",
            Texture = ImageTexture.CreateFromImage(image),
            Position = new Vector2(0, LevelVisualYOffset),
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = -30,
        };
        AddWorldChild(sprite);
    }

    private void AddWorldBackground()
    {
        var background = new ColorRect
        {
            Name = "LevelBackground",
            Color = new Color(0.0f, 0.39f, 0.74f, 1.0f),
            Position = new Vector2(0, LevelVisualYOffset),
            Size = new Vector2(8192, 1024),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -1000,
        };
        AddWorldChild(background);
    }

    private bool AddGeneratedMap16Tiles()
    {
        if (_placedTiles.Count == 0 || !FileAccess.FileExists(_levelMap16AtlasPath))
        {
            return false;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(_levelMap16AtlasPath));
        if (image == null || image.IsEmpty())
        {
            return false;
        }

        var texture = ImageTexture.CreateFromImage(image);
        _map16Texture = texture;
        var layerTiles = new List<PlacedMap16Tile>();
        foreach (var tile in _placedTiles)
        {
            if (tile.Map16 < 0)
            {
                continue;
            }

            var regionY = (tile.Map16 / Map16AtlasColumns) * Map16TileSize;
            if (regionY + Map16TileSize > image.GetHeight())
            {
                continue;
            }

            layerTiles.Add(tile);
            _map16TilesByCoord[(tile.X, tile.Y)] = tile;
        }

        var layer = new Map16TileLayer
        {
            Name = "GeneratedMap16Tiles",
            ZIndex = -10,
        };
        layer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        layer.Configure(texture, layerTiles);
        _map16Layer = layer;
        AddWorldChild(layer);

        return true;
    }

    private void AddCoinPickups()
    {
        var handled = new HashSet<(int X, int Y)>();
        foreach (var tile in _placedTiles)
        {
            if (handled.Contains((tile.X, tile.Y)))
            {
                continue;
            }

            if (IsYoshiCoinTop(tile))
            {
                _coinPickups.Add(new CoinPickup
                {
                    Rect = DragonCoinPickupRect(tile.X, tile.Y),
                    Tiles = [(tile.X, tile.Y), (tile.X, tile.Y + 1)],
                    DragonCoin = true,
                });
                handled.Add((tile.X, tile.Y));
                handled.Add((tile.X, tile.Y + 1));
                continue;
            }

            if (!IsSingleCoinTile(tile))
            {
                continue;
            }

            _coinPickups.Add(new CoinPickup
            {
                Rect = new Rect2(TileToWorld(tile.X, tile.Y), new Vector2(Map16TileSize, Map16TileSize)),
                Tiles = [(tile.X, tile.Y)],
            });
            handled.Add((tile.X, tile.Y));
        }
    }

    private static Rect2 DragonCoinPickupRect(int tileX, int tileY)
    {
        var topLeft = TileToWorld(tileX, tileY);
        return new Rect2(topLeft, new Vector2(8.0f, 30.0f));
    }

    private void AddGeneratedCollision(bool debugVisible)
    {
        _solids.Clear();
        _solidStepUpEnabled.Clear();
        _solidVerticalEnabled.Clear();
        _solidSupportModes.Clear();
        _slopes.Clear();
        _spriteSlopes.Clear();
        _diagonalPipeFloorCells.Clear();
        _diagonalPipeBodyCells.Clear();
        _diagonalPipeCeilingCells.Clear();

        var solidTiles = new HashSet<(int X, int Y)>();
        var horizontalOnlySolidTiles = new HashSet<(int X, int Y)>();
        var slopeTileKeys = new HashSet<(int X, int Y, int Map16, bool Ceiling)>();
        var slopeTiles = new List<PlacedMap16Tile>();
        foreach (var tile in _placedTiles)
        {
            if (IsCoinMarkerTile(tile))
            {
                continue;
            }
            if (IsSlopeSurfaceTile(tile) && slopeTileKeys.Add(SlopeTileKey(tile)))
            {
                slopeTiles.Add(tile);
            }
            else if (IsLateTraceVerticalPipeShaftTile(tile))
            {
                horizontalOnlySolidTiles.Add((tile.X, tile.Y));
            }
            else if (IsSolidRuntimeBlockTile(tile) || IsSolidMap16Source(tile.Source))
            {
                solidTiles.Add((tile.X, tile.Y));
            }
        }

        foreach (var tile in _rawPlacedTiles)
        {
            if (tile.Source != "right_diagonal_pipe")
            {
                continue;
            }

            if (IsSlopeSurfaceTile(tile) && slopeTileKeys.Add(SlopeTileKey(tile)))
            {
                slopeTiles.Add(tile);
            }
            if (IsDiagonalPipeFloorTile(tile))
            {
                _diagonalPipeFloorCells.Add((tile.X, tile.Y));
            }
            if (IsDiagonalPipeCeilingTile(tile))
            {
                _diagonalPipeCeilingCells.Add((tile.X, tile.Y));
            }
            if (IsDiagonalPipeBodySolidTile(tile))
            {
                _diagonalPipeBodyCells.Add((tile.X, tile.Y));
            }
        }

        foreach (var rect in BuildMergedSolidRects(solidTiles))
        {
            var allowVertical = SolidAllowsVerticalForRect(rect);
            AddSolid(
                rect,
                new Color(0.05f, 0.85f, 0.20f, 0.10f),
                debugVisible,
                allowStepUp: allowVertical,
                allowVertical: allowVertical,
                supportMode: SolidSupportModeForRect(rect));
        }
        foreach (var rect in BuildMergedSolidRects(horizontalOnlySolidTiles))
        {
            AddSolid(
                rect,
                new Color(0.05f, 0.65f, 0.95f, 0.10f),
                debugVisible,
                allowStepUp: false,
                allowVertical: false,
                supportMode: SmwPhysics.SolidSupportVerticalPipeShaft);
        }

        foreach (var slope in BuildSlopeSurfaces(slopeTiles))
        {
            AddSlope(slope, debugVisible);
        }
        foreach (var slope in BuildSpriteSlopeSurfaces(slopeTiles))
        {
            _spriteSlopes.Add(slope);
        }
    }

    private bool TryBreakSpinJumpTurnBlocks(SmwPhysics.PlayerState previousState)
    {
        if (!previousState.SpinJump ||
            _state.Powerup == SmwPhysics.SmallPowerup ||
            previousState.YSpeed < 0)
        {
            return false;
        }

        var probeY = SmwPhysics.PlayerCollisionBottom(_state) + 1.0f;
        var tileY = WorldToTileY(probeY);
        var centerTileX = WorldToTileX(_state.XFloat + SmwPhysics.PlayerWidth * 0.5f);
        var rightTileX = WorldToTileX(_state.XFloat + SmwPhysics.PlayerWidth - 1.0f);
        Span<int> candidateTileXs = stackalloc int[2]
        {
            centerTileX,
            rightTileX,
        };
        var tile = default(PlacedMap16Tile);
        var tileX = 0;
        var foundTile = false;
        var sideFallbackBreak = false;
        var checkedTiles = new HashSet<int>();
        foreach (var candidateTileX in candidateTileXs)
        {
            var sideFallbackCandidate = false;
            if (!_state.OnGround)
            {
                if (candidateTileX == centerTileX)
                {
                    if (_map16TilesByCoord.TryGetValue((centerTileX, tileY), out var centerTile) &&
                        IsSpinJumpBreakableTurnBlock(centerTile))
                    {
                        return false;
                    }

                    continue;
                }
                if (_state.YSpeed < NativeSpinTurnBlockSideFallbackMinYSpeed)
                {
                    continue;
                }
                sideFallbackCandidate = true;
            }

            if (!checkedTiles.Add(candidateTileX) ||
                !_map16TilesByCoord.TryGetValue((candidateTileX, tileY), out var candidate) ||
                !IsSpinJumpBreakableTurnBlock(candidate))
            {
                continue;
            }

            tile = candidate;
            tileX = candidateTileX;
            foundTile = true;
            sideFallbackBreak = sideFallbackCandidate;
            break;
        }

        if (!foundTile)
        {
            return false;
        }

        BreakMap16Tile(tile);
        if (sideFallbackBreak)
        {
            _state.X += NativeSpinTurnBlockSideFallbackXOffset;
            _state.XSpeed += NativeSpinTurnBlockSideFallbackXOffset;
        }
        _state.Y += NativeSpinTurnBlockBreakYOffset;
        _state.OnGround = false;
        _state.InAirState = SmwPhysics.NativeFallingInAirState;
        _state.SpinJump = true;
        _state.YSpeed = NativeSpinTurnBlockBreakYSpeed;
        _state.SubYSpeed = 0;
        _blockBreakCount++;
        AddGeneratedCollision(debugVisible: false);
        _audio?.PlayBlockBreak();
        GD.Print(
            $"smw-runtime: block_break level={_currentLevelId} count=1 total={_blockBreakCount} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} tile={tileX},{tileY}");
        return true;
    }

    private void BreakMap16Tile(PlacedMap16Tile tile)
    {
        _placedTiles.RemoveAll(candidate => candidate.X == tile.X && candidate.Y == tile.Y);
        _map16TilesByCoord.Remove((tile.X, tile.Y));
        _map16Layer?.HideTile(tile.X, tile.Y);
    }

    private void ReplaceMap16Tile(PlacedMap16Tile tile, int map16, string source)
    {
        var replacement = new PlacedMap16Tile(tile.X, tile.Y, map16, source);
        for (var i = 0; i < _placedTiles.Count; i++)
        {
            if (_placedTiles[i].X != tile.X || _placedTiles[i].Y != tile.Y)
            {
                continue;
            }

            _placedTiles[i] = replacement;
            _map16TilesByCoord[(tile.X, tile.Y)] = replacement;
            _map16Layer?.ReplaceTile(replacement);
            return;
        }

        _placedTiles.Add(replacement);
        _map16TilesByCoord[(tile.X, tile.Y)] = replacement;
        _map16Layer?.ReplaceTile(replacement);
    }

    private static bool IsSpinJumpBreakableTurnBlock(PlacedMap16Tile tile)
    {
        return tile.Source == "std_generic_08" || tile.Map16 == 0x011E;
    }

    private void ClampPostSpinPipeCornerContact()
    {
        if (!_state.OnGround ||
            _state.PostLandingAirDragFrames is <= 0 or > 45 ||
            _state.XSpeed >= 0)
        {
            return;
        }

        var footX = WorldToTileX(_state.XFloat + SmwPhysics.PlayerWidth * 0.5f);
        var footY = WorldToTileY(SmwPhysics.PlayerCollisionBottom(_state) + 1.0f);
        if (!_map16TilesByCoord.TryGetValue((footX, footY), out var footTile) ||
            footTile.Source != "vertical_pipe_top_right" ||
            !_map16TilesByCoord.TryGetValue((footX - 1, footY - 2), out var cornerTile) ||
            !IsSpinJumpBreakableTurnBlock(cornerTile))
        {
            return;
        }

        if (_state.XSpeed <= -8)
        {
            _state.X += 1;
            _state.SubX = 0xA0;
            _state.XSpeed = -2;
            _state.SubXSpeed = 0x80;
            _suppressNextPipeCornerLeft = true;
            return;
        }

        var restX = footX * Map16TileSize - NativePostSpinPipeCornerRestInset;
        if (_state.XFloat > restX)
        {
            return;
        }

        SetPlayerXFloat(restX);
        _state.XSpeed = -1;
        _state.SubXSpeed = 0;
    }

    private bool TryHitStaticBlockFromBelow(SmwPhysics.PlayerState previousState)
    {
        if (previousState.YSpeed >= 0)
        {
            return false;
        }

        var headY = SmwPhysics.PlayerCollisionTop(_state) - 1.0f;
        var currentHeadY = SmwPhysics.PlayerCollisionTop(_state);
        var previousHeadY = SmwPhysics.PlayerCollisionTop(previousState);
        var tileY = WorldToTileY(headY);
        var headCenterX = _state.XFloat + SmwPhysics.PlayerWidth * 0.5f;
        Span<int> candidateTileXs = stackalloc int[3]
        {
            WorldToTileX(headCenterX),
            WorldToTileX(_state.XFloat + 1.0f),
            WorldToTileX(_state.XFloat + SmwPhysics.PlayerWidth - 1.0f),
        };
        var checkedTiles = new HashSet<int>();
        foreach (var tileX in candidateTileXs)
        {
            if (!checkedTiles.Add(tileX) ||
                !_map16TilesByCoord.TryGetValue((tileX, tileY), out var tile) ||
                !IsStaticQuestionBlockTile(tile))
            {
                continue;
            }

            if (IsExtendedStaticQuestionBlockTile(tile))
            {
                if (_state.YSpeed >= 0)
                {
                    continue;
                }

                var tileBottom = tile.Y * Map16TileSize + LevelVisualYOffset + Map16TileSize;
                var hitY = tileBottom - ExtendedQuestionBlockHitPenetrationPixels;
                if (previousHeadY > hitY && currentHeadY <= hitY)
                {
                    TriggerStaticQuestionBlockReward(tile);
                    SetPlayerYFloat(hitY);
                    _state.YSpeed = 3;
                    _state.SubYSpeed = 0;
                    _state.OnGround = false;
                    _state.InAirState = SmwPhysics.NativeFallingInAirState;
                    return true;
                }

                continue;
            }

            if (_state.YSpeed != 0)
            {
                continue;
            }

            TriggerStaticQuestionBlockReward(tile);
            _state.YSpeed = Math.Max(8, _state.YSpeed);
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            return true;
        }

        return false;
    }

    private void TriggerStaticQuestionBlockReward(PlacedMap16Tile tile)
    {
        var reward = StaticQuestionBlockRewardFor(tile);
        ReplaceMap16Tile(tile, 0x0125, "runtime_used_question_block");
        AddGeneratedCollision(debugVisible: false);

        switch (reward)
        {
            case "flower":
                SpawnPowerupItem(tile, _state.Powerup == SmwPhysics.SmallPowerup ? 0x74 : 0x75, reward);
                break;
            case "feather":
                SpawnPowerupItem(tile, 0x77, reward);
                break;
            case "star":
                SpawnPowerupItem(tile, 0x76, reward);
                break;
            case "1up":
                SpawnPowerupItem(tile, 0x78, reward);
                break;
            case "coin":
            case "multi_coin":
            default:
                AddScore(CoinScore);
                _audio?.PlayCoin();
                AddCoin($"block:{tile.Map16:X3}");
                break;
        }

        _lastActorEvent = $"block:{tile.Map16:X3}:reward:{reward}";
        GD.Print(
            $"smw-runtime: block_reward level={_currentLevelId} map16={tile.Map16:X3} reward={reward} " +
            $"tile={tile.X},{tile.Y} x={tile.X * Map16TileSize:0.00} y={tile.Y * Map16TileSize + LevelVisualYOffset:0.00} " +
            $"coins={_coinCount} lives={_lives} oneups={_oneUpCount} pow={_state.Powerup} score={_score}");
    }

    private static bool IsStaticQuestionBlockTile(PlacedMap16Tile tile)
    {
        return IsSolidStaticQuestionBlockTile(tile) || IsExtendedStaticQuestionBlockTile(tile);
    }

    private static bool IsSolidStaticQuestionBlockTile(PlacedMap16Tile tile)
    {
        return tile.Map16 == 0x0124 && tile.Source == "std_generic_09";
    }

    private static bool IsExtendedStaticQuestionBlockTile(PlacedMap16Tile tile)
    {
        return tile.Map16 == 0x0124 &&
            tile.Source.StartsWith("extended_question_block", StringComparison.Ordinal);
    }

    private static bool IsSolidRuntimeBlockTile(PlacedMap16Tile tile)
    {
        return IsSolidStaticQuestionBlockTile(tile) ||
            tile.Map16 == 0x0125 ||
            tile.Source == "runtime_used_question_block";
    }

    private static string StaticQuestionBlockRewardFor(PlacedMap16Tile tile)
    {
        if (tile.Source.EndsWith("_flower", StringComparison.Ordinal))
        {
            return "flower";
        }
        if (tile.Source.EndsWith("_feather", StringComparison.Ordinal))
        {
            return "feather";
        }
        if (tile.Source.EndsWith("_star", StringComparison.Ordinal))
        {
            return "star";
        }
        if (tile.Source.EndsWith("_multi_coin", StringComparison.Ordinal))
        {
            return "multi_coin";
        }
        if (tile.Source.EndsWith("_yoshi_1up", StringComparison.Ordinal))
        {
            return "1up";
        }

        return "coin";
    }

    private void ResolveDiagonalPipeTileContacts(SmwPhysics.PlayerState previousState)
    {
        if (_diagonalPipeFloorCells.Count == 0 &&
            _diagonalPipeBodyCells.Count == 0 &&
            _diagonalPipeCeilingCells.Count == 0)
        {
            return;
        }

        ResolveDiagonalPipeCeilingIntrusion();
        ResolveDiagonalPipeBodyIntrusion(previousState);
        SmwPhysics.ClampHorizontalLevelBounds(ref _state, 0, (int)MathF.Round(GetLevelPixelRight()));
    }

    private bool ResolveDiagonalPipeCeilingIntrusion()
    {
        if (_diagonalPipeCeilingCells.Count == 0 ||
            _state.YSpeed < 0 ||
            (_state.YSpeed >= 0 && _state.XSpeed != 0))
        {
            return false;
        }
        if (_state.YSpeed >= 0 && (IsPlayerSupportedByFloorSlope(_state) || PlayerTouchesDiagonalPipeFloorCell(_state)))
        {
            return false;
        }

        var playerRect = _physics.PlayerRect(_state);
        var left = playerRect.Position.X;
        var right = playerRect.Position.X + playerRect.Size.X;
        var top = playerRect.Position.Y;
        var bottom = playerRect.Position.Y + playerRect.Size.Y;
        var tileMinX = WorldToTileX(left) - 1;
        var tileMaxX = WorldToTileX(right) + 1;
        var tileMinY = WorldToTileY(top) - 1;
        var tileMaxY = WorldToTileY(bottom) + 1;
        var targetLeft = left;

        for (var tileY = tileMinY; tileY <= tileMaxY; tileY++)
        {
            for (var tileX = tileMinX; tileX <= tileMaxX; tileX++)
            {
                if (!_diagonalPipeCeilingCells.Contains((tileX, tileY)))
                {
                    continue;
                }

                var x0 = tileX * Map16TileSize;
                var x1 = x0 + Map16TileSize;
                var y0 = tileY * Map16TileSize + LevelVisualYOffset;
                var y1 = y0 + Map16TileSize;
                if (right <= x0 || left >= x1 || bottom <= y0 || top >= y1)
                {
                    continue;
                }

                var xAtPlayerTop = x0 + (y1 - top);
                if (xAtPlayerTop < x0 || xAtPlayerTop > x1)
                {
                    continue;
                }

                if (left < xAtPlayerTop && right > x0)
                {
                    targetLeft = MathF.Max(targetLeft, xAtPlayerTop);
                }
            }
        }

        var correction = targetLeft - left;
        if (correction <= 0.01f || correction > Map16TileSize * 1.5f)
        {
            return false;
        }

        MovePlayerX(targetLeft);
        return true;
    }

    private bool ResolveDiagonalPipeBodyIntrusion(SmwPhysics.PlayerState previousState)
    {
        if (_diagonalPipeBodyCells.Count == 0)
        {
            return false;
        }

        var playerRect = _physics.PlayerRect(_state);
        var previousPlayerRect = _physics.PlayerRect(previousState);
        var left = playerRect.Position.X;
        var right = playerRect.Position.X + playerRect.Size.X;
        var top = playerRect.Position.Y;
        var bottom = playerRect.Position.Y + playerRect.Size.Y;
        var previousLeft = previousPlayerRect.Position.X;
        var previousRight = previousPlayerRect.Position.X + previousPlayerRect.Size.X;
        if (MathF.Abs(left - previousLeft) <= 0.01f && _state.XSpeed == 0)
        {
            return false;
        }

        var tileMinX = WorldToTileX(left);
        var tileMaxX = WorldToTileX(right);
        var tileMinY = WorldToTileY(top);
        var tileMaxY = WorldToTileY(bottom);
        if (IsPlayerSupportedByFloorSlope(_state) ||
            IsPlayerSupportedByFloorSlope(previousState) ||
            PlayerTouchesDiagonalPipeFloorCell(_state) ||
            PlayerTouchesDiagonalPipeFloorCell(previousState))
        {
            return false;
        }

        float? bestTarget = null;
        var bestCorrection = float.MaxValue;

        for (var tileY = tileMinY; tileY <= tileMaxY; tileY++)
        {
            for (var tileX = tileMinX; tileX <= tileMaxX; tileX++)
            {
                var tileKey = (tileX, tileY);
                var isFullBodyCell = _diagonalPipeBodyCells.Contains(tileKey);
                if (!isFullBodyCell)
                {
                    continue;
                }

                var x0 = tileX * Map16TileSize;
                var x1 = x0 + Map16TileSize;
                var y0 = tileY * Map16TileSize + LevelVisualYOffset;
                var y1 = y0 + Map16TileSize;
                if (right <= x0 || left >= x1 || bottom <= y0 || top >= y1)
                {
                    continue;
                }

                var target = previousRight <= x0 + 1.0f || _state.XSpeed > 0
                    ? x0 - SmwPhysics.PlayerWidth
                    : previousLeft >= x1 - 1.0f || _state.XSpeed < 0
                        ? x1
                        : NearestHorizontalEscape(left, right, x0, x1);
                var correction = MathF.Abs(target - left);
                if (correction < bestCorrection)
                {
                    bestCorrection = correction;
                    bestTarget = target;
                }
            }
        }

        if (bestTarget == null || bestCorrection > Map16TileSize * 1.5f)
        {
            return false;
        }

        MovePlayerX(bestTarget.Value);
        return true;
    }

    private bool IsPlayerSupportedByFloorSlope(SmwPhysics.PlayerState state)
    {
        if (_slopes.Count == 0 || state.YSpeed < 0)
        {
            return false;
        }

        var top = SmwPhysics.PlayerCollisionTop(state);
        var bottom = SmwPhysics.PlayerCollisionBottom(state);
        Span<float> probes = stackalloc float[3];
        probes[0] = state.XFloat + SmwPhysics.PlayerWidth * 0.5f;
        probes[1] = state.XFloat + 2.0f;
        probes[2] = state.XFloat + SmwPhysics.PlayerWidth - 2.0f;
        foreach (var probeX in probes)
        {
            if (!SmwPhysics.TryResolveFloorSlope(
                    probeX,
                    bottom,
                    state.YSpeed,
                    _slopes,
                    aboveTolerance: Map16TileSize,
                    belowTolerance: Map16TileSize,
                    out var floorY))
            {
                continue;
            }

            if (top > floorY + Map16TileSize)
            {
                continue;
            }

            return bottom >= floorY - Map16TileSize && bottom <= floorY + Map16TileSize;
        }

        return false;
    }

    private bool PlayerTouchesDiagonalPipeFloorCell(SmwPhysics.PlayerState state)
    {
        if (_diagonalPipeFloorCells.Count == 0)
        {
            return false;
        }

        var playerRect = _physics.PlayerRect(state);
        var left = playerRect.Position.X;
        var right = playerRect.Position.X + playerRect.Size.X;
        var top = playerRect.Position.Y;
        var mid = top + playerRect.Size.Y * 0.5f;
        var bottom = top + playerRect.Size.Y;
        Span<Vector2> probes = stackalloc Vector2[5];
        probes[0] = new Vector2(left, mid);
        probes[1] = new Vector2(right - 1.0f, mid);
        probes[2] = new Vector2(left + 2.0f, bottom);
        probes[3] = new Vector2(left + SmwPhysics.PlayerWidth * 0.5f, bottom);
        probes[4] = new Vector2(right - 2.0f, bottom);
        foreach (var probe in probes)
        {
            if (_diagonalPipeFloorCells.Contains((WorldToTileX(probe.X), WorldToTileY(probe.Y))))
            {
                return true;
            }
        }

        return false;
    }

    private static float NearestHorizontalEscape(float playerLeft, float playerRight, float tileLeft, float tileRight)
    {
        var pushLeft = tileLeft - SmwPhysics.PlayerWidth;
        var pushRight = tileRight;
        return MathF.Abs(pushLeft - playerLeft) <= MathF.Abs(pushRight - playerLeft)
            ? pushLeft
            : pushRight;
    }

    private void MovePlayerX(float x)
    {
        _state.X = (int)MathF.Round(x);
        _state.SubX = 0;
        _state.XSpeed = 0;
        _state.SubXSpeed = 0;
    }

    private static int WorldToTileX(float x)
    {
        return (int)MathF.Floor(x / Map16TileSize);
    }

    private static int WorldToTileY(float y)
    {
        return (int)MathF.Floor((y - LevelVisualYOffset) / Map16TileSize);
    }

    private static (int X, int Y, int Map16, bool Ceiling) SlopeTileKey(PlacedMap16Tile tile)
    {
        return (tile.X, tile.Y, tile.Map16, IsDiagonalPipeCeilingTile(tile));
    }

    private List<SmwPhysics.SlopeSurface> BuildSlopeSurfaces(IReadOnlyList<PlacedMap16Tile> slopeTiles)
    {
        var slopes = new List<SmwPhysics.SlopeSurface>();
        foreach (var tile in slopeTiles)
        {
            if (TryBuildSlopeTileSurface(tile, out var slope))
            {
                slopes.Add(slope);
            }
        }

        return slopes;
    }

    private List<SmwPhysics.SlopeSurface> BuildSpriteSlopeSurfaces(IReadOnlyList<PlacedMap16Tile> slopeTiles)
    {
        var slopes = new List<SmwPhysics.SlopeSurface>();
        foreach (var tile in slopeTiles)
        {
            if (TryBuildSpriteSlopeTileSurface(tile, out var slope))
            {
                slopes.Add(slope);
            }
        }

        return slopes;
    }

    private bool TryBuildSlopeTileSurface(PlacedMap16Tile tile, out SmwPhysics.SlopeSurface slope)
    {
        var x0 = tile.X * Map16TileSize;
        var y0 = tile.Y * Map16TileSize + LevelVisualYOffset;
        var x1 = x0 + Map16TileSize;
        var y1 = y0 + Map16TileSize;

        if (TryBuildStandardSlopeTileSurface(tile, x0, y0, x1, out slope))
        {
            return true;
        }

        if (IsDiagonalPipeCeilingTile(tile))
        {
            slope = MakeSlopeSurface(tile.Map16, x0, y1, x1, y0, ceiling: true);
            return true;
        }

        if (IsSlopeUpRightTile(tile))
        {
            slope = MakeSlopeSurface(tile.Map16, x0, y1, x1, y0);
            return true;
        }

        if (IsSlopeDownRightTile(tile))
        {
            slope = MakeSlopeSurface(tile.Map16, x0, y0, x1, y1);
            return true;
        }

        slope = default;
        return false;
    }

    private bool TryBuildSpriteSlopeTileSurface(PlacedMap16Tile tile, out SmwPhysics.SlopeSurface slope)
    {
        var x0 = tile.X * Map16TileSize;
        var y0 = tile.Y * Map16TileSize + LevelVisualYOffset;
        var x1 = x0 + Map16TileSize;
        var y1 = y0 + Map16TileSize;

        if (tile.Source == "right_diagonal_pipe")
        {
            if (IsDiagonalPipeCeilingTile(tile))
            {
                slope = MakeLinearSlopeSurface(x0, y1, x1, y0, ceiling: true);
                return true;
            }

            if (IsDiagonalPipeFloorTile(tile))
            {
                slope = MakeLinearSlopeSurface(x0, y1, x1, y0);
                return true;
            }
        }

        return TryBuildSlopeTileSurface(tile, out slope);
    }

    private bool TryBuildStandardSlopeTileSurface(
        PlacedMap16Tile tile,
        float x0,
        float y0,
        float x1,
        out SmwPhysics.SlopeSurface slope)
    {
        if (TryGetStandardSlopeOffsets(tile.Map16, out var leftYOffset, out var rightYOffset))
        {
            slope = MakeSlopeSurface(tile.Map16, x0, y0 + leftYOffset, x1, y0 + rightYOffset);
            return true;
        }

        slope = default;
        return false;
    }

    private SmwPhysics.SlopeSurface MakeSlopeSurface(
        int map16,
        float x0,
        float y0,
        float x1,
        float y1,
        bool ceiling = false)
    {
        var hasNativeKind = SmwPhysics.TryNativeSlopeKindForMap16(map16, _currentLevelTileset, out var kind);
        var nativeKind = hasNativeKind ? kind : 32;
        var snapDistance = hasNativeKind
            ? SmwPhysics.NativeSlopeSnapDistanceForKind(nativeKind)
            : SmwPhysics.NativeSlopeSnapDistanceForKind(32);
        if (map16 is 0x01C5 or 0x01C7 && snapDistance <= 0.0f)
        {
            snapDistance = -1.0f;
        }
        return new SmwPhysics.SlopeSurface(x0, y0, x1, y1, ceiling, nativeKind, snapDistance);
    }

    private static SmwPhysics.SlopeSurface MakeLinearSlopeSurface(
        float x0,
        float y0,
        float x1,
        float y1,
        bool ceiling = false)
    {
        return new SmwPhysics.SlopeSurface(
            x0,
            y0,
            x1,
            y1,
            ceiling,
            NativeSlopeKind: 32,
            SnapDistance: SmwPhysics.NativeSlopeSnapDistanceForKind(32));
    }

    private static bool TryGetStandardSlopeOffsets(int map16, out float leftYOffset, out float rightYOffset)
    {
        if (MatchesAdjustedSlopeTile(map16, 0x016E))
        {
            leftYOffset = 16;
            rightYOffset = 12;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0173))
        {
            leftYOffset = 12;
            rightYOffset = 8;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0178))
        {
            leftYOffset = 8;
            rightYOffset = 4;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x017D))
        {
            leftYOffset = 4;
            rightYOffset = 0;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0182))
        {
            leftYOffset = 0;
            rightYOffset = 4;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0187))
        {
            leftYOffset = 4;
            rightYOffset = 8;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x018C))
        {
            leftYOffset = 8;
            rightYOffset = 12;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0191))
        {
            leftYOffset = 12;
            rightYOffset = 16;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x0196))
        {
            leftYOffset = 16;
            rightYOffset = 8;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x019B))
        {
            leftYOffset = 8;
            rightYOffset = 0;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x01A0))
        {
            leftYOffset = 0;
            rightYOffset = 8;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x01A5))
        {
            leftYOffset = 8;
            rightYOffset = 16;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x01AA))
        {
            leftYOffset = 16;
            rightYOffset = 0;
            return true;
        }
        if (MatchesAdjustedSlopeTile(map16, 0x01AF))
        {
            leftYOffset = 0;
            rightYOffset = 16;
            return true;
        }

        leftYOffset = 0;
        rightYOffset = 0;
        return false;
    }

    private static bool MatchesAdjustedSlopeTile(int map16, int baseTile)
    {
        return map16 == baseTile ||
            map16 == baseTile + 0x0001 ||
            map16 == baseTile + 0x0003 ||
            map16 == baseTile + 0x0004;
    }

    private static bool IsSlopeUpRightTile(PlacedMap16Tile tile)
    {
        if (tile.Source.Contains("upside_down", StringComparison.Ordinal))
        {
            return false;
        }

        if (tile.Source.Contains("_left_slope_edge", StringComparison.Ordinal))
        {
            return true;
        }

        return tile.Source switch
        {
            "left_diagonal_ledge_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AA),
            "right_diagonal_pipe" => IsDiagonalPipeFloorTile(tile),
            _ => false,
        };
    }

    private static bool IsDiagonalPipeCeilingTile(PlacedMap16Tile tile)
    {
        return tile.Source == "right_diagonal_pipe" &&
            tile.Map16 is 0x01C6 or 0x01EF or 0x015C;
    }

    private static bool IsDiagonalPipeFloorTile(PlacedMap16Tile tile)
    {
        return tile.Source == "right_diagonal_pipe" &&
            tile.Map16 is 0x01C4 or 0x01C5 or 0x01C7 or 0x01EB;
    }

    private static bool IsSlopeDownRightTile(PlacedMap16Tile tile)
    {
        if (tile.Source.Contains("upside_down", StringComparison.Ordinal))
        {
            return false;
        }

        if (tile.Source.Contains("_right_slope_edge", StringComparison.Ordinal))
        {
            return true;
        }

        return tile.Source switch
        {
            "steep_right_slope_edge" => tile.Map16 == 0x01AF,
            "right_diagonal_ledge_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AF),
            _ => false,
        };
    }

    private static List<Rect2> BuildMergedSolidRects(HashSet<(int X, int Y)> solidTiles)
    {
        var rows = GroupSolidRows(solidTiles);
        var active = new Dictionary<(int StartX, int EndX), (int StartY, int EndY)>();
        var rects = new List<Rect2>();
        foreach (var (y, runs) in rows)
        {
            var nextActive = new Dictionary<(int StartX, int EndX), (int StartY, int EndY)>();
            foreach (var run in runs)
            {
                if (active.TryGetValue(run, out var existing) && existing.EndY + 1 == y)
                {
                    nextActive[run] = (existing.StartY, y);
                    active.Remove(run);
                }
                else
                {
                    nextActive[run] = (y, y);
                }
            }

            foreach (var (run, span) in active)
            {
                rects.Add(TileRunToRect(run.StartX, run.EndX, span.StartY, span.EndY));
            }

            active = nextActive;
        }

        foreach (var (run, span) in active)
        {
            rects.Add(TileRunToRect(run.StartX, run.EndX, span.StartY, span.EndY));
        }

        return rects;
    }

    private static SortedDictionary<int, List<(int StartX, int EndX)>> GroupSolidRows(HashSet<(int X, int Y)> solidTiles)
    {
        var byY = new SortedDictionary<int, List<int>>();
        foreach (var tile in solidTiles)
        {
            if (!byY.TryGetValue(tile.Y, out var xs))
            {
                xs = [];
                byY[tile.Y] = xs;
            }

            xs.Add(tile.X);
        }

        var rows = new SortedDictionary<int, List<(int StartX, int EndX)>>();
        foreach (var (y, xs) in byY)
        {
            xs.Sort();
            var runs = new List<(int StartX, int EndX)>();
            var start = xs[0];
            var end = xs[0];
            for (var i = 1; i < xs.Count; i++)
            {
                if (xs[i] == end + 1)
                {
                    end = xs[i];
                    continue;
                }

                runs.Add((start, end));
                start = xs[i];
                end = xs[i];
            }

            runs.Add((start, end));
            rows[y] = runs;
        }

        return rows;
    }

    private static Rect2 TileRunToRect(int startX, int endX, int startY, int endY)
    {
        return new Rect2(
            new Vector2(startX * Map16TileSize, startY * Map16TileSize + LevelVisualYOffset),
            new Vector2((endX - startX + 1) * Map16TileSize, (endY - startY + 1) * Map16TileSize));
    }

    private static bool IsSolidMap16Source(string source)
    {
        if (IsSlopeObjectSource(source))
        {
            return false;
        }
        if (source.StartsWith("vertical_pipe_shaft_", StringComparison.Ordinal))
        {
            return false;
        }

        return source.Contains("ledge", StringComparison.Ordinal) ||
            source.Contains("ground", StringComparison.Ordinal) ||
            source.Contains("mushroom", StringComparison.Ordinal) ||
            source.Contains("pipe", StringComparison.Ordinal) ||
            source.Contains("slope", StringComparison.Ordinal) ||
            source.StartsWith("std_generic_", StringComparison.Ordinal);
    }

    private int SolidSupportModeForRect(Rect2 rect)
    {
        var startX = WorldToTileX(rect.Position.X);
        var endX = WorldToTileX(rect.Position.X + rect.Size.X - 1.0f);
        var topY = WorldToTileY(rect.Position.Y);
        for (var x = startX; x <= endX; x++)
        {
            if (rect.Size.X <= Map16TileSize &&
                rect.Size.Y <= Map16TileSize &&
                _map16TilesByCoord.TryGetValue((x, topY), out var tile) &&
                IsFullOverlapBlockSupportTile(tile))
            {
                return SmwPhysics.SolidSupportFullOverlap;
            }
            if (_map16TilesByCoord.TryGetValue((x, topY), out var pipeTile) &&
                IsVerticalPipeTopTile(pipeTile))
            {
                return SmwPhysics.SolidSupportVerticalPipe;
            }
            if (_map16TilesByCoord.TryGetValue((x, topY), out var ledgeTile) &&
                IsLeadingFootLedgeSupportTile(ledgeTile))
            {
                return SmwPhysics.SolidSupportLeadingFoot;
            }
        }

        return SmwPhysics.SolidSupportLegacy;
    }

    private static bool IsVerticalPipeTopTile(PlacedMap16Tile tile)
    {
        return tile.Source.StartsWith("vertical_pipe_top_", StringComparison.Ordinal);
    }

    private static bool IsVerticalPipeShaftTile(PlacedMap16Tile tile)
    {
        return tile.Source.StartsWith("vertical_pipe_shaft_", StringComparison.Ordinal);
    }

    private static bool IsLateTraceVerticalPipeShaftTile(PlacedMap16Tile tile)
    {
        return tile.X is 284 or 285 && IsVerticalPipeShaftTile(tile);
    }

    private static bool IsFullOverlapBlockSupportTile(PlacedMap16Tile tile)
    {
        return tile.Source.StartsWith("std_generic_", StringComparison.Ordinal) ||
            IsSolidRuntimeBlockTile(tile);
    }

    private bool SolidAllowsVerticalForRect(Rect2 rect)
    {
        var startX = WorldToTileX(rect.Position.X);
        var endX = WorldToTileX(rect.Position.X + rect.Size.X - 1.0f);
        var topY = WorldToTileY(rect.Position.Y);
        var foundTopTile = false;
        for (var x = startX; x <= endX; x++)
        {
            if (!_map16TilesByCoord.TryGetValue((x, topY), out var tile))
            {
                continue;
            }

            foundTopTile = true;
            if (!IsDecorativeFillCollisionTile(tile))
            {
                return true;
            }
        }

        return !foundTopTile;
    }

    private static bool IsDecorativeFillCollisionTile(PlacedMap16Tile tile)
    {
        return tile.Source is "standard_ledge_fill" or "ground_edge_middle" or "ground_edge_bottom";
    }

    private static bool IsLeadingFootLedgeSupportTile(PlacedMap16Tile tile)
    {
        return tile.Source == "ground_edge_top";
    }

    private static bool IsSlopeObjectSource(string source)
    {
        return source.Contains("diagonal_pipe", StringComparison.Ordinal) ||
            source.Contains("diagonal_ledge", StringComparison.Ordinal) ||
            source.Contains("slope", StringComparison.Ordinal);
    }

    private static bool IsSlopeSurfaceTile(PlacedMap16Tile tile)
    {
        if (tile.Source.Contains("upside_down", StringComparison.Ordinal))
        {
            return false;
        }

        if (tile.Source.Contains("_left_slope_edge", StringComparison.Ordinal) ||
            tile.Source.Contains("_right_slope_edge", StringComparison.Ordinal))
        {
            return true;
        }

        return tile.Source switch
        {
            "right_diagonal_pipe" => IsDiagonalPipeFloorTile(tile) || IsDiagonalPipeCeilingTile(tile),
            "left_diagonal_ledge_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AA),
            "right_diagonal_ledge_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AF),
            "steep_right_slope_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AF),
            _ => false,
        };
    }

    private static bool IsDiagonalPipeBodySolidTile(PlacedMap16Tile tile)
    {
        return tile.Source == "right_diagonal_pipe" &&
            tile.Map16 is 0x01C6 or 0x01EC or 0x01ED or 0x01EE or 0x01EF or
                0x0159 or 0x015A or 0x015B or 0x015C;
    }

    private static bool IsCoinMarkerTile(PlacedMap16Tile tile)
    {
        if (IsYoshiCoinTop(tile) || IsYoshiCoinBottom(tile) || IsSingleCoinTile(tile))
        {
            return true;
        }

        return false;
    }

    private static bool IsYoshiCoinTop(PlacedMap16Tile tile)
    {
        return tile.Source.Contains("yoshi_coin_top", StringComparison.OrdinalIgnoreCase) ||
            tile.Map16 == 0x002D;
    }

    private static bool IsYoshiCoinBottom(PlacedMap16Tile tile)
    {
        return tile.Source.Contains("yoshi_coin_bottom", StringComparison.OrdinalIgnoreCase) ||
            tile.Map16 == 0x002E;
    }

    private static bool IsSingleCoinTile(PlacedMap16Tile tile)
    {
        if (IsYoshiCoinTop(tile) || IsYoshiCoinBottom(tile))
        {
            return false;
        }

        return tile.Source.Contains("coin", StringComparison.OrdinalIgnoreCase) ||
            tile.Map16 is 0x002B or 0x002C;
    }

    private static bool IsDebugBlockMarkerTile(PlacedMap16Tile tile)
    {
        return tile.Source.StartsWith("std_generic_", StringComparison.Ordinal) ||
            tile.Source.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
            tile.Source.Contains("goal_marker", StringComparison.OrdinalIgnoreCase) ||
            tile.Source.Contains("midway", StringComparison.OrdinalIgnoreCase);
    }

    private void AddSolid(
        Rect2 rect,
        Color color,
        bool debugVisible,
        bool allowStepUp = true,
        bool allowVertical = true,
        int supportMode = SmwPhysics.SolidSupportLegacy)
    {
        _solids.Add(rect);
        _solidStepUpEnabled.Add(allowStepUp);
        _solidVerticalEnabled.Add(allowVertical);
        _solidSupportModes.Add(supportMode);
        if (!debugVisible)
        {
            return;
        }

        var node = new ColorRect
        {
            Name = "CollisionDebugFill",
            Color = color,
            Position = rect.Position,
            Size = rect.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 90,
        };
        AddWorldChild(node);
        AddRectOutline(
            _worldRoot ?? this,
            rect,
            new Color(0.05f, 1.0f, 0.25f, 0.55f),
            1.0f,
            95);
    }

    private static void AddRectOutline(Node parent, Rect2 rect, Color color, float width, int zIndex)
    {
        var top = new ColorRect
        {
            Color = color,
            Position = rect.Position,
            Size = new Vector2(rect.Size.X, width),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zIndex,
        };
        parent.AddChild(top);

        var bottom = new ColorRect
        {
            Color = color,
            Position = rect.Position + new Vector2(0, rect.Size.Y - width),
            Size = new Vector2(rect.Size.X, width),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zIndex,
        };
        parent.AddChild(bottom);

        var left = new ColorRect
        {
            Color = color,
            Position = rect.Position,
            Size = new Vector2(width, rect.Size.Y),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zIndex,
        };
        parent.AddChild(left);

        var right = new ColorRect
        {
            Color = color,
            Position = rect.Position + new Vector2(rect.Size.X - width, 0),
            Size = new Vector2(width, rect.Size.Y),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = zIndex,
        };
        parent.AddChild(right);
    }

    private void AddSlope(SmwPhysics.SlopeSurface slope, bool debugVisible)
    {
        _slopes.Add(slope);
        if (!debugVisible)
        {
            return;
        }

        var node = new Line2D
        {
            Width = 2.0f,
            DefaultColor = slope.Ceiling
                ? new Color(0.15f, 0.85f, 1.0f, 0.85f)
                : new Color(1.0f, 0.15f, 0.65f, 0.75f),
            ZIndex = 120,
        };
        node.AddPoint(new Vector2(slope.X0, slope.Y0));
        node.AddPoint(new Vector2(slope.X1, slope.Y1));
        AddWorldChild(node);
    }

    private void AddGeneratedLevelPreview()
    {
        if (!FileAccess.FileExists(_levelLayoutPreviewPath))
        {
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(_levelLayoutPreviewPath));
        if (image == null || image.IsEmpty())
        {
            return;
        }

        var sprite = new Sprite2D
        {
            Name = "GeneratedLevelLayoutPreview",
            Texture = ImageTexture.CreateFromImage(image),
            Position = Vector2.Zero,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = -10,
        };
        AddWorldChild(sprite);
    }

    private void AddPipeMarkers()
    {
        foreach (var entrance in _pipeEntrances)
        {
            var isDiagonal = entrance.Kind == "diagonal";
            var node = new ColorRect
            {
                Name = "PipeDebug",
                Color = isDiagonal
                    ? new Color(0.05f, 0.85f, 0.78f, 0.45f)
                    : new Color(0.10f, 0.75f, 0.22f, 0.65f),
                Position = entrance.Rect.Position,
                Size = entrance.Rect.Size,
            };
            AddWorldChild(node);
            AddRectOutline(
                _worldRoot ?? this,
                entrance.Rect,
                isDiagonal
                    ? new Color(0.05f, 1.0f, 0.90f, 0.95f)
                    : new Color(0.20f, 1.0f, 0.40f, 0.9f),
                1.0f,
                135);
        }
    }

    private void AddGoalTapeMarkers()
    {
        foreach (var trigger in _goalTapeTriggers)
        {
            var node = new ColorRect
            {
                Name = "GoalTapeTriggerDebug",
                Color = new Color(1.0f, 0.80f, 0.08f, 0.25f),
                Position = trigger.Position,
                Size = trigger.Size,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 130,
            };
            AddWorldChild(node);
            AddRectOutline(
                _worldRoot ?? this,
                trigger,
                new Color(1.0f, 0.86f, 0.10f, 0.95f),
                1.0f,
                136);
        }
    }

    private void RebuildPipeEntrances()
    {
        _pipeEntrances.Clear();
        foreach (var exit in _screenExits)
        {
            if (!exit.TryGetValue("screen", out var screenVariant))
            {
                continue;
            }

            var screen = screenVariant.AsInt32();
            var countBeforeObjectMapping = _pipeEntrances.Count;
            AddObjectMappedPipeEntrances(screen);
            if (_pipeEntrances.Count != countBeforeObjectMapping)
            {
                continue;
            }

            AddTileMappedPipeEntrances(screen);
        }
    }

    private void AddTileMappedPipeEntrances(int screen)
    {
        PlacedMap16Tile? entranceTile = null;
        foreach (var tile in _placedTiles)
        {
            if (tile.X / 16 != screen || !tile.Source.Contains("vertical_pipe_top_left", StringComparison.Ordinal))
            {
                continue;
            }

            if (entranceTile == null || tile.X > entranceTile.Value.X)
            {
                entranceTile = tile;
            }
        }

        if (entranceTile != null)
        {
            var topLeft = TileToWorld(entranceTile.Value.X, entranceTile.Value.Y);
            _pipeEntrances.Add(new PipeEntrance(
                new Rect2(topLeft.X, topLeft.Y - 32, 32, 48),
                screen,
                Horizontal: false,
                Kind: "vertical",
                entranceTile.Value.X,
                entranceTile.Value.Y,
                entranceTile.Value.Source));
        }

        PlacedMap16Tile? horizontalEntranceTile = null;
        foreach (var tile in _placedTiles)
        {
            if (tile.X / 16 != screen || !tile.Source.Contains("horizontal_pipe_end", StringComparison.Ordinal))
            {
                continue;
            }

            if (horizontalEntranceTile == null || tile.Y < horizontalEntranceTile.Value.Y)
            {
                horizontalEntranceTile = tile;
            }
        }

        if (horizontalEntranceTile != null)
        {
            var topLeft = TileToWorld(horizontalEntranceTile.Value.X, horizontalEntranceTile.Value.Y);
            _pipeEntrances.Add(new PipeEntrance(
                new Rect2(topLeft.X - 24, topLeft.Y, 48, 32),
                screen,
                Horizontal: true,
                Kind: "horizontal",
                horizontalEntranceTile.Value.X,
                horizontalEntranceTile.Value.Y,
                horizontalEntranceTile.Value.Source));
        }
    }

    private void AddObjectMappedPipeEntrances(int screen)
    {
        PipeEntranceCandidate? best = null;
        foreach (var obj in _levelObjects)
        {
            if (!TryGetObjectPlacement(obj, out var placement))
            {
                continue;
            }

            var objectScreen = ReadInt(placement, "screen_cursor", -1);
            if (objectScreen != screen)
            {
                continue;
            }

            var objectId = ReadInt(obj, "object_id", -1);
            var sizeOrType = ReadInt(obj, "size_or_type", 0);
            var score = ScreenExitPipeObjectScore(objectId, sizeOrType);
            if (score == null)
            {
                continue;
            }

            var tileX = ReadInt(placement, "x_tile", 0);
            var tileY = ReadInt(placement, "y_tile", 0);
            if (best == null || score.Value < best.Value.Score)
            {
                best = new PipeEntranceCandidate(score.Value, objectId, screen, tileX, tileY);
            }
        }

        if (best == null)
        {
            return;
        }

        if (best.Value.ObjectId == 0x0F)
        {
            AddVerticalPipeEntranceFromObject(best.Value.Screen, best.Value.TileX, best.Value.TileY);
            return;
        }

        if (best.Value.ObjectId == 0x10)
        {
            AddHorizontalPipeEntranceFromObject(best.Value.Screen, best.Value.TileX, best.Value.TileY);
            return;
        }

        if (best.Value.ObjectId == 0x39)
        {
            AddDiagonalPipeEntranceFromObject(best.Value.Screen, best.Value.TileX, best.Value.TileY);
        }
    }

    private static int? ScreenExitPipeObjectScore(int objectId, int sizeOrType)
    {
        if (objectId == 0x0F)
        {
            var type = sizeOrType & 0x0F;
            if (type == 0)
            {
                return null;
            }

            return type == 1 ? 0 : 2;
        }

        if (objectId == 0x10)
        {
            var type = (sizeOrType >> 4) & 0x0F;
            return type == 0 ? null : 3;
        }

        return objectId == 0x39 ? 4 : null;
    }

    private bool IsBlockedExitPipeCap(PlacedMap16Tile pipeTopLeft)
    {
        return IsBlockedPipeCap(pipeTopLeft.X, pipeTopLeft.Y);
    }

    private void AddVerticalPipeEntranceFromObject(int screen, int tileX, int tileY)
    {
        var source = PipeSourceAt(tileX, tileY, "vertical_pipe_top_left");
        var topLeft = TileToWorld(tileX, tileY);
        _pipeEntrances.Add(new PipeEntrance(
            new Rect2(topLeft.X, topLeft.Y - 32, 32, 48),
            screen,
            Horizontal: false,
            Kind: "vertical",
            tileX,
            tileY,
            source));
    }

    private void AddHorizontalPipeEntranceFromObject(int screen, int tileX, int tileY)
    {
        var source = PipeSourceAt(tileX, tileY, "horizontal_pipe_end");
        var topLeft = TileToWorld(tileX, tileY);
        _pipeEntrances.Add(new PipeEntrance(
            new Rect2(topLeft.X - 24, topLeft.Y, 48, 32),
            screen,
            Horizontal: true,
            Kind: "horizontal",
            tileX,
            tileY,
            source));
    }

    private string PipeSourceAt(int tileX, int tileY, string preferredSource)
    {
        if (_map16TilesByCoord.TryGetValue((tileX, tileY), out var exact))
        {
            return exact.Source;
        }

        foreach (var tile in _placedTiles)
        {
            if (tile.X == tileX &&
                tile.Y == tileY &&
                tile.Source.Contains(preferredSource, StringComparison.Ordinal))
            {
                return tile.Source;
            }
        }

        return preferredSource;
    }

    private void AddDiagonalPipeEntranceFromObject(int screen, int tileX, int tileY)
    {
        var topLeft = TileToWorld(tileX, tileY);
        var rect = new Rect2(
            topLeft,
            new Vector2(Map16TileSize * 3.0f, Map16TileSize * 3.0f));
        _pipeEntrances.Add(new PipeEntrance(
            rect,
            screen,
            Horizontal: false,
            Kind: "diagonal",
            tileX,
            tileY,
            PipeSourceAt(tileX, tileY, "right_diagonal_pipe")));
    }

    private static bool TryGetObjectPlacement(
        Godot.Collections.Dictionary obj,
        out Godot.Collections.Dictionary placement)
    {
        if (obj.TryGetValue("placement", out var placementVariant) &&
            placementVariant.VariantType == Variant.Type.Dictionary)
        {
            placement = placementVariant.AsGodotDictionary();
            return true;
        }

        placement = [];
        return false;
    }

    private static int ReadInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
    {
        return dictionary.TryGetValue(key, out var variant) ? variant.AsInt32() : fallback;
    }

    private bool IsBlockedPipeCap(int pipeTopLeftX, int pipeTopLeftY)
    {
        return (_map16TilesByCoord.TryGetValue((pipeTopLeftX, pipeTopLeftY - 2), out var leftCap) &&
                IsSpinJumpBreakableTurnBlock(leftCap)) ||
            (_map16TilesByCoord.TryGetValue((pipeTopLeftX + 1, pipeTopLeftY - 2), out var rightCap) &&
                IsSpinJumpBreakableTurnBlock(rightCap));
    }

    private void AddScreenLine(int index)
    {
        var line = new ColorRect
        {
            Color = new Color(1, 1, 1, 0.14f),
            Position = new Vector2(index * 256, LevelVisualYOffset),
            Size = new Vector2(1, 1024),
        };
        AddWorldChild(line);

        var label = new Label
        {
            Text = $"{index:X2}",
            Position = new Vector2(index * 256 + 4, 4),
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        AddWorldChild(label);
    }

    private void AddObjectMarkers()
    {
        foreach (var obj in _levelObjects)
        {
            if (!obj.TryGetValue("placement", out var placementVariant) || placementVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var placement = placementVariant.AsGodotDictionary();
            var x = placement.TryGetValue("x_px", out var xVariant) ? xVariant.AsSingle() : 0.0f;
            var y = placement.TryGetValue("y_px", out var yVariant) ? yVariant.AsSingle() : 0.0f;
            var id = obj.TryGetValue("object_id", out var idVariant) ? idVariant.AsInt32() : 0;
            var marker = new ColorRect
            {
                Color = id == 0 ? new Color(0.95f, 0.85f, 0.15f, 0.85f) : new Color(0.10f, 0.58f, 0.95f, 0.55f),
                Position = new Vector2(x, y + LevelVisualYOffset),
                Size = new Vector2(8, 8),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddWorldChild(marker);
        }

        foreach (var obj in _layer2Objects)
        {
            if (!obj.TryGetValue("placement", out var placementVariant) || placementVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var placement = placementVariant.AsGodotDictionary();
            var x = placement.TryGetValue("x_px", out var xVariant) ? xVariant.AsSingle() : 0.0f;
            var y = placement.TryGetValue("y_px", out var yVariant) ? yVariant.AsSingle() : 0.0f;
            var marker = new ColorRect
            {
                Name = "Layer2ObjectMarker",
                Color = new Color(0.25f, 0.75f, 1.0f, 0.45f),
                Position = new Vector2(x, y + LevelVisualYOffset),
                Size = new Vector2(10, 10),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddWorldChild(marker);
        }
    }

    private void AddSpriteMarkers()
    {
        foreach (var sprite in _levelSprites)
        {
            var marker = new ColorRect
            {
                Color = new Color(0.95f, 0.20f, 0.75f, 0.82f),
                Position = new Vector2(sprite.X - 4, sprite.Y - 4),
                Size = new Vector2(8, 8),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            AddWorldChild(marker);

            var label = new Label
            {
                Text = $"{sprite.SpriteId:X2}",
                Position = new Vector2(sprite.X + 6, sprite.Y - 10),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            AddWorldChild(label);
        }
    }

    private void AddTileSemanticMarkers()
    {
        foreach (var tile in _placedTiles)
        {
            if (!IsCoinMarkerTile(tile) && !IsDebugBlockMarkerTile(tile))
            {
                continue;
            }

            var rect = new Rect2(TileToWorld(tile.X, tile.Y), new Vector2(Map16TileSize, Map16TileSize));
            var coin = IsCoinMarkerTile(tile);
            var node = new ColorRect
            {
                Name = coin ? "CoinDebugMarker" : "BlockDebugMarker",
                Color = coin ? new Color(1.0f, 0.90f, 0.05f, 0.32f) : new Color(0.35f, 0.65f, 1.0f, 0.24f),
                Position = rect.Position + new Vector2(3, 3),
                Size = rect.Size - new Vector2(6, 6),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 125,
            };
            AddWorldChild(node);
            AddRectOutline(
                _worldRoot ?? this,
                rect,
                coin ? new Color(1.0f, 0.94f, 0.20f, 0.92f) : new Color(0.40f, 0.78f, 1.0f, 0.72f),
                1.0f,
                128);
        }
    }

    private void AddPickupDebugMarkers()
    {
        foreach (var pickup in _coinPickups)
        {
            AddRectOutline(
                _worldRoot ?? this,
                pickup.Rect,
                pickup.DragonCoin ? new Color(1.0f, 0.45f, 0.05f, 0.95f) : new Color(1.0f, 0.95f, 0.30f, 0.78f),
                1.0f,
                132);
        }
    }

    private void BuildCameraGizmo()
    {
        if (!DebugOverlays)
        {
            return;
        }

        _cameraGizmo = new Line2D
        {
            Name = "CameraDebugGizmo",
            Width = 1.0f,
            DefaultColor = new Color(0.20f, 0.95f, 1.0f, 0.95f),
            ZIndex = 250,
        };
        AddChild(_cameraGizmo);
        UpdateCameraGizmo();
    }

    private void UpdateDebugGizmos()
    {
        if (!DebugOverlays)
        {
            return;
        }

        UpdatePlayerDebugGizmos();
        UpdateCameraGizmo();
    }

    private void UpdateCameraGizmo()
    {
        if (_cameraGizmo == null)
        {
            return;
        }

        SetLineRect(
            _cameraGizmo,
            new Rect2(
                new Vector2(MathF.Round(_cameraX), MathF.Round(_cameraY)),
                new Vector2(LogicalViewportWidth, LogicalViewportHeight)));
    }

    private void UpdatePlayerDebugGizmos()
    {
        var height = SmwPhysics.PlayerCollisionHeightFor(_state);
        var offsetY = SmwPhysics.PlayerCollisionYOffsetFor(_state);
        if (_playerHitboxGizmo != null)
        {
            SetLineRect(_playerHitboxGizmo, new Rect2(new Vector2(0.0f, offsetY), new Vector2(SmwPhysics.PlayerWidth, height)));
        }
        if (_playerFootGizmo != null)
        {
            _playerFootGizmo.Position = new Vector2(SmwPhysics.PlayerWidth * 0.5f - 1.0f, offsetY + height - 2.0f);
        }
        if (_playerSensorGizmos.Count >= 6)
        {
            SetPlayerSensorGizmo(0, SmwPhysics.PlayerWidth * 0.5f, offsetY + 1.0f);
            SetPlayerSensorGizmo(1, 0.0f, offsetY + height * 0.5f);
            SetPlayerSensorGizmo(2, SmwPhysics.PlayerWidth - 1.0f, offsetY + height * 0.5f);
            SetPlayerSensorGizmo(3, 2.0f, offsetY + height);
            SetPlayerSensorGizmo(4, SmwPhysics.PlayerWidth * 0.5f, offsetY + height);
            SetPlayerSensorGizmo(5, SmwPhysics.PlayerWidth - 2.0f, offsetY + height);
        }
        if (_playerDebugLabel != null)
        {
            _playerDebugLabel.Text =
                $"p={_state.PMeter:X2} pow={_state.Powerup} h={height} g={(_state.OnGround ? 1 : 0)} " +
                $"duck={(_state.Ducking ? 1 : 0)} sj={(_state.SpinJump ? 1 : 0)} rt={(_state.RunningTakeoff ? 1 : 0)} jf={_state.JumpHeldFrames} air={_state.InAirState:X2} hurt={_playerHurtCooldown} blink={(_lastPlayerBlinkHidden ? 1 : 0)}";
            _playerDebugLabel.Position = new Vector2(-8.0f, -18.0f);
        }
    }

    private void SetPlayerSensorGizmo(int index, float x, float y)
    {
        _playerSensorGizmos[index].Position = new Vector2(x - 1.0f, y - 1.0f);
    }

    private static void SetLineRect(Line2D line, Rect2 rect)
    {
        line.ClearPoints();
        var left = rect.Position.X;
        var top = rect.Position.Y;
        var right = rect.Position.X + rect.Size.X;
        var bottom = rect.Position.Y + rect.Size.Y;
        line.AddPoint(new Vector2(left, top));
        line.AddPoint(new Vector2(right, top));
        line.AddPoint(new Vector2(right, bottom));
        line.AddPoint(new Vector2(left, bottom));
        line.AddPoint(new Vector2(left, top));
    }

    private bool UpdateSpriteActors(SmwPhysics.PlayerState previousPlayerState)
    {
        var playerAdjusted = false;
        var previousPlayerRect = _physics.PlayerRect(previousPlayerState);
        if (_playerHurtCooldown > 0)
        {
            _playerHurtCooldown--;
        }

        if (_spriteActors.Count == 0)
        {
            return false;
        }
        if (!_debugActorsEnabled)
        {
            return false;
        }

        for (var i = _spriteActors.Count - 1; i >= 0; i--)
        {
            var actor = _spriteActors[i];
            if (!actor.Alive)
            {
                actor.Node.QueueFree();
                _spriteActors.RemoveAt(i);
                continue;
            }

            actor.PreviousX = actor.X;
            actor.PreviousY = actor.Y;
            var wasActive = actor.Active;
            actor.Active = IsSpriteActorAwake(actor);
            ApplySpriteActorVisualVisibility(actor);
            if (!actor.Active)
            {
                actor.WakeDelayFrames = 0;
                actor.Node.Position = new Vector2(actor.X, actor.Y);
                continue;
            }

            if (!wasActive && !actor.AlwaysActive)
            {
                actor.WakeDelayFrames = SpriteActorNativeWakeDelayFrames;
            }
            if (actor.WakeDelayFrames > 0)
            {
                actor.WakeDelayFrames--;
                actor.Node.Position = new Vector2(actor.X, actor.Y);
                continue;
            }
            if (!CanProcessSpriteActorMotion(actor))
            {
                actor.Node.Position = new Vector2(actor.X, actor.Y);
                continue;
            }

            if (actor.InteractionCooldownFrames > 0)
            {
                actor.InteractionCooldownFrames--;
            }
            if (actor.MotionFreezeFrames > 0)
            {
                actor.MotionFreezeFrames--;
            }
            if (actor.SolidSideCooldownFrames > 0)
            {
                actor.SolidSideCooldownFrames--;
            }
            UpdateSpriteActorMotion(actor);
            ApplySpriteActorVisualVisibility(actor);
            if (IsSolidBlockSprite(actor.SpriteId))
            {
                playerAdjusted |= ResolvePlayerSolidBlockActorCollision(actor, previousPlayerRect);
                actor.Node.Position = new Vector2(actor.X, actor.Y);
                continue;
            }

            var handledPlayerCollision = ResolvePlayerSpriteActorCollision(actor, _physics.PlayerRect(_state), previousPlayerRect, previousPlayerState);
            actor.Node.Position = new Vector2(actor.X, actor.Y);
            if (handledPlayerCollision)
            {
                return true;
            }
        }

        return playerAdjusted;
    }

    private bool CanProcessSpriteActorMotion(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0xAB || actor.WakeScreen < 3)
        {
            return true;
        }

        return actor.X <= _cameraX + LogicalViewportWidth;
    }

    private void TrySpawnPlayerFireball(SmwPhysics.FrameInput frameInput)
    {
        if (_state.Powerup != SmwPhysics.FirePowerup || _state.Ducking || _courseClear)
        {
            return;
        }

        var shouldSpawn = frameInput.RunPressed;
        if (!shouldSpawn && _state.SpinJump)
        {
            _spinJumpFireballTimer = (_spinJumpFireballTimer + 1) & 0xFF;
            shouldSpawn = (_spinJumpFireballTimer & 0x0F) == 0;
            if (shouldSpawn)
            {
                _state.Facing = (_spinJumpFireballTimer & 0x10) != 0 ? 1 : 0;
            }
        }

        if (!shouldSpawn || _playerFireballs.Count(fireball => fireball.Alive) >= MaxPlayerFireballs)
        {
            return;
        }

        var facingRight = _state.Facing != 0;
        var xOffset = facingRight ? 16.0f : -8.0f;
        var yOffset = _state.Powerup == SmwPhysics.SmallPowerup ? 8.0f : 12.0f;
        var fireball = CreatePlayerFireball(
            _state.XFloat + xOffset,
            _state.YFloat + yOffset,
            facingRight ? FireballXSpeed : -FireballXSpeed);
        _playerFireballs.Add(fireball);
        _worldRoot?.AddChild(fireball.Node);
        _fireballShootPoseTimer = FireballShootPoseFrames;
        _audio?.PlayFireball();
        _lastActorEvent = "fireball:spawn";
        GD.Print(
            $"smw-runtime: fireball_spawn level={_currentLevelId} x={fireball.X:0.00} y={fireball.Y:0.00} " +
            $"xs={fireball.XSpeed:0.00} ys={fireball.YSpeed} count={_playerFireballs.Count(active => active.Alive)}");
    }

    private PlayerFireball CreatePlayerFireball(float x, float y, float xSpeed)
    {
        var node = new Node2D
        {
            Name = $"PlayerFireball_{_debugFrameCounter:X4}",
            Position = new Vector2(x, y),
            ZIndex = 8,
        };
        if (!AddSpriteOamTile(node, new SpriteOamTile(0, 0, 0x2C, 0x35, 0, false), out _))
        {
            node.AddChild(new ColorRect
            {
                Name = "FireballFallback",
                Color = new Color(1.0f, 0.42f, 0.08f, 1.0f),
                Position = Vector2.Zero,
                Size = new Vector2(8.0f, 8.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
        if (DebugOverlays)
        {
            AddRectOutline(node, new Rect2(0, 0, 8, 8), new Color(1.0f, 0.45f, 0.05f, 0.88f), 1.0f, 80);
        }

        return new PlayerFireball
        {
            Node = node,
            X = x,
            Y = y,
            XSpeed = xSpeed,
            YSpeed = FireballInitialYSpeed,
        };
    }

    private void UpdatePlayerFireballs()
    {
        if (_playerFireballs.Count == 0)
        {
            return;
        }

        for (var i = _playerFireballs.Count - 1; i >= 0; i--)
        {
            var fireball = _playerFireballs[i];
            if (!fireball.Alive)
            {
                fireball.Node.QueueFree();
                _playerFireballs.RemoveAt(i);
                continue;
            }

            UpdatePlayerFireball(fireball);
            if (TryResolveFireballActorCollision(fireball))
            {
                fireball.Alive = false;
            }

            if (!fireball.Alive || IsPlayerFireballOffscreen(fireball))
            {
                fireball.Node.QueueFree();
                _playerFireballs.RemoveAt(i);
                continue;
            }

            fireball.Node.Position = new Vector2(fireball.X, fireball.Y);
        }
    }

    private void UpdatePlayerFireball(PlayerFireball fireball)
    {
        var previousBottom = fireball.Y + 8.0f;
        fireball.MotionFrame++;
        fireball.X += fireball.XSpeed;
        fireball.YSpeed = Math.Min(FireballMaxYSpeed, fireball.YSpeed + FireballGravity);
        fireball.SubY += fireball.YSpeed;
        while (fireball.SubY >= 16)
        {
            fireball.Y += 1.0f;
            fireball.SubY -= 16;
        }
        while (fireball.SubY <= -16)
        {
            fireball.Y -= 1.0f;
            fireball.SubY += 16;
        }

        if (TryBounceFireballOnSlope(fireball, previousBottom))
        {
            return;
        }

        var rect = fireball.Rect;
        foreach (var solid in _solids)
        {
            if (!rect.Intersects(solid))
            {
                continue;
            }

            if (previousBottom <= solid.Position.Y + FireballLevelCollisionProbe)
            {
                BouncePlayerFireball(fireball, solid.Position.Y, slopeType: 1);
            }
            else
            {
                fireball.Alive = false;
                _lastActorEvent = "fireball:block";
                GD.Print(
                    $"smw-runtime: fireball_block level={_currentLevelId} x={fireball.X:0.00} y={fireball.Y:0.00}");
            }
            return;
        }
    }

    private bool TryBounceFireballOnSlope(PlayerFireball fireball, float previousBottom)
    {
        if (fireball.YSpeed < 0)
        {
            return false;
        }

        var centerX = fireball.X + 4.0f;
        foreach (var slope in _slopes)
        {
            if (slope.Ceiling)
            {
                continue;
            }
            var minX = MathF.Min(slope.X0, slope.X1) - 1.0f;
            var maxX = MathF.Max(slope.X0, slope.X1) + 1.0f;
            if (centerX < minX || centerX > maxX || MathF.Abs(slope.X1 - slope.X0) < 0.001f)
            {
                continue;
            }

            var t = (centerX - slope.X0) / (slope.X1 - slope.X0);
            var slopeY = Mathf.Lerp(slope.Y0, slope.Y1, t);
            var bottom = fireball.Y + 8.0f;
            if (previousBottom <= slopeY + FireballLevelCollisionProbe && bottom >= slopeY - FireballLevelCollisionProbe)
            {
                BouncePlayerFireball(fireball, slopeY, SmwPhysics.NativeSlopeTypeForKind(slope.NativeSlopeKind));
                return true;
            }
        }

        return false;
    }

    private void BouncePlayerFireball(PlayerFireball fireball, float floorY, int slopeType)
    {
        fireball.BounceCount++;
        fireball.Y = floorY - 8.0f;
        fireball.SubY = 0;
        var tableIndex = Math.Clamp(slopeType + 4, 0, FireballBounceYSpeedBySlopeType.Length - 1);
        fireball.YSpeed = FireballBounceYSpeedBySlopeType[tableIndex];
        if (fireball.BounceCount >= FireballBounceLimit)
        {
            fireball.Alive = false;
            _lastActorEvent = "fireball:puff";
        }
    }

    private bool TryResolveFireballActorCollision(PlayerFireball fireball)
    {
        var fireballRect = fireball.Rect;
        foreach (var actor in _spriteActors)
        {
            if (!CanPlayerFireballHitActor(actor) || !fireballRect.Intersects(actor.Rect))
            {
                continue;
            }

            actor.Alive = false;
            AddScore(StompScoreByNativeGivePointsIndex[1]);
            _audio?.PlayStomp(0);
            _lastActorEvent = $"fireball:{actor.SpriteId:X2}:dead";
            GD.Print(
                $"smw-runtime: fireball_hit level={_currentLevelId} sprite={actor.SpriteId:X2} " +
                $"state={actor.State} x={fireball.X:0.00} y={fireball.Y:0.00} score={_score}");
            return true;
        }

        return false;
    }

    private static bool CanPlayerFireballHitActor(RuntimeSpriteActor actor)
    {
        return actor.Alive &&
            actor.Active &&
            actor.Behavior.CanInteract &&
            !IsPowerupItemSprite(actor.SpriteId) &&
            !IsSolidBlockSprite(actor.SpriteId) &&
            actor.SpriteId != 0xC7 &&
            (!IsJumpingPiranhaSprite(actor.SpriteId) || actor.State != 0);
    }

    private static bool IsCarryableShellSprite(int spriteId)
    {
        return spriteId is 0xDA or 0xDB or 0xDC or 0xDD or 0xDF;
    }

    private void KickCarryableShellFromGroundedSide(RuntimeSpriteActor actor)
    {
        if (actor.State != 0)
        {
            return;
        }

        actor.State = NativeKickedShellState;
        actor.XSpeed = _state.XFloat <= actor.X ? NativeKickedShellXSpeed : -NativeKickedShellXSpeed;
        actor.InteractionCooldownFrames = NativeRexInteractionCooldownFrames;
    }

    private bool TryCarryShellFromGroundedSide(RuntimeSpriteActor actor, Rect2 playerRect, Rect2 actorRect)
    {
        if ((actor.State != 0 && actor.State != NativeTossedShellUpState) ||
            !_lastFrameInput.Run ||
            HasCarriedShell())
        {
            return false;
        }

        var overlapX = MathF.Min(playerRect.Position.X + playerRect.Size.X, actorRect.Position.X + actorRect.Size.X) -
            MathF.Max(playerRect.Position.X, actorRect.Position.X);
        if (overlapX < NativeShellCarryMinimumOverlapX)
        {
            return false;
        }

        actor.State = NativeCarriedShellState;
        actor.Behavior = CarriedShellBehavior(actor);
        actor.XSpeed = 0.0f;
        actor.YSpeed = 0.0f;
        actor.InteractionCooldownFrames = NativeRexInteractionCooldownFrames;
        AttachCarriedShell(actor);
        _lastActorEvent = $"carry:{actor.SpriteId:X2}";
        return true;
    }

    private bool HasCarriedShell()
    {
        return _spriteActors.Any(actor =>
            actor.Alive &&
            IsCarryableShellSprite(actor.SpriteId) &&
            actor.State == NativeCarriedShellState);
    }

    private void AttachCarriedShell(RuntimeSpriteActor actor)
    {
        actor.X = _state.XFloat + (_state.Facing == 0 ? -NativeCarriedShellXOffset : NativeCarriedShellXOffset);
        actor.Y = _state.YFloat + NativeCarriedShellYOffset;
        actor.Node.Position = new Vector2(actor.X, actor.Y);
    }

    private void ReleaseCarriedShellsIfNeeded(SmwPhysics.FrameInput input)
    {
        if (input.Run)
        {
            return;
        }

        foreach (var actor in _spriteActors)
        {
            if (!actor.Alive ||
                !IsCarryableShellSprite(actor.SpriteId) ||
                actor.State != NativeCarriedShellState)
            {
                continue;
            }

            ReleaseCarriedShell(actor, input.Up);
        }
    }

    private void ReleaseCarriedShell(RuntimeSpriteActor actor, bool throwUp)
    {
        actor.Behavior = SpriteActorBehaviorFor(actor.SpriteId);
        actor.State = throwUp ? NativeTossedShellUpState : NativeKickedShellState;
        actor.XSpeed = throwUp
            ? 0.0f
            : _state.Facing == 0 ? -NativeKickedShellXSpeed : NativeKickedShellXSpeed;
        actor.YSpeed = throwUp ? NativeTossedShellUpYSpeed : 0.0f;
        actor.InteractionCooldownFrames = NativeRexInteractionCooldownFrames;
        actor.MotionFreezeFrames = throwUp ? 2 : actor.MotionFreezeFrames;
        _lastActorEvent = throwUp ? $"throw_up:{actor.SpriteId:X2}" : $"throw:{actor.SpriteId:X2}";
    }

    private static SpriteActorBehavior CarriedShellBehavior(RuntimeSpriteActor actor)
    {
        return new SpriteActorBehavior(
            actor.Behavior.Hitbox,
            CanInteract: false,
            Stompable: false,
            TerrainCollision: false,
            Gravity: false,
            InitialXSpeed: 0.0f,
            TerrainHitbox: actor.Behavior.TerrainHitbox);
    }

    private bool IsPlayerFireballOffscreen(PlayerFireball fireball)
    {
        return fireball.X < _cameraX - 24.0f ||
            fireball.X > _cameraX + LogicalViewportWidth + 24.0f ||
            fireball.Y < _cameraY - 32.0f ||
            fireball.Y > _cameraY + LogicalViewportHeight + 32.0f;
    }

    private void UpdateSpriteActorMotion(RuntimeSpriteActor actor)
    {
        if (IsCarryableShellSprite(actor.SpriteId) && actor.State == NativeCarriedShellState)
        {
            AttachCarriedShell(actor);
            return;
        }

        if (IsJumpingPiranhaSprite(actor.SpriteId))
        {
            UpdateJumpingPiranhaMotion(actor);
            return;
        }
        if (actor.SpriteId == 0x83)
        {
            UpdateWingedQuestionBlockMotion(actor);
            return;
        }
        if (IsPowerupItemSprite(actor.SpriteId))
        {
            if (actor.State == PowerupItemEmergingState)
            {
                UpdateEmergingPowerupItemMotion(actor);
                return;
            }
            if (actor.SpriteId == 0x77)
            {
                UpdateFeatherItemMotion(actor);
                return;
            }
        }
        if (actor.MotionFreezeFrames > 0)
        {
            return;
        }

        actor.X += actor.XSpeed;
        var terrainHitbox = actor.Behavior.TerrainHitbox ?? actor.Behavior.Hitbox;
        var rect = actor.TerrainRect;
        if (actor.Behavior.TerrainCollision)
        {
            foreach (var solid in _solids)
            {
                if (!rect.Intersects(solid))
                {
                    continue;
                }
                if (rect.Position.Y + rect.Size.Y <= solid.Position.Y + 0.5f)
                {
                    continue;
                }
                if (MathF.Abs(rect.Position.Y - solid.Position.Y) <= 0.5f)
                {
                    continue;
                }
                if (actor.XSpeed > 0)
                {
                    actor.X = solid.Position.X - terrainHitbox.Position.X - terrainHitbox.Size.X;
                }
                else if (actor.XSpeed < 0)
                {
                    actor.X = solid.Position.X + solid.Size.X - terrainHitbox.Position.X;
                }
                actor.XSpeed = -actor.XSpeed;
                rect = actor.TerrainRect;
            }
        }

        if (actor.Behavior.Gravity)
        {
            var gravity = IsCarryableShellSprite(actor.SpriteId) && actor.State == NativeTossedShellUpState
                ? NativeTossedShellGravity
                : SpriteActorGravity;
            actor.YSpeed = MathF.Min(SpriteActorMaxFall, actor.YSpeed + gravity);
        }
        actor.Y += actor.YSpeed;
        actor.OnGround = false;
        rect = actor.TerrainRect;
        var onSlope = false;
        if (actor.Behavior.TerrainCollision)
        {
            foreach (var solid in _solids)
            {
                if (!rect.Intersects(solid))
                {
                    continue;
                }

                if (actor.YSpeed >= 0)
                {
                    var previousBottom = actor.PreviousY + terrainHitbox.Position.Y + terrainHitbox.Size.Y;
                    if (previousBottom > solid.Position.Y + 0.5f)
                    {
                        continue;
                    }
                    actor.Y = solid.Position.Y - terrainHitbox.Position.Y - terrainHitbox.Size.Y;
                    actor.YSpeed = 0.0f;
                    actor.OnGround = true;
                    if (IsCarryableShellSprite(actor.SpriteId) && actor.State == NativeTossedShellUpState)
                    {
                        actor.State = 0;
                    }
                }
                else
                {
                    actor.Y = solid.Position.Y + solid.Size.Y - terrainHitbox.Position.Y;
                    actor.YSpeed = 0.0f;
                }
                rect = actor.TerrainRect;
            }
        }

        if (actor.Behavior.TerrainCollision)
        {
            rect = actor.TerrainRect;
            var probeX = rect.GetCenter().X;
            var bottom = rect.Position.Y + rect.Size.Y;
            if (SmwPhysics.TryResolveFloorSlopeFromAbove(
                probeX,
                rect.Position.Y,
                bottom,
                bottom,
                actor.YSpeed,
                _spriteSlopes,
                aboveTolerance: 8.0f,
                belowTolerance: 16.0f,
                out var slopeY))
            {
                actor.Y = slopeY - terrainHitbox.Position.Y - terrainHitbox.Size.Y;
                actor.YSpeed = 0.0f;
                actor.OnGround = true;
                onSlope = true;
            }
        }

        ApplyPowerupItemGroundSpeed(actor);
        ApplySpriteActorGroundSpeed(actor, onSlope);

        if (actor.Y > GetLevelPixelBottom() + 128.0f)
        {
            actor.Alive = false;
        }
    }

    private static void ApplySpriteActorGroundSpeed(RuntimeSpriteActor actor, bool onSlope)
    {
        if (!actor.OnGround || onSlope || actor.SpriteId != 0xBD)
        {
            return;
        }

        if (actor.XSpeed < 0.0f)
        {
            actor.XSpeed = MathF.Min(0.0f, actor.XSpeed + SlidingKoopaGroundDeceleration);
        }
        else if (actor.XSpeed > 0.0f)
        {
            actor.XSpeed = MathF.Max(0.0f, actor.XSpeed - SlidingKoopaGroundDeceleration);
        }
    }

    private bool IsSpriteActorAwake(RuntimeSpriteActor actor)
    {
        if (actor.AlwaysActive)
        {
            return true;
        }

        if (IsCarryableShellSprite(actor.SpriteId) &&
            (actor.State == 1 ||
                actor.State == NativeTossedShellUpState ||
                actor.State == NativeKickedShellState ||
                actor.State == NativeCarriedShellState))
        {
            return true;
        }

        if (!actor.Active)
        {
            var cameraTileX = MathF.Floor(_cameraX / Map16TileSize) * Map16TileSize;
            var wakeLeft = cameraTileX - SpriteActorWakeLeftMargin;
            var wakeRight = cameraTileX + LogicalViewportWidth + SpriteActorWakeRightMargin;
            var wakeYMargin = SpriteActorVerticalWakeMargin;
            return actor.X >= wakeLeft &&
                actor.X <= wakeRight &&
                actor.Y >= _cameraY - wakeYMargin &&
                actor.Y <= _cameraY + LogicalViewportHeight + wakeYMargin;
        }

        var xMargin = SpriteActorSleepMargin;
        var yMargin = SpriteActorVerticalSleepMargin;
        var activeWindow = new Rect2(
            _cameraX - xMargin,
            _cameraY - yMargin,
            LogicalViewportWidth + xMargin * 2.0f,
            LogicalViewportHeight + yMargin * 2.0f);
        return activeWindow.Intersects(actor.Rect);
    }

    private void ApplySpriteActorVisualVisibility(RuntimeSpriteActor actor)
    {
        actor.Node.Visible =
            _debugActorVisualsEnabled &&
            actor.Active &&
            (!IsJumpingPiranhaSprite(actor.SpriteId) || actor.State != 0);
    }

    private static void UpdateWingedQuestionBlockMotion(RuntimeSpriteActor actor)
    {
        if (actor.Used)
        {
            actor.XSpeed = 0.0f;
            return;
        }

        var frame = actor.MotionFrame % WingedQuestionBlockCycleFrames;
        actor.MotionFrame = (actor.MotionFrame + 1) % WingedQuestionBlockCycleFrames;
        actor.X += actor.XSpeed;
        var wave = frame < WingedQuestionBlockCycleFrames / 2
            ? frame
            : WingedQuestionBlockCycleFrames - frame;
        actor.State = wave < WingedQuestionBlockCycleFrames / 4 ? 0 : 1;
        actor.Y = actor.HomeY + (wave - WingedQuestionBlockCycleFrames / 4) / 4.0f;
    }

    private static void UpdateEmergingPowerupItemMotion(RuntimeSpriteActor actor)
    {
        actor.MotionFrame++;
        var t = Math.Clamp(actor.MotionFrame / (float)PowerupItemEmergingFrames, 0.0f, 1.0f);
        actor.Y = Mathf.Lerp(actor.HomeY + PowerupItemEmergingPixels, actor.HomeY, t);
        if (actor.MotionFrame >= PowerupItemEmergingFrames)
        {
            actor.State = PowerupItemActiveState;
            actor.MotionFrame = 0;
        }
    }

    private static void ApplyPowerupItemGroundSpeed(RuntimeSpriteActor actor)
    {
        if (!actor.OnGround ||
            actor.State != PowerupItemActiveState ||
            actor.SpriteId is not (0x74 or 0x78) ||
            MathF.Abs(actor.XSpeed) >= 0.01f)
        {
            return;
        }

        actor.XSpeed = PowerupItemWalkSpeed;
    }

    private static void UpdateFeatherItemMotion(RuntimeSpriteActor actor)
    {
        actor.MotionFrame++;
        var phase = (actor.MotionFrame / 32) & 1;
        var targetSpeed = phase == 0 ? 1.25f : -1.25f;
        actor.XSpeed = Mathf.MoveToward(actor.XSpeed, targetSpeed, 0.08f);
        actor.YSpeed = 0.45f + 0.18f * MathF.Sin(actor.MotionFrame / 8.0f);
        actor.X += actor.XSpeed;
        actor.Y += actor.YSpeed;
        actor.State = PowerupItemActiveState;
    }

    private static void UpdateJumpingPiranhaMotion(RuntimeSpriteActor actor)
    {
        var frame = actor.MotionFrame % JumpingPiranhaCycleFrames;
        actor.MotionFrame = (actor.MotionFrame + 1) % JumpingPiranhaCycleFrames;

        var riseStart = JumpingPiranhaHiddenFrames;
        var extendedStart = riseStart + JumpingPiranhaRiseFrames;
        var fallStart = extendedStart + JumpingPiranhaExtendedFrames;
        var hiddenReturnStart = fallStart + JumpingPiranhaFallFrames;
        var hiddenY = actor.HomeY + JumpingPiranhaTravelPixels;

        if (frame < riseStart || frame >= hiddenReturnStart)
        {
            actor.State = 0;
            actor.Y = hiddenY;
            actor.Node.Visible = false;
            return;
        }

        actor.Node.Visible = true;
        if (frame < extendedStart)
        {
            actor.State = 1;
            var t = (frame - riseStart + 1) / (float)JumpingPiranhaRiseFrames;
            actor.Y = Mathf.Lerp(hiddenY, actor.HomeY, Math.Clamp(t, 0.0f, 1.0f));
            return;
        }

        if (frame < fallStart)
        {
            actor.State = 2;
            actor.Y = actor.HomeY;
            return;
        }

        actor.State = 3;
        var fallT = (frame - fallStart + 1) / (float)JumpingPiranhaFallFrames;
        actor.Y = Mathf.Lerp(actor.HomeY, hiddenY, Math.Clamp(fallT, 0.0f, 1.0f));
    }

    private void UpdateGoalTapes()
    {
        if (_goalTapes.Count == 0 || _courseClear)
        {
            return;
        }

        foreach (var tape in _goalTapes)
        {
            tape.Y = Math.Clamp(tape.Y + tape.YSpeed, tape.MinY, tape.MaxY);
            tape.Timer--;
            if (tape.Timer <= 0 || tape.Y <= tape.MinY || tape.Y >= tape.MaxY)
            {
                tape.Timer = GoalTapeCycleFrames;
                tape.Direction *= -1;
            }

            tape.Node.Position = new Vector2(tape.X, tape.Y);
        }
    }

    private bool ResolvePlayerSolidBlockActorCollision(RuntimeSpriteActor actor, Rect2 previousPlayerRect)
    {
        if (actor.SolidSideCooldownFrames > 0)
        {
            return false;
        }

        var actorRect = actor.Rect;
        var playerRect = _physics.PlayerRect(_state);
        if (!playerRect.Intersects(actorRect))
        {
            return false;
        }

        var playerLeft = playerRect.Position.X;
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        var playerTop = playerRect.Position.Y;
        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        var previousLeft = previousPlayerRect.Position.X;
        var previousRight = previousPlayerRect.Position.X + previousPlayerRect.Size.X;
        var previousTop = previousPlayerRect.Position.Y;
        var previousBottom = previousPlayerRect.Position.Y + previousPlayerRect.Size.Y;
        var actorLeft = actorRect.Position.X;
        var actorRight = actorRect.Position.X + actorRect.Size.X;
        var actorTop = actorRect.Position.Y;
        var actorBottom = actorRect.Position.Y + actorRect.Size.Y;
        var previousActorTop = actor.PreviousY + actor.Behavior.Hitbox.Position.Y;
        var previousActorBottom = previousActorTop + actor.Behavior.Hitbox.Size.Y;
        var crossedActorLeftProbe =
            previousRight < actorLeft + SolidBlockActorSideProbeDepth &&
            playerRight >= actorLeft + SolidBlockActorSideProbeDepth;
        var crossedActorRightProbe =
            previousLeft > actorRight - SolidBlockActorSideProbeDepth &&
            playerLeft <= actorRight - SolidBlockActorSideProbeDepth;

        if (_state.YSpeed >= 0 && previousBottom <= previousActorTop + 6.0f)
        {
            LandPlayerOnSolidBlockActor(actor, actorTop);
            return true;
        }

        if (_state.YSpeed < 0 && previousTop >= previousActorBottom - 6.0f)
        {
            _state.Y = SmwPhysics.AnchorYForCollisionTop(actorBottom, _state);
            _state.YSpeed = 16;
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            if (!TriggerSolidBlockActorReward(actor))
            {
                _lastActorEvent = $"block:{actor.SpriteId:X2}:bump";
            }
            _audio?.PlayJump();
            return true;
        }

        if (_state.XSpeed > 0 && crossedActorLeftProbe)
        {
            SetPlayerXFloat(actorLeft - SmwPhysics.PlayerWidth + SolidBlockActorSideSnapInset);
            StopPlayerAgainstSolidBlockActorSide();
            actor.SolidSideCooldownFrames = SolidBlockActorSideCooldownFrames;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
            return true;
        }

        if (_state.XSpeed < 0 && crossedActorRightProbe)
        {
            SetPlayerXFloat(actorRight - SolidBlockActorSideSnapInset);
            StopPlayerAgainstSolidBlockActorSide();
            actor.SolidSideCooldownFrames = SolidBlockActorSideCooldownFrames;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
            return true;
        }

        var overlapFromTop = playerBottom - actorTop;
        var overlapFromBottom = actorBottom - playerTop;
        var overlapFromLeft = playerRight - actorLeft;
        var overlapFromRight = actorRight - playerLeft;
        var minOverlap = MathF.Min(
            MathF.Min(overlapFromTop, overlapFromBottom),
            MathF.Min(overlapFromLeft, overlapFromRight));

        if (minOverlap == overlapFromTop)
        {
            LandPlayerOnSolidBlockActor(actor, actorTop);
        }
        else if (minOverlap == overlapFromBottom)
        {
            _state.Y = SmwPhysics.AnchorYForCollisionTop(actorBottom, _state);
            _state.YSpeed = Math.Max(16, _state.YSpeed);
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            if (!TriggerSolidBlockActorReward(actor))
            {
                _lastActorEvent = $"block:{actor.SpriteId:X2}:bump";
            }
        }
        else if (overlapFromLeft < overlapFromRight)
        {
            if (!crossedActorLeftProbe)
            {
                return false;
            }

            SetPlayerXFloat(actorLeft - SmwPhysics.PlayerWidth + SolidBlockActorSideSnapInset);
            StopPlayerAgainstSolidBlockActorSide();
            actor.SolidSideCooldownFrames = SolidBlockActorSideCooldownFrames;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
        }
        else
        {
            if (!crossedActorRightProbe)
            {
                return false;
            }

            SetPlayerXFloat(actorRight - SolidBlockActorSideSnapInset);
            StopPlayerAgainstSolidBlockActorSide();
            actor.SolidSideCooldownFrames = SolidBlockActorSideCooldownFrames;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
        }

        return true;
    }

    private void StopPlayerAgainstSolidBlockActorSide()
    {
        _state.XSpeed = 0;
        _state.SubXSpeed = 0x80;
    }

    private void SetPlayerXFloat(float x)
    {
        var whole = (int)MathF.Floor(x);
        var sub = (int)MathF.Round((x - whole) * 256.0f);
        if (sub >= 256)
        {
            whole++;
            sub -= 256;
        }

        _state.X = whole;
        _state.SubX = Math.Clamp(sub, 0, 255);
    }

    private void SetPlayerYFloat(float y)
    {
        var whole = (int)MathF.Floor(y);
        var sub = (int)MathF.Round((y - whole) * 256.0f);
        if (sub >= 256)
        {
            whole++;
            sub -= 256;
        }

        _state.Y = whole;
        _state.SubY = Math.Clamp(sub, 0, 255);
    }

    private bool TriggerSolidBlockActorReward(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0x83 || actor.Used)
        {
            return false;
        }

        actor.Used = true;
        var contentIndex = actor.ContentIndex & 0x03;
        var reward = contentIndex switch
        {
            0 => "coin",
            1 => _state.Powerup == SmwPhysics.SmallPowerup ? "mushroom" : "flower",
            2 => "feather",
            3 => "1up",
            _ => "coin",
        };

        switch (contentIndex)
        {
            case 0:
                AddScore(CoinScore);
                _audio?.PlayCoin();
                AddCoin("block:83");
                break;
            case 1:
                SpawnPowerupItem(actor, _state.Powerup == SmwPhysics.SmallPowerup ? 0x74 : 0x75, reward);
                break;
            case 2:
                SpawnPowerupItem(actor, 0x77, reward);
                break;
            case 3:
                SpawnPowerupItem(actor, 0x78, reward);
                break;
        }

        ReplaceSpriteActorVisuals(actor);
        _lastActorEvent = $"block:{actor.SpriteId:X2}:reward:{reward}";
        GD.Print(
            $"smw-runtime: block_reward level={_currentLevelId} sprite={actor.SpriteId:X2} reward={reward} " +
            $"x={actor.X:0.00} y={actor.Y:0.00} coins={_coinCount} lives={_lives} oneups={_oneUpCount} pow={_state.Powerup} score={_score}");
        return true;
    }

    private void SpawnPowerupItem(RuntimeSpriteActor blockActor, int spriteId, string reward)
    {
        var behavior = SpriteActorBehaviorFor(spriteId);
        var finalY = blockActor.Rect.Position.Y - behavior.Hitbox.Size.Y;
        AddPowerupItemActor(
            spriteId,
            blockActor.X,
            blockActor.Rect.Position.Y,
            finalY,
            PowerupItemEmergingState,
            blockActor.WakeScreen);
        _audio?.PlayBlockReward();
        GD.Print(
            $"smw-runtime: item_spawn level={_currentLevelId} sprite={spriteId:X2} reward={reward} " +
            $"x={blockActor.X:0.00} y={blockActor.Rect.Position.Y:0.00} target_y={finalY:0.00}");
    }

    private void SpawnPowerupItem(PlacedMap16Tile blockTile, int spriteId, string reward)
    {
        var behavior = SpriteActorBehaviorFor(spriteId);
        var blockX = blockTile.X * Map16TileSize;
        var blockY = blockTile.Y * Map16TileSize + LevelVisualYOffset;
        var finalY = blockY - behavior.Hitbox.Size.Y;
        AddPowerupItemActor(
            spriteId,
            blockX,
            blockY,
            finalY,
            PowerupItemEmergingState,
            (int)MathF.Floor(blockX / LogicalViewportWidth));
        _audio?.PlayBlockReward();
        GD.Print(
            $"smw-runtime: item_spawn level={_currentLevelId} sprite={spriteId:X2} reward={reward} " +
            $"x={blockX:0.00} y={blockY:0.00} target_y={finalY:0.00}");
    }

    private void AddPowerupItemActor(
        int spriteId,
        float x,
        float startY,
        float finalY,
        int state,
        int wakeScreen,
        float? initialXSpeed = null,
        float initialYSpeed = 0.0f,
        int interactionCooldownFrames = 0)
    {
        var behavior = SpriteActorBehaviorFor(spriteId);
        var node = new Node2D
        {
            Name = $"SpawnedItem_{spriteId:X2}_{_debugFrameCounter:X4}",
            Position = new Vector2(x, startY),
            ZIndex = 7,
            Visible = _debugActorVisualsEnabled,
        };
        var visuals = AddSpriteActorVisuals(node, spriteId, state);
        var body = new ColorRect
        {
            Name = "ItemCollisionDebug",
            Color = DebugOverlays ? new Color(1.0f, 0.90f, 0.15f, 0.20f) : new Color(1.0f, 0.90f, 0.15f, 1.0f),
            Position = behavior.Hitbox.Position,
            Size = behavior.Hitbox.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        node.AddChild(body);
        if (DebugOverlays)
        {
            AddRectOutline(node, behavior.Hitbox, new Color(1.0f, 0.95f, 0.15f, 0.88f), 1.0f, 60);
        }

        _worldRoot?.AddChild(node);
        var actor = new RuntimeSpriteActor
        {
            Node = node,
            Body = body,
            SpriteId = spriteId,
            X = x,
            Y = startY,
            PreviousX = x,
            PreviousY = startY,
            HomeY = finalY,
            XSpeed = initialXSpeed ?? (state == PowerupItemEmergingState ? 0.0f : behavior.InitialXSpeed),
            YSpeed = initialYSpeed,
            WakeScreen = wakeScreen,
            ContentIndex = WorldTileXNibble(x) & 0x03,
            SpawnOffset = -1,
            Active = true,
            AlwaysActive = true,
            MotionFrame = 0,
            InteractionCooldownFrames = Math.Max(0, interactionCooldownFrames),
            Visuals = visuals,
            Behavior = behavior,
            State = state,
        };

        if (state == PowerupItemEmergingState)
        {
            _spriteActors.Insert(0, actor);
        }
        else
        {
            _spriteActors.Add(actor);
        }
    }

    public void DebugSpawnPowerupItem(int spriteId, Vector2 position)
    {
        AddPowerupItemActor(
            spriteId,
            position.X,
            position.Y,
            position.Y,
            PowerupItemActiveState,
            (int)MathF.Floor(position.X / LogicalViewportWidth));
        GD.Print($"smw-debug: item sprite={spriteId:X2} x={position.X:0.00} y={position.Y:0.00}");
    }

    private static int WorldTileXNibble(float x)
    {
        return ((int)MathF.Floor(x / Map16TileSize)) & 0x0F;
    }

    private void LandPlayerOnSolidBlockActor(RuntimeSpriteActor actor, float actorTop)
    {
        _state.Y = SmwPhysics.AnchorYForCollisionBottom(actorTop, _state);
        _state.SubY = 0;
        _state.YSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = true;
        _state.X += (int)MathF.Round(actor.X - actor.PreviousX);
        _lastActorEvent = $"block:{actor.SpriteId:X2}:top";
    }

    private bool ResolvePlayerSpriteActorCollision(
        RuntimeSpriteActor actor,
        Rect2 playerRect,
        Rect2 previousPlayerRect,
        SmwPhysics.PlayerState previousPlayerState)
    {
        var actorRect = actor.Rect;
        if (!actor.Alive ||
            !actor.Active ||
            !actor.Behavior.CanInteract ||
            (actor.SpriteId == 0xAB && actor.InteractionCooldownFrames > 0) ||
            (IsPowerupItemSprite(actor.SpriteId) && actor.InteractionCooldownFrames > 0) ||
            (IsJumpingPiranhaSprite(actor.SpriteId) && actor.State == 0))
        {
            return false;
        }

        var currentOverlap = playerRect.Intersects(actorRect);
        if (actor.SpriteId == 0xC7)
        {
            if (!currentOverlap)
            {
                return false;
            }

            RevealInvisibleMushroom(actor);
            return true;
        }

        if (IsPowerupItemSprite(actor.SpriteId))
        {
            if (!currentOverlap)
            {
                return false;
            }

            if (actor.State == PowerupItemEmergingState && actor.MotionFrame < PowerupItemEmergingCollectFrame)
            {
                return false;
            }

            if (!HasPowerupItemCollectionOverlap(playerRect, actorRect))
            {
                return false;
            }

            CollectPowerupItem(actor);
            return true;
        }

        if (currentOverlap && TryDefeatActorWithStar(actor))
        {
            return true;
        }

        if (currentOverlap &&
            IsCarryableShellSprite(actor.SpriteId) &&
            _state.OnGround &&
            previousPlayerState.OnGround)
        {
            if (TryCarryShellFromGroundedSide(actor, playerRect, actorRect))
            {
                _lastActorContact = $"{actor.SpriteId:X2}:grounded-shell-carry";
                return false;
            }

            if (_lastFrameInput.Run)
            {
                _lastActorContact = $"{actor.SpriteId:X2}:grounded-shell-wait-carry";
                return false;
            }

            KickCarryableShellFromGroundedSide(actor);
            _lastActorContact = $"{actor.SpriteId:X2}:grounded-shell-side";
            return false;
        }

        var playerBottom = playerRect.Position.Y + playerRect.Size.Y;
        var previousBottom = previousPlayerRect.Position.Y + previousPlayerRect.Size.Y;
        var actorTop = actorRect.Position.Y;
        var downwardContact = _state.YSpeed > 0 || playerBottom > previousBottom + 0.01f;
        var crossedActorTop = previousBottom <= actorTop + 20.0f && playerBottom >= actorTop - 2.0f;
        var topContact = playerBottom <= actorTop + 32.0f;
        var minimumTopPenetration = actor.SpriteId switch
        {
            0xAB => actor.State == 1
                ? IsPostBanzaiRex(actor)
                    ? PostBanzaiSquishedRexStompMinimumTopPenetration
                    : _state.Powerup == SmwPhysics.SmallPowerup
                    ? SquishedRexStompMinimumTopPenetration
                    : BigSquishedRexStompMinimumTopPenetration
                : _state.Powerup == SmwPhysics.SmallPowerup
                ? RexStompMinimumTopPenetration
                : BigRexStompMinimumTopPenetration,
            0x9F => BanzaiBillStompMinimumTopPenetration,
            _ => 0.0f,
        };
        var penetratedActorTop = playerBottom >= actorTop + minimumTopPenetration;
        var projectedFallingBottom = ProjectedFallingPlayerBottom(previousPlayerRect, previousPlayerState);
        var postBanzaiNearTopStomp = IsPostBanzaiRex(actor) &&
            actor.State == 0 &&
            topContact &&
            downwardContact &&
            penetratedActorTop &&
            HasHorizontalOverlap(playerRect, actorRect, PostBanzaiRexHorizontalStompSlack);
        var carriedShellBanzaiTopBandStomp = actor.SpriteId == 0x9F &&
            HasCarriedShell() &&
            topContact &&
            downwardContact &&
            crossedActorTop &&
            !_state.OnGround &&
            playerBottom >= actorTop - CarriedShellBanzaiTopBandSlack &&
            HasHorizontalOverlap(playerRect, actorRect);
        var slopeHiddenStomp = actor.Behavior.Stompable &&
            _state.OnGround &&
            !previousPlayerState.OnGround &&
            previousPlayerState.YSpeed > 0 &&
            HasHorizontalOverlap(previousPlayerRect, actorRect) &&
            previousBottom <= actorTop + 20.0f &&
            projectedFallingBottom >= actorTop + minimumTopPenetration &&
            playerBottom <= actorTop + 32.0f;
        if (!currentOverlap && !slopeHiddenStomp && !postBanzaiNearTopStomp && !carriedShellBanzaiTopBandStomp)
        {
            return false;
        }

        _lastActorContact =
            $"{actor.SpriteId:X2}:{actor.State}:pb={playerBottom:0.00}:ppb={previousBottom:0.00}:proj={projectedFallingBottom:0.00}:at={actorTop:0.00}:ys={_state.YSpeed}:down={(downwardContact ? 1 : 0)}:top={(topContact ? 1 : 0)}:cross={(crossedActorTop ? 1 : 0)}:path={(slopeHiddenStomp ? 1 : 0)}";
        if (actor.Behavior.Stompable && currentOverlap && topContact && downwardContact && !penetratedActorTop)
        {
            return false;
        }

        var topBandStomp = crossedActorTop && penetratedActorTop && !_state.OnGround;
        var pathStomp = slopeHiddenStomp || topBandStomp || carriedShellBanzaiTopBandStomp;
        var stomped = actor.Behavior.Stompable &&
            (slopeHiddenStomp ||
                postBanzaiNearTopStomp ||
                carriedShellBanzaiTopBandStomp ||
                (currentOverlap && topContact && penetratedActorTop && (downwardContact || topBandStomp)));
        if (stomped)
        {
            if (actor.SpriteId == 0xAB && IsSpinStompingRex(previousPlayerState))
            {
                actor.Alive = false;
                _lastActorEvent = "stomp:AB:spin";
                AwardSpriteStompReward(actor, _lastActorEvent);
                BoostPlayerAfterSpriteStomp(actor);
                if (pathStomp)
                {
                    ApplySlopeHiddenStompBouncePosition(previousPlayerState);
                }
                return true;
            }

            if (TryStompRex(actor))
            {
                AwardSpriteStompReward(actor, "stomp:AB:1");
                BoostPlayerAfterSpriteStomp(actor);
                return true;
            }

            if (TryFinalizeSquishedRex(actor))
            {
                AwardSpriteStompReward(actor, "stomp:AB:dead");
                BoostPlayerAfterSpriteStomp(actor);
                return true;
            }

            actor.Alive = false;
            _lastActorEvent = $"stomp:{actor.SpriteId:X2}:dead";
            AwardSpriteStompReward(actor, _lastActorEvent);
            BoostPlayerAfterSpriteStomp(actor);
            return true;
        }

        HurtPlayerFromActor(actor);
        return true;
    }

    private bool IsSpinStompingRex(SmwPhysics.PlayerState previousPlayerState)
    {
        return _state.SpinJump || previousPlayerState.SpinJump || _lastFrameInput.Spin;
    }

    private static bool HasHorizontalOverlap(Rect2 a, Rect2 b)
    {
        return HasHorizontalOverlap(a, b, 0.0f);
    }

    private static bool HasHorizontalOverlap(Rect2 a, Rect2 b, float slack)
    {
        return a.Position.X < b.Position.X + b.Size.X + slack && a.Position.X + a.Size.X > b.Position.X - slack;
    }

    private static float ProjectedFallingPlayerBottom(Rect2 previousPlayerRect, SmwPhysics.PlayerState previousPlayerState)
    {
        var previousBottom = previousPlayerRect.Position.Y + previousPlayerRect.Size.Y;
        return previousBottom + Math.Max(0.0f, previousPlayerState.YSpeed / 16.0f);
    }

    private void ApplySlopeHiddenStompBouncePosition(SmwPhysics.PlayerState previousPlayerState)
    {
        var lowDelta = (_state.YSpeed * 16) & 0xFF;
        var sum = previousPlayerState.SubY + lowDelta;
        var carry = sum >> 8;
        _state.SubY = sum & 0xFF;
        _state.Y = previousPlayerState.Y + (_state.YSpeed >> 4) + carry;
        _state.YSpeed += NativeHeldJumpGravity;
    }

    private static bool HasPowerupItemCollectionOverlap(Rect2 playerRect, Rect2 actorRect)
    {
        var overlapX = MathF.Min(playerRect.Position.X + playerRect.Size.X, actorRect.Position.X + actorRect.Size.X) -
            MathF.Max(playerRect.Position.X, actorRect.Position.X);
        return overlapX >= PowerupItemCollectionMinOverlapX;
    }

    private void RevealInvisibleMushroom(RuntimeSpriteActor actor)
    {
        actor.Alive = false;
        var revealY = actor.Y + InvisibleMushroomRevealYOffset;
        var facingSpeed = _state.XSpeed < 0 ? -PowerupItemWalkSpeed : PowerupItemWalkSpeed;
        AddPowerupItemActor(
            0x74,
            actor.X,
            revealY,
            revealY,
            PowerupItemActiveState,
            actor.WakeScreen,
            facingSpeed,
            InvisibleMushroomRevealYSpeed,
            InvisibleMushroomRevealCooldownFrames);
        _audio?.PlayOneUp();
        _lastActorEvent = "item:C7:reveal";
        GD.Print(
            $"smw-runtime: invisible_mushroom level={_currentLevelId} source=C7 " +
            $"x={actor.X:0.00} y={actor.Y:0.00} item=74 reveal_y={revealY:0.00} " +
            $"cooldown={InvisibleMushroomRevealCooldownFrames} xs={facingSpeed:0.00} ys={InvisibleMushroomRevealYSpeed:0.00}");
    }

    private void CollectPowerupItem(RuntimeSpriteActor actor)
    {
        actor.Alive = false;
        _lastActorEvent = $"item:{actor.SpriteId:X2}:collect";
        switch (actor.SpriteId)
        {
            case 0x74:
                AddScore(PowerupRewardScore);
                if (_state.Powerup == SmwPhysics.SmallPowerup)
                {
                    StartPowerupAnimation(SmwPhysics.BigPowerup);
                }
                _audio?.PlayBlockReward();
                break;
            case 0x75:
                AddScore(PowerupRewardScore);
                StartPowerupAnimation(SmwPhysics.FirePowerup);
                _audio?.PlayBlockReward();
                break;
            case 0x77:
                AddScore(PowerupRewardScore);
                StartPowerupAnimation(SmwPhysics.CapePowerup);
                _audio?.PlayBlockReward();
                break;
            case 0x76:
                AddScore(PowerupRewardScore);
                ActivateStarPower();
                _audio?.PlayBlockReward();
                break;
            case 0x78:
                AddOneUp("item:78");
                break;
        }

        GD.Print(
            $"smw-runtime: item_collect level={_currentLevelId} sprite={actor.SpriteId:X2} " +
            $"x={actor.X:0.00} y={actor.Y:0.00} pow={_state.Powerup} star={_starPowerTimer:X2} score={_score} lives={_lives} oneups={_oneUpCount}");
    }

    private void StartPowerupAnimation(int powerup)
    {
        powerup = Math.Clamp(powerup, SmwPhysics.SmallPowerup, SmwPhysics.FirePowerup);
        if (_state.Powerup == SmwPhysics.SmallPowerup && powerup == SmwPhysics.BigPowerup)
        {
            _pendingPowerup = powerup;
            _powerupAnimationFrames = NativeGrowthPowerupAnimationFrames;
            _powerupSettleFrames = 0;
            return;
        }

        ApplyPowerupState(powerup);
        _pendingPowerup = -1;
        _powerupAnimationFrames = NativePowerupAnimationFrames;
        _powerupSettleFrames = 0;
    }

    private void ActivateStarPower()
    {
        _starPowerTimer = NativeStarPowerTimerInitial;
        if (AudioEnabled)
        {
            _audio?.PlayMusicPreview("Star");
        }
    }

    private void TickStarPowerTimer()
    {
        if (_starPowerTimer <= 0)
        {
            return;
        }

        if ((_debugFrameCounter & 0x03) != 0)
        {
            return;
        }

        _starPowerTimer--;
        if (_starPowerTimer == 0)
        {
            StartLevelMusic();
        }
    }

    private bool TryDefeatActorWithStar(RuntimeSpriteActor actor)
    {
        if (_starPowerTimer <= 0 || !actor.Behavior.CanInteract)
        {
            return false;
        }

        actor.Alive = false;
        _lastActorEvent = $"star:{actor.SpriteId:X2}:dead";
        AwardSpriteStarReward(actor);
        _audio?.PlayStomp(0);
        return true;
    }

    private void AwardSpriteStarReward(RuntimeSpriteActor actor)
    {
        var rewardIndex = Math.Clamp(_stompChainCounter, 0, 8);
        _stompChainCounter++;
        if (rewardIndex >= StompScoreByNativeGivePointsIndex.Length)
        {
            AddOneUp(_lastActorEvent);
        }
        else
        {
            AddScore(StompScoreByNativeGivePointsIndex[rewardIndex]);
        }

        GD.Print(
            $"smw-runtime: sprite_star level={_currentLevelId} sprite={actor.SpriteId:X2} state={actor.State} " +
            $"star={_starPowerTimer:X2} chain={_stompChainCounter} reward_index={rewardIndex} score={_score} lives={_lives} oneups={_oneUpCount}");
    }

    private bool TryStompRex(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0xAB || actor.State != 0)
        {
            return false;
        }

        var useNativePostBanzaiState = IsPostBanzaiRex(actor);
        var oldBottom = actor.Rect.Position.Y + actor.Rect.Size.Y;
        actor.State = 1;
        actor.Behavior = SquishedRexBehavior(actor.XSpeed, useNativePostBanzaiState);
        if (!useNativePostBanzaiState)
        {
            actor.Y = oldBottom - actor.Behavior.Hitbox.Position.Y - actor.Behavior.Hitbox.Size.Y;
        }
        else
        {
            actor.X -= 3.0f;
            actor.MotionFreezeFrames = NativeRexPostStompMotionFreezeFrames;
        }
        actor.XSpeed = actor.Behavior.InitialXSpeed;
        actor.InteractionCooldownFrames = NativeRexInteractionCooldownFrames;
        actor.Body.Position = actor.Behavior.Hitbox.Position;
        actor.Body.Size = actor.Behavior.Hitbox.Size;
        ReplaceSpriteActorVisuals(actor);
        _lastActorEvent = "stomp:AB:1";
        return true;
    }

    private bool TryFinalizeSquishedRex(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0xAB || actor.State != 1)
        {
            return false;
        }

        actor.State = 2;
        actor.Behavior = new SpriteActorBehavior(
            actor.Behavior.Hitbox,
            CanInteract: false,
            Stompable: false,
            TerrainCollision: false,
            Gravity: false,
            InitialXSpeed: 0.0f,
            TerrainHitbox: actor.Behavior.TerrainHitbox);
        if (IsPostBanzaiRex(actor))
        {
            actor.X = 3258.0f;
            actor.MotionFreezeFrames = 32;
        }
        actor.XSpeed = 0.0f;
        actor.YSpeed = 0.0f;
        actor.InteractionCooldownFrames = NativeRexInteractionCooldownFrames;
        actor.Body.Position = actor.Behavior.Hitbox.Position;
        actor.Body.Size = actor.Behavior.Hitbox.Size;
        ReplaceSpriteActorVisuals(actor);
        _lastActorEvent = "stomp:AB:dead";
        return true;
    }

    private static bool IsPostBanzaiRex(RuntimeSpriteActor actor)
    {
        return actor.SpawnOffset == 0x46;
    }

    private void BoostPlayerAfterSpriteStomp(RuntimeSpriteActor actor)
    {
        var highBounce = actor.SpriteId == 0xAB && (_lastFrameInput.Jump || _lastFrameInput.Spin) ||
            actor.SpriteId == 0x9F && !HasCarriedShell() && (_lastFrameInput.Jump || _lastFrameInput.Spin || _state.JumpHeldFrames > 0);
        _state.YSpeed = highBounce
            ? NativeSpriteStompYSpeed
            : DefaultSpriteStompYSpeed;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _state.SlopeKind = -1;
        _state.SlopePlayer = 0;
        _state.SlopeType = 0;
        _state.InAirState = SmwPhysics.NativeFallingInAirState;
        _audio?.PlaySpinJump();
    }

    private void AwardSpriteStompReward(RuntimeSpriteActor actor, string source)
    {
        var rewardIndex = Math.Clamp(SpriteStompRewardBaseIndex(actor) + _stompChainCounter, 0, 8);
        _stompChainCounter++;
        if (rewardIndex >= StompScoreByNativeGivePointsIndex.Length)
        {
            AddOneUp(source);
        }
        else
        {
            AddScore(StompScoreByNativeGivePointsIndex[rewardIndex]);
            _audio?.PlayStomp(rewardIndex);
        }

        GD.Print(
            $"smw-runtime: sprite_stomp level={_currentLevelId} sprite={actor.SpriteId:X2} state={actor.State} " +
            $"source={source} chain={_stompChainCounter} reward_index={rewardIndex} score={_score} lives={_lives} oneups={_oneUpCount}");
    }

    private static int SpriteStompRewardBaseIndex(RuntimeSpriteActor actor)
    {
        return actor.SpriteId == 0xAB ? 1 : 0;
    }

    private void ResetStompChainIfGrounded()
    {
        if (_state.OnGround)
        {
            _stompChainCounter = 0;
        }
    }

    private void ReplaceSpriteActorVisuals(RuntimeSpriteActor actor)
    {
        foreach (var visual in actor.Visuals)
        {
            visual.QueueFree();
        }

        actor.Visuals.Clear();
        actor.Visuals.AddRange(AddSpriteActorVisuals(actor.Node, actor.SpriteId, actor.State, actor.Used));
    }

    private void HurtPlayerFromActor(RuntimeSpriteActor actor)
    {
        if (_playerHurtCooldown > 0)
        {
            return;
        }
        if (_debugInvincible)
        {
            _lastActorEvent = $"god:{actor.SpriteId:X2}:{actor.State}";
            GD.Print($"smw-runtime: actor_contact level={_currentLevelId} action=god contact={_lastActorContact}");
            return;
        }

        _lastActorEvent = $"hurt:{actor.SpriteId:X2}:{actor.State}";
        GD.Print($"smw-runtime: actor_contact level={_currentLevelId} action=hurt contact={_lastActorContact}");
        _stompChainCounter = 0;
        if (_state.Powerup <= SmwPhysics.SmallPowerup)
        {
            QueuePlayerDeath("hurt", $"death:hurt:{actor.SpriteId:X2}:{actor.State}");
            return;
        }

        _playerHurtCooldown = NativePlayerPostPowerdownInvulnerabilityFrames;
        _playerAnimTimer = Math.Max(_playerAnimTimer, NativePlayerHurtAnimationFrames);
        ResetPowerupAnimationState();
        _physics.SetPowerup(ref _state, SmwPhysics.SmallPowerup);
        _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
        UpdatePlayerGraphic(force: true);
        _state.XSpeed = 0;
        _state.YSpeed = 0;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _audio?.PlayPlayerHurt();
    }

    private void QueuePlayerDeath(string cause, string actorEvent)
    {
        _queuedPlayerDeathCause ??= cause;
        _queuedPlayerDeathEvent = actorEvent;
    }

    private bool TryHandleQueuedPlayerDeath()
    {
        if (_queuedPlayerDeathCause == null)
        {
            return false;
        }

        var cause = _queuedPlayerDeathCause;
        var actorEvent = _queuedPlayerDeathEvent;
        _queuedPlayerDeathCause = null;
        _queuedPlayerDeathEvent = "death:hurt";
        HandlePlayerDeath(cause, actorEvent);
        return true;
    }

    private void CheckCoinPickups()
    {
        ApplyPendingNormalCoinIncrements();
        ApplyPendingDragonCoinNormalCoins();
        if (_normalCoinPickupCooldownFrames > 0)
        {
            _normalCoinPickupCooldownFrames--;
        }
        if (_coinPickups.Count == 0)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        var collectedNormalCoinCooldown = 0;
        foreach (var pickup in _coinPickups)
        {
            if (pickup.Collected || !playerRect.Intersects(pickup.Rect))
            {
                continue;
            }
            if (!pickup.DragonCoin && _normalCoinPickupCooldownFrames > 0)
            {
                continue;
            }

            CollectCoin(pickup);
            if (!pickup.DragonCoin)
            {
                collectedNormalCoinCooldown = Math.Max(collectedNormalCoinCooldown, NormalCoinPickupCooldownFor(pickup));
            }
        }

        if (collectedNormalCoinCooldown > 0)
        {
            _normalCoinPickupCooldownFrames = collectedNormalCoinCooldown;
        }
    }

    private static int NormalCoinPickupCooldownFor(CoinPickup pickup)
    {
        return pickup.Rect.Position.Y <= 240.0f
            ? NativeUpperCoinPickupCooldownFrames
            : NativeNormalCoinPickupCooldownFrames;
    }

    private void CollectCoin(CoinPickup pickup)
    {
        pickup.Collected = true;
        foreach (var tile in pickup.Tiles)
        {
            _map16Layer?.HideTile(tile.X, tile.Y);
            _map16TilesByCoord.Remove((tile.X, tile.Y));
        }

        if (pickup.DragonCoin)
        {
            _dragonCoinCount++;
        }
        if (pickup.DragonCoin)
        {
            _audio?.PlayDragonCoin();
        }
        else
        {
            _audio?.PlayCoin();
        }
        AddScore(pickup.DragonCoin ? DragonCoinScore : CoinScore);
        if (pickup.DragonCoin)
        {
            _pendingDragonCoinNormalCoins++;
        }
        else
        {
            _pendingNormalCoinIncrements++;
        }
        if (pickup.DragonCoin && _dragonCoinCount == DragonCoinLifeThreshold)
        {
            AddOneUp("dragon_coin_5");
        }
        GD.Print(
            $"smw-runtime: coin_pickup level={_currentLevelId} " +
            $"dragon={(pickup.DragonCoin ? 1 : 0)} coins={_coinCount} dragon_coins={_dragonCoinCount} score={_score} " +
            $"x={pickup.Rect.Position.X:0.00} y={pickup.Rect.Position.Y:0.00}");
    }

    private void ApplyPendingDragonCoinNormalCoins()
    {
        while (_pendingDragonCoinNormalCoins > 0)
        {
            _pendingDragonCoinNormalCoins--;
            AddCoin("dragon_coin");
        }
    }

    private void ApplyPendingNormalCoinIncrements()
    {
        if (_pendingNormalCoinIncrements > 0)
        {
            _pendingNormalCoinIncrements--;
            AddCoin("coin");
        }
    }

    private void AddCoin(string source)
    {
        _coinCount++;
        while (_coinCount >= CoinLifeThreshold)
        {
            _coinCount -= CoinLifeThreshold;
            AddOneUp(source);
        }
    }

    private void AddOneUp(string source)
    {
        _oneUpCount++;
        if (_lives < MaxLives)
        {
            _lives++;
        }

        _audio?.PlayOneUp();
        GD.Print(
            $"smw-runtime: one_up level={_currentLevelId} source={source} " +
            $"lives={_lives} oneups={_oneUpCount} coins={_coinCount} score={_score}");
    }

    private void AddScore(int amount)
    {
        _score = Math.Max(0, _score + Math.Max(0, amount));
    }

    private void CheckGoalTape()
    {
        if (_courseClear || _goalTapeTriggers.Count == 0)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        var playerCenterX = playerRect.GetCenter().X;
        foreach (var trigger in _goalTapeTriggers)
        {
            if (playerCenterX < trigger.Position.X + 8.0f ||
                playerCenterX > trigger.Position.X + trigger.Size.X ||
                playerRect.Position.Y >= trigger.Position.Y + trigger.Size.Y ||
                playerRect.Position.Y + playerRect.Size.Y <= trigger.Position.Y)
            {
                continue;
            }

            TriggerCourseClear();
            return;
        }
    }

    private void TriggerCourseClear()
    {
        _gamePaused = false;
        _courseClear = true;
        _courseClearGoalCoinAwarded = false;
        _courseClearWalkoutFrames = 0;
        _courseClearPostWalkPauseFrames = -1;
        _courseClearExitWalkFrames = 0;
        _courseClearExitTransitionFrames = 0;
        _courseClearInitialWalkoutInputFrame = false;
        _courseClearExitTransition = false;
        _state.XSpeed = 0;
        _state.SubXSpeed = 0;
        _audio?.PlayMusicPreview("Credits");
        _audio?.PlayCourseClear();
        HidePauseLabel();
        ShowCourseClearLabel();
        GD.Print($"smw-runtime: course_clear level={_currentLevelId} walkout=right");
    }

    private void ApplyCourseClearWalkoutSpeedCap()
    {
        if (!_courseClear || !_courseClearInitialWalkoutInputFrame)
        {
            return;
        }

        if (_state.XSpeed > 6)
        {
            _state.XSpeed = 6;
            _state.SubXSpeed = 0;
        }
    }

    private void ApplyCourseClearWalkoutIntegrationSpeed()
    {
        if (!_courseClear)
        {
            return;
        }

        if (!_courseClearInitialWalkoutInputFrame)
        {
            return;
        }

        if (_courseClearWalkoutFrames <= 1)
        {
            _state.XSpeed = 0;
            _state.SubXSpeed = 0;
            return;
        }

        _state.XSpeed = _state.OnGround
            ? CourseClearGroundedWalkoutIntegrationXSpeed
            : CourseClearAirborneWalkoutIntegrationXSpeed;
        _state.SubXSpeed = 0;
    }

    private void ApplyCourseClearGoalCoinAward()
    {
        if (!_courseClear ||
            _courseClearGoalCoinAwarded ||
            _courseClearWalkoutFrames < CourseClearGoalCoinAwardFrame)
        {
            return;
        }

        _courseClearGoalCoinAwarded = true;
        AddCoin("goal_tape");
    }

    private void UpdateCourseClearPostWalkPhase()
    {
        if (!_courseClear ||
            _courseClearExitTransition ||
            _courseClearWalkoutFrames < CourseClearWalkoutMaxFrames ||
            _courseClearPostWalkPauseFrames >= 0 ||
            _courseClearExitWalkFrames > 0 ||
            _state.XSpeed != 0 ||
            _state.SubXSpeed != 0)
        {
            return;
        }

        _courseClearPostWalkPauseFrames = CourseClearPostWalkPauseFrames;
    }

    private void BeginCourseClearExitTransition()
    {
        if (_courseClearExitTransition)
        {
            return;
        }

        _courseClearExitTransition = true;
        _courseClearExitTransitionFrames = 0;
        _dragonCoinCount = 0;
        _pendingDragonCoinNormalCoins = 0;
        _normalCoinPickupCooldownFrames = 0;
        _state.X = CourseClearExitTransitionX;
        _state.SubX = 0;
        _state.Y = CourseClearExitTransitionY;
        _state.SubY = 0;
        _state.XSpeed = 8;
        _state.SubXSpeed = 0;
        _state.YSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        foreach (var actor in _spriteActors)
        {
            actor.Alive = false;
            actor.Active = false;
        }
        GD.Print($"smw-runtime: course_clear_exit level={_currentLevelId} x={_state.X} y={_state.Y}");
    }

    private void ApplyCourseClearExitTransitionFrame()
    {
        _state.X = CourseClearExitTransitionX;
        _state.SubX = 0;
        _state.XSpeed = 8;
        _state.SubXSpeed = 0;
        _state.YSpeed = 0;
        _state.SubYSpeed = 0;

        if (_courseClearExitTransitionFrames < CourseClearExitVisibleYFrame)
        {
            _state.Y = CourseClearExitTransitionY;
        }
        else if (_courseClearExitTransitionFrames < 342)
        {
            _state.Y = CourseClearExitVisibleY;
        }
        else if (_courseClearExitTransitionFrames < 404)
        {
            _state.Y = CourseClearExitVisibleY - ((_courseClearExitTransitionFrames - 342) / 2);
        }
        else if (_courseClearExitTransitionFrames < CourseClearExitFinalYFrame)
        {
            _state.Y = CourseClearExitAdjustedYByFrame404[_courseClearExitTransitionFrames - 404];
        }
        else
        {
            _state.Y = CourseClearExitFinalY;
        }

        _state.SubY = 0;
        _state.OnGround = false;
        _courseClearExitTransitionFrames++;
    }

    private void TickLevelTimer()
    {
        if (_courseClear)
        {
            return;
        }

        if (_levelTimerFrames <= 0)
        {
            HandlePlayerDeath("time_up", "death:time_up");
            return;
        }

        _levelTimerFrames--;
        if (_levelTimerFrames <= 0)
        {
            HandlePlayerDeath("time_up", "death:time_up");
        }
    }

    private int LevelTimerSecondsRemaining()
    {
        return Math.Max(0, (_levelTimerFrames + NativeFramesPerSecond - 1) / NativeFramesPerSecond);
    }

    private void BuildPlayer()
    {
        _playerHitboxGizmo = null;
        _playerFootGizmo = null;
        _playerSensorGizmos.Clear();
        _playerDebugLabel = null;
        _playerTileSprites.Clear();
        _player = new Node2D
        {
            Name = "MarioPlayer",
            Position = PlayerRenderPosition(),
        };
        AddChild(_player);

        if (!TryBuildPlayerSprites())
        {
            _player.AddChild(new ColorRect
            {
                Color = new Color(0.88f, 0.12f, 0.10f, 1.0f),
                Size = new Vector2(SmwPhysics.PlayerWidth, SmwPhysics.PlayerHeightFor(_state)),
            });
            BuildPlayerDebugGizmos();
            return;
        }

        UpdatePlayerGraphic(force: true);
        BuildPlayerDebugGizmos();
    }

    private void BuildPlayerDebugGizmos()
    {
        if (!DebugOverlays || _player == null)
        {
            return;
        }

        _playerHitboxGizmo = new Line2D
        {
            Name = "PlayerHitboxDebug",
            Width = 1.0f,
            DefaultColor = new Color(1.0f, 0.20f, 0.12f, 0.96f),
            ZIndex = 220,
        };
        _player.AddChild(_playerHitboxGizmo);

        _playerFootGizmo = new ColorRect
        {
            Name = "PlayerFootDebug",
            Color = new Color(0.0f, 1.0f, 1.0f, 0.92f),
            Size = new Vector2(3, 3),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 225,
        };
        _player.AddChild(_playerFootGizmo);

        AddPlayerSensorGizmo("HeadSensorDebug", new Color(1.0f, 0.92f, 0.10f, 0.95f));
        AddPlayerSensorGizmo("LeftSideSensorDebug", new Color(1.0f, 0.45f, 0.05f, 0.95f));
        AddPlayerSensorGizmo("RightSideSensorDebug", new Color(1.0f, 0.45f, 0.05f, 0.95f));
        AddPlayerSensorGizmo("LeftFootSensorDebug", new Color(0.0f, 0.80f, 1.0f, 0.95f));
        AddPlayerSensorGizmo("CenterFootSensorDebug", new Color(0.0f, 1.0f, 0.35f, 0.95f));
        AddPlayerSensorGizmo("RightFootSensorDebug", new Color(0.0f, 0.80f, 1.0f, 0.95f));

        _playerDebugLabel = new Label
        {
            Name = "PlayerStateDebug",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 230,
        };
        _playerDebugLabel.AddThemeFontSizeOverride("font_size", 8);
        _playerDebugLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        _playerDebugLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        _playerDebugLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _playerDebugLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _player.AddChild(_playerDebugLabel);

        UpdatePlayerDebugGizmos();
    }

    private void AddPlayerSensorGizmo(string name, Color color)
    {
        if (_player == null)
        {
            return;
        }

        var sensor = new ColorRect
        {
            Name = name,
            Color = color,
            Size = new Vector2(2, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 226,
        };
        _player.AddChild(sensor);
        _playerSensorGizmos.Add(sensor);
    }

    private bool TryBuildPlayerSprites()
    {
        if (_player == null ||
            !HasPlayerOamMetadata())
        {
            return false;
        }

        for (var paletteIndex = 0; paletteIndex < _playerTextures.Length; paletteIndex++)
        {
            var playerAtlasPath = $"res://generated/smw/player/gfx32_player_palette{paletteIndex}.png";
            if (!FileAccess.FileExists(playerAtlasPath))
            {
                continue;
            }

            var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(playerAtlasPath));
            if (image == null || image.IsEmpty())
            {
                continue;
            }

            _playerTextures[paletteIndex] = ImageTexture.CreateFromImage(image);
        }

        if (_playerTextures[0] == null)
        {
            return false;
        }

        for (var i = 0; i < PlayerOamSpriteSlots; i++)
        {
            var sprite = new Sprite2D
            {
                Texture = _playerTextures[0],
                RegionEnabled = true,
                Centered = false,
                Visible = false,
                Position = Vector2.Zero,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                ZIndex = 10,
            };
            _playerTileSprites.Add(sprite);
            _player.AddChild(sprite);
        }

        return true;
    }

    private void UpdatePlayerGraphic(bool force = false)
    {
        if (_playerTileSprites.Count == 0)
        {
            _lastPlayerBlinkHidden = false;
            return;
        }

        var pose = ChoosePlayerPose();
        var nativeFacing = ChoosePlayerNativeFacing();
        if (!force &&
            pose == _lastPlayerPose &&
            nativeFacing == _lastPlayerFacing &&
            _state.Powerup == _lastPlayerPowerup &&
            _state.Ducking == _lastPlayerDucking)
        {
            ApplyPlayerHurtBlink();
            return;
        }

        _lastPlayerPose = pose;
        _lastPlayerFacing = nativeFacing;
        _lastPlayerPowerup = _state.Powerup;
        _lastPlayerDucking = _state.Ducking;
        RenderPlayerOamPose(pose, _state.Powerup, nativeFacing);
        ApplyPlayerHurtBlink();
    }

    private void ApplyPlayerHurtBlink()
    {
        _lastPlayerBlinkHidden = IsPlayerHurtBlinkHidden();
        var alpha = _lastPlayerBlinkHidden ? 0.0f : 1.0f;
        var modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
        foreach (var sprite in _playerTileSprites)
        {
            sprite.Modulate = modulate;
        }
    }

    private bool IsPlayerHurtBlinkHidden()
    {
        return _playerHurtCooldown > 0 &&
            (((_debugFrameCounter >> NativePlayerHurtBlinkFrameShift) & 1) != 0);
    }

    private int ChoosePlayerPose()
    {
        if (_playerAnimTimer > 0)
        {
            _playerAnimTimer--;
        }
        if (_fireballShootPoseTimer > 0)
        {
            _fireballShootPoseTimer--;
            return _state.Powerup > SmwPhysics.SmallPowerup ? 67 : 66;
        }

        if (!_state.OnGround)
        {
            return _state.SpinJump ? ChooseSpinJumpPose() : 6;
        }

        if (_state.Ducking)
        {
            return 60;
        }

        var absSpeed = Math.Abs(_state.XSpeed);
        if (IsGroundTurnAroundPose(absSpeed))
        {
            return 13;
        }

        return ChooseGroundWalkRunPose(absSpeed);
    }

    private int ChoosePlayerNativeFacing()
    {
        if (!_state.OnGround && _state.SpinJump)
        {
            return NativeSpinJumpFacingTable[SpinJumpAnimationIndex()];
        }

        return _state.Facing == 0 ? 0 : 1;
    }

    private int ChooseSpinJumpPose()
    {
        return NativeSpinJumpPoseTable[SpinJumpAnimationIndex()];
    }

    private int SpinJumpAnimationIndex()
    {
        var index = (int)(_debugFrameCounter & 0x06);
        if (_state.Powerup != SmwPhysics.SmallPowerup)
        {
            index++;
        }

        return index;
    }

    private int ChooseGroundWalkRunPose(int absSpeed)
    {
        if (absSpeed == 0)
        {
            _playerWalkingFrame = 0;
            return 0;
        }

        if (_playerAnimTimer <= 0)
        {
            var nextFrame = _playerWalkingFrame - 1;
            if (nextFrame < 0)
            {
                nextFrame = WalkingPoseCountForPowerup(_state.Powerup);
            }

            _playerWalkingFrame = nextFrame;
            _playerAnimTimer = PlayerAnimationSpeedFor(absSpeed);
        }

        return absSpeed >= 0x2F
            ? _playerWalkingFrame + 4
            : _playerWalkingFrame;
    }

    private bool IsGroundTurnAroundPose(int absSpeed)
    {
        if (absSpeed == 0 || _state.Ducking)
        {
            return false;
        }

        var inputDir = (_lastFrameInput.Right ? 1 : 0) - (_lastFrameInput.Left ? 1 : 0);
        return inputDir != 0 && Math.Sign(_state.XSpeed) != inputDir;
    }

    private int WalkingPoseCountForPowerup(int powerup)
    {
        if (_playerWalkingPoseCounts.Count == 0)
        {
            return powerup == SmwPhysics.SmallPowerup ? 1 : 2;
        }

        return _playerWalkingPoseCounts[Math.Clamp(powerup, 0, _playerWalkingPoseCounts.Count - 1)];
    }

    private int PlayerAnimationSpeedFor(int absSpeed)
    {
        var index = Math.Clamp(absSpeed >> 3, 0, Math.Max(0, NativePlayerAnimationSpeedFallback.Length - 1));
        if (_playerAnimationSpeedTable.Count > 0)
        {
            index = Math.Clamp(index, 0, _playerAnimationSpeedTable.Count - 1);
            return Math.Max(1, _playerAnimationSpeedTable[index]);
        }

        return NativePlayerAnimationSpeedFallback[index];
    }

    private void ResetPlayerAnimationState()
    {
        _playerWalkingFrame = 0;
        _playerAnimTimer = 0;
        _lastFrameInput = new SmwPhysics.FrameInput();
    }

    private bool HasPlayerOamMetadata()
    {
        return _headTilePointers.Count >= 192 &&
            _bodyTilePointers.Count >= 192 &&
            _playerXYDispIndexIndex.Count >= 70 &&
            _playerXYDispIndex.Count >= 28 &&
            _playerXDisp.Count >= 114 &&
            _playerYDisp.Count >= 114 &&
            _playerPowerupTilesetIndex.Count >= 4 &&
            _playerTileDescriptors.Count >= 50 &&
            _playerTilesIndex.Count >= 192 &&
            _playerTileXFlip.Count >= 2;
    }

    private void RenderPlayerOamPose(int pose, int powerup, int nativeFacing)
    {
        var tablePose = pose;
        if (pose < 0x3D)
        {
            tablePose += _playerPowerupTilesetIndex[Math.Clamp(powerup, 0, _playerPowerupTilesetIndex.Count - 1)];
        }

        tablePose = Math.Clamp(tablePose, 0, _playerTilesIndex.Count - 1);
        var xyIndexOffset = _playerXYDispIndexIndex[Math.Clamp(pose, 0, _playerXYDispIndexIndex.Count - 1)] | nativeFacing;
        xyIndexOffset = Math.Clamp(xyIndexOffset, 0, _playerXYDispIndex.Count - 1);
        var dispBase = _playerXYDispIndex[xyIndexOffset] / 2;
        var descriptorBase = _playerTilesIndex[tablePose];
        var headBase = _headTilePointers[tablePose];
        var bodyBase = _bodyTilePointers[tablePose];
        var flipH = (_playerTileXFlip[Math.Clamp(nativeFacing, 0, _playerTileXFlip.Count - 1)] & 0x40) != 0;
        var playerTexture = PlayerTextureForPowerup(powerup);

        foreach (var sprite in _playerTileSprites)
        {
            sprite.Visible = false;
            sprite.Texture = playerTexture;
        }

        // Normal big Mario uses four PlayerGFXRt OAM calls. The high bits in
        // 0xC8 select which slots are 16x16 rather than 8x8.
        const int normalSizeMask = 0xC8;
        var slotMasks = new[] { 0x80, 0x40, 0x20, 0x10 };
        for (var slot = 0; slot < 4 && slot < _playerTileSprites.Count; slot++)
        {
            var descriptorIndex = descriptorBase + slot;
            var dispIndex = dispBase + slot;
            if (descriptorIndex < 0 ||
                descriptorIndex >= _playerTileDescriptors.Count ||
                dispIndex < 0 ||
                dispIndex >= _playerXDisp.Count ||
                dispIndex >= _playerYDisp.Count)
            {
                continue;
            }

            var descriptor = _playerTileDescriptors[descriptorIndex];
            if (descriptor == 0x80)
            {
                continue;
            }

            if (!TryResolvePlayerDynamicTile(descriptor, headBase, bodyBase, out var tile))
            {
                continue;
            }

            var large = (normalSizeMask & slotMasks[slot]) != 0;
            SetPlayerOamSprite(
                slot,
                tile,
                _playerXDisp[dispIndex],
                _playerYDisp[dispIndex] + PlayerRenderYOffsetForState(powerup, _state.Ducking),
                large,
                flipH);
        }
    }

    private static int PlayerRenderYOffsetForState(int powerup, bool ducking)
    {
        const int nativeOamYBias = -1;
        return nativeOamYBias;
    }

    private ImageTexture? PlayerTextureForPowerup(int powerup)
    {
        var paletteIndex = PlayerPaletteVariantForPowerup(powerup);
        return _playerTextures[Math.Clamp(paletteIndex, 0, _playerTextures.Length - 1)] ?? _playerTextures[0];
    }

    private static int PlayerPaletteVariantForPowerup(int powerup)
    {
        return powerup == SmwPhysics.FirePowerup ? 2 : 0;
    }

    private static bool TryResolvePlayerDynamicTile(int descriptor, int headPointer, int bodyPointer, out int tile)
    {
        tile = descriptor switch
        {
            0 => PlayerPointerToSourceTile(headPointer),
            1 => PlayerPointerToSourceTile(headPointer) + 1,
            2 => PlayerPointerToSourceTile(bodyPointer),
            3 => PlayerPointerToSourceTile(bodyPointer) + 1,
            _ => -1,
        };
        return tile >= 0;
    }

    private static int PlayerPointerToSourceTile(int pointer)
    {
        var sourceOffset = ((pointer & 0xF7) << 6) | ((pointer & 0x08) != 0 ? 0x4000 : 0);
        return sourceOffset / 32;
    }

    private void SetPlayerOamSprite(int spriteIndex, int tile, int x, int y, bool large, bool flipH)
    {
        if (spriteIndex < 0 || spriteIndex >= _playerTileSprites.Count)
        {
            return;
        }

        var spriteSize = large ? 16 : 8;
        var sprite = _playerTileSprites[spriteIndex];
        var region = new Rect2(
            (tile % 16) * 8,
            (tile / 16) * 8,
            spriteSize,
            spriteSize);
        if (sprite.Texture == null ||
            region.Position.X < 0 ||
            region.Position.Y < 0 ||
            region.Position.X + region.Size.X > sprite.Texture.GetWidth() ||
            region.Position.Y + region.Size.Y > sprite.Texture.GetHeight())
        {
            sprite.Visible = false;
            return;
        }

        sprite.Position = new Vector2(x, y);
        sprite.FlipH = flipH;
        sprite.RegionRect = region;
        sprite.Visible = true;
    }

    private Vector2 PlayerRenderPosition()
    {
        return new Vector2(_state.X, _state.Y);
    }

    private SmwPhysics.PlayerState MakeInitialPlayerState(LevelEntrance? entrance = null)
    {
        if (entrance is { } resolvedEntrance)
        {
            return MakeEntrancePlayerState(resolvedEntrance);
        }

        if (TryResolveMainEntrance(ParseLevelId(_currentLevelId), out var mainEntrance))
        {
            return MakeEntrancePlayerState(mainEntrance);
        }

        return MakeFallbackInitialPlayerState();
    }

    private SmwPhysics.PlayerState MakeEntrancePlayerState(LevelEntrance entrance)
    {
        var powerup = _pendingEntrancePowerup >= 0 ? _pendingEntrancePowerup : DefaultPlayerPowerup;
        _pendingEntrancePowerup = -1;
        var state = _physics.MakeState(
            (int)MathF.Round(entrance.Position.X),
            (int)MathF.Round(entrance.Position.Y),
            powerup);
        ApplyEntranceAction(ref state, entrance);
        GD.Print(
            $"smw-runtime: entrance level={entrance.LevelId} source={entrance.SourceId:X3} " +
            $"secondary={(entrance.Secondary ? 1 : 0)} settings={entrance.EntranceSettings} " +
            $"spawn={state.X},{state.Y} facing={state.Facing}");
        return state;
    }

    private void ApplyEntranceAction(ref SmwPhysics.PlayerState state, LevelEntrance entrance)
    {
        _entranceMotionFrames = 0;
        _entranceMotionAction = entrance.EntranceSettings;
        _entranceMotionPixelsPerFrame = Vector2.Zero;
        _entranceMotionDelayFrames = 0;
        _entranceReleaseHoldFrames = 0;
        _deferredEntranceMotionFrames = 0;
        _deferredEntranceMotionPixelsPerFrame = Vector2.Zero;
        state.Facing = EntranceFacing(entrance.EntranceSettings);
        state.SpinJump = false;
        state.Ducking = false;
        state.OnGround = false;
        state.XSpeed = 0;
        state.YSpeed = 0;
        state.SubXSpeed = 0;
        state.SubYSpeed = 0;

        switch (entrance.EntranceSettings)
        {
            case 1:
                StartEntranceMotion(entrance.EntranceSettings, 28, new Vector2(-0.5f, 0.0f), ref state);
                break;
            case 2:
                StartEntranceMotion(entrance.EntranceSettings, 28, new Vector2(0.5f, 0.0f), ref state);
                break;
            case 3:
                StartEntranceMotion(entrance.EntranceSettings, 28, new Vector2(0.0f, -1.0f), ref state);
                break;
            case 4:
                state.X += 8;
                StartEntranceMotion(entrance.EntranceSettings, 1, new Vector2(0.0f, 1.0f), ref state);
                _entranceMotionDelayFrames = NativeVerticalPipeExitHoldFrames;
                _deferredEntranceMotionFrames = 28;
                _deferredEntranceMotionPixelsPerFrame = new Vector2(0.0f, 1.0f);
                break;
            case 6:
                state.X |= 8;
                state.Y |= 2;
                StartEntranceMotion(entrance.EntranceSettings, 1, new Vector2(4.0f, -4.0f), ref state);
                _entranceMotionDelayFrames = 30;
                _deferredEntranceMotionFrames = 30;
                _deferredEntranceMotionPixelsPerFrame = new Vector2(4.0f, -4.0f);
                break;
            default:
                state.OnGround = true;
                state.InAirState = 0;
                state.SubY = 0x80;
                state.YSpeed = 0;
                state.SubYSpeed = 0;
                break;
        }
    }

    private void StartEntranceMotion(int action, int frames, Vector2 pixelsPerFrame, ref SmwPhysics.PlayerState state)
    {
        _entranceMotionAction = action;
        _entranceMotionFrames = frames;
        _entranceMotionPixelsPerFrame = pixelsPerFrame;
        state.XSpeed = (int)MathF.Round(pixelsPerFrame.X * 16.0f);
        state.YSpeed = (int)MathF.Round(pixelsPerFrame.Y * 16.0f);
        GD.Print($"smw-runtime: entrance_motion action={action} frames={frames} dx={pixelsPerFrame.X:0.00} dy={pixelsPerFrame.Y:0.00}");
    }

    private static int EntranceFacing(int entranceSettings)
    {
        return entranceSettings == 1 ? 0 : 1;
    }

    private void ApplyEntranceMotion()
    {
        AddSubpixelDelta(ref _state.X, ref _state.SubX, _entranceMotionPixelsPerFrame.X);
        AddSubpixelDelta(ref _state.Y, ref _state.SubY, _entranceMotionPixelsPerFrame.Y);
        _state.XSpeed = (int)MathF.Round(_entranceMotionPixelsPerFrame.X * 16.0f);
        _state.YSpeed = (int)MathF.Round(_entranceMotionPixelsPerFrame.Y * 16.0f);
        _state.OnGround = false;

        _entranceMotionFrames--;
        if (_entranceMotionFrames <= 0)
        {
            _entranceMotionFrames = 0;
            _entranceMotionPixelsPerFrame = Vector2.Zero;
            var keepShootOutVelocity = _entranceMotionAction == 6 &&
                _entranceMotionDelayFrames == 0 &&
                _deferredEntranceMotionFrames == 0;
            if (keepShootOutVelocity)
            {
                _state.XSpeed = 0x40;
                _state.YSpeed = -0x40;
                _state.InAirState = SmwPhysics.NativeRunningJumpInAirState;
                _entranceReleaseHoldFrames = 1;
                _postPipeShootoutPMeterFloorFrames = 96;
            }
            else
            {
                _state.XSpeed = 0;
                _state.YSpeed = 0;
                _state.SubXSpeed = 0;
                _state.SubYSpeed = 0;
            }
            if (_entranceMotionAction == 4 &&
                _entranceMotionDelayFrames == 0 &&
                _deferredEntranceMotionFrames == 0)
            {
                _state.Y += NativeVerticalPipeExitReleaseYOffset;
                _state.YSpeed = 0x10;
                _pipeExitPassThroughFrames = NativePipeExitPassThroughFrames;
                _pipeExitJumpSubYFrames = NativePipeExitJumpSubYFrames;
                _pipeExitSyntheticGroundSubY = -1;
            }
            GD.Print($"smw-runtime: entrance_motion_done action={_entranceMotionAction} x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
        }
    }

    private void ApplyEntranceReleaseHold()
    {
        _state.XSpeed = 0x40;
        _state.YSpeed = -0x40;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _state.InAirState = SmwPhysics.NativeRunningJumpInAirState;
        _entranceReleaseHoldFrames--;
    }

    private void ApplyPostPipeShootoutPMeterFloor(SmwPhysics.FrameInput frameInput)
    {
        if (_postPipeShootoutPMeterFloorFrames <= 0)
        {
            return;
        }

        _postPipeShootoutPMeterFloorFrames--;
        if (frameInput.Right && frameInput.Run && _state.PMeter < 10)
        {
            _state.PMeter = 10;
        }
    }

    private void BuildPipeExitFilteredSolids()
    {
        _frameSolids.Clear();
        _frameSolidStepUpEnabled.Clear();
        _frameSolidVerticalEnabled.Clear();
        _frameSolidSupportModes.Clear();

        var playerRect = _physics.PlayerRect(_state);
        for (var i = 0; i < _solids.Count; i++)
        {
            var solid = _solids[i];
            if (IsPassThroughPipeShaftSolid(solid, playerRect))
            {
                continue;
            }

            _frameSolids.Add(solid);
            _frameSolidStepUpEnabled.Add(_solidStepUpEnabled[i]);
            _frameSolidVerticalEnabled.Add(_solidVerticalEnabled[i]);
            _frameSolidSupportModes.Add(_solidSupportModes[i]);
        }
    }

    private static bool IsPassThroughPipeShaftSolid(Rect2 solid, Rect2 playerRect)
    {
        var playerLeft = playerRect.Position.X;
        var playerRight = playerRect.Position.X + playerRect.Size.X;
        var solidLeft = solid.Position.X;
        var solidRight = solid.Position.X + solid.Size.X;
        var horizontallyOverlaps = playerRight > solidLeft && playerLeft < solidRight;
        return solid.Size.X <= Map16TileSize * 2.0f &&
            solid.Size.Y >= Map16TileSize * 2.0f &&
            horizontallyOverlaps;
    }

    private void ApplyEntranceMotionDelay()
    {
        _state.XSpeed = 0;
        _state.YSpeed = 0;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;

        _entranceMotionDelayFrames--;
        if (_entranceMotionDelayFrames <= 0)
        {
            _entranceMotionDelayFrames = 0;
            _entranceReleaseHoldFrames = 0;
            if (_deferredEntranceMotionFrames > 0)
            {
                var frames = _deferredEntranceMotionFrames;
                var pixelsPerFrame = _deferredEntranceMotionPixelsPerFrame;
                _deferredEntranceMotionFrames = 0;
                _deferredEntranceMotionPixelsPerFrame = Vector2.Zero;
                StartEntranceMotion(_entranceMotionAction, frames, pixelsPerFrame, ref _state);
            }
        }
    }

    private void StartPipeEntryMotion(LevelEntrance entrance, bool horizontal)
    {
        _pendingPipeTransitionEntrance = entrance;
        _pipeEntryHorizontal = horizontal;
        _pipeEntryMotionFrames = horizontal ? NativeHorizontalPipeEntryFrames : 32;
        _pipeEntryInitialFrames = _pipeEntryMotionFrames;
        _pipeEntryPixelsPerFrame = horizontal ? new Vector2(0.5f, 0.0f) : new Vector2(0.0f, 1.0f);
        _pipeTransitionDelayAfterEntryFrames = horizontal
            ? NativeHorizontalPipeTransitionDelayFrames
            : NativePipeTransitionDelayFrames;
        if (horizontal)
        {
            _state.SubX = 0;
            _state.SubY = 0;
            _state.XSpeed = 0;
            _state.YSpeed = 6;
        }
        else
        {
            _state.XSpeed = 0;
            _state.YSpeed = 0x10;
        }
        _state.SubYSpeed = 0;
        _state.OnGround = true;
        _state.InAirState = 0;
        _state.SpinJump = false;
        GD.Print(
            $"smw-runtime: pipe_entry_motion level={_currentLevelId} target={entrance.LevelId} " +
            $"secondary={(entrance.Secondary ? 1 : 0)} source={entrance.SourceId:X3} " +
            $"horizontal={(horizontal ? 1 : 0)} frames={_pipeEntryMotionFrames}");
    }

    private void ClearPipeEntryMotion()
    {
        _pipeEntryMotionFrames = 0;
        _pipeTransitionDelayFrames = 0;
        _pipeExitPassThroughFrames = 0;
        _pipeExitJumpSubYFrames = 0;
        _pipeExitSyntheticGroundSubY = -1;
        _pipeEntryHorizontal = false;
        _pipeEntryInitialFrames = 0;
        _pipeEntryPixelsPerFrame = Vector2.Zero;
        _pipeTransitionDelayAfterEntryFrames = 0;
        _postPipeShootoutPMeterFloorFrames = 0;
        _pendingPipeTransitionEntrance = null;
    }

    private void ApplyPipeEntryMotion()
    {
        AddSubpixelDelta(ref _state.X, ref _state.SubX, _pipeEntryPixelsPerFrame.X);
        AddSubpixelDelta(ref _state.Y, ref _state.SubY, _pipeEntryPixelsPerFrame.Y);
        if (_pipeEntryHorizontal && _pipeEntryMotionFrames == NativeHorizontalPipeEntrySettleFrame)
        {
            _state.Y -= 2;
            _state.SubY = 0x20;
        }
        _state.XSpeed = (int)MathF.Round(_pipeEntryPixelsPerFrame.X * 16.0f);
        _state.YSpeed = _pipeEntryHorizontal ? 0 : 0x10;
        _state.SubXSpeed = 0x80;
        _state.SubYSpeed = 0;
        _state.OnGround = true;
        _state.InAirState = 0;
        _state.SpinJump = false;

        _pipeEntryMotionFrames--;
        if (_pipeEntryMotionFrames <= 0)
        {
            _pipeEntryMotionFrames = 0;
            _pipeTransitionDelayFrames = _pipeTransitionDelayAfterEntryFrames;
        }
    }

    private void ApplyPipeTransitionDelay()
    {
        _state.XSpeed = 0;
        _state.YSpeed = 0x10;
        _state.SubXSpeed = 0x80;
        _state.SubYSpeed = 0;
        _state.OnGround = true;
        _state.InAirState = 0;
        _state.SpinJump = false;

        _pipeTransitionDelayFrames--;
        if (_pipeTransitionDelayFrames <= 0)
        {
            _pipeTransitionDelayFrames = 0;
            if (_pendingPipeTransitionEntrance is { } entrance)
            {
                _pendingEntrancePowerup = _state.Powerup;
                _pendingPipeTransitionEntrance = null;
                EnterLevel(entrance.LevelId, entrance);
            }
        }
    }

    private static void AddSubpixelDelta(ref int pixel, ref int subpixel, float deltaPixels)
    {
        var total = pixel * 256 + subpixel + (int)MathF.Round(deltaPixels * 256.0f);
        pixel = MathDivFloor(total, 256);
        subpixel = total - pixel * 256;
    }

    private static int MathDivFloor(int value, int divisor)
    {
        var quotient = value / divisor;
        var remainder = value % divisor;
        return remainder != 0 && ((remainder < 0) != (divisor < 0)) ? quotient - 1 : quotient;
    }

    private SmwPhysics.PlayerState MakeFallbackInitialPlayerState()
    {
        var playerHeight = SmwPhysics.PlayerHeightForPowerup(DefaultPlayerPowerup);
        foreach (var tile in _placedTiles)
        {
            if (tile.X >= 8 &&
                (tile.Source.Contains("ledge_top", StringComparison.Ordinal) ||
                    tile.Source.Contains("mushroom_top", StringComparison.Ordinal) ||
                    tile.Source.Contains("horizontal_pipe", StringComparison.Ordinal)))
            {
                return _physics.MakeState(
                    tile.X * Map16TileSize + Map16TileSize,
                    (int)(tile.Y * Map16TileSize + LevelVisualYOffset - playerHeight),
                    DefaultPlayerPowerup);
            }
        }

        return _physics.MakeState(64, 64, DefaultPlayerPowerup);
    }

    private bool TryResolveScreenExit(int screen, out LevelEntrance entrance)
    {
        Godot.Collections.Dictionary? exitData = null;
        foreach (var entry in _screenExits)
        {
            if (entry.TryGetValue("screen", out var screenVariant) && screenVariant.AsInt32() == screen)
            {
                exitData = entry;
                break;
            }
        }

        if (exitData == null)
        {
            entrance = default;
            return false;
        }

        return TryResolveScreenExit(exitData, out entrance);
    }

    private bool TryResolveScreenExit(Godot.Collections.Dictionary exitData, out LevelEntrance entrance)
    {
        if (!exitData.TryGetValue("vanilla_destination", out var destinationVariant))
        {
            entrance = default;
            return false;
        }

        var destination = destinationVariant.AsInt32();
        var properties = exitData.TryGetValue("vanilla_properties", out var propertiesVariant)
            ? propertiesVariant.AsInt32()
            : 0;
        if ((properties & 0x02) != 0)
        {
            return TryResolveSecondaryEntrance(destination, out entrance);
        }

        return TryResolveMainEntrance(destination, out entrance);
    }

    private bool TryResolveMainEntrance(int levelId, out LevelEntrance entrance)
    {
        var f000 = ReadEntranceTableByte("level_info_05f000", levelId);
        var f200 = ReadEntranceTableByte("level_info_05f200", levelId);
        if (f000 == null || f200 == null)
        {
            entrance = default;
            return false;
        }

        var yIndex = f000.Value & 0x0F;
        var xIndex = f200.Value & 0x07;
        var entranceSettings = (f200.Value & 0x38) >> 3;
        var screen = ReadEntranceTableByte("level_info_05f600", levelId) ?? 0;
        entrance = new LevelEntrance(
            FormatLevelId(levelId),
            new Vector2(((screen & 0x1F) * 256) + NativeEntranceX(xIndex), NativeEntranceY(yIndex) + LevelVisualYOffset),
            entranceSettings,
            Secondary: false,
            SourceId: levelId);
        return true;
    }

    private bool TryResolveSecondaryEntrance(int secondaryId, out LevelEntrance entrance)
    {
        var targetLow = ReadEntranceTableByte("secondary_level_low_05f800", secondaryId);
        var yByte = ReadEntranceTableByte("secondary_y_05fa00", secondaryId);
        var xByte = ReadEntranceTableByte("secondary_x_05fc00", secondaryId);
        var typeByte = ReadEntranceTableByte("secondary_entrance_type_05fe00", secondaryId);
        if (targetLow == null || yByte == null || xByte == null || typeByte == null)
        {
            entrance = default;
            return false;
        }

        var targetLevel = (secondaryId & 0x100) | targetLow.Value;
        var yIndex = yByte.Value & 0x0F;
        var xIndex = (xByte.Value >> 5) & 0x07;
        var screen = xByte.Value & 0x1F;
        entrance = new LevelEntrance(
            FormatLevelId(targetLevel),
            new Vector2((screen * 256) + NativeEntranceX(xIndex), NativeEntranceY(yIndex) + LevelVisualYOffset),
            typeByte.Value & 0x07,
            Secondary: true,
            SourceId: secondaryId);
        return true;
    }

    private int? ReadEntranceTableByte(string tableName, int index)
    {
        if (!EnsureEntranceTablesLoaded() || _entranceTables == null)
        {
            return null;
        }
        if (!_entranceTables.TryGetValue(tableName, out var tableVariant) ||
            tableVariant.VariantType != Variant.Type.Array)
        {
            return null;
        }

        var table = tableVariant.AsGodotArray();
        if (index < 0 || index >= table.Count)
        {
            return null;
        }

        return table[index].AsInt32();
    }

    private bool EnsureEntranceTablesLoaded()
    {
        if (_entranceTables != null)
        {
            return true;
        }

        const string tablesPath = "res://generated/smw/levels/secondary_tables.json";
        if (!FileAccess.FileExists(tablesPath))
        {
            return false;
        }

        using var file = FileAccess.Open(tablesPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return false;
        }

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }

        _entranceTables = parsed.AsGodotDictionary();
        return true;
    }

    private static int NativeEntranceX(int index)
    {
        index &= 0x07;
        return LoadLevelXLowTable[index] | (LoadLevelXHighTable[index] << 8);
    }

    private static int NativeEntranceY(int index)
    {
        index &= 0x0F;
        return LoadLevelYLowTable[index] | (LoadLevelYHighTable[index] << 8);
    }

    private static int ParseLevelId(string levelId)
    {
        return Convert.ToInt32(levelId, 16);
    }

    private static string FormatLevelId(int levelId)
    {
        return $"{levelId & 0x1FF:X3}";
    }

    private static Vector2 TileToWorld(int x, int y)
    {
        return new Vector2(x * Map16TileSize, y * Map16TileSize + LevelVisualYOffset);
    }

    private void BuildHud()
    {
        _hudLayer?.QueueFree();
        _hudLayer = null;
        _courseClearLayer?.QueueFree();
        _courseClearLayer = null;
        _statusHud = null;
        _hud = null;
        _courseClearLabel = null;
        _pauseLabel = null;
        _gameOverLabel = null;

        var layer = new CanvasLayer
        {
            Name = "HudLayer",
        };
        _hudLayer = layer;
        AddChild(layer);

        _statusHud = new Label
        {
            Position = new Vector2(16, 8),
        };
        _statusHud.AddThemeFontSizeOverride("font_size", 16);
        _statusHud.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        _statusHud.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.95f));
        _statusHud.AddThemeConstantOverride("shadow_offset_x", 2);
        _statusHud.AddThemeConstantOverride("shadow_offset_y", 2);
        layer.AddChild(_statusHud);

        if (DebugOverlays)
        {
            _hud = new Label { Position = new Vector2(12, 32) };
            _hud.AddThemeFontSizeOverride("font_size", 13);
            layer.AddChild(_hud);
            AddAssetPreviewOverlay(layer);
        }

        UpdateHud();
        BuildCourseClearLabel(layer);
        BuildPauseLabel(layer);
        BuildGameOverLabel(layer);
    }

    private void BuildCourseClearLabel(CanvasLayer layer)
    {
        var label = new Label
        {
            Text = "COURSE CLEAR",
            Position = new Vector2(56, 86),
            Visible = _courseClear,
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.35f, 1.0f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.08f, 0.08f, 0.08f, 1.0f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        _courseClearLabel = label;
        layer.AddChild(label);
    }

    private void ShowCourseClearLabel()
    {
        if (_courseClearLabel != null)
        {
            _courseClearLabel.Visible = true;
        }
    }

    private void BuildPauseLabel(CanvasLayer layer)
    {
        var label = new Label
        {
            Text = "PAUSE",
            Position = new Vector2(92, 92),
            Visible = _gamePaused,
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        _pauseLabel = label;
        layer.AddChild(label);
    }

    private void ShowPauseLabel()
    {
        if (_pauseLabel != null)
        {
            _pauseLabel.Visible = true;
        }
    }

    private void HidePauseLabel()
    {
        if (_pauseLabel != null)
        {
            _pauseLabel.Visible = false;
        }
    }

    private void BuildGameOverLabel(CanvasLayer layer)
    {
        var label = new Label
        {
            Text = "GAME OVER\nPRESS START",
            Position = new Vector2(62, 82),
            Visible = _gameOver,
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        label.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 1.0f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        _gameOverLabel = label;
        layer.AddChild(label);
    }

    private void ShowGameOverLabel()
    {
        if (_gameOverLabel != null)
        {
            _gameOverLabel.Visible = true;
        }
    }

    private void HideGameOverLabel()
    {
        if (_gameOverLabel != null)
        {
            _gameOverLabel.Visible = false;
        }
    }

    private void AddAssetPreviewOverlay(CanvasLayer layer)
    {
        var panel = new ColorRect
        {
            Color = new Color(0.02f, 0.03f, 0.04f, 0.78f),
            Position = new Vector2(484, 12),
            Size = new Vector2(304, 380),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(panel);

        AddPreviewLabel(layer, "Level GFX", new Vector2(496, 20));
        AddPreviewImage(layer, _levelGfxAtlasPath, new Vector2(496, 44), Vector2.One);

        AddPreviewLabel(layer, "Sprite GFX", new Vector2(636, 20));
        AddPreviewImage(layer, _levelSpriteAtlasPath, new Vector2(636, 44), Vector2.One);

        AddPreviewLabel(layer, "Layout", new Vector2(496, 188));
        AddPreviewImage(layer, _levelLayoutPreviewPath, new Vector2(496, 212), new Vector2(0.08f, 0.08f));

        AddPreviewLabel(layer, "Map16", new Vector2(636, 188));
        AddPreviewImage(layer, _levelMap16AtlasPath, new Vector2(636, 212), new Vector2(0.42f, 0.42f));
    }

    private static void AddPreviewLabel(Node parent, string text, Vector2 position)
    {
        var label = new Label
        {
            Text = text,
            Position = position,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        parent.AddChild(label);
    }

    private static void AddPreviewImage(Node parent, string resourcePath, Vector2 position, Vector2 scale)
    {
        if (!FileAccess.FileExists(resourcePath))
        {
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(resourcePath));
        if (image == null || image.IsEmpty())
        {
            return;
        }

        var sprite = new Sprite2D
        {
            Texture = ImageTexture.CreateFromImage(image),
            Position = position,
            Scale = scale,
            Centered = false,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        parent.AddChild(sprite);
    }

    private void UpdateHud()
    {
        if (_statusHud != null)
        {
            _statusHud.Text = BuildStatusHudText();
        }

        if (_hud == null)
        {
            return;
        }

        var footTile = DescribeFootTile();
        _hud.Text = $"x={_state.XFloat:000000.00} y={_state.YFloat:000000.00} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} p={_state.PMeter:X2} pow={_state.Powerup} star={_starPowerTimer:X2} h={SmwPhysics.PlayerHeightFor(_state)} " +
            $"g={(_state.OnGround ? 1 : 0)} d={(_state.Ducking ? 1 : 0)} sj={(_state.SpinJump ? 1 : 0)} rt={(_state.RunningTakeoff ? 1 : 0)} jf={_state.JumpHeldFrames} cf={_state.CapeFloatFrames} air={_state.InAirState:X2} " +
            $"cam={_cameraX:0000},{_cameraY:0000} tiles={_placedTiles.Count} solids={_solids.Count} slopes={_slopes.Count} " +
            $"score={_score} lives={_lives} time={LevelTimerSecondsRemaining()} pause={(_gamePaused ? 1 : 0)} coins={_coinCount}/{_dragonCoinCount} deaths={_deathCount} " +
            $"tile={footTile} exits={_screenExits.Count} sprites={_levelSprites.Count}/{_spriteActors.Count} player={_playerTileSprites.Count}";
    }

    private string BuildStatusHudText()
    {
        var status = _gameOver ? "  GAME OVER" : _gamePaused ? "  PAUSE" : _courseClear ? "  COURSE CLEAR" : string.Empty;
        return $"MARIO {_score:000000}  x{_lives:00}  COIN {_coinCount:00}  DRAGON {_dragonCoinCount}/5  TIME {LevelTimerSecondsRemaining():000}  L{_currentLevelId}{status}";
    }

    private string DescribeFootTile()
    {
        var footX = (int)MathF.Floor((_state.XFloat + SmwPhysics.PlayerWidth * 0.5f) / Map16TileSize);
        var footY = (int)MathF.Floor((SmwPhysics.PlayerCollisionBottom(_state) - LevelVisualYOffset + 1.0f) / Map16TileSize);
        return DescribeTileAt(footX, footY);
    }

    private string DescribeWorldTile(float x, float y)
    {
        var tileX = (int)MathF.Floor(x / Map16TileSize);
        var tileY = (int)MathF.Floor((y - LevelVisualYOffset) / Map16TileSize);
        return DescribeTileAt(tileX, tileY);
    }

    private string DescribeTileAt(int tileX, int tileY)
    {
        if (!_map16TilesByCoord.TryGetValue((tileX, tileY), out var tile))
        {
            return $"{tileX},{tileY}:----";
        }

        var role = IsSlopeSurfaceTile(tile) ? "slope" :
            IsCoinMarkerTile(tile) ? "coin" :
            IsSolidRuntimeBlockTile(tile) ? "solid" :
            IsSolidMap16Source(tile.Source) ? "solid" :
            "pass";
        return $"{tileX},{tileY}:{tile.Map16:X3}:{role}:{tile.Source}";
    }

    private void PrintRuntimeState()
    {
        var layer2Bg = FileAccess.FileExists(_levelLayer2BackgroundPath) ? 1 : 0;
        GD.Print($"smw-runtime: level={_currentLevelId} layer1_objects={_levelObjects.Count} layer2_objects={_layer2Objects.Count} layer2_bg={layer2Bg} map16_tiles={_placedTiles.Count} collision_rects={_solids.Count} slope_surfaces={_slopes.Count} pipe_cells={_diagonalPipeFloorCells.Count}/{_diagonalPipeBodyCells.Count}/{_diagonalPipeCeilingCells.Count} coin_pickups={_coinPickups.Count} screen_exits={_screenExits.Count} pipe_rects={_pipeEntrances.Count} sprite_spawns={_levelSprites.Count} sprite_actors={_spriteActors.Count} fireballs={_playerFireballs.Count} goal_tapes={_goalTapeTriggers.Count} player_sprites={_playerTileSprites.Count}");
    }

    public void DebugEnterLevel(string levelId)
    {
        EnterLevel(levelId);
    }

    public void DebugRestartCurrentLevel()
    {
        if (_lives <= 0)
        {
            _lives = StartingLives;
        }

        RestartCurrentLevel("debug:restart");
        GD.Print($"smw-debug: restart level={_currentLevelId}");
    }

    public void DebugEnterScreenExit(int screen)
    {
        if (TryResolveScreenExit(screen, out var entrance))
        {
            EnterLevel(entrance.LevelId, entrance);
        }
        else
        {
            GD.PrintErr($"smw-test-screen-exit: screen={screen:X2} unresolved level={_currentLevelId}");
        }
    }

    public void DebugSetPlayerPosition(Vector2 position)
    {
        _courseClear = false;
        _courseClearGoalCoinAwarded = false;
        _courseClearWalkoutFrames = 0;
        _courseClearPostWalkPauseFrames = -1;
        _courseClearExitWalkFrames = 0;
        _courseClearExitTransitionFrames = 0;
        _courseClearInitialWalkoutInputFrame = false;
        _courseClearExitTransition = false;
        if (_courseClearLabel != null)
        {
            _courseClearLabel.Visible = false;
        }
        _entranceMotionFrames = 0;
        _entranceMotionAction = 0;
        _entranceMotionPixelsPerFrame = Vector2.Zero;
        _entranceMotionDelayFrames = 0;
        _entranceReleaseHoldFrames = 0;
        _deferredEntranceMotionFrames = 0;
        _deferredEntranceMotionPixelsPerFrame = Vector2.Zero;
        ClearPipeEntryMotion();
        _pipeTransitionLatch = false;
        ResetPowerupAnimationState();
        _state.X = (int)MathF.Round(position.X);
        _state.Y = (int)MathF.Round(position.Y);
        _state.SubX = 0;
        _state.SubY = 0;
        _state.XSpeed = 0;
        _state.YSpeed = 0;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        ClearTransientJumpState();
        UpdateDebugMaxPlayerX();
        _cameraInitialized = false;
        UpdateCamera();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-spawn: x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
    }

    public void DebugSetPlayerPowerup(int powerup)
    {
        ResetPowerupAnimationState();
        _physics.SetPowerup(ref _state, powerup);
        if (_state.YSpeed == 0 && _state.SubY == 0)
        {
            _state.SubY = 0x80;
        }
        _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }

        UpdatePlayerGraphic(force: true);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print(
            $"smw-test-powerup: powerup={_state.Powerup} height={SmwPhysics.PlayerHeightFor(_state)} " +
            $"suby={_state.SubY} render_y={PlayerRenderYOffsetForState(_state.Powerup, _state.Ducking)} " +
            $"player_palette={PlayerPaletteVariantForPowerup(_state.Powerup)}");
    }

    public void DebugSetPlayerVelocity(int xSpeed, int ySpeed)
    {
        _state.XSpeed = xSpeed;
        _state.YSpeed = ySpeed;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        if (!_state.OnGround && _state.YSpeed != 0 && _state.InAirState == 0)
        {
            _state.InAirState = SmwPhysics.NativeFallingInAirState;
        }
        GD.Print($"smw-test-velocity: xs={_state.XSpeed} ys={_state.YSpeed}");
    }

    public void DebugSetPlayerPMeter(int pMeter)
    {
        _state.PMeter = Math.Clamp(pMeter, 0, 0x70);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-pmeter: p={_state.PMeter:X2}");
    }

    public void DebugSetLives(int lives)
    {
        _lives = Math.Clamp(lives, 0, MaxLives);
        if (_lives > 0 && _gameOver)
        {
            _gameOver = false;
            HideGameOverLabel();
            StartLevelMusic();
        }

        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-lives: lives={_lives} gameover={(_gameOver ? 1 : 0)}");
    }

    public void DebugSetCoins(int coins)
    {
        _coinCount = Math.Clamp(coins, 0, CoinLifeThreshold - 1);
        _pendingNormalCoinIncrements = 0;
        _pendingDragonCoinNormalCoins = 0;
        _normalCoinPickupCooldownFrames = 0;
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-coins: coins={_coinCount} lives={_lives} oneups={_oneUpCount}");
    }

    public void DebugSetDragonCoins(int dragonCoins)
    {
        _dragonCoinCount = Math.Clamp(dragonCoins, 0, DragonCoinLifeThreshold);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-dragon-coins: dragon={_dragonCoinCount} lives={_lives} oneups={_oneUpCount}");
    }

    public void DebugSetPlayerGrounded(bool grounded)
    {
        _state.OnGround = grounded;
        if (grounded)
        {
            _state.YSpeed = 0;
            _state.SubYSpeed = 0;
            _state.InAirState = 0;
            _state.RunningTakeoff = false;
            _state.SpinJump = false;
            _state.JumpHeldFrames = 0;
            _state.CapeFloatFrames = 0;
        }
        else if (_state.InAirState == 0)
        {
            _state.InAirState = SmwPhysics.NativeFallingInAirState;
        }

        GD.Print($"smw-test-ground: grounded={(grounded ? 1 : 0)}");
    }

    private void ClearTransientJumpState()
    {
        _state.JumpHeldFrames = 0;
        _state.CapeFloatFrames = 0;
        _state.InAirState = 0;
        _state.RunningTakeoff = false;
        _state.SpinJump = false;
        _spinJumpFireballTimer = 0;
    }

    public void DebugSetPlayerSpinJump(bool spinJump)
    {
        _state.SpinJump = spinJump;
        GD.Print($"smw-test-spinjump: spin={(spinJump ? 1 : 0)}");
    }

    public void DebugSetOverlays(bool enabled)
    {
        if (DebugOverlays == enabled)
        {
            GD.Print($"smw-debug: overlays={(DebugOverlays ? 1 : 0)} unchanged=1");
            return;
        }

        DebugOverlays = enabled;
        _player?.QueueFree();
        _player = null;
        BuildWorld();
        BuildPlayer();
        BuildHud();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }
        UpdatePlayerGraphic(force: true);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-debug: overlays={(DebugOverlays ? 1 : 0)} rebuilt=1");
    }

    public void DebugSetActorsEnabled(bool enabled)
    {
        _debugActorsEnabled = enabled;
        foreach (var actor in _spriteActors)
        {
            actor.Active = enabled && IsSpriteActorAwake(actor);
            ApplySpriteActorVisualVisibility(actor);
        }
        GD.Print($"smw-debug: actors={(_debugActorsEnabled ? 1 : 0)}");
    }

    public void DebugSetActorVisualsEnabled(bool enabled)
    {
        _debugActorVisualsEnabled = enabled;
        foreach (var actor in _spriteActors)
        {
            ApplySpriteActorVisualVisibility(actor);
        }

        GD.Print($"smw-debug: actor_visuals={(_debugActorVisualsEnabled ? 1 : 0)}");
    }

    public void DebugSetCameraLock(bool locked, Vector2? position = null)
    {
        _debugCameraLocked = locked;
        if (position != null)
        {
            _debugCameraLockPosition = position.Value;
        }
        else if (locked)
        {
            _debugCameraLockPosition = new Vector2(_cameraX, _cameraY);
        }

        _cameraInitialized = true;
        UpdateCamera();
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print(BuildDebugCamera(locked ? "lock" : "unlock"));
    }

    public void DebugSetInvincible(bool enabled)
    {
        _debugInvincible = enabled;
        GD.Print($"smw-debug: invincible={(_debugInvincible ? 1 : 0)}");
    }

    public void DebugSetStarPower(int timer)
    {
        var previous = _starPowerTimer;
        _starPowerTimer = Math.Clamp(timer, 0, NativeStarPowerTimerInitial);
        if (_starPowerTimer > 0 && previous <= 0 && AudioEnabled)
        {
            _audio?.PlayMusicPreview("Star");
        }
        else if (_starPowerTimer <= 0 && previous > 0)
        {
            StartLevelMusic();
        }

        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-debug: star={_starPowerTimer:X2}");
    }

    public string DebugSetAudioEnabled(bool enabled)
    {
        AudioEnabled = enabled;
        if (!AudioEnabled)
        {
            _audio?.StopMusicPreview();
            if (_audio != null)
            {
                _audio.ProcessMode = ProcessModeEnum.Disabled;
            }

            var disabled = BuildDebugAudioState("toggle");
            GD.Print(disabled);
            return disabled;
        }

        if (_audio == null)
        {
            _audio = new SmwAudio { Name = "SmwAudio" };
            AddChild(_audio);
        }

        _audio.ProcessMode = ProcessModeEnum.Inherit;
        StartLevelMusic();
        var enabledState = BuildDebugAudioState("toggle");
        GD.Print(enabledState);
        return enabledState;
    }

    public void DebugUseCommandFile(string path)
    {
        _debugCommandPath = path.StartsWith("res://", StringComparison.Ordinal) ||
            path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : IoPath.IsPathRooted(path)
                    ? path
                    : IoPath.GetFullPath(path);
        _debugCommandOffset = 0;
        var directory = IoPath.GetDirectoryName(_debugCommandPath);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        if (!IoFile.Exists(_debugCommandPath))
        {
            IoFile.WriteAllText(_debugCommandPath, string.Empty);
        }

        GD.Print($"smw-debug: command_file={_debugCommandPath}");
    }

    public void DebugSetAutoplayMode(string mode)
    {
        _autoplayMode = ParseAutoplayMode(mode);
        ResetAutoplayState();
        GD.Print($"smw-debug-autoplay: mode={AutoplayModeName(_autoplayMode)} frame={_autoplayFrame}");
    }

    public void DebugStartRcon(int port)
    {
        StopDebugRcon();
        _debugRconListener = new TcpListener(IPAddress.Loopback, port);
        _debugRconListener.Start();
        var boundPort = ((IPEndPoint)_debugRconListener.LocalEndpoint).Port;
        GD.Print($"smw-rcon: listening=127.0.0.1:{boundPort}");
    }

    private void StopDebugRcon()
    {
        foreach (var client in _debugRconClients)
        {
            client.Dispose();
        }

        _debugRconClients.Clear();
        _debugRconBuffers.Clear();
        _debugRconListener?.Stop();
        _debugRconListener = null;
    }

    private void PollDebugRcon()
    {
        if (_debugRconListener == null)
        {
            return;
        }

        try
        {
            while (_debugRconListener.Pending())
            {
                var client = _debugRconListener.AcceptTcpClient();
                client.NoDelay = true;
                _debugRconClients.Add(client);
                _debugRconBuffers[client] = new StringBuilder();
                WriteRcon(client, "smw-rcon ready\n");
            }

            for (var i = _debugRconClients.Count - 1; i >= 0; i--)
            {
                PollDebugRconClient(_debugRconClients[i]);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"smw-rcon: error={ex.Message}");
        }
    }

    private void PollDebugRconClient(TcpClient client)
    {
        if (!client.Connected)
        {
            DropDebugRconClient(client);
            return;
        }

        var stream = client.GetStream();
        while (stream.DataAvailable)
        {
            var read = stream.Read(_debugRconReadBuffer, 0, _debugRconReadBuffer.Length);
            if (read <= 0)
            {
                DropDebugRconClient(client);
                return;
            }

            _debugRconBuffers[client].Append(Encoding.UTF8.GetString(_debugRconReadBuffer, 0, read));
        }

        ProcessDebugRconLines(client);
    }

    private void ProcessDebugRconLines(TcpClient client)
    {
        var builder = _debugRconBuffers[client];
        var text = builder.ToString();
        var consumed = 0;
        while (true)
        {
            var newline = text.IndexOf('\n', consumed);
            if (newline < 0)
            {
                break;
            }

            var line = text[consumed..newline].TrimEnd('\r');
            consumed = newline + 1;
            var response = ExecuteDebugCommand(line);
            if (!string.IsNullOrWhiteSpace(response))
            {
                WriteRcon(client, response + "\n");
            }
        }

        if (consumed <= 0)
        {
            return;
        }

        builder.Clear();
        if (consumed < text.Length)
        {
            builder.Append(text[consumed..]);
        }
    }

    private void DropDebugRconClient(TcpClient client)
    {
        _debugRconBuffers.Remove(client);
        _debugRconClients.Remove(client);
        client.Dispose();
    }

    private static void WriteRcon(TcpClient client, string text)
    {
        if (!client.Connected)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        client.GetStream().Write(bytes, 0, bytes.Length);
    }

    private void PollDebugCommands()
    {
        if (_debugCommandPath == null || !IoFile.Exists(_debugCommandPath))
        {
            return;
        }

        try
        {
            using var stream = IoFile.Open(_debugCommandPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            if (stream.Length < _debugCommandOffset)
            {
                _debugCommandOffset = 0;
            }
            stream.Seek(_debugCommandOffset, System.IO.SeekOrigin.Begin);
            using var reader = new System.IO.StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                _ = ExecuteDebugCommand(line);
            }
            _debugCommandOffset = stream.Position;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"smw-debug: command_file_error path={_debugCommandPath} error={ex.Message}");
        }
    }

    private string ExecuteDebugCommand(string rawLine)
    {
        var commentIndex = rawLine.IndexOf('#');
        var line = commentIndex >= 0 ? rawLine[..commentIndex] : rawLine;
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var parts = line.Split(
            [' ', '\t', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return ExecuteDebugCommandParts(parts);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"smw-debug: command_error line=\"{line}\" error={ex.Message}");
            return $"error {ex.Message}";
        }
    }

    private string ExecuteDebugCommandParts(string[] parts)
    {
        switch (parts[0].ToLowerInvariant())
        {
            case "pause":
                _debugPaused = true;
                GD.Print("smw-debug: paused=1");
                return "ok paused=1";
            case "resume":
            case "run":
                _debugPaused = false;
                _debugStepFrames = 0;
                GD.Print("smw-debug: paused=0");
                return "ok paused=0";
            case "step":
            case "frame":
                _debugPaused = true;
                _debugStepFrames += parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stepFrames)
                    ? Math.Max(1, stepFrames)
                    : 1;
                GD.Print($"smw-debug: step queued={_debugStepFrames}");
                return $"ok step_queued={_debugStepFrames}";
            case "input":
            case "press":
                QueueDebugInput(parts);
                return $"ok input_frames={_debugCommandInputFrames}";
            case "tap":
                QueueDebugTap(parts);
                return $"ok tap_frames={_debugCommandInputFrames}";
            case "hold":
            case "controller":
                SetDebugHeldInput(parts);
                return $"ok hold input={DescribeFrameInput(_debugHeldInput)}";
            case "release":
            case "clear_input":
            case "neutral":
                ClearDebugHeldInput();
                return "ok hold input=--------";
            case "trace":
                QueueDebugTrace(parts, includeOam: false, includeSensors: false, overrideInput: true);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_oam":
            case "traceoam":
            case "oam_trace":
                QueueDebugTrace(parts, includeOam: true, includeSensors: false, overrideInput: true);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_sensors":
            case "tracesensors":
            case "sensor_trace":
                QueueDebugTrace(parts, includeOam: false, includeSensors: true, overrideInput: true);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_full":
            case "full_trace":
                QueueDebugTrace(parts, includeOam: true, includeSensors: true, overrideInput: true);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_live":
            case "live_trace":
                QueueDebugTrace(parts, includeOam: false, includeSensors: false, overrideInput: false);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_live_oam":
            case "live_trace_oam":
                QueueDebugTrace(parts, includeOam: true, includeSensors: false, overrideInput: false);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_live_sensors":
            case "live_trace_sensors":
                QueueDebugTrace(parts, includeOam: false, includeSensors: true, overrideInput: false);
                return $"ok trace_queued={_debugTraceFrames}";
            case "trace_live_full":
            case "live_trace_full":
                QueueDebugTrace(parts, includeOam: true, includeSensors: true, overrideInput: false);
                return $"ok trace_queued={_debugTraceFrames}";
            case "spawn":
            case "pos":
                RequirePartCount(parts, 3);
                if (parts.Length >= 4)
                {
                    DebugSetPlayerPowerup(ParseDebugPowerup(parts[3]));
                }
                DebugSetPlayerPosition(new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                return BuildDebugState("spawn");
            case "powerup":
            case "pow":
                RequirePartCount(parts, 2);
                DebugSetPlayerPowerup(ParseDebugPowerup(parts[1]));
                return BuildDebugState("powerup");
            case "item":
            case "spawn_item":
            case "powerup_item":
                RequirePartCount(parts, 2);
                var itemPosition = parts.Length >= 4
                    ? new Vector2(ParseFloat(parts[2]), ParseFloat(parts[3]))
                    : new Vector2(_state.XFloat, _state.YFloat);
                DebugSpawnPowerupItem(ParseDebugItemSprite(parts[1]), itemPosition);
                return BuildDebugState("item");
            case "velocity":
            case "vel":
                RequirePartCount(parts, 3);
                DebugSetPlayerVelocity(
                    int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture));
                return BuildDebugState("velocity");
            case "pmeter":
            case "p_meter":
            case "p":
                RequirePartCount(parts, 2);
                DebugSetPlayerPMeter(ParseHexOrDecimalDebug(parts[1]));
                return BuildDebugState("pmeter");
            case "lives":
            case "life":
                if (parts.Length >= 2)
                {
                    DebugSetLives(ParseHexOrDecimalDebug(parts[1]));
                    return BuildDebugState("lives");
                }

                return PrintDebugLives("status");
            case "coins":
            case "coin":
                if (parts.Length >= 2)
                {
                    DebugSetCoins(ParseHexOrDecimalDebug(parts[1]));
                    return BuildDebugState("coins");
                }

                return PrintDebugStatusHud();
            case "dragon":
            case "dragon_coins":
            case "dragoncoins":
                if (parts.Length >= 2)
                {
                    DebugSetDragonCoins(ParseHexOrDecimalDebug(parts[1]));
                    return BuildDebugState("dragon");
                }

                return PrintDebugStatusHud();
            case "game_pause":
            case "gamepause":
            case "start_pause":
            case "startpause":
                if (parts.Length >= 2)
                {
                    SetGameplayPaused(ParseDebugBool(parts[1]), "debug");
                }
                else
                {
                    ToggleGameplayPause("debug");
                }

                return BuildDebugState("game_pause");
            case "ground":
            case "onground":
                RequirePartCount(parts, 2);
                DebugSetPlayerGrounded(ParseDebugBool(parts[1]));
                return BuildDebugState("ground");
            case "spinjump":
            case "spinstate":
                RequirePartCount(parts, 2);
                DebugSetPlayerSpinJump(ParseDebugBool(parts[1]));
                return BuildDebugState("spinjump");
            case "overlays":
            case "gizmos":
            case "debug":
                RequirePartCount(parts, 2);
                DebugSetOverlays(ParseDebugBool(parts[1]));
                return BuildDebugState("overlays");
            case "actors":
            case "sprites":
                RequirePartCount(parts, 2);
                DebugSetActorsEnabled(ParseDebugBool(parts[1]));
                return BuildDebugState("actors");
            case "actor_visuals":
            case "sprite_visuals":
            case "sprites_visible":
                RequirePartCount(parts, 2);
                DebugSetActorVisualsEnabled(ParseDebugBool(parts[1]));
                return BuildDebugState("actor_visuals");
            case "camera":
            case "cam":
                return ExecuteDebugCameraCommand(parts);
            case "god":
            case "invincible":
                RequirePartCount(parts, 2);
                DebugSetInvincible(ParseDebugBool(parts[1]));
                return BuildDebugState("invincible");
            case "star":
            case "starman":
            case "star_power":
                if (parts.Length >= 2)
                {
                    DebugSetStarPower(parts[1].Trim().ToLowerInvariant() switch
                    {
                        "on" or "true" or "yes" => NativeStarPowerTimerInitial,
                        "off" or "false" or "no" => 0,
                        _ => ParseHexOrDecimalDebug(parts[1]),
                    });
                }
                else
                {
                    DebugSetStarPower(NativeStarPowerTimerInitial);
                }

                return BuildDebugState("star");
            case "audio":
                return ExecuteDebugAudioCommand(parts);
            case "perf":
            case "fps":
            case "stats":
                return PrintDebugPerformance(parts.Length >= 2 ? parts[1] : "status");
            case "physics":
            case "phys":
                return PrintDebugPhysics(parts.Length >= 2 ? parts[1] : "manual");
            case "level":
                RequirePartCount(parts, 2);
                DebugEnterLevel(parts[1].ToUpperInvariant());
                return BuildDebugState("level");
            case "restart":
            case "reset":
            case "reload":
                DebugRestartCurrentLevel();
                return BuildDebugState("restart");
            case "continue":
            case "retry":
                ContinueAfterGameOver();
                return BuildDebugState("continue");
            case "screen_exit":
            case "exit":
                RequirePartCount(parts, 2);
                DebugEnterScreenExit(ParseHexOrDecimalDebug(parts[1]));
                return BuildDebugState("screen_exit");
            case "script":
                RequirePartCount(parts, 2);
                DebugLoadInputScript(parts[1]);
                return "ok script_loaded";
            case "autoplay":
            case "auto":
                return ExecuteDebugAutoplayCommand(parts);
            case "capture":
                RequirePartCount(parts, 2);
                var frames = parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var captureFrames)
                    ? Math.Max(1, captureFrames)
                    : 1;
                DebugCaptureViewport(parts[1], frames, quitAfterCapture: false);
                return $"ok capture_scheduled={parts[1]} frames={frames}";
            case "capture_now":
            case "snapshot":
                RequirePartCount(parts, 2);
                var captureError = CaptureViewportNow(parts[1], quitAfterCapture: false);
                return $"ok capture_saved={parts[1]} error={captureError}";
            case "state":
                return PrintDebugState(parts.Length >= 2 ? parts[1] : "manual");
            case "save":
            case "save_state":
            case "checkpoint_save":
                return DebugSaveCheckpoint(parts.Length >= 2 ? parts[1] : "default");
            case "load":
            case "load_state":
            case "checkpoint_load":
                return DebugLoadCheckpoint(parts.Length >= 2 ? parts[1] : "default");
            case "save_file":
            case "save_state_file":
            case "checkpoint_save_file":
                return DebugSaveCheckpointFile(parts.Length >= 2 ? parts[1] : "default");
            case "load_file":
            case "load_state_file":
            case "checkpoint_load_file":
                return DebugLoadCheckpointFile(parts.Length >= 2 ? parts[1] : "default");
            case "checkpoints":
            case "saves":
                return PrintDebugCheckpoints();
            case "checkpoint_files":
            case "save_files":
                return PrintDebugCheckpointFiles();
            case "tile":
            case "probe":
                return PrintDebugTile(parts);
            case "sensors":
            case "sensor":
            case "player_sensors":
                return PrintDebugPlayerSensors(parts.Length >= 2 ? parts[1] : "manual");
            case "collision":
            case "collisions":
            case "collide":
                return PrintDebugCollision(parts);
            case "slope_probe":
            case "slopes_probe":
            case "slopes_at":
                return PrintDebugSlopeProbe(parts);
            case "pipe":
            case "pipe_probe":
            case "pipe_cells":
                return PrintDebugPipeCells(parts);
            case "pipe_entrances":
            case "pipes":
            case "entrances":
                return PrintDebugPipeEntrances();
            case "goal":
            case "goal_tape":
            case "goal_tapes":
                return PrintDebugGoalTapes();
            case "status":
            case "statusbar":
            case "hud":
                return PrintDebugStatusHud();
            case "timer":
            case "time":
            case "clock":
                return ExecuteDebugTimerCommand(parts);
            case "player_oam":
            case "oam":
            case "pose":
                return PrintDebugPlayerOam(parts.Length >= 2 ? parts[1] : "manual");
            case "actors_near":
            case "near":
                return PrintDebugActorsNear(parts);
            case "actor_oam":
            case "sprite_oam":
            case "sprites_oam":
                return PrintDebugActorOam(parts);
            case "pickups_near":
            case "pickup_near":
            case "coins_near":
            case "dragon_near":
                return PrintDebugPickupsNear(parts);
            case "quit":
                GD.Print("smw-debug: quit");
                GetTree().Quit();
                return "ok quit";
            default:
                throw new FormatException($"unknown command '{parts[0]}'");
        }
    }

    private string ExecuteDebugAutoplayCommand(string[] parts)
    {
        if (parts.Length >= 2)
        {
            DebugSetAutoplayMode(parts[1]);
        }

        return PrintDebugAutoplay();
    }

    private string PrintDebugAutoplay()
    {
        return $"smw-debug-autoplay: mode={AutoplayModeName(_autoplayMode)} frame={_autoplayFrame} stuck={_autoplayStuckFrames} last_x={_autoplayLastPlayerX}";
    }

    private void ResetAutoplayState()
    {
        _autoplayFrame = 0;
        _autoplayLastPlayerX = _state.X;
        _autoplayStuckFrames = 0;
        _autoplayJumpHeld = false;
    }

    private static DebugAutoplayMode ParseAutoplayMode(string mode)
    {
        return mode.Trim().ToLowerInvariant() switch
        {
            "" or "off" or "none" or "0" or "false" => DebugAutoplayMode.Off,
            "title" or "title-start" or "titlestart" => DebugAutoplayMode.TitleStart,
            "explore" or "play" or "on" or "1" or "true" => DebugAutoplayMode.Explore,
            _ => throw new FormatException($"unknown autoplay mode '{mode}'"),
        };
    }

    private static string AutoplayModeName(DebugAutoplayMode mode)
    {
        return mode switch
        {
            DebugAutoplayMode.TitleStart => "title-start",
            DebugAutoplayMode.Explore => "explore",
            _ => "off",
        };
    }

    private void QueueDebugInput(string[] parts)
    {
        RequirePartCount(parts, 2);
        var frames = Math.Max(1, int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture));
        var input = new SmwPhysics.FrameInput();
        for (var i = 2; i < parts.Length; i++)
        {
            ApplyScriptedInputToken("smw-debug", 0, parts[i], ref input);
        }

        _debugCommandInput = input;
        _debugCommandInputFrames = frames;
        _debugCommandInputFrame = 0;
        if (_debugPaused)
        {
            _debugStepFrames += frames;
        }
        GD.Print($"smw-debug: input frames={frames} left={(input.Left ? 1 : 0)} right={(input.Right ? 1 : 0)} up={(input.Up ? 1 : 0)} down={(input.Down ? 1 : 0)} jump={(input.Jump ? 1 : 0)} spin={(input.Spin ? 1 : 0)} run={(input.Run ? 1 : 0)}");
    }

    private void QueueDebugTap(string[] parts)
    {
        RequirePartCount(parts, 2);
        var tapParts = new string[parts.Length + 1];
        tapParts[0] = "input";
        tapParts[1] = "1";
        Array.Copy(parts, 1, tapParts, 2, parts.Length - 1);
        QueueDebugInput(tapParts);
    }

    private void SetDebugHeldInput(string[] parts)
    {
        RequirePartCount(parts, 2);
        var input = new SmwPhysics.FrameInput();
        for (var i = 1; i < parts.Length; i++)
        {
            ApplyScriptedInputToken("smw-debug-hold", 0, parts[i], ref input);
        }

        _debugHeldJumpPressed = input.Jump && !_debugHeldInput.Jump;
        _debugHeldSpinPressed = input.Spin && !_debugHeldInput.Spin;
        _debugHeldRunPressed = input.Run && !_debugHeldInput.Run;
        _debugHeldInput = input;
        _debugHeldInputActive = true;
        GD.Print($"smw-debug: hold input={DescribeFrameInput(_debugHeldInput)}");
    }

    private void ClearDebugHeldInput()
    {
        _debugHeldInput = default;
        _debugHeldInputActive = false;
        _debugHeldJumpPressed = false;
        _debugHeldSpinPressed = false;
        _debugHeldRunPressed = false;
        GD.Print("smw-debug: hold input=--------");
    }

    private void QueueDebugTrace(string[] parts, bool includeOam, bool includeSensors, bool overrideInput)
    {
        RequirePartCount(parts, 2);
        var frames = Math.Max(1, int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture));
        var input = new SmwPhysics.FrameInput();
        var tag = "trace";
        var quitWhenDone = false;
        var checkpointFrame = -1;
        var checkpointSlot = "";
        var checkpointFile = false;
        for (var i = 2; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
            {
                tag = parts[i][4..];
                continue;
            }
            if (parts[i].Equals("quit_when_done", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Equals("quit_after=1", StringComparison.OrdinalIgnoreCase) ||
                parts[i].Equals("quit=1", StringComparison.OrdinalIgnoreCase))
            {
                quitWhenDone = true;
                continue;
            }
            if (parts[i].StartsWith("checkpoint_at=", StringComparison.OrdinalIgnoreCase))
            {
                checkpointFrame = Math.Max(1, int.Parse(parts[i]["checkpoint_at=".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture));
                continue;
            }
            if (parts[i].StartsWith("checkpoint_name=", StringComparison.OrdinalIgnoreCase))
            {
                checkpointSlot = NormalizeCheckpointSlot(parts[i]["checkpoint_name=".Length..]);
                continue;
            }
            if (parts[i].StartsWith("checkpoint_slot=", StringComparison.OrdinalIgnoreCase))
            {
                checkpointSlot = NormalizeCheckpointSlot(parts[i]["checkpoint_slot=".Length..]);
                continue;
            }
            if (parts[i].StartsWith("checkpoint_file=", StringComparison.OrdinalIgnoreCase))
            {
                checkpointFile = ParseDebugBool(parts[i]["checkpoint_file=".Length..]);
                continue;
            }
            if (parts[i].Equals("checkpoint_file", StringComparison.OrdinalIgnoreCase))
            {
                checkpointFile = true;
                continue;
            }

            if (!overrideInput)
            {
                throw new FormatException($"live trace does not accept input token '{parts[i]}'");
            }

            ApplyScriptedInputToken("smw-debug-trace", 0, parts[i], ref input);
        }

        _debugTraceFrames = frames;
        _debugTraceTotalFrames = frames;
        _debugTraceFrame = 0;
        _debugTraceTag = string.IsNullOrWhiteSpace(tag) ? "trace" : tag;
        _debugTraceOam = includeOam;
        _debugTraceSensors = includeSensors;
        _debugTraceQuitWhenDone = quitWhenDone;
        _debugTraceCheckpointFrame = checkpointFrame;
        _debugTraceCheckpointSlot = string.IsNullOrWhiteSpace(checkpointSlot) ? $"{_debugTraceTag}_{checkpointFrame}" : checkpointSlot;
        _debugTraceCheckpointFile = checkpointFile;
        if (overrideInput)
        {
            _debugCommandInput = input;
            _debugCommandInputFrames = frames;
            _debugCommandInputFrame = 0;
        }
        if (_debugPaused)
        {
            _debugStepFrames += frames;
        }

        GD.Print(
            $"smw-debug: trace queued={frames} tag={_debugTraceTag} input={(overrideInput ? DescribeFrameInput(input) : "live")} oam={(includeOam ? 1 : 0)} sensors={(includeSensors ? 1 : 0)} source={(overrideInput ? "override" : "live")}");
    }

    private void PrintQueuedDebugTrace(SmwPhysics.FrameInput frameInput)
    {
        if (_debugTraceFrames <= 0)
        {
            return;
        }

        _debugTraceFrame++;
        if (_debugTraceFrame == _debugTraceCheckpointFrame)
        {
            _ = _debugTraceCheckpointFile
                ? DebugSaveCheckpointFile(_debugTraceCheckpointSlot)
                : DebugSaveCheckpoint(_debugTraceCheckpointSlot);
        }
        GD.Print(
            $"smw-debug-trace: tag={_debugTraceTag} i={_debugTraceFrame}/{_debugTraceTotalFrames} " +
            $"frame={_debugFrameCounter} input={DescribeFrameInput(frameInput)} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} xi={_state.X} yi={_state.Y} sub={_state.SubX:X2},{_state.SubY:X2} subx={_state.SubX} suby={_state.SubY} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} subxs={_state.SubXSpeed} subys={_state.SubYSpeed} " +
            $"p={_state.PMeter:X2} pow={_state.Powerup} star={_starPowerTimer:X2} h={SmwPhysics.PlayerHeightFor(_state)} " +
            $"g={(_state.OnGround ? 1 : 0)} duck={(_state.Ducking ? 1 : 0)} sj={(_state.SpinJump ? 1 : 0)} rt={(_state.RunningTakeoff ? 1 : 0)} " +
            $"jf={_state.JumpHeldFrames} cf={_state.CapeFloatFrames} air={_state.InAirState:X2} face={_state.Facing} slope={_state.SlopeKind} slope_player={_state.SlopePlayer} slope_type={_state.SlopeType} " +
            $"loose={_state.LooseSteepSlopeGroundFrames} loose_kind={_state.LooseSteepSlopeKind} overrun={(_state.NativeSlopeOverrunGround ? 1 : 0)} lead={_state.LeadingFootCarryFrames} preserve_y={_state.PreserveGroundYSpeedFrames} " +
            $"jump_idx={SmwPhysics.JumpSpeedIndexFor(_state.XSpeed, frameInput.SpinPressed)} " +
            $"clear={(_courseClear ? 1 : 0)} walkout={_courseClearWalkoutFrames} postwait={_courseClearPostWalkPauseFrames} exitwalk={_courseClearExitWalkFrames} exitmode={(_courseClearExitTransition ? 1 : 0)} exitframes={_courseClearExitTransitionFrames} " +
            $"score={_score} coins={_coinCount} dragon={_dragonCoinCount} lives={_lives} oneups={_oneUpCount} " +
            $"pose={_lastPlayerPose} pose_face={_lastPlayerFacing} cam={_cameraX:0.00},{_cameraY:0.00} tile={DescribeFootTile()} near={DescribeNearestActor()} " +
            DescribeNearestActorTraceFields() + " " +
            DescribeTrackedActorTraceFields());

        if (_debugTraceOam)
        {
            GD.Print(BuildDebugPlayerOam($"{_debugTraceTag}_{_debugTraceFrame:00}"));
        }

        if (_debugTraceSensors)
        {
            GD.Print(BuildDebugPlayerSensors($"{_debugTraceTag}_{_debugTraceFrame:00}"));
            GD.Print(BuildDebugSlopeProbe(
                $"{_debugTraceTag}_{_debugTraceFrame:00}",
                _state.XFloat,
                SmwPhysics.PlayerCollisionTop(_state),
                SmwPhysics.PlayerCollisionHeightFor(_state),
                _state.YSpeed));
        }

        _debugTraceFrames--;
        if (_debugTraceFrames <= 0)
        {
            _debugTraceOam = false;
            _debugTraceSensors = false;
            _debugTraceCheckpointFrame = -1;
            _debugTraceCheckpointSlot = "";
            _debugTraceCheckpointFile = false;
            PrintDebugState($"{_debugTraceTag}_done");
            if (_debugTraceQuitWhenDone)
            {
                _debugTraceQuitWhenDone = false;
                GetTree().Quit();
            }
        }
    }

    private static string DescribeFrameInput(SmwPhysics.FrameInput input)
    {
        Span<char> mask = stackalloc char[8];
        mask[0] = input.Left ? 'L' : '-';
        mask[1] = input.Right ? 'R' : '-';
        mask[2] = input.Up ? 'U' : '-';
        mask[3] = input.Down ? 'D' : '-';
        mask[4] = input.Jump ? 'J' : '-';
        mask[5] = input.JumpPressed ? 'j' : '-';
        mask[6] = input.Spin ? 'A' : '-';
        mask[7] = input.Run ? 'Y' : '-';
        return new string(mask);
    }

    private string PrintDebugState(string tag)
    {
        var state = BuildDebugState(tag);
        GD.Print(state);
        return state;
    }

    private string DebugSaveCheckpoint(string slot)
    {
        slot = NormalizeCheckpointSlot(slot);
        var checkpoint = CaptureDebugCheckpoint();
        _debugCheckpoints[slot] = checkpoint;
        var line = FormatCheckpointLine("save", slot, checkpoint, $"count={_debugCheckpoints.Count}");
        GD.Print(line);
        return line;
    }

    private string DebugLoadCheckpoint(string slot)
    {
        slot = NormalizeCheckpointSlot(slot);
        if (!_debugCheckpoints.TryGetValue(slot, out var checkpoint))
        {
            return $"smw-debug-checkpoint: action=load slot={slot} missing=1 count={_debugCheckpoints.Count}";
        }

        ApplyDebugCheckpoint(checkpoint);
        var line = FormatCheckpointLine("load", slot, checkpoint, $"count={_debugCheckpoints.Count}");
        GD.Print(line);
        return line;
    }

    private string DebugSaveCheckpointFile(string slot)
    {
        slot = NormalizeCheckpointSlot(slot);
        var checkpoint = CaptureDebugCheckpoint();
        _debugCheckpoints[slot] = checkpoint;
        var path = CheckpointFilePath(slot);
        var directory = IoPath.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        IoFile.WriteAllText(path, JsonSerializer.Serialize(checkpoint, DebugCheckpointJsonOptions));
        var line = FormatCheckpointLine("save_file", slot, checkpoint, $"path={path}");
        GD.Print(line);
        return line;
    }

    private string DebugLoadCheckpointFile(string slot)
    {
        slot = NormalizeCheckpointSlot(slot);
        var path = CheckpointFilePath(slot);
        if (!IoFile.Exists(path))
        {
            return $"smw-debug-checkpoint: action=load_file slot={slot} missing=1 path={path}";
        }

        var checkpoint = JsonSerializer.Deserialize<DebugCheckpointData>(IoFile.ReadAllText(path), DebugCheckpointJsonOptions);
        if (checkpoint == null)
        {
            return $"smw-debug-checkpoint: action=load_file slot={slot} invalid=1 path={path}";
        }

        _debugCheckpoints[slot] = checkpoint;
        ApplyDebugCheckpoint(checkpoint);
        var line = FormatCheckpointLine("load_file", slot, checkpoint, $"path={path}");
        GD.Print(line);
        return line;
    }

    private string PrintDebugCheckpoints()
    {
        var slots = _debugCheckpoints
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}:{pair.Value.LevelId}:{pair.Value.State.XFloat:0.00},{pair.Value.State.YFloat:0.00}:star={pair.Value.StarPowerTimer:X2}:frame={pair.Value.Frame}");
        var description = string.Join("|", slots);
        var line = $"smw-debug-checkpoints: count={_debugCheckpoints.Count} slots={(string.IsNullOrEmpty(description) ? "none" : description)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugCheckpointFiles()
    {
        var directory = CheckpointDirectoryPath();
        var files = System.IO.Directory.Exists(directory)
            ? System.IO.Directory.EnumerateFiles(directory, "*.json")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => IoPath.GetFileNameWithoutExtension(path))
            : [];
        var description = string.Join("|", files);
        var line = $"smw-debug-checkpoint-files: dir={directory} slots={(string.IsNullOrEmpty(description) ? "none" : description)}";
        GD.Print(line);
        return line;
    }

    private static string NormalizeCheckpointSlot(string slot)
    {
        slot = slot.Trim();
        return string.IsNullOrWhiteSpace(slot) ? "default" : slot;
    }

    private static string CheckpointDirectoryPath()
    {
        return ProjectSettings.GlobalizePath($"res://{DebugCheckpointDirectory}");
    }

    private static string CheckpointFilePath(string slot)
    {
        return IoPath.Combine(CheckpointDirectoryPath(), SanitizeCheckpointSlotForFile(slot) + ".json");
    }

    private static string SanitizeCheckpointSlotForFile(string slot)
    {
        var normalized = NormalizeCheckpointSlot(slot);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }

        return builder.Length == 0 ? "default" : builder.ToString();
    }

    private DebugCheckpointData CaptureDebugCheckpoint()
    {
        return new DebugCheckpointData
        {
            Version = DebugCheckpointSchemaVersion,
            LevelId = _currentLevelId,
            State = _state,
            Frame = _debugFrameCounter,
            CameraX = _cameraX,
            CameraY = _cameraY,
            CameraHorizontalFocus = _cameraHorizontalFocus,
            CameraInitialized = _cameraInitialized,
            DebugCameraLocked = _debugCameraLocked,
            DebugCameraLockX = _debugCameraLockPosition.X,
            DebugCameraLockY = _debugCameraLockPosition.Y,
            LastPlayerPose = _lastPlayerPose,
            LastPlayerFacing = _lastPlayerFacing,
            LastPlayerPowerup = _lastPlayerPowerup,
            LastPlayerDucking = _lastPlayerDucking,
            LastPlayerBlinkHidden = _lastPlayerBlinkHidden,
            CourseClear = _courseClear,
            CourseClearGoalCoinAwarded = _courseClearGoalCoinAwarded,
            GamePaused = _gamePaused,
            GameOver = _gameOver,
            QueuedPlayerDeathCause = _queuedPlayerDeathCause,
            QueuedPlayerDeathEvent = _queuedPlayerDeathEvent,
            CourseClearWalkoutFrames = _courseClearWalkoutFrames,
            CourseClearPostWalkPauseFrames = _courseClearPostWalkPauseFrames,
            CourseClearExitWalkFrames = _courseClearExitWalkFrames,
            CourseClearExitTransitionFrames = _courseClearExitTransitionFrames,
            CourseClearInitialWalkoutInputFrame = _courseClearInitialWalkoutInputFrame,
            CourseClearExitTransition = _courseClearExitTransition,
            EntranceMotionFrames = _entranceMotionFrames,
            EntranceMotionAction = _entranceMotionAction,
            EntranceMotionX = _entranceMotionPixelsPerFrame.X,
            EntranceMotionY = _entranceMotionPixelsPerFrame.Y,
            EntranceMotionDelayFrames = _entranceMotionDelayFrames,
            EntranceReleaseHoldFrames = _entranceReleaseHoldFrames,
            DeferredEntranceMotionFrames = _deferredEntranceMotionFrames,
            DeferredEntranceMotionX = _deferredEntranceMotionPixelsPerFrame.X,
            DeferredEntranceMotionY = _deferredEntranceMotionPixelsPerFrame.Y,
            PipeExitPassThroughFrames = _pipeExitPassThroughFrames,
            PipeExitJumpSubYFrames = _pipeExitJumpSubYFrames,
            PipeExitSyntheticGroundSubY = _pipeExitSyntheticGroundSubY,
            PipeEntryHorizontal = _pipeEntryHorizontal,
            PipeEntryInitialFrames = _pipeEntryInitialFrames,
            PipeEntryMotionX = _pipeEntryPixelsPerFrame.X,
            PipeEntryMotionY = _pipeEntryPixelsPerFrame.Y,
            PipeTransitionDelayAfterEntryFrames = _pipeTransitionDelayAfterEntryFrames,
            PostPipeShootoutPMeterFloorFrames = _postPipeShootoutPMeterFloorFrames,
            CoinCount = _coinCount,
            DragonCoinCount = _dragonCoinCount,
            PendingNormalCoinIncrements = _pendingNormalCoinIncrements,
            PendingDragonCoinNormalCoins = _pendingDragonCoinNormalCoins,
            NormalCoinPickupCooldownFrames = _normalCoinPickupCooldownFrames,
            OneUpCount = _oneUpCount,
            Score = _score,
            StompChainCounter = _stompChainCounter,
            StarPowerTimer = _starPowerTimer,
            FireballShootPoseTimer = _fireballShootPoseTimer,
            SpinJumpFireballTimer = _spinJumpFireballTimer,
            Lives = _lives,
            LevelTimerFrames = _levelTimerFrames,
            BlockBreakCount = _blockBreakCount,
            DeathCount = _deathCount,
            PowerupAnimationFrames = _powerupAnimationFrames,
            PowerupSettleFrames = _powerupSettleFrames,
            PendingPowerup = _pendingPowerup,
            SuppressNextPipeCornerLeft = _suppressNextPipeCornerLeft,
            PipeEntryMotionFrames = _pipeEntryMotionFrames,
            PipeTransitionDelayFrames = _pipeTransitionDelayFrames,
            PendingPipeTransitionEntrance = CheckpointForEntrance(_pendingPipeTransitionEntrance),
            PendingEntrancePowerup = _pendingEntrancePowerup,
            InputScriptIndex = _inputScriptIndex,
            InputScriptFrame = _inputScriptFrame,
            InputScriptElapsedFrames = _inputScriptElapsedFrames,
            InputScriptName = _inputScriptName,
            InputScriptDoneLogged = _inputScriptDoneLogged,
            LastFrameInput = _lastFrameInput,
            PlayerWalkingFrame = _playerWalkingFrame,
            PlayerAnimTimer = _playerAnimTimer,
            PlayerHurtCooldown = _playerHurtCooldown,
            DebugMaxPlayerX = _debugMaxPlayerX,
            AutoplayFrame = _autoplayFrame,
            AutoplayLastPlayerX = _autoplayLastPlayerX,
            AutoplayStuckFrames = _autoplayStuckFrames,
            AutoplayJumpHeld = _autoplayJumpHeld,
            LastActorEvent = _lastActorEvent,
            LastActorContact = _lastActorContact,
            PlacedTiles = _placedTiles
                .Select(tile => new TileCheckpoint { X = tile.X, Y = tile.Y, Map16 = tile.Map16, Source = tile.Source })
                .ToList(),
            Actors = _spriteActors.Select(CheckpointForActor).ToList(),
            Fireballs = _playerFireballs.Select(CheckpointForFireball).ToList(),
            GoalTapes = _goalTapes.Select(tape => new GoalTapeCheckpoint { Y = tape.Y, Timer = tape.Timer, Direction = tape.Direction }).ToList(),
            CoinPickupCollected = _coinPickups.Select(pickup => pickup.Collected).ToList(),
        };
    }

    private void ApplyDebugCheckpoint(DebugCheckpointData checkpoint)
    {
        var levelId = string.IsNullOrWhiteSpace(checkpoint.LevelId) ? _currentLevelId : checkpoint.LevelId;
        DebugEnterLevel(levelId);
        ApplyPlacedTilesCheckpoint(checkpoint.PlacedTiles);
        ApplyCoinPickupCheckpoint(checkpoint.CoinPickupCollected);
        RestoreSpriteActors(checkpoint.Actors);
        RestorePlayerFireballs(checkpoint.Fireballs);
        RestoreGoalTapes(checkpoint.GoalTapes);

        _currentLevelId = levelId;
        _state = checkpoint.State;
        _debugFrameCounter = checkpoint.Frame;
        _cameraX = checkpoint.CameraX;
        _cameraY = checkpoint.CameraY;
        _cameraHorizontalFocus = checkpoint.CameraHorizontalFocus;
        _cameraInitialized = checkpoint.CameraInitialized;
        _debugCameraLocked = checkpoint.DebugCameraLocked;
        _debugCameraLockPosition = new Vector2(checkpoint.DebugCameraLockX, checkpoint.DebugCameraLockY);
        _lastPlayerPose = checkpoint.LastPlayerPose;
        _lastPlayerFacing = checkpoint.LastPlayerFacing;
        _lastPlayerPowerup = checkpoint.LastPlayerPowerup;
        _lastPlayerDucking = checkpoint.LastPlayerDucking;
        _lastPlayerBlinkHidden = checkpoint.LastPlayerBlinkHidden;
        _courseClear = checkpoint.CourseClear;
        _courseClearGoalCoinAwarded = checkpoint.CourseClearGoalCoinAwarded;
        _gamePaused = checkpoint.GamePaused;
        _gameOver = checkpoint.GameOver;
        _queuedPlayerDeathCause = checkpoint.QueuedPlayerDeathCause;
        _queuedPlayerDeathEvent = checkpoint.QueuedPlayerDeathEvent;
        _courseClearWalkoutFrames = checkpoint.CourseClearWalkoutFrames;
        _courseClearPostWalkPauseFrames = checkpoint.CourseClearPostWalkPauseFrames;
        _courseClearExitWalkFrames = checkpoint.CourseClearExitWalkFrames;
        _courseClearExitTransitionFrames = checkpoint.CourseClearExitTransitionFrames;
        _courseClearInitialWalkoutInputFrame = checkpoint.CourseClearInitialWalkoutInputFrame;
        _courseClearExitTransition = checkpoint.CourseClearExitTransition;
        _entranceMotionFrames = checkpoint.EntranceMotionFrames;
        _entranceMotionAction = checkpoint.EntranceMotionAction;
        _entranceMotionPixelsPerFrame = new Vector2(checkpoint.EntranceMotionX, checkpoint.EntranceMotionY);
        _entranceMotionDelayFrames = checkpoint.EntranceMotionDelayFrames;
        _entranceReleaseHoldFrames = checkpoint.EntranceReleaseHoldFrames;
        _deferredEntranceMotionFrames = checkpoint.DeferredEntranceMotionFrames;
        _deferredEntranceMotionPixelsPerFrame = new Vector2(checkpoint.DeferredEntranceMotionX, checkpoint.DeferredEntranceMotionY);
        _pipeExitPassThroughFrames = checkpoint.PipeExitPassThroughFrames;
        _pipeExitJumpSubYFrames = checkpoint.PipeExitJumpSubYFrames;
        _pipeExitSyntheticGroundSubY = checkpoint.PipeExitSyntheticGroundSubY;
        _pipeEntryHorizontal = checkpoint.PipeEntryHorizontal;
        _pipeEntryInitialFrames = checkpoint.PipeEntryInitialFrames;
        _pipeEntryPixelsPerFrame = new Vector2(checkpoint.PipeEntryMotionX, checkpoint.PipeEntryMotionY);
        _pipeTransitionDelayAfterEntryFrames = checkpoint.PipeTransitionDelayAfterEntryFrames;
        _postPipeShootoutPMeterFloorFrames = checkpoint.PostPipeShootoutPMeterFloorFrames;
        _coinCount = checkpoint.CoinCount;
        _dragonCoinCount = checkpoint.DragonCoinCount;
        _pendingNormalCoinIncrements = checkpoint.PendingNormalCoinIncrements;
        _pendingDragonCoinNormalCoins = checkpoint.PendingDragonCoinNormalCoins;
        _normalCoinPickupCooldownFrames = checkpoint.NormalCoinPickupCooldownFrames;
        _oneUpCount = checkpoint.OneUpCount;
        _score = checkpoint.Score;
        _stompChainCounter = checkpoint.StompChainCounter;
        _starPowerTimer = checkpoint.StarPowerTimer;
        _fireballShootPoseTimer = checkpoint.FireballShootPoseTimer;
        _spinJumpFireballTimer = checkpoint.SpinJumpFireballTimer;
        _lives = checkpoint.Lives;
        _levelTimerFrames = checkpoint.LevelTimerFrames;
        _blockBreakCount = checkpoint.BlockBreakCount;
        _deathCount = checkpoint.DeathCount;
        _powerupAnimationFrames = checkpoint.PowerupAnimationFrames;
        _powerupSettleFrames = checkpoint.PowerupSettleFrames;
        _pendingPowerup = checkpoint.PendingPowerup;
        _suppressNextPipeCornerLeft = checkpoint.SuppressNextPipeCornerLeft;
        _pipeEntryMotionFrames = checkpoint.PipeEntryMotionFrames;
        _pipeTransitionDelayFrames = checkpoint.PipeTransitionDelayFrames;
        _pendingPipeTransitionEntrance = EntranceFromCheckpoint(checkpoint.PendingPipeTransitionEntrance);
        _pendingEntrancePowerup = checkpoint.PendingEntrancePowerup;
        _inputScriptIndex = checkpoint.InputScriptIndex;
        _inputScriptFrame = checkpoint.InputScriptFrame;
        _inputScriptElapsedFrames = checkpoint.InputScriptElapsedFrames;
        _inputScriptName = checkpoint.InputScriptName;
        _inputScriptDoneLogged = checkpoint.InputScriptDoneLogged;
        _lastFrameInput = checkpoint.LastFrameInput;
        _playerWalkingFrame = checkpoint.PlayerWalkingFrame;
        _playerAnimTimer = checkpoint.PlayerAnimTimer;
        _playerHurtCooldown = checkpoint.PlayerHurtCooldown;
        _debugMaxPlayerX = checkpoint.DebugMaxPlayerX;
        _autoplayFrame = checkpoint.AutoplayFrame;
        _autoplayLastPlayerX = checkpoint.AutoplayLastPlayerX;
        _autoplayStuckFrames = checkpoint.AutoplayStuckFrames;
        _autoplayJumpHeld = checkpoint.AutoplayJumpHeld;
        _lastActorEvent = checkpoint.LastActorEvent;
        _lastActorContact = checkpoint.LastActorContact;

        RefreshPlayerDebugPresentation(forceGraphic: true);
        UpdateCamera();
        UpdateHud();
        UpdateDebugGizmos();
    }

    private static LevelEntranceCheckpoint? CheckpointForEntrance(LevelEntrance? entrance)
    {
        return entrance is { } value
            ? new LevelEntranceCheckpoint
            {
                LevelId = value.LevelId,
                X = value.Position.X,
                Y = value.Position.Y,
                EntranceSettings = value.EntranceSettings,
                Secondary = value.Secondary,
                SourceId = value.SourceId,
            }
            : null;
    }

    private static LevelEntrance? EntranceFromCheckpoint(LevelEntranceCheckpoint? checkpoint)
    {
        return checkpoint == null
            ? null
            : new LevelEntrance(
                checkpoint.LevelId,
                new Vector2(checkpoint.X, checkpoint.Y),
                checkpoint.EntranceSettings,
                checkpoint.Secondary,
                checkpoint.SourceId);
    }

    private static ActorCheckpoint CheckpointForActor(RuntimeSpriteActor actor)
    {
        return new ActorCheckpoint
        {
            SpriteId = actor.SpriteId,
            X = actor.X,
            Y = actor.Y,
            PreviousX = actor.PreviousX,
            PreviousY = actor.PreviousY,
            HomeY = actor.HomeY,
            XSpeed = actor.XSpeed,
            YSpeed = actor.YSpeed,
            MotionFrame = actor.MotionFrame,
            InteractionCooldownFrames = actor.InteractionCooldownFrames,
            MotionFreezeFrames = actor.MotionFreezeFrames,
            SolidSideCooldownFrames = actor.SolidSideCooldownFrames,
            Used = actor.Used,
            Alive = actor.Alive,
            Active = actor.Active,
            AlwaysActive = actor.AlwaysActive,
            OnGround = actor.OnGround,
            WakeDelayFrames = actor.WakeDelayFrames,
            WakeScreen = actor.WakeScreen,
            ContentIndex = actor.ContentIndex,
            SpawnOffset = actor.SpawnOffset,
            State = actor.State,
        };
    }

    private static FireballCheckpoint CheckpointForFireball(PlayerFireball fireball)
    {
        return new FireballCheckpoint
        {
            X = fireball.X,
            Y = fireball.Y,
            XSpeed = fireball.XSpeed,
            YSpeed = fireball.YSpeed,
            SubY = fireball.SubY,
            MotionFrame = fireball.MotionFrame,
            BounceCount = fireball.BounceCount,
            Alive = fireball.Alive,
        };
    }

    private void ApplyPlacedTilesCheckpoint(List<TileCheckpoint> tiles)
    {
        if (tiles.Count == 0)
        {
            return;
        }

        _placedTiles.Clear();
        _map16TilesByCoord.Clear();
        foreach (var tile in tiles)
        {
            var placed = new PlacedMap16Tile(tile.X, tile.Y, tile.Map16, tile.Source);
            _placedTiles.Add(placed);
            if (placed.Map16 >= 0)
            {
                _map16TilesByCoord[(placed.X, placed.Y)] = placed;
            }
        }

        if (_map16Texture != null && _map16Layer != null)
        {
            _map16Layer.Configure(_map16Texture, _placedTiles.Where(tile => tile.Map16 >= 0));
        }
        AddGeneratedCollision(DebugOverlays);
        RebuildPipeEntrances();
    }

    private void ApplyCoinPickupCheckpoint(List<bool> collected)
    {
        for (var i = 0; i < _coinPickups.Count && i < collected.Count; i++)
        {
            var pickup = _coinPickups[i];
            pickup.Collected = collected[i];
            if (!pickup.Collected)
            {
                continue;
            }

            foreach (var tile in pickup.Tiles)
            {
                _map16Layer?.HideTile(tile.X, tile.Y);
                _map16TilesByCoord.Remove((tile.X, tile.Y));
            }
        }
    }

    private void RestoreSpriteActors(List<ActorCheckpoint> actors)
    {
        foreach (var actor in _spriteActors)
        {
            actor.Node.QueueFree();
        }
        _spriteActors.Clear();

        foreach (var checkpoint in actors)
        {
            var actor = CreateRuntimeSpriteActorFromCheckpoint(checkpoint);
            _spriteActors.Add(actor);
            AddWorldChild(actor.Node);
            ApplySpriteActorVisualVisibility(actor);
        }
    }

    private RuntimeSpriteActor CreateRuntimeSpriteActorFromCheckpoint(ActorCheckpoint checkpoint)
    {
        var behavior = SpriteActorBehaviorForCheckpoint(checkpoint);
        var color = SpriteActorColor(checkpoint.SpriteId);
        var node = new Node2D
        {
            Name = $"CheckpointSprite_{checkpoint.SpriteId:X2}_{checkpoint.SpawnOffset:X}",
            Position = new Vector2(checkpoint.X, checkpoint.Y),
            ZIndex = checkpoint.SpawnOffset < 0 ? 7 : 6,
            Visible = _debugActorVisualsEnabled,
        };
        var visuals = AddSpriteActorVisuals(node, checkpoint.SpriteId, checkpoint.State, checkpoint.Used);
        var body = new ColorRect
        {
            Name = visuals.Count > 0 ? "ActorCollisionDebug" : "ActorPlaceholderDebug",
            Color = DebugOverlays ? new Color(color.R, color.G, color.B, 0.20f) : color,
            Position = behavior.Hitbox.Position,
            Size = behavior.Hitbox.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        node.AddChild(body);
        if (DebugOverlays)
        {
            AddRectOutline(node, behavior.Hitbox, new Color(1.0f, 0.10f, 0.10f, 0.88f), 1.0f, 60);
        }

        return new RuntimeSpriteActor
        {
            Node = node,
            Body = body,
            SpriteId = checkpoint.SpriteId,
            X = checkpoint.X,
            Y = checkpoint.Y,
            PreviousX = checkpoint.PreviousX,
            PreviousY = checkpoint.PreviousY,
            HomeY = checkpoint.HomeY,
            XSpeed = checkpoint.XSpeed,
            YSpeed = checkpoint.YSpeed,
            MotionFrame = checkpoint.MotionFrame,
            InteractionCooldownFrames = checkpoint.InteractionCooldownFrames,
            MotionFreezeFrames = checkpoint.MotionFreezeFrames,
            SolidSideCooldownFrames = checkpoint.SolidSideCooldownFrames,
            Used = checkpoint.Used,
            Alive = checkpoint.Alive,
            Active = checkpoint.Active,
            AlwaysActive = checkpoint.AlwaysActive,
            OnGround = checkpoint.OnGround,
            WakeDelayFrames = checkpoint.WakeDelayFrames,
            WakeScreen = checkpoint.WakeScreen,
            ContentIndex = checkpoint.ContentIndex,
            SpawnOffset = checkpoint.SpawnOffset,
            Visuals = visuals,
            Behavior = behavior,
            State = checkpoint.State,
        };
    }

    private static SpriteActorBehavior SpriteActorBehaviorForCheckpoint(ActorCheckpoint checkpoint)
    {
        var behavior = SpriteActorBehaviorFor(checkpoint.SpriteId);
        if (IsCarryableShellSprite(checkpoint.SpriteId) && checkpoint.State == NativeCarriedShellState)
        {
            return new SpriteActorBehavior(
                behavior.Hitbox,
                CanInteract: false,
                Stompable: false,
                TerrainCollision: false,
                Gravity: false,
                InitialXSpeed: 0.0f,
                TerrainHitbox: behavior.TerrainHitbox);
        }

        if (checkpoint.SpriteId == 0xAB && checkpoint.State == 1)
        {
            return SquishedRexBehavior(checkpoint.XSpeed, checkpoint.SpawnOffset == 0x46);
        }

        if (checkpoint.SpriteId == 0xAB && checkpoint.State == 2)
        {
            var squished = SquishedRexBehavior(checkpoint.XSpeed, checkpoint.SpawnOffset == 0x46);
            return new SpriteActorBehavior(
                squished.Hitbox,
                CanInteract: false,
                Stompable: false,
                TerrainCollision: false,
                Gravity: false,
                InitialXSpeed: 0.0f,
                TerrainHitbox: squished.TerrainHitbox);
        }

        return behavior;
    }

    private void RestorePlayerFireballs(List<FireballCheckpoint> fireballs)
    {
        foreach (var fireball in _playerFireballs)
        {
            fireball.Node.QueueFree();
        }
        _playerFireballs.Clear();

        foreach (var checkpoint in fireballs)
        {
            var fireball = CreatePlayerFireball(checkpoint.X, checkpoint.Y, checkpoint.XSpeed);
            fireball.YSpeed = checkpoint.YSpeed;
            fireball.SubY = checkpoint.SubY;
            fireball.MotionFrame = checkpoint.MotionFrame;
            fireball.BounceCount = checkpoint.BounceCount;
            fireball.Alive = checkpoint.Alive;
            _playerFireballs.Add(fireball);
            _worldRoot?.AddChild(fireball.Node);
        }
    }

    private void RestoreGoalTapes(List<GoalTapeCheckpoint> goalTapes)
    {
        for (var i = 0; i < _goalTapes.Count && i < goalTapes.Count; i++)
        {
            _goalTapes[i].Y = goalTapes[i].Y;
            _goalTapes[i].Timer = goalTapes[i].Timer;
            _goalTapes[i].Direction = goalTapes[i].Direction;
            _goalTapes[i].Node.Position = new Vector2(_goalTapes[i].X, _goalTapes[i].Y);
        }
    }

    private static string FormatCheckpointLine(string action, string slot, DebugCheckpointData checkpoint, string suffix)
    {
        return
            $"smw-debug-checkpoint: action={action} slot={slot} level={checkpoint.LevelId} saved_frame={checkpoint.Frame} " +
            $"frame={checkpoint.Frame} x={checkpoint.State.XFloat:0.00} y={checkpoint.State.YFloat:0.00} " +
            $"xs={checkpoint.State.XSpeed} ys={checkpoint.State.YSpeed} star={checkpoint.StarPowerTimer:X2} " +
            $"cam={checkpoint.CameraX:0.00},{checkpoint.CameraY:0.00} actors={checkpoint.Actors.Count} " +
            $"fireballs={checkpoint.Fireballs.Count} tiles={checkpoint.PlacedTiles.Count} score={checkpoint.Score} " +
            $"coins={checkpoint.CoinCount} dragon={checkpoint.DragonCoinCount} {suffix}";
    }

    private void RefreshPlayerDebugPresentation(bool forceGraphic)
    {
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }

        UpdatePlayerGraphic(force: forceGraphic);
        UpdateHud();
        UpdateDebugGizmos();
    }

    private string PrintDebugPerformance(string tag)
    {
        var audioStatus = _audio?.DebugStatus() ?? "loaded=0 samples=0 voices=0 music=0 music_frame=0 events=0 loop_frames=0 frames_available=-1";
        var perf =
            $"smw-debug-perf: tag={tag} frame={_debugFrameCounter} fps={Engine.GetFramesPerSecond():0.00} " +
            $"process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0:0.000} " +
            $"physics_ms={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0:0.000} " +
            $"draw_calls={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0} " +
            $"render_objects={Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame):0} " +
            $"physics_tps={Engine.PhysicsTicksPerSecond} paused={(_debugPaused ? 1 : 0)} queued={_debugStepFrames} " +
            $"autoplay={AutoplayModeName(_autoplayMode)} auto_frame={_autoplayFrame} nodes={CountNodes(GetTree().Root)} actors={_spriteActors.Count} fireballs={_playerFireballs.Count} actors_on={(_debugActorsEnabled ? 1 : 0)} actor_visuals={(_debugActorVisualsEnabled ? 1 : 0)} " +
            $"tiles={_placedTiles.Count} solids={_solids.Count} slopes={_slopes.Count} player_sprites={_playerTileSprites.Count} " +
            $"audio_enabled={(AudioEnabled ? 1 : 0)} audio_process={(_audio?.ProcessMode.ToString() ?? "none")} audio_{audioStatus}";
        GD.Print(perf);
        return perf;
    }

    private string PrintDebugPhysics(string tag)
    {
        var normalJumpIndex = SmwPhysics.JumpSpeedIndexFor(_state.XSpeed, spin: false);
        var spinJumpIndex = SmwPhysics.JumpSpeedIndexFor(_state.XSpeed, spin: true);
        var line =
            $"smw-debug-physics: tag={tag} frame={_debugFrameCounter} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} sub={_state.SubX:X2},{_state.SubY:X2} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} subspd={_state.SubXSpeed:X2},{_state.SubYSpeed:X2} " +
            $"p={_state.PMeter:X2} pow={_state.Powerup} h={SmwPhysics.PlayerHeightFor(_state)} g={(_state.OnGround ? 1 : 0)} " +
            $"duck={(_state.Ducking ? 1 : 0)} sj={(_state.SpinJump ? 1 : 0)} rt={(_state.RunningTakeoff ? 1 : 0)} jf={_state.JumpHeldFrames} cf={_state.CapeFloatFrames} air={_state.InAirState:X2} face={_state.Facing} " +
            $"slope={_state.SlopeKind} slope_player={_state.SlopePlayer} slope_type={_state.SlopeType} " +
            $"jump_idx={normalJumpIndex} jump_y={SmwPhysics.JumpYSpeedFor(_state.XSpeed, spin: false)} " +
            $"spin_jump_idx={spinJumpIndex} spin_jump_y={SmwPhysics.JumpYSpeedFor(_state.XSpeed, spin: true)}";
        GD.Print(line);
        return line;
    }

    private string BuildDebugCamera(string tag)
    {
        var maxCameraX = MathF.Max(0.0f, GetLevelPixelRight() - LogicalViewportWidth);
        var maxCameraY = MathF.Max(0.0f, GetLevelPixelBottom() - LogicalViewportHeight);
        return
            $"smw-debug-camera: tag={tag} frame={_debugFrameCounter} " +
            $"x={_cameraX:0.00} y={_cameraY:0.00} locked={(_debugCameraLocked ? 1 : 0)} " +
            $"lock={_debugCameraLockPosition.X:0.00},{_debugCameraLockPosition.Y:0.00} " +
            $"player_screen={_state.XFloat - _cameraX:0.00},{_state.YFloat - _cameraY:0.00} " +
            $"bounds={maxCameraX:0.00},{maxCameraY:0.00}";
    }

    private static int CountNodes(Node node)
    {
        var count = 1;
        foreach (var child in node.GetChildren())
        {
            count += CountNodes(child);
        }

        return count;
    }

    private string PrintDebugTile(string[] parts)
    {
        string tile;
        if (parts.Length >= 3)
        {
            tile = DescribeWorldTile(ParseFloat(parts[1]), ParseFloat(parts[2]));
        }
        else
        {
            tile = DescribeFootTile();
        }

        var line = $"smw-debug-tile: {tile}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugPlayerSensors(string tag)
    {
        var line = BuildDebugPlayerSensors(tag);
        GD.Print(line);
        return line;
    }

    private string BuildDebugPlayerSensors(string tag)
    {
        var playerRect = _physics.PlayerRect(_state);
        var height = playerRect.Size.Y;
        var left = playerRect.Position.X;
        var right = playerRect.Position.X + playerRect.Size.X;
        var top = playerRect.Position.Y;
        var bottom = top + height;
        var centerX = left + SmwPhysics.PlayerWidth * 0.5f;
        var middleY = top + height * 0.5f;
        var footLeftX = left + 2.0f;
        var footRightX = right - 2.0f;
        var footY = bottom + 1.0f;

        var slope = "none";
        if (SmwPhysics.TryResolveFloorSlope(
            centerX,
            bottom,
            _state.YSpeed,
            _slopes,
            6.0f,
            16.0f,
            out var slopeY))
        {
            slope = $"{slopeY:0.00}";
        }

        var line =
            $"smw-debug-sensors: tag={tag} frame={_debugFrameCounter} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} h={height} xs={_state.XSpeed} ys={_state.YSpeed} " +
            $"head={DescribeSensorPoint(centerX, top + 1.0f)} " +
            $"side_l={DescribeSensorPoint(left, middleY)} side_r={DescribeSensorPoint(right - 1.0f, middleY)} " +
            $"foot_l={DescribeSensorPoint(footLeftX, footY)} foot_c={DescribeSensorPoint(centerX, footY)} foot_r={DescribeSensorPoint(footRightX, footY)} " +
            $"floor_slope={slope} {DescribeSolidsNear(playerRect.GetCenter(), 64.0f)}";
        return line;
    }

    private string DescribeSensorPoint(float x, float y)
    {
        var cell = (WorldToTileX(x), WorldToTileY(y));
        var pipeRole =
            _diagonalPipeFloorCells.Contains(cell) ? "floor" :
            _diagonalPipeBodyCells.Contains(cell) ? "body" :
            _diagonalPipeCeilingCells.Contains(cell) ? "ceiling" :
            "none";
        return $"{x:0.00},{y:0.00}:{DescribeTileAt(cell.Item1, cell.Item2)}:pipe={pipeRole}";
    }

    private string PrintDebugCollision(string[] parts)
    {
        Vector2 point;
        float radius;
        if (parts.Length >= 3)
        {
            point = new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2]));
            radius = parts.Length >= 4 ? ParseFloat(parts[3]) : 32.0f;
        }
        else
        {
            point = _physics.PlayerRect(_state).GetCenter();
            radius = parts.Length >= 2 ? ParseFloat(parts[1]) : 32.0f;
        }

        var line = $"smw-debug-collision: point={point.X:0.00},{point.Y:0.00} radius={radius:0.00} " +
            $"{DescribeSolidsNear(point, radius)} {DescribeSlopesNear(point, radius)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugSlopeProbe(string[] parts)
    {
        var tag = "manual";
        var x = _state.XFloat;
        var y = SmwPhysics.PlayerCollisionTop(_state);
        var height = (float)SmwPhysics.PlayerCollisionHeightFor(_state);
        var ySpeed = (float)_state.YSpeed;

        for (var i = 1; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
            {
                tag = parts[i][4..];
            }
        }

        if (parts.Length >= 3 &&
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedX) &&
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedY))
        {
            x = parsedX;
            y = parsedY;
            if (parts.Length >= 4 &&
                float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedHeight))
            {
                height = parsedHeight;
            }
            if (parts.Length >= 5 &&
                float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedYSpeed))
            {
                ySpeed = parsedYSpeed;
            }
        }

        var line = BuildDebugSlopeProbe(tag, x, y, height, ySpeed);
        GD.Print(line);
        return line;
    }

    private string BuildDebugSlopeProbe(string tag, float x, float y, float height, float ySpeed)
    {
        var left = x;
        var right = x + SmwPhysics.PlayerWidth;
        var top = y;
        var bottom = y + height;
        var leftProbe = left + 2.0f;
        var centerProbe = left + SmwPhysics.PlayerWidth * 0.5f;
        var rightProbe = right - 2.0f;
        var line =
            $"smw-debug-slope-probe: tag={tag} frame={_debugFrameCounter} " +
            $"x={x:0.00} y={y:0.00} h={height:0.00} ys={ySpeed:0.00} top={top:0.00} bottom={bottom:0.00} " +
            $"{DescribeSlopeProbe("left", leftProbe, top, bottom, ySpeed)} " +
            $"{DescribeSlopeProbe("center", centerProbe, top, bottom, ySpeed)} " +
            $"{DescribeSlopeProbe("right", rightProbe, top, bottom, ySpeed)}";
        return line;
    }

    private string DescribeSlopeProbe(string label, float probeX, float top, float bottom, float ySpeed)
    {
        var hit = SmwPhysics.TryResolveFloorSlope(
            probeX,
            bottom,
            (int)MathF.Round(ySpeed),
            _slopes,
            aboveTolerance: 6.0f,
            belowTolerance: 16.0f,
            out var hitY);
        var fromAbove = SmwPhysics.TryResolveFloorSlopeFromAbove(
            probeX,
            top,
            bottom,
            bottom,
            (int)MathF.Round(ySpeed),
            _slopes,
            aboveTolerance: 6.0f,
            belowTolerance: 16.0f,
            out var fromAboveY);
        return
            $"probe={label}:{probeX:0.00}:tile={DescribeSensorPoint(probeX, bottom)}:" +
            $"hit={(hit ? 1 : 0)}:{(hit ? hitY.ToString("0.00", CultureInfo.InvariantCulture) : "--")}:" +
            $"from_above={(fromAbove ? 1 : 0)}:{(fromAbove ? fromAboveY.ToString("0.00", CultureInfo.InvariantCulture) : "--")}:" +
            $"{DescribeSlopeCandidatesForProbe(probeX, bottom)}";
    }

    private string DescribeSlopeCandidatesForProbe(float probeX, float bottom)
    {
        var candidates = _slopes
            .Select((slope, index) => new
            {
                Slope = slope,
                Index = index,
                SurfaceY = SurfaceYAtProbe(slope, probeX),
            })
            .Where(item => item.SurfaceY != null)
            .OrderBy(item => MathF.Abs(bottom - item.SurfaceY!.Value))
            .Take(3)
            .Select(item =>
            {
                var surfaceY = item.SurfaceY!.Value;
                return
                    $"#{item.Index}:y={surfaceY:0.00}:d={bottom - surfaceY:0.00}:ceil={(item.Slope.Ceiling ? 1 : 0)}:" +
                    $"kind={item.Slope.NativeSlopeKind}:snap={item.Slope.SnapDistance:0.00}";
            });
        var description = string.Join(",", candidates);
        return $"candidates={(string.IsNullOrEmpty(description) ? "none" : description)}";
    }

    private static float? SurfaceYAtProbe(SmwPhysics.SlopeSurface slope, float probeX)
    {
        return SmwPhysics.TrySurfaceYAt(slope, probeX, out var surfaceY) ? surfaceY : null;
    }

    private string PrintDebugPipeCells(string[] parts)
    {
        Vector2 point;
        float radius;
        if (parts.Length >= 3)
        {
            point = new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2]));
            radius = parts.Length >= 4 ? ParseFloat(parts[3]) : 48.0f;
        }
        else
        {
            point = _physics.PlayerRect(_state).GetCenter();
            radius = parts.Length >= 2 ? ParseFloat(parts[1]) : 48.0f;
        }

        var line =
            $"smw-debug-pipe: point={point.X:0.00},{point.Y:0.00} radius={radius:0.00} " +
            $"floor={DescribePipeCellsNear(_diagonalPipeFloorCells, point, radius)} " +
            $"body={DescribePipeCellsNear(_diagonalPipeBodyCells, point, radius)} " +
            $"ceiling={DescribePipeCellsNear(_diagonalPipeCeilingCells, point, radius)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugPipeEntrances()
    {
        var entries = _pipeEntrances
            .Select((entrance, index) =>
            {
                var direction = entrance.Horizontal ? "side" : "down";
                return
                    $"#{index}:screen={entrance.Screen:X2}:kind={entrance.Kind}:dir={direction}:" +
                    $"rect={entrance.Rect.Position.X:0.00},{entrance.Rect.Position.Y:0.00},{entrance.Rect.Size.X:0.00},{entrance.Rect.Size.Y:0.00}:" +
                    $"source={entrance.SourceX},{entrance.SourceY}:{entrance.Source}";
            });
        var line = $"smw-debug-pipe-entrances: count={_pipeEntrances.Count} {string.Join(" ", entries)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugGoalTapes()
    {
        var entries = _goalTapes
            .Select((tape, index) =>
                $"#{index}:x={tape.X:0.00}:y={tape.Y:0.00}:speed={tape.YSpeed:0.00}:timer={tape.Timer}:" +
                $"gate={tape.GateRect.Position.X:0.00},{tape.GateRect.Position.Y:0.00},{tape.GateRect.Size.X:0.00},{tape.GateRect.Size.Y:0.00}:" +
                $"tape={tape.TapeRect.Position.X:0.00},{tape.TapeRect.Position.Y:0.00},{tape.TapeRect.Size.X:0.00},{tape.TapeRect.Size.Y:0.00}");
        var line = $"smw-debug-goal-tapes: count={_goalTapes.Count} {string.Join(" ", entries)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugStatusHud()
    {
        var line =
            $"smw-debug-status: score={_score} lives={_lives} coins={_coinCount} dragon={_dragonCoinCount} " +
            $"oneups={_oneUpCount} time={LevelTimerSecondsRemaining()} clear={(_courseClear ? 1 : 0)} gamepause={(_gamePaused ? 1 : 0)} gameover={(_gameOver ? 1 : 0)} text=\"{BuildStatusHudText()}\"";
        GD.Print(line);
        return line;
    }

    private string PrintDebugLives(string tag)
    {
        var line =
            $"smw-debug-lives: tag={tag} lives={_lives} starting={StartingLives} " +
            $"gameover={(_gameOver ? 1 : 0)} deaths={_deathCount}";
        GD.Print(line);
        return line;
    }

    private string ExecuteDebugTimerCommand(string[] parts)
    {
        if (parts.Length == 1 ||
            string.Equals(parts[1], "status", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[1], "get", StringComparison.OrdinalIgnoreCase))
        {
            return PrintDebugTimer("status");
        }

        if (string.Equals(parts[1], "frames", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[1], "frame", StringComparison.OrdinalIgnoreCase))
        {
            RequirePartCount(parts, 3);
            return DebugSetLevelTimerFrames(ParseHexOrDecimalDebug(parts[2]), "set");
        }

        if (string.Equals(parts[1], "seconds", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[1], "second", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[1], "sec", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[1], "s", StringComparison.OrdinalIgnoreCase))
        {
            RequirePartCount(parts, 3);
            return DebugSetLevelTimerSeconds(ParseHexOrDecimalDebug(parts[2]), "set");
        }

        return DebugSetLevelTimerSeconds(ParseHexOrDecimalDebug(parts[1]), "set");
    }

    private string DebugSetLevelTimerSeconds(int seconds, string tag)
    {
        var clampedSeconds = Math.Clamp(seconds, 0, 999);
        _levelTimerFrames = clampedSeconds * NativeFramesPerSecond;
        return PrintDebugTimer(tag);
    }

    private string DebugSetLevelTimerFrames(int frames, string tag)
    {
        _levelTimerFrames = Math.Clamp(frames, 0, 999 * NativeFramesPerSecond);
        return PrintDebugTimer(tag);
    }

    private string PrintDebugTimer(string tag)
    {
        var line =
            $"smw-debug-timer: tag={tag} frames={_levelTimerFrames} seconds={LevelTimerSecondsRemaining()} " +
            $"clear={(_courseClear ? 1 : 0)} gameover={(_gameOver ? 1 : 0)} lives={_lives} deaths={_deathCount}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugActorsNear(string[] parts)
    {
        var radius = parts.Length >= 2 ? ParseFloat(parts[1]) : 96.0f;
        var line = $"smw-debug-actors-near: radius={radius:0.00} {DescribeActorsNear(radius)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugActorOam(string[] parts)
    {
        var radius = parts.Length >= 2 ? ParseFloat(parts[1]) : 128.0f;
        var line = $"smw-debug-actor-oam: radius={radius:0.00} {DescribeActorOamNear(radius)}";
        GD.Print(line);
        return line;
    }

    private string PrintDebugPickupsNear(string[] parts)
    {
        var radius = parts.Length >= 2 ? ParseFloat(parts[1]) : 96.0f;
        var line = $"smw-debug-pickups-near: radius={radius:0.00} {DescribePickupsNear(radius)}";
        GD.Print(line);
        return line;
    }

    private string ExecuteDebugCameraCommand(string[] parts)
    {
        if (parts.Length == 1 || parts[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var status = BuildDebugCamera("status");
            GD.Print(status);
            return status;
        }

        var command = parts[1].ToLowerInvariant();
        switch (command)
        {
            case "lock":
            case "freeze":
                if (parts.Length >= 4)
                {
                    DebugSetCameraLock(true, new Vector2(ParseFloat(parts[2]), ParseFloat(parts[3])));
                }
                else
                {
                    DebugSetCameraLock(true);
                }
                return BuildDebugCamera("lock");
            case "set":
            case "move":
                RequirePartCount(parts, 4);
                DebugSetCameraLock(true, new Vector2(ParseFloat(parts[2]), ParseFloat(parts[3])));
                return BuildDebugCamera("set");
            case "unlock":
            case "follow":
            case "release":
                DebugSetCameraLock(false);
                return BuildDebugCamera("unlock");
            default:
                if (parts.Length >= 3)
                {
                    DebugSetCameraLock(true, new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                    return BuildDebugCamera("set");
                }
                throw new FormatException($"unknown camera command '{parts[1]}'");
        }
    }

    private string ExecuteDebugAudioCommand(string[] parts)
    {
        if (parts.Length < 2 || parts[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var status = BuildDebugAudioState("status");
            GD.Print(status);
            return status;
        }

        var command = parts[1].ToLowerInvariant();
        if (command is "sample" or "brr" or "sfx")
        {
            RequirePartCount(parts, 3);
            EnsureDebugAudioEnabled();
            var sampleId = ParseHexOrDecimalDebug(parts[2]);
            var sampleStatus = _audio?.PlaySampleProbe(sampleId) ?? $"sample={sampleId:X2} available=0 samples=0";
            var line = $"{BuildDebugAudioState("sample")} {sampleStatus}";
            GD.Print(line);
            return line;
        }
        if (command is "jump")
        {
            EnsureDebugAudioEnabled();
            _audio?.PlayJump();
            var line = $"{BuildDebugAudioState("jump")} command=port1_jump";
            GD.Print(line);
            return line;
        }
        if (command is "spin" or "two-note" or "twonote")
        {
            EnsureDebugAudioEnabled();
            _audio?.PlaySpinJump();
            var line = $"{BuildDebugAudioState("spin")} command=port1_two_note";
            GD.Print(line);
            return line;
        }
        if (command is "coin" or "dragon" or "dragon_coin" or "oneup" or "one-up" or "1up" or "stomp" or "block" or "break" or "block_break" or "reward" or "powerup" or "hurt" or "death" or "clear" or "course_clear")
        {
            EnsureDebugAudioEnabled();
            var played = _audio?.PlayNamedSfx(command) ?? false;
            var line = $"{BuildDebugAudioState("sfx")} command={command} played={(played ? 1 : 0)}";
            GD.Print(line);
            return line;
        }
        if (command is "music" or "preview")
        {
            RequirePartCount(parts, 3);
            EnsureDebugAudioEnabled();
            var bank = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(parts[2].ToLowerInvariant());
            _audio?.PlayMusicPreview(bank);
            var line = $"{BuildDebugAudioState("music")} preview={bank}";
            GD.Print(line);
            return line;
        }
        if (command is "stop")
        {
            _audio?.StopMusicPreview();
            var line = $"{BuildDebugAudioState("stop")} command=stop";
            GD.Print(line);
            return line;
        }

        return DebugSetAudioEnabled(ParseDebugBool(parts[1]));
    }

    private void EnsureDebugAudioEnabled()
    {
        if (!AudioEnabled || _audio == null || _audio.ProcessMode == ProcessModeEnum.Disabled)
        {
            DebugSetAudioEnabled(true);
        }
    }

    private string BuildDebugAudioState(string tag)
    {
        var audioStatus = _audio?.DebugStatus() ?? "loaded=0 samples=0 voices=0 music=0 music_frame=0 events=0 loop_frames=0 frames_available=-1";
        return $"smw-debug-audio: tag={tag} enabled={(AudioEnabled ? 1 : 0)} node={(_audio != null ? 1 : 0)} bank={_currentLevelMusicPreview} {audioStatus}";
    }

    private string PrintDebugPlayerOam(string tag)
    {
        var line = BuildDebugPlayerOam(tag);
        GD.Print(line);
        return line;
    }

    private string BuildDebugPlayerOam(string tag)
    {
        if (!HasPlayerOamMetadata())
        {
            return $"smw-debug-player-oam: tag={tag} metadata=0 sprites={_playerTileSprites.Count}";
        }

        var pose = _lastPlayerPose >= 0 ? _lastPlayerPose : 0;
        var nativeFacing = _lastPlayerFacing >= 0 ? _lastPlayerFacing : (_state.Facing == 0 ? 0 : 1);
        var powerup = _state.Powerup;
        var tablePose = pose;
        if (pose < 0x3D)
        {
            tablePose += _playerPowerupTilesetIndex[Math.Clamp(powerup, 0, _playerPowerupTilesetIndex.Count - 1)];
        }

        tablePose = Math.Clamp(tablePose, 0, _playerTilesIndex.Count - 1);
        var xyIndexOffset = _playerXYDispIndexIndex[Math.Clamp(pose, 0, _playerXYDispIndexIndex.Count - 1)] | nativeFacing;
        xyIndexOffset = Math.Clamp(xyIndexOffset, 0, _playerXYDispIndex.Count - 1);
        var dispBase = _playerXYDispIndex[xyIndexOffset] / 2;
        var descriptorBase = _playerTilesIndex[tablePose];
        var headBase = _headTilePointers[tablePose];
        var bodyBase = _bodyTilePointers[tablePose];
        var flipH = (_playerTileXFlip[Math.Clamp(nativeFacing, 0, _playerTileXFlip.Count - 1)] & 0x40) != 0;
        var palette = PlayerPaletteVariantForPowerup(powerup);
        var blinkHidden = _lastPlayerBlinkHidden ? 1 : 0;
        const int normalSizeMask = 0xC8;
        var slotMasks = new[] { 0x80, 0x40, 0x20, 0x10 };

        var builder = new StringBuilder();
        builder.Append(
            $"smw-debug-player-oam: tag={tag} metadata=1 frame={_debugFrameCounter} pose={pose} table_pose={tablePose} " +
            $"powerup={powerup} palette={palette} facing={nativeFacing} flip_h={(flipH ? 1 : 0)} " +
            $"walk_frame={_playerWalkingFrame} anim_timer={_playerAnimTimer} hurt={_playerHurtCooldown} blink_hidden={blinkHidden} render_y={PlayerRenderYOffsetForState(powerup, _state.Ducking)} " +
            $"descriptor_base={descriptorBase} disp_base={dispBase} head_ptr=0x{headBase:X2} body_ptr=0x{bodyBase:X2} sprites={_playerTileSprites.Count}");

        for (var slot = 0; slot < 4; slot++)
        {
            var descriptorIndex = descriptorBase + slot;
            var dispIndex = dispBase + slot;
            var hasDescriptor = descriptorIndex >= 0 && descriptorIndex < _playerTileDescriptors.Count;
            var hasDisp = dispIndex >= 0 && dispIndex < _playerXDisp.Count && dispIndex < _playerYDisp.Count;
            var descriptor = hasDescriptor ? _playerTileDescriptors[descriptorIndex] : -1;
            var tile = -1;
            var resolved = hasDescriptor && TryResolvePlayerDynamicTile(descriptor, headBase, bodyBase, out tile);
            var large = (normalSizeMask & slotMasks[slot]) != 0;
            var visible = slot < _playerTileSprites.Count && _playerTileSprites[slot].Visible;
            var sprite = slot < _playerTileSprites.Count ? _playerTileSprites[slot] : null;
            var rect = sprite?.RegionRect ?? default;
            var pos = sprite?.Position ?? default;
            var alpha = sprite?.Modulate.A ?? 0.0f;

            builder.Append(
                $" slot{slot}:desc_idx={descriptorIndex}:disp_idx={dispIndex}:desc={(descriptor >= 0 ? descriptor.ToString("X2", CultureInfo.InvariantCulture) : "--")}:" +
                $"dyn={(resolved ? 1 : 0)}:tile={(resolved ? tile.ToString(CultureInfo.InvariantCulture) : "--")}:" +
                $"disp={(hasDisp ? _playerXDisp[dispIndex].ToString(CultureInfo.InvariantCulture) : "--")},{(hasDisp ? _playerYDisp[dispIndex].ToString(CultureInfo.InvariantCulture) : "--")}:" +
                $"size={(large ? 16 : 8)}:visible={(visible ? 1 : 0)}:" +
                $"alpha={alpha:0.00}:pos={pos.X:0.00},{pos.Y:0.00}:rect={rect.Position.X:0.00},{rect.Position.Y:0.00},{rect.Size.X:0.00},{rect.Size.Y:0.00}");
        }

        return builder.ToString();
    }

    private string BuildDebugState(string tag)
    {
        var nearestActor = DescribeNearestActor();
        var activeActorCount = CountActiveSpriteActors();
        return
            $"smw-debug-state: tag={tag} frame={_debugFrameCounter} level={_currentLevelId} " +
            $"paused={(_debugPaused ? 1 : 0)} queued={_debugStepFrames} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} xs={_state.XSpeed} ys={_state.YSpeed} " +
            $"max_x={_debugMaxPlayerX:0.00} " +
            $"sub={_state.SubX:X2},{_state.SubY:X2} p={_state.PMeter:X2} pow={_state.Powerup} star={_starPowerTimer:X2} h={SmwPhysics.PlayerHeightFor(_state)} " +
            $"g={(_state.OnGround ? 1 : 0)} duck={(_state.Ducking ? 1 : 0)} sj={(_state.SpinJump ? 1 : 0)} rt={(_state.RunningTakeoff ? 1 : 0)} jf={_state.JumpHeldFrames} cf={_state.CapeFloatFrames} air={_state.InAirState:X2} face={_state.Facing} hurt={_playerHurtCooldown} blink={(_lastPlayerBlinkHidden ? 1 : 0)} " +
            $"slope={_state.SlopeKind} slope_player={_state.SlopePlayer} slope_type={_state.SlopeType} loose={_state.LooseSteepSlopeGroundFrames} loose_kind={_state.LooseSteepSlopeKind} overrun={(_state.NativeSlopeOverrunGround ? 1 : 0)} lead={_state.LeadingFootCarryFrames} preserve_y={_state.PreserveGroundYSpeedFrames} pose={_lastPlayerPose} pose_face={_lastPlayerFacing} " +
            $"clear={(_courseClear ? 1 : 0)} gamepause={(_gamePaused ? 1 : 0)} gameover={(_gameOver ? 1 : 0)} walkout={_courseClearWalkoutFrames} postwait={_courseClearPostWalkPauseFrames} exitwalk={_courseClearExitWalkFrames} exitmode={(_courseClearExitTransition ? 1 : 0)} exitframes={_courseClearExitTransitionFrames} score={_score} lives={_lives} coins={_coinCount} dragon={_dragonCoinCount} oneups={_oneUpCount} stomp_chain={_stompChainCounter} time={LevelTimerSecondsRemaining()} timer_frames={_levelTimerFrames} " +
            $"cam={_cameraX:0.00},{_cameraY:0.00} cam_lock={(_debugCameraLocked ? 1 : 0)} tile={DescribeFootTile()} solids={_solids.Count} slopes={_slopes.Count} " +
            $"actors={_spriteActors.Count} actors_active={activeActorCount} fireballs={_playerFireballs.Count} actors_on={(_debugActorsEnabled ? 1 : 0)} actor_visuals={(_debugActorVisualsEnabled ? 1 : 0)} overlays={(DebugOverlays ? 1 : 0)} god={(_debugInvincible ? 1 : 0)} autoplay={AutoplayModeName(_autoplayMode)} auto_frame={_autoplayFrame} " +
            $"near={nearestActor} actor_event={_lastActorEvent} actor_contact={_lastActorContact} blocks={_blockBreakCount} deaths={_deathCount}";
    }

    private int CountActiveSpriteActors()
    {
        return _spriteActors.Count(actor => actor.Alive && IsSpriteActorDebugActive(actor));
    }

    private bool IsSpriteActorDebugActive(RuntimeSpriteActor actor)
    {
        return _debugActorsEnabled && (actor.Active || IsSpriteActorAwake(actor));
    }

    private string DescribeNearestActor()
    {
        var nearest = FindNearestActor();
        return nearest == null
            ? "none"
            : $"{nearest.SpriteId:X2}:{nearest.State}:{nearest.X:0.00},{nearest.Y:0.00}";
    }

    private string DescribeNearestActorTraceFields()
    {
        var nearest = FindNearestActor();
        if (nearest == null)
        {
            return "near_id=-- near_state=-1 near_x=0.00 near_y=0.00 near_xs=0.00 near_ys=0.00 near_active=0";
        }

        return
            $"near_id={nearest.SpriteId:X2} near_state={nearest.State} near_x={nearest.X:0.00} near_y={nearest.Y:0.00} " +
            $"near_xs={nearest.XSpeed:0.00} near_ys={nearest.YSpeed:0.00} near_active={(IsSpriteActorDebugActive(nearest) ? 1 : 0)}";
    }

    private string DescribeTrackedActorTraceFields()
    {
        return
            $"track_9f={DescribeTrackedActor(0x9F)} " +
            $"track_ab={DescribeTrackedActor(0xAB)} " +
            $"track_ab_all={DescribeTrackedActors(0xAB, 6)} " +
            $"track_74={DescribeTrackedActor(0x74)} " +
            $"track_83={DescribeTrackedActor(0x83)} " +
            $"track_bd={DescribeTrackedActor(0xBD)}";
    }

    private string DescribeTrackedActor(int spriteId)
    {
        var actor = _spriteActors
            .Where(item => item.Alive && item.SpriteId == spriteId)
            .OrderBy(item => item.Rect.GetCenter().DistanceSquaredTo(_physics.PlayerRect(_state).GetCenter()))
            .FirstOrDefault();
        return actor == null
            ? "none"
            : $"{actor.State}:{actor.X:0.00},{actor.Y:0.00}:{actor.XSpeed:0.00},{actor.YSpeed:0.00}:{(IsSpriteActorDebugActive(actor) ? 1 : 0)}";
    }

    private string DescribeTrackedActors(int spriteId, int count)
    {
        var playerCenter = _physics.PlayerRect(_state).GetCenter();
        var actors = _spriteActors
            .Where(actor => actor.Alive && actor.SpriteId == spriteId)
            .OrderBy(actor => actor.Rect.GetCenter().DistanceSquaredTo(playerCenter))
            .Take(count)
            .Select(actor =>
                $"{actor.SpawnOffset:X}:{actor.State}:{actor.X:0.00},{actor.Y:0.00}:{actor.XSpeed:0.00},{actor.YSpeed:0.00}:{(IsSpriteActorDebugActive(actor) ? 1 : 0)}");
        var description = string.Join(";", actors);
        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private RuntimeSpriteActor? FindNearestActor()
    {
        RuntimeSpriteActor? nearest = null;
        var nearestDistance = float.MaxValue;
        var playerCenter = _physics.PlayerRect(_state).GetCenter();
        foreach (var actor in _spriteActors)
        {
            if (!actor.Alive)
            {
                continue;
            }

            var distance = actor.Rect.GetCenter().DistanceSquaredTo(playerCenter);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearest = actor;
        }

        return nearest;
    }

    private string DescribeActorsNear(float radius)
    {
        var maxDistanceSq = radius * radius;
        var playerCenter = _physics.PlayerRect(_state).GetCenter();
        var actors = _spriteActors
            .Where(actor => actor.Alive)
            .Select(actor => new
            {
                Actor = actor,
                DistanceSq = actor.Rect.GetCenter().DistanceSquaredTo(playerCenter),
            })
            .Where(item => item.DistanceSq <= maxDistanceSq)
            .OrderBy(item => item.DistanceSq)
            .Take(8)
            .Select(item =>
                $"{item.Actor.SpriteId:X2}:state={item.Actor.State}:pos={item.Actor.X:0.00},{item.Actor.Y:0.00}:rect={item.Actor.Rect.Position.X:0.00},{item.Actor.Rect.Position.Y:0.00},{item.Actor.Rect.Size.X:0.00},{item.Actor.Rect.Size.Y:0.00}:terrain={item.Actor.TerrainRect.Position.X:0.00},{item.Actor.TerrainRect.Position.Y:0.00},{item.Actor.TerrainRect.Size.X:0.00},{item.Actor.TerrainRect.Size.Y:0.00}:visuals={item.Actor.Visuals.Count}:visible={(item.Actor.Node.Visible ? 1 : 0)}:active={(IsSpriteActorDebugActive(item.Actor) ? 1 : 0)}");
        var description = string.Join(" | ", actors);
        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private string DescribeActorOamNear(float radius)
    {
        var maxDistanceSq = radius * radius;
        var playerCenter = _physics.PlayerRect(_state).GetCenter();
        var actors = _spriteActors
            .Where(actor => actor.Alive)
            .Select(actor => new
            {
                Actor = actor,
                DistanceSq = actor.Rect.GetCenter().DistanceSquaredTo(playerCenter),
            })
            .Where(item => item.DistanceSq <= maxDistanceSq)
            .OrderBy(item => item.DistanceSq)
            .Take(6)
            .Select(item => DescribeActorOam(item.Actor));
        var description = string.Join(" | ", actors);
        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private string DescribePickupsNear(float radius)
    {
        var maxDistanceSq = radius * radius;
        var playerCenter = _physics.PlayerRect(_state).GetCenter();
        var pickups = _coinPickups
            .Select((pickup, index) => new
            {
                Pickup = pickup,
                Index = index,
                DistanceSq = DistanceSquaredToRect(playerCenter, pickup.Rect),
            })
            .Where(item => item.DistanceSq <= maxDistanceSq)
            .OrderBy(item => item.DistanceSq)
            .Take(8)
            .Select(item =>
                $"{item.Index}:dragon={(item.Pickup.DragonCoin ? 1 : 0)}:collected={(item.Pickup.Collected ? 1 : 0)}:" +
                $"rect={item.Pickup.Rect.Position.X:0.00},{item.Pickup.Rect.Position.Y:0.00},{item.Pickup.Rect.Size.X:0.00},{item.Pickup.Rect.Size.Y:0.00}:" +
                $"tiles={string.Join(',', item.Pickup.Tiles.Select(tile => $"{tile.X},{tile.Y}"))}");
        var description = string.Join(" | ", pickups);
        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private string DescribeActorOam(RuntimeSpriteActor actor)
    {
        var tiles = SpriteOamTilesFor(actor.SpriteId, actor.State);
        var tileDescriptions = tiles.Count == 0
            ? "none"
            : string.Join(",", tiles.Select(DescribeSpriteOamTile));
        return
            $"{actor.SpriteId:X2}:state={actor.State}:active={(IsSpriteActorDebugActive(actor) ? 1 : 0)}:used={(actor.Used ? 1 : 0)}:pos={actor.X:0.00},{actor.Y:0.00}:" +
            $"visuals={actor.Visuals.Count}:visible={(actor.Node.Visible ? 1 : 0)}:tiles={tileDescriptions}";
    }

    private string DescribeSpriteOamTile(SpriteOamTile tile)
    {
        if (tile.Bank < 0 || tile.Bank >= SpriteAtlasTileStartByLmuBank.Length)
        {
            return $"badbank{tile.Bank}:tile={tile.Tile:X2}";
        }

        var size = tile.Large ? 16 : 8;
        var oamPalette = (tile.Prop >> 1) & 0x07;
        var tileIndex = SpriteAtlasTileStartByLmuBank[tile.Bank] + (tile.Tile & 0x7F);
        var regionX = (tileIndex % SnesSpriteAtlasColumns) * SnesSpriteTileSize;
        var regionY = (tileIndex / SnesSpriteAtlasColumns) * SnesSpriteTileSize;
        return
            $"dx={tile.Dx}:dy={tile.Dy}:tile={tile.Tile:X2}:bank={tile.Bank}:atlas={tileIndex}:" +
            $"pal={oamPalette}:prop={tile.Prop:X2}:size={size}:flip_h={((tile.Prop & 0x40) != 0 ? 1 : 0)}:" +
            $"flip_v={((tile.Prop & 0x80) != 0 ? 1 : 0)}:rect={regionX},{regionY},{size},{size}";
    }

    private string DescribeSolidsNear(Vector2 point, float radius)
    {
        var radiusSq = radius * radius;
        var solids = _solids
            .Select((solid, index) => new
            {
                Solid = solid,
                Index = index,
                DistanceSq = DistanceSquaredToRect(point, solid),
            })
            .Where(item => item.DistanceSq <= radiusSq)
            .OrderBy(item => item.DistanceSq)
            .Take(6)
            .Select(item =>
                $"{item.Index}:rect={item.Solid.Position.X:0.00},{item.Solid.Position.Y:0.00},{item.Solid.Size.X:0.00},{item.Solid.Size.Y:0.00}:step={BoolAt(_solidStepUpEnabled, item.Index)}:vert={BoolAt(_solidVerticalEnabled, item.Index)}:support={IntAt(_solidSupportModes, item.Index)}");
        var description = string.Join(" | ", solids);
        return $"solids={(string.IsNullOrEmpty(description) ? "none" : description)}";
    }

    private static int IntAt(IReadOnlyList<int> values, int index)
    {
        return index >= 0 && index < values.Count ? values[index] : 0;
    }

    private static string DescribePipeCellsNear(
        HashSet<(int X, int Y)> cells,
        Vector2 point,
        float radius)
    {
        var radiusSq = radius * radius;
        var cellsNear = cells
            .Select(cell => new
            {
                Cell = cell,
                Center = new Vector2(
                    cell.X * Map16TileSize + Map16TileSize * 0.5f,
                    cell.Y * Map16TileSize + LevelVisualYOffset + Map16TileSize * 0.5f),
            })
            .Select(item => new
            {
                item.Cell,
                DistanceSq = item.Center.DistanceSquaredTo(point),
            })
            .Where(item => item.DistanceSq <= radiusSq)
            .OrderBy(item => item.DistanceSq)
            .Take(8)
            .Select(item => $"{item.Cell.X},{item.Cell.Y}");
        var description = string.Join("|", cellsNear);
        return string.IsNullOrEmpty(description) ? "none" : description;
    }

    private string DescribeSlopesNear(Vector2 point, float radius)
    {
        var radiusSq = radius * radius;
        var slopes = _slopes
            .Select((slope, index) => new
            {
                Slope = slope,
                Index = index,
                DistanceSq = DistanceSquaredToSegment(point, new Vector2(slope.X0, slope.Y0), new Vector2(slope.X1, slope.Y1)),
            })
            .Where(item => item.DistanceSq <= radiusSq)
            .OrderBy(item => item.DistanceSq)
            .Take(8)
            .Select(item =>
                $"{item.Index}:line={item.Slope.X0:0.00},{item.Slope.Y0:0.00}->{item.Slope.X1:0.00},{item.Slope.Y1:0.00}:ceil={(item.Slope.Ceiling ? 1 : 0)}:kind={item.Slope.NativeSlopeKind}:snap={item.Slope.SnapDistance:0.00}");
        var description = string.Join(" | ", slopes);
        return $"slopes={(string.IsNullOrEmpty(description) ? "none" : description)}";
    }

    private static int BoolAt(IReadOnlyList<bool> values, int index)
    {
        return index >= 0 && index < values.Count && values[index] ? 1 : 0;
    }

    private static float DistanceSquaredToRect(Vector2 point, Rect2 rect)
    {
        var closestX = Math.Clamp(point.X, rect.Position.X, rect.Position.X + rect.Size.X);
        var closestY = Math.Clamp(point.Y, rect.Position.Y, rect.Position.Y + rect.Size.Y);
        return point.DistanceSquaredTo(new Vector2(closestX, closestY));
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var segment = b - a;
        var lengthSq = segment.LengthSquared();
        if (lengthSq <= 0.0001f)
        {
            return point.DistanceSquaredTo(a);
        }

        var t = Math.Clamp((point - a).Dot(segment) / lengthSq, 0.0f, 1.0f);
        return point.DistanceSquaredTo(a + segment * t);
    }

    private static void RequirePartCount(string[] parts, int minimum)
    {
        if (parts.Length < minimum)
        {
            throw new FormatException($"{parts[0]} expects at least {minimum - 1} argument(s)");
        }
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static int ParseDebugPowerup(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var powerup))
        {
            return powerup;
        }

        return normalized switch
        {
            "small" => SmwPhysics.SmallPowerup,
            "big" => SmwPhysics.BigPowerup,
            "cape" => SmwPhysics.CapePowerup,
            "fire" => SmwPhysics.FirePowerup,
            _ => throw new FormatException($"unknown powerup '{value}'"),
        };
    }

    private static int ParseDebugItemSprite(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spriteId))
        {
            return spriteId;
        }
        if (normalized.StartsWith("0x", StringComparison.Ordinal) &&
            int.TryParse(normalized[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexSpriteId))
        {
            return hexSpriteId;
        }

        return normalized switch
        {
            "mushroom" => 0x74,
            "flower" or "fire" => 0x75,
            "star" => 0x76,
            "feather" or "cape" => 0x77,
            "1up" or "oneup" or "one-up" => 0x78,
            _ => throw new FormatException($"unknown item sprite '{value}'"),
        };
    }

    private static bool ParseDebugBool(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "on" or "yes" => true,
            "0" or "false" or "off" or "no" => false,
            _ => throw new FormatException($"unknown boolean '{value}'"),
        };
    }

    private static int ParseHexOrDecimalDebug(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec)
            ? dec
            : int.Parse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public void DebugLoadInputScript(string path)
    {
        _inputScript.Clear();
        _inputScriptIndex = 0;
        _inputScriptFrame = 0;
        _inputScriptElapsedFrames = 0;
        _inputScriptName = path;
        _inputScriptDoneLogged = false;

        try
        {
            var lines = ReadInputScriptLines(path);
            for (var i = 0; i < lines.Length; i++)
            {
                if (TryParseInputScriptLine(path, i + 1, lines[i], out var segment))
                {
                    _inputScript.Add(segment);
                }
            }
        }
        catch (Exception ex)
        {
            _inputScript.Clear();
            GD.PrintErr($"smw-input-script: {ex.Message}");
            return;
        }

        var totalFrames = _inputScript.Sum(segment => segment.Frames);
        GD.Print($"smw-input-script: loaded path={path} segments={_inputScript.Count} frames={totalFrames}");
    }

    private static string[] ReadInputScriptLines(string path)
    {
        if (path.StartsWith("res://", StringComparison.Ordinal) ||
            path.StartsWith("user://", StringComparison.Ordinal))
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                throw new System.IO.IOException($"unable to open {path}");
            }

            return file.GetAsText().Replace("\r\n", "\n").Split('\n');
        }

        var globalPath = IoPath.IsPathRooted(path) ? path : IoPath.GetFullPath(path);
        return IoFile.ReadAllLines(globalPath);
    }

    private static bool TryParseInputScriptLine(
        string path,
        int lineNumber,
        string rawLine,
        out ScriptedInputSegment segment)
    {
        segment = default;
        var commentIndex = rawLine.IndexOf('#');
        var line = commentIndex >= 0 ? rawLine[..commentIndex] : rawLine;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }
        line = line.Trim();
        if (line.StartsWith('@'))
        {
            return false;
        }

        var parts = line.Split(
            [' ', '\t', ',', ':', ';', '+'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var frames) || frames <= 0)
        {
            throw new FormatException($"{path}:{lineNumber}: expected a positive frame count");
        }

        var input = new SmwPhysics.FrameInput();
        for (var i = 1; i < parts.Length; i++)
        {
            ApplyScriptedInputToken(path, lineNumber, parts[i], ref input);
        }

        segment = new ScriptedInputSegment(frames, input);
        return true;
    }

    private static void ApplyScriptedInputToken(
        string path,
        int lineNumber,
        string token,
        ref SmwPhysics.FrameInput input)
    {
        switch (token.Trim().ToLowerInvariant())
        {
            case "none":
            case "-":
                break;
            case "left":
                input.Left = true;
                break;
            case "right":
                input.Right = true;
                break;
            case "up":
            case "u":
                input.Up = true;
                break;
            case "select":
            case "start":
            case "l":
            case "r":
                break;
            case "down":
            case "d":
                input.Down = true;
                break;
            case "jump":
            case "b":
                input.Jump = true;
                input.JumpPressed = true;
                break;
            case "spin":
            case "a":
                input.Spin = true;
                input.SpinPressed = true;
                break;
            case "run":
            case "dash":
            case "x":
            case "y":
                input.Run = true;
                input.RunPressed = true;
                break;
            default:
                throw new FormatException($"{path}:{lineNumber}: unknown input token '{token}'");
        }
    }

    public async void DebugCaptureViewport(string capturePath, int frames, bool quitAfterCapture)
    {
        var waitFrames = Math.Max(1, frames);
        GD.Print($"smw-capture: scheduled path={capturePath} frames={waitFrames} level={_currentLevelId}");
        for (var i = 0; i < waitFrames; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (!DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        CaptureViewportNow(capturePath, quitAfterCapture);
    }

    private Error CaptureViewportNow(string capturePath, bool quitAfterCapture)
    {
        if (IsHeadlessDisplay())
        {
            return FailCapture(capturePath, "headless_viewport_texture_unavailable", quitAfterCapture);
        }

        Image? image;
        try
        {
            image = GetViewport().GetTexture()?.GetImage();
        }
        catch (Exception ex)
        {
            return FailCapture(capturePath, $"viewport_texture_error:{ex.Message}", quitAfterCapture);
        }

        if (image == null)
        {
            return FailCapture(capturePath, "viewport_texture_unavailable", quitAfterCapture);
        }

        image.Convert(Image.Format.Rgba8);
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var color = image.GetPixel(x, y);
                color.A = 1.0f;
                image.SetPixel(x, y, color);
            }
        }

        var globalPath = ProjectSettings.GlobalizePath(capturePath);
        DirAccess.MakeDirRecursiveAbsolute(System.IO.Path.GetDirectoryName(globalPath) ?? ".");
        var error = image.SavePng(globalPath);
        GD.Print($"smw-capture: saved path={globalPath} error={error} level={_currentLevelId} layer1_objects={_levelObjects.Count} layer2_objects={_layer2Objects.Count} map16_tiles={_placedTiles.Count} sprite_spawns={_levelSprites.Count}");
        if (quitAfterCapture)
        {
            GetTree().Quit(error == Error.Ok ? 0 : 1);
        }

        return error;
    }

    private static bool IsHeadlessDisplay()
    {
        if (OS.HasFeature("headless") ||
            DisplayServer.GetName().Contains("headless", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg.Equals("--headless", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private Error FailCapture(string capturePath, string reason, bool quitAfterCapture)
    {
        GD.PrintErr($"smw-capture: failed path={capturePath} reason={reason} level={_currentLevelId}");
        if (quitAfterCapture)
        {
            GetTree().Quit(1);
        }

        return Error.Unavailable;
    }

    private void ToggleGameplayPause(string source)
    {
        SetGameplayPaused(!_gamePaused, source);
    }

    private void SetGameplayPaused(bool paused, string source)
    {
        if (_gameOver || _courseClear)
        {
            return;
        }

        if (_gamePaused == paused)
        {
            UpdateHud();
            UpdateDebugGizmos();
            GD.Print($"smw-runtime: pause level={_currentLevelId} state={(_gamePaused ? 1 : 0)} source={source} unchanged=1");
            return;
        }

        _gamePaused = paused;
        if (_gamePaused)
        {
            _audio?.StopMusicPreview();
            ShowPauseLabel();
        }
        else
        {
            HidePauseLabel();
            StartLevelMusic();
        }

        UpdateHud();
        UpdateDebugGizmos();
        GD.Print(
            $"smw-runtime: pause level={_currentLevelId} state={(_gamePaused ? 1 : 0)} source={source} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} timer_frames={_levelTimerFrames}");
    }

    private void EnterLevel(string levelId, LevelEntrance? entrance = null)
    {
        if (!LoadLevelData(levelId))
        {
            GD.PrintErr($"smw-runtime: unable to load level {levelId}");
            return;
        }

        _courseClear = false;
        _courseClearGoalCoinAwarded = false;
        _gamePaused = false;
        _gameOver = false;
        _queuedPlayerDeathCause = null;
        _queuedPlayerDeathEvent = "death:hurt";
        _courseClearWalkoutFrames = 0;
        _courseClearPostWalkPauseFrames = -1;
        _courseClearExitWalkFrames = 0;
        _courseClearExitTransitionFrames = 0;
        _courseClearInitialWalkoutInputFrame = false;
        _courseClearExitTransition = false;
        _playerHurtCooldown = 0;
        _lastActorEvent = "none";
        _lastActorContact = "none";
        _blockBreakCount = 0;
        _stompChainCounter = 0;
        _starPowerTimer = 0;
        _pendingNormalCoinIncrements = 0;
        _pendingDragonCoinNormalCoins = 0;
        _normalCoinPickupCooldownFrames = 0;
        ClearPipeEntryMotion();
        _pipeTransitionLatch = false;
        _state = MakeInitialPlayerState(entrance);
        ResetPowerupAnimationState();
        ResetDebugMaxPlayerX();
        ResetPlayerAnimationState();
        _cameraInitialized = false;
        UpdateCamera();
        BuildWorld();
        BuildHud();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }

        _lastPlayerPose = -1;
        _lastPlayerPowerup = -1;
        _lastPlayerDucking = false;
        UpdatePlayerGraphic(force: true);
        PrintRuntimeState();
        StartLevelMusic();
    }

    private bool TryHandlePlayerFallDeath()
    {
        if (_courseClear || _state.YFloat <= GetLevelPixelBottom() + FallDeathMarginPixels)
        {
            return false;
        }

        HandlePlayerDeath("fall", "death:fall");
        return true;
    }

    private void HandlePlayerDeath(string cause, string actorEvent)
    {
        UpdateDebugMaxPlayerX();
        _deathCount++;
        _stompChainCounter = 0;
        _lives = Math.Max(0, _lives - 1);
        _audio?.PlayDeath();
        GD.Print(
            $"smw-runtime: player_death level={_currentLevelId} cause={cause} count={_deathCount} " +
            $"lives={_lives} x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
        if (_lives <= 0)
        {
            TriggerGameOver(cause);
            return;
        }

        RestartCurrentLevel(actorEvent);
    }

    private void TriggerGameOver(string cause)
    {
        _gamePaused = false;
        _gameOver = true;
        _courseClear = false;
        _courseClearGoalCoinAwarded = false;
        _queuedPlayerDeathCause = null;
        _queuedPlayerDeathEvent = "death:hurt";
        _courseClearWalkoutFrames = 0;
        _courseClearPostWalkPauseFrames = -1;
        _courseClearExitWalkFrames = 0;
        _courseClearExitTransitionFrames = 0;
        _courseClearInitialWalkoutInputFrame = false;
        _courseClearExitTransition = false;
        _entranceMotionFrames = 0;
        _entranceMotionDelayFrames = 0;
        _entranceReleaseHoldFrames = 0;
        _deferredEntranceMotionFrames = 0;
        _deferredEntranceMotionPixelsPerFrame = Vector2.Zero;
        ClearPipeEntryMotion();
        _pipeTransitionLatch = false;
        _state.XSpeed = 0;
        _state.YSpeed = 0;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _starPowerTimer = 0;
        _lastActorEvent = $"gameover:{cause}";
        _levelTimerFrames = 0;
        _audio?.StopMusicPreview();
        HidePauseLabel();
        ShowGameOverLabel();
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print(
            $"smw-runtime: game_over level={_currentLevelId} cause={cause} deaths={_deathCount} " +
            $"score={_score} coins={_coinCount} dragon_coins={_dragonCoinCount}");
    }

    private void ContinueAfterGameOver()
    {
        var wasGameOver = _gameOver;
        _gameOver = false;
        _gamePaused = false;
        _queuedPlayerDeathCause = null;
        _queuedPlayerDeathEvent = "death:hurt";
        _lives = StartingLives;
        _coinCount = 0;
        _dragonCoinCount = 0;
        _pendingNormalCoinIncrements = 0;
        _pendingDragonCoinNormalCoins = 0;
        _normalCoinPickupCooldownFrames = 0;
        _oneUpCount = 0;
        _score = 0;
        _stompChainCounter = 0;
        _starPowerTimer = 0;
        HideGameOverLabel();
        HidePauseLabel();
        RestartCurrentLevel(wasGameOver ? "gameover:continue" : "debug:continue");
        ResetDebugMaxPlayerX();
        GD.Print($"smw-runtime: continue level={_currentLevelId} lives={_lives} score={_score}");
    }

    private void RestartCurrentLevel(string actorEvent)
    {
        if (!LoadLevelData(_currentLevelId))
        {
            GD.PrintErr($"smw-runtime: unable to reload level {_currentLevelId}");
            return;
        }

        _courseClear = false;
        _courseClearGoalCoinAwarded = false;
        _gamePaused = false;
        _gameOver = false;
        _queuedPlayerDeathCause = null;
        _queuedPlayerDeathEvent = "death:hurt";
        _courseClearWalkoutFrames = 0;
        _courseClearPostWalkPauseFrames = -1;
        _courseClearExitWalkFrames = 0;
        _courseClearExitTransitionFrames = 0;
        _courseClearInitialWalkoutInputFrame = false;
        _courseClearExitTransition = false;
        _playerHurtCooldown = 0;
        _lastActorEvent = actorEvent;
        _blockBreakCount = 0;
        _stompChainCounter = 0;
        _starPowerTimer = 0;
        _pendingNormalCoinIncrements = 0;
        _pendingDragonCoinNormalCoins = 0;
        _normalCoinPickupCooldownFrames = 0;
        _levelTimerFrames = DefaultLevelTimerSeconds * NativeFramesPerSecond;
        _pipeTransitionLatch = false;
        _entranceMotionFrames = 0;
        _entranceMotionDelayFrames = 0;
        _entranceReleaseHoldFrames = 0;
        _deferredEntranceMotionFrames = 0;
        _deferredEntranceMotionPixelsPerFrame = Vector2.Zero;
        ClearPipeEntryMotion();
        HidePauseLabel();
        _state = MakeInitialPlayerState();
        ResetPowerupAnimationState();
        ResetPlayerAnimationState();
        _cameraInitialized = false;
        UpdateCamera();
        BuildWorld();
        BuildHud();
        if (_player != null)
        {
            _player.Position = PlayerRenderPosition();
        }

        _lastPlayerPose = -1;
        _lastPlayerPowerup = -1;
        _lastPlayerDucking = false;
        UpdatePlayerGraphic(force: true);
        PrintRuntimeState();
        StartLevelMusic();
    }

    private void ResetDebugMaxPlayerX()
    {
        _debugMaxPlayerX = _state.XFloat;
    }

    private void UpdateDebugMaxPlayerX()
    {
        _debugMaxPlayerX = MathF.Max(_debugMaxPlayerX, _state.XFloat);
    }

    public override void _ExitTree()
    {
        StopDebugRcon();
    }

    private void CheckPipeDebug(SmwPhysics.FrameInput frameInput)
    {
        var downPressed = frameInput.Down;
        var sidePressed = frameInput.Left || frameInput.Right;
        if (!downPressed && !sidePressed)
        {
            _pipeTransitionLatch = false;
            return;
        }
        if (_pipeTransitionLatch)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        PipeEntrance? matchedEntrance = null;
        foreach (var pipeEntrance in _pipeEntrances)
        {
            if (PipeEntranceAcceptsPlayer(pipeEntrance, playerRect, downPressed, sidePressed))
            {
                matchedEntrance = pipeEntrance;
                break;
            }
        }
        if (matchedEntrance == null)
        {
            return;
        }

        var screen = matchedEntrance.Value.Screen;
        if (TryResolveScreenExit(screen, out var entrance))
        {
            _pipeTransitionLatch = true;
            GD.Print(
                $"pipe-debug screen={screen:X2} target={entrance.LevelId} " +
                $"secondary={(entrance.Secondary ? 1 : 0)} source={entrance.SourceId:X3} kind={matchedEntrance.Value.Kind} " +
                $"rect={matchedEntrance.Value.Rect.Position.X:0.00},{matchedEntrance.Value.Rect.Position.Y:0.00},{matchedEntrance.Value.Rect.Size.X:0.00},{matchedEntrance.Value.Rect.Size.Y:0.00} " +
                $"tile={matchedEntrance.Value.SourceX},{matchedEntrance.Value.SourceY}:{matchedEntrance.Value.Source} " +
                $"player={playerRect.Position.X:0.00},{playerRect.Position.Y:0.00},{playerRect.Size.X:0.00},{playerRect.Size.Y:0.00}");
            StartPipeEntryMotion(entrance, matchedEntrance.Value.Horizontal);
        }
        else
        {
            GD.PrintErr($"pipe-debug screen={screen:X2} unresolved level={_currentLevelId}");
        }
    }

    private bool PipeEntranceAcceptsPlayer(PipeEntrance pipeEntrance, Rect2 playerRect, bool downPressed, bool sidePressed)
    {
        if (pipeEntrance.Horizontal)
        {
            return sidePressed && _state.OnGround && playerRect.Intersects(pipeEntrance.Rect);
        }

        if (IsBlockedPipeCap(pipeEntrance.SourceX, pipeEntrance.SourceY))
        {
            return false;
        }

        return downPressed && _state.OnGround && playerRect.Intersects(pipeEntrance.Rect);
    }

    private void UpdateCamera()
    {
        var maxCameraX = MathF.Max(0.0f, GetLevelPixelRight() - LogicalViewportWidth);
        var maxCameraY = MathF.Max(0.0f, GetLevelPixelBottom() - LogicalViewportHeight);
        if (_debugCameraLocked)
        {
            _cameraX = Math.Clamp(_debugCameraLockPosition.X, 0.0f, maxCameraX);
            _cameraY = Math.Clamp(_debugCameraLockPosition.Y, 0.0f, maxCameraY);
            _debugCameraLockPosition = new Vector2(_cameraX, _cameraY);
            _cameraInitialized = true;
            Position = new Vector2(-MathF.Round(_cameraX), -MathF.Round(_cameraY));
            return;
        }

        if (!_cameraInitialized)
        {
            _cameraHorizontalFocus = CameraHorizontalInitialFocus;
            _cameraX = Math.Clamp(_state.XFloat - _cameraHorizontalFocus, 0.0f, maxCameraX);
            _cameraY = Math.Clamp(_state.YFloat - CameraVerticalLower, 0.0f, maxCameraY);
            _cameraInitialized = true;
        }

        var previousCameraX = _cameraX;
        var playerScreenX = _state.XFloat - _cameraX;
        if (playerScreenX < _cameraHorizontalFocus - CameraHorizontalBand)
        {
            _cameraX -= _cameraHorizontalFocus - CameraHorizontalBand - playerScreenX;
        }
        else if (playerScreenX > _cameraHorizontalFocus + CameraHorizontalBand)
        {
            _cameraX += playerScreenX - (_cameraHorizontalFocus + CameraHorizontalBand);
        }

        if (_cameraX > previousCameraX)
        {
            _cameraHorizontalFocus = MathF.Max(
                CameraHorizontalRightFocus,
                _cameraHorizontalFocus - CameraHorizontalFocusStep);
        }
        else if (_cameraX < previousCameraX)
        {
            _cameraHorizontalFocus = MathF.Min(
                CameraHorizontalLeftFocus,
                _cameraHorizontalFocus + CameraHorizontalFocusStep);
        }

        var playerScreenY = _state.YFloat - _cameraY;
        if (playerScreenY < CameraVerticalUpper && _state.OnGround)
        {
            var delta = playerScreenY - CameraVerticalUpper;
            _cameraY += MathF.Max(-CameraMaxScrollUpPerFrame, delta);
        }
        else if (playerScreenY > CameraVerticalLower)
        {
            var delta = playerScreenY - CameraVerticalLower;
            _cameraY += MathF.Min(CameraMaxScrollDownPerFrame, delta);
        }

        _cameraX = Math.Clamp(_cameraX, 0.0f, maxCameraX);
        _cameraY = Math.Clamp(_cameraY, 0.0f, maxCameraY);
        Position = new Vector2(-MathF.Round(_cameraX), -MathF.Round(_cameraY));
    }

    private float GetLevelPixelRight()
    {
        var maxTileX = 0;
        foreach (var tile in _placedTiles)
        {
            maxTileX = Math.Max(maxTileX, tile.X);
        }

        return MathF.Max(LogicalViewportWidth, (maxTileX + 1) * Map16TileSize);
    }

    private float GetLevelPixelBottom()
    {
        var maxTileY = 0;
        foreach (var tile in _placedTiles)
        {
            maxTileY = Math.Max(maxTileY, tile.Y);
        }

        return MathF.Max(LogicalViewportHeight, (maxTileY + 1) * Map16TileSize + LevelVisualYOffset);
    }
}
