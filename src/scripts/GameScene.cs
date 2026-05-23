using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
    private const float CameraHorizontalAnchor = 0x80;
    private const float CameraHorizontalBand = 12.0f;
    private const float CameraVerticalUpper = 0x64;
    private const float CameraVerticalLower = 0x7C;
    private const float CameraMaxScrollUpPerFrame = 3.0f;
    private const float CameraMaxScrollDownPerFrame = 5.0f;
    private const int SpriteActorWidth = 16;
    private const int SpriteActorHeight = 16;
    private const float SpriteActorGravity = 0.42f;
    private const float SpriteActorMaxFall = 4.0f;
    private const int PlayerHurtCooldownFrames = 90;
    private const int JumpingPiranhaCycleFrames = 192;
    private const int JumpingPiranhaHiddenFrames = 48;
    private const int JumpingPiranhaRiseFrames = 24;
    private const int JumpingPiranhaExtendedFrames = 48;
    private const int JumpingPiranhaFallFrames = 24;
    private const float JumpingPiranhaTravelPixels = 32.0f;
    private const int WingedQuestionBlockCycleFrames = 64;
    private const int GoalTapeSpriteId = 0x7B;
    private const int DefaultPlayerPowerup = SmwPhysics.BigPowerup;
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

    private readonly SmwPhysics _physics = new();
    private readonly List<Rect2> _solids = [];
    private readonly List<bool> _solidStepUpEnabled = [];
    private readonly List<bool> _solidVerticalEnabled = [];
    private readonly List<SmwPhysics.SlopeSurface> _slopes = [];
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
    private readonly List<ScriptedInputSegment> _inputScript = [];
    private readonly List<Rect2> _goalTapeTriggers = [];
    private readonly List<CoinPickup> _coinPickups = [];
    private readonly Dictionary<(int X, int Y), PlacedMap16Tile> _map16TilesByCoord = [];
    private readonly HashSet<(int X, int Y)> _diagonalPipeBodyCells = [];
    private readonly HashSet<(int X, int Y)> _diagonalPipeCeilingCells = [];

    private SmwPhysics.PlayerState _state;
    private Node2D? _player;
    private Line2D? _playerHitboxGizmo;
    private ColorRect? _playerFootGizmo;
    private Label? _playerDebugLabel;
    private Line2D? _cameraGizmo;
    private Label? _hud;
    private Label? _courseClearLabel;
    private CanvasLayer? _hudLayer;
    private CanvasLayer? _courseClearLayer;
    private Node2D? _worldRoot;
    private SmwAudio? _audio;
    private ImageTexture? _playerTexture;
    private ImageTexture? _spriteTexture;
    private ImageTexture? _map16Texture;
    private Map16TileLayer? _map16Layer;
    private Godot.Collections.Dictionary? _entranceTables;
    private string _currentLevelId = "105";
    private string _levelGfxAtlasPath = "res://generated/smw/tilesets/level_105_tileset7_8x8.png";
    private string _levelMap16AtlasPath = "res://generated/smw/tilesets/level_105_tileset7_map16_preview.png";
    private string _levelSpriteAtlasPath = "res://generated/smw/spritesets/level_105_spritegfx8_8x8.png";
    private string _levelLayoutPreviewPath = "res://generated/smw/levels/level_105_partial_layout.png";
    private string _levelTilemapPath = "res://generated/smw/levels/level_105_partial_tilemap.json";
    private string _levelLayer2BackgroundPath = "res://generated/smw/levels/level_105_layer2_background.png";
    private int _currentLevelMusicIndex;
    private string _currentLevelMusicPreview = "Level";
    private float _cameraX;
    private float _cameraY;
    private bool _cameraInitialized;
    private int _lastPlayerPose = -1;
    private int _lastPlayerFacing = -1;
    private int _lastPlayerPowerup = -1;
    private bool _lastPlayerDucking;
    private bool _pipeTransitionLatch;
    private int _playerHurtCooldown;
    private string _lastActorEvent = "none";
    private bool _courseClear;
    private int _entranceMotionFrames;
    private int _entranceMotionAction;
    private Vector2 _entranceMotionPixelsPerFrame;
    private int _coinCount;
    private int _dragonCoinCount;
    private int _oneUpCount;
    private int _blockBreakCount;
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
    private TcpListener? _debugRconListener;
    private readonly List<TcpClient> _debugRconClients = [];
    private readonly Dictionary<TcpClient, StringBuilder> _debugRconBuffers = [];
    private readonly byte[] _debugRconReadBuffer = new byte[4096];

    public bool DebugOverlays { get; set; }
    public SmwAudio? Audio { get; set; }
    public bool AudioEnabled { get; set; } = true;

    public override void _Ready()
    {
        GetViewport().TransparentBg = false;
        RenderingServer.SetDefaultClearColor(new Color(0.0f, 0.39f, 0.74f, 1.0f));
        _audio = Audio;
        if (_audio == null && AudioEnabled)
        {
            _audio = new SmwAudio { Name = "SmwAudio" };
            AddChild(_audio);
        }
        LoadAssetPack();
        _state = MakeInitialPlayerState();
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
        if (_debugPaused && _debugStepFrames <= 0)
        {
            UpdateHud();
            UpdateDebugGizmos();
            return;
        }

        var isDebugStep = _debugStepFrames > 0;
        var frameInput = _courseClear
            ? new SmwPhysics.FrameInput()
            : ReadFrameInput();
        _lastFrameInput = frameInput;
        var previousStateForActors = _state;

        var entranceLocked = _entranceMotionFrames > 0;
        if (!entranceLocked && _state.OnGround && frameInput.SpinPressed)
        {
            _audio?.PlaySpinJump();
        }
        else if (!entranceLocked && _state.OnGround && frameInput.JumpPressed)
        {
            _audio?.PlayJump();
        }

        if (entranceLocked)
        {
            ApplyEntranceMotion();
        }
        else
        {
            var previousState = _state;
            _physics.Step(
                ref _state,
                frameInput,
                _solids,
                _solidStepUpEnabled,
                _solidVerticalEnabled,
                _slopes,
                0,
                (int)MathF.Round(GetLevelPixelRight()));
            ResolveDiagonalPipeTileContacts(previousState);
            TryBreakSpinJumpTurnBlocks(previousState);
        }
        UpdateCamera();

        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        UpdatePlayerGraphic();
        if (UpdateSpriteActors(_physics.PlayerRect(previousStateForActors)))
        {
            if (_player != null)
            {
                _player.Position = new Vector2(_state.XFloat, _state.YFloat);
            }
            UpdatePlayerGraphic(force: true);
        }
        CheckCoinPickups();
        CheckGoalTape();
        UpdateHud();
        UpdateDebugGizmos();
        if (!entranceLocked)
        {
            CheckPipeDebug(frameInput);
        }

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

    private readonly record struct PlacedMap16Tile(int X, int Y, int Map16, string Source);
    private readonly record struct SpriteSpawn(int X, int Y, int Screen, int SpriteId, int ExtraBits, int Offset);
    private readonly record struct SpriteOamTile(int Dx, int Dy, int Tile, int Prop, int Bank, bool Large);
    private readonly record struct SpriteActorBehavior(
        Rect2 Hitbox,
        bool CanInteract,
        bool Stompable,
        bool TerrainCollision,
        bool Gravity,
        float InitialXSpeed);
    private readonly record struct PipeEntrance(Rect2 Rect, int Screen, bool Horizontal, string Kind);
    private readonly record struct ScriptedInputSegment(int Frames, SmwPhysics.FrameInput Input);
    private readonly record struct LevelEntrance(
        string LevelId,
        Vector2 Position,
        int EntranceSettings,
        bool Secondary,
        int SourceId);
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
        public bool Used { get; set; }
        public bool Alive { get; set; } = true;
        public bool OnGround { get; set; }
        public int WakeScreen { get; init; }
        public required List<Node> Visuals { get; init; }
        public required SpriteActorBehavior Behavior { get; set; }
        public int State { get; set; }
        public Rect2 Rect => new(X + Behavior.Hitbox.Position.X, Y + Behavior.Hitbox.Position.Y, Behavior.Hitbox.Size.X, Behavior.Hitbox.Size.Y);
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

        if (_inputScript.Count > 0)
        {
            return ReadScriptedFrameInput();
        }

        return new SmwPhysics.FrameInput
        {
            Left = Input.IsActionPressed("smw_left"),
            Right = Input.IsActionPressed("smw_right"),
            Down = Input.IsActionPressed("smw_down"),
            Jump = Input.IsActionPressed("smw_jump"),
            JumpPressed = Input.IsActionJustPressed("smw_jump"),
            Spin = Input.IsActionPressed("smw_spin"),
            SpinPressed = Input.IsActionJustPressed("smw_spin"),
            Run = Input.IsActionPressed("smw_run"),
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

        _audio?.PlayMusicPreview(_currentLevelMusicPreview);
        GD.Print($"smw-runtime: level_music level={_currentLevelId} music_index={_currentLevelMusicIndex} bank={_currentLevelMusicPreview}");
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

            ApplySpriteUploadTileStarts(spriteTileset);
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
        _slopes.Clear();
        _spriteActors.Clear();
        _goalTapeTriggers.Clear();
        _coinPickups.Clear();
        _map16TilesByCoord.Clear();
        _diagonalPipeBodyCells.Clear();
        _diagonalPipeCeilingCells.Clear();
        _cameraGizmo?.QueueFree();
        _cameraGizmo = null;
        _spriteTexture = null;
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
        AddGoalTapeTriggers();
        if (DebugOverlays)
        {
            AddPipeMarkers();
            AddGoalTapeMarkers();
            AddObjectMarkers();
            AddSpriteMarkers();
            AddTileSemanticMarkers();
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

            var top = spawn.Y - 72;
            var rect = new Rect2(spawn.X - 8, top, 24, 88);
            _goalTapeTriggers.Add(rect);

            var postLeft = new ColorRect
            {
                Color = new Color(0.96f, 0.96f, 0.86f, 1.0f),
                Position = new Vector2(spawn.X - 16, top),
                Size = new Vector2(4, 88),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 5,
            };
            AddWorldChild(postLeft);

            var postRight = new ColorRect
            {
                Color = new Color(0.96f, 0.96f, 0.86f, 1.0f),
                Position = new Vector2(spawn.X + 16, top),
                Size = new Vector2(4, 88),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 5,
            };
            AddWorldChild(postRight);

            var tape = new ColorRect
            {
                Color = new Color(1.0f, 0.86f, 0.18f, 1.0f),
                Position = new Vector2(spawn.X - 8, spawn.Y - 38),
                Size = new Vector2(28, 6),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 7,
            };
            AddWorldChild(tape);
        }
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

    private void LoadSpriteTexture()
    {
        if (!FileAccess.FileExists(_levelSpriteAtlasPath))
        {
            return;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(_levelSpriteAtlasPath));
        if (image == null || image.IsEmpty())
        {
            return;
        }

        _spriteTexture = ImageTexture.CreateFromImage(image);
    }

    private static bool IsRuntimeEnemySprite(int spriteId)
    {
        return spriteId is 0x4F or 0x83 or 0x8E or 0x95 or 0x9F or 0xAB or 0xB9 or 0xBD or 0xC7 or 0xDA or 0xDB or 0xDC or 0xDD or 0xDF;
    }

    private static bool IsJumpingPiranhaSprite(int spriteId)
    {
        return spriteId is 0x4F or 0x50;
    }

    private static bool IsSolidBlockSprite(int spriteId)
    {
        return spriteId is 0x83 or 0xB9;
    }

    private RuntimeSpriteActor CreateRuntimeSpriteActor(SpriteSpawn spawn, bool debugOverlays)
    {
        var color = SpriteActorColor(spawn.SpriteId);
        var behavior = SpriteActorBehaviorFor(spawn.SpriteId);
        var node = new Node2D
        {
            Name = $"Sprite_{spawn.SpriteId:X2}_{spawn.Offset:X2}",
            Position = new Vector2(spawn.X, spawn.Y - SpriteActorHeight),
            ZIndex = 6,
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
            Y = spawn.Y - SpriteActorHeight,
            PreviousX = spawn.X,
            PreviousY = spawn.Y - SpriteActorHeight,
            HomeY = spawn.Y - SpriteActorHeight,
            XSpeed = behavior.InitialXSpeed,
            WakeScreen = spawn.Screen,
            MotionFrame = InitialSpriteMotionFrame(spawn),
            Visuals = visuals,
            Behavior = behavior,
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
        if (_spriteTexture == null ||
            tile.Bank < 0 ||
            tile.Bank >= SpriteAtlasTileStartByLmuBank.Length)
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
        if (region.Position.X + size > _spriteTexture.GetWidth() ||
            region.Position.Y + size > _spriteTexture.GetHeight())
        {
            return false;
        }

        sprite = new Sprite2D
        {
            Texture = _spriteTexture,
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
                AddMap16SpriteVisual(node, used ? 0x0125 : 0x0124, Vector2.Zero, visuals);
                break;
            case 0xB9:
                if (!AddMap16SpriteVisual(node, 0x0125, Vector2.Zero, visuals))
                {
                    AddFallbackBlockVisual(node, "!", new Color(0.92f, 0.60f, 0.18f, 1.0f), visuals);
                }
                break;
            case 0xC7:
                AddBubbleVisual(node, visuals);
                break;
            case 0x8E:
                AddWarpHoleVisual(node, visuals);
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

    private static void AddWarpHoleVisual(Node2D node, List<Node> visuals)
    {
        visuals.Add(AddDebugRect(node, new Rect2(2, 2, 12, 12), new Color(1.0f, 0.76f, 0.26f, 0.92f), 2));
        var label = new Label
        {
            Text = "Warp",
            Position = new Vector2(17, -2),
            ZIndex = 3,
        };
        label.AddThemeFontSizeOverride("font_size", 10);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        node.AddChild(label);
        visuals.Add(label);
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
            0xDA or 0xDB or 0xDC or 0xDD or 0xDF => ShellOamTilesFor(spriteId),
            0x4F or 0x50 => JumpingPiranhaOamTiles,
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
        new(0, -4, 0x06, 0x0B, 3, true),
        new(0, 0, 0x2D, 0x0B, 3, true),
        new(4, 0, 0x2D, 0x4B, 3, true),
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
        new(-1, -4, 0x5D, 0x46, 0, false),
        new(-9, -12, 0xC6, 0x46, 0, true),
        new(9, -4, 0x5D, 0x06, 0, false),
        new(9, -12, 0xC6, 0x06, 0, true),
    ];

    private static readonly SpriteOamTile[] JumpingPiranhaOamTiles =
    [
        new(8, -1, 0xAC, 0x58, 1, true),
        new(8, 7, 0xCE, 0x5B, 1, true),
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
            0x9F => new SpriteActorBehavior(new Rect2(0, 0, 64, 64), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: -1.35f),
            0x95 => new SpriteActorBehavior(new Rect2(0, -4, 20, 36), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -0.22f),
            0xAB => new SpriteActorBehavior(new Rect2(-4, -15, 20, 31), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -0.42f),
            0xBD => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: -0.58f),
            0xDA or 0xDB or 0xDC or 0xDD or 0xDF => new SpriteActorBehavior(new Rect2(0, 0, 16, 16), CanInteract: true, Stompable: true, TerrainCollision: true, Gravity: true, InitialXSpeed: 0.0f),
            0x4F or 0x50 => new SpriteActorBehavior(new Rect2(8, -16, 16, 32), CanInteract: true, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
            _ => new SpriteActorBehavior(new Rect2(0, 0, SpriteActorWidth, SpriteActorHeight), CanInteract: false, Stompable: false, TerrainCollision: false, Gravity: false, InitialXSpeed: 0.0f),
        };
    }

    private static SpriteActorBehavior SquishedRexBehavior(float currentXSpeed)
    {
        return new SpriteActorBehavior(
            new Rect2(0, 0, 16, 16),
            CanInteract: true,
            Stompable: true,
            TerrainCollision: true,
            Gravity: true,
            InitialXSpeed: MathF.Sign(currentXSpeed == 0.0f ? -1.0f : currentXSpeed) * 0.84f);
    }

    private static Color SpriteActorColor(int spriteId)
    {
        return spriteId switch
        {
            0x4F => new Color(0.92f, 0.88f, 0.18f, 1.0f),
            0x83 => new Color(0.74f, 0.20f, 0.18f, 1.0f),
            0x8E => new Color(0.18f, 0.76f, 0.28f, 1.0f),
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
                    Rect = new Rect2(TileToWorld(tile.X, tile.Y), new Vector2(Map16TileSize, Map16TileSize * 2)),
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

    private void AddGeneratedCollision(bool debugVisible)
    {
        _solids.Clear();
        _solidStepUpEnabled.Clear();
        _solidVerticalEnabled.Clear();
        _slopes.Clear();
        _diagonalPipeBodyCells.Clear();
        _diagonalPipeCeilingCells.Clear();

        var solidTiles = new HashSet<(int X, int Y)>();
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
            else if (IsSolidMap16Source(tile.Source))
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
            AddSolid(rect, new Color(0.05f, 0.85f, 0.20f, 0.10f), debugVisible);
        }

        foreach (var slope in BuildSlopeSurfaces(slopeTiles))
        {
            AddSlope(slope, debugVisible);
        }
    }

    private bool TryBreakSpinJumpTurnBlocks(SmwPhysics.PlayerState previousState)
    {
        if (!_state.SpinJump ||
            _state.Powerup == SmwPhysics.SmallPowerup ||
            !_state.OnGround ||
            previousState.YSpeed < 0)
        {
            return false;
        }

        var playerLeft = _state.XFloat + 1.0f;
        var playerRight = _state.XFloat + SmwPhysics.PlayerWidth - 1.0f;
        var probeY = _state.YFloat + SmwPhysics.PlayerHeightFor(_state) + 1.0f;
        var tileY = WorldToTileY(probeY);
        var minTileX = WorldToTileX(playerLeft);
        var maxTileX = WorldToTileX(playerRight);
        var broken = 0;
        for (var tileX = minTileX; tileX <= maxTileX; tileX++)
        {
            if (!_map16TilesByCoord.TryGetValue((tileX, tileY), out var tile) ||
                !IsSpinJumpBreakableTurnBlock(tile))
            {
                continue;
            }

            BreakMap16Tile(tile);
            broken++;
        }

        if (broken == 0)
        {
            return false;
        }

        _state.OnGround = false;
        _state.YSpeed = 8;
        _state.SubYSpeed = 0;
        _blockBreakCount += broken;
        AddGeneratedCollision(debugVisible: false);
        _audio?.PlaySpinJump();
        GD.Print(
            $"smw-runtime: block_break level={_currentLevelId} count={broken} total={_blockBreakCount} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} tile_y={tileY}");
        return true;
    }

    private void BreakMap16Tile(PlacedMap16Tile tile)
    {
        _placedTiles.RemoveAll(candidate => candidate.X == tile.X && candidate.Y == tile.Y);
        _map16TilesByCoord.Remove((tile.X, tile.Y));
        _map16Layer?.HideTile(tile.X, tile.Y);
    }

    private static bool IsSpinJumpBreakableTurnBlock(PlacedMap16Tile tile)
    {
        return tile.Source == "std_generic_08" || tile.Map16 == 0x011E;
    }

    private void ResolveDiagonalPipeTileContacts(SmwPhysics.PlayerState previousState)
    {
        if (_diagonalPipeBodyCells.Count == 0 && _diagonalPipeCeilingCells.Count == 0)
        {
            return;
        }

        ResolveDiagonalPipeCeilingIntrusion();
        ResolveDiagonalPipeBodyIntrusion(previousState);
        SmwPhysics.ClampHorizontalLevelBounds(ref _state, 0, (int)MathF.Round(GetLevelPixelRight()));
    }

    private bool ResolveDiagonalPipeCeilingIntrusion()
    {
        if (_diagonalPipeCeilingCells.Count == 0)
        {
            return false;
        }

        var height = SmwPhysics.PlayerHeightFor(_state);
        var left = _state.XFloat;
        var right = left + SmwPhysics.PlayerWidth;
        var top = _state.YFloat;
        var bottom = top + height;
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

        var height = SmwPhysics.PlayerHeightFor(_state);
        var left = _state.XFloat;
        var right = left + SmwPhysics.PlayerWidth;
        var top = _state.YFloat;
        var bottom = top + height;
        var previousLeft = previousState.XFloat;
        var previousRight = previousLeft + SmwPhysics.PlayerWidth;
        var tileMinX = WorldToTileX(left);
        var tileMaxX = WorldToTileX(right);
        var tileMinY = WorldToTileY(top);
        var tileMaxY = WorldToTileY(bottom);
        float? bestTarget = null;
        var bestCorrection = float.MaxValue;

        for (var tileY = tileMinY; tileY <= tileMaxY; tileY++)
        {
            for (var tileX = tileMinX; tileX <= tileMaxX; tileX++)
            {
                if (!_diagonalPipeBodyCells.Contains((tileX, tileY)))
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

    private static List<SmwPhysics.SlopeSurface> BuildSlopeSurfaces(IReadOnlyList<PlacedMap16Tile> slopeTiles)
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

    private static bool TryBuildSlopeTileSurface(PlacedMap16Tile tile, out SmwPhysics.SlopeSurface slope)
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
            slope = new SmwPhysics.SlopeSurface(x0, y1, x1, y0, Ceiling: true);
            return true;
        }

        if (IsSlopeUpRightTile(tile))
        {
            slope = new SmwPhysics.SlopeSurface(x0, y1, x1, y0);
            return true;
        }

        if (IsSlopeDownRightTile(tile))
        {
            slope = new SmwPhysics.SlopeSurface(x0, y0, x1, y1);
            return true;
        }

        slope = default;
        return false;
    }

    private static bool TryBuildStandardSlopeTileSurface(
        PlacedMap16Tile tile,
        float x0,
        float y0,
        float x1,
        out SmwPhysics.SlopeSurface slope)
    {
        if (TryGetStandardSlopeOffsets(tile.Map16, out var leftYOffset, out var rightYOffset))
        {
            slope = new SmwPhysics.SlopeSurface(x0, y0 + leftYOffset, x1, y0 + rightYOffset);
            return true;
        }

        slope = default;
        return false;
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
            "right_diagonal_pipe" => tile.Map16 is 0x01C4 or 0x01C5 or 0x01C7 or 0x01EB,
            _ => false,
        };
    }

    private static bool IsDiagonalPipeCeilingTile(PlacedMap16Tile tile)
    {
        return tile.Source == "right_diagonal_pipe" &&
            tile.Map16 is 0x01C6 or 0x01EF or 0x015C;
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

        return source.Contains("ledge", StringComparison.Ordinal) ||
            source.Contains("ground", StringComparison.Ordinal) ||
            source.Contains("mushroom", StringComparison.Ordinal) ||
            source.Contains("pipe", StringComparison.Ordinal) ||
            source.Contains("slope", StringComparison.Ordinal) ||
            source.StartsWith("std_generic_", StringComparison.Ordinal);
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
            "right_diagonal_pipe" => tile.Map16 is 0x01C4 or 0x01C5 or 0x01C7 or 0x01EB ||
                IsDiagonalPipeCeilingTile(tile),
            "left_diagonal_ledge_edge" => MatchesAdjustedSlopeTile(tile.Map16, 0x01AA),
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

    private void AddSolid(Rect2 rect, Color color, bool debugVisible, bool allowStepUp = true, bool allowVertical = true)
    {
        _solids.Add(rect);
        _solidStepUpEnabled.Add(allowStepUp);
        _solidVerticalEnabled.Add(allowVertical);
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
                _pipeEntrances.Add(new PipeEntrance(new Rect2(topLeft.X, topLeft.Y - 32, 32, 48), screen, Horizontal: false, Kind: "vertical"));
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
                _pipeEntrances.Add(new PipeEntrance(new Rect2(topLeft.X - 24, topLeft.Y, 48, 32), screen, Horizontal: true, Kind: "horizontal"));
            }
        }
    }

    private void AddDiagonalPipeEntrance(int screen)
    {
        var diagonalTiles = new HashSet<(int X, int Y)>();
        foreach (var tile in _placedTiles)
        {
            if (tile.Source == "right_diagonal_pipe")
            {
                diagonalTiles.Add((tile.X, tile.Y));
            }
        }

        if (diagonalTiles.Count == 0)
        {
            return;
        }

        var visited = new HashSet<(int X, int Y)>();
        List<(int X, int Y)>? bestCluster = null;
        foreach (var tile in diagonalTiles)
        {
            if (visited.Contains(tile))
            {
                continue;
            }

            var cluster = FloodDiagonalPipeCluster(tile, diagonalTiles, visited);
            if (!cluster.Exists(cell => cell.X / 16 == screen))
            {
                continue;
            }

            if (bestCluster == null || MaxTileX(cluster) > MaxTileX(bestCluster))
            {
                bestCluster = cluster;
            }
        }

        if (bestCluster == null)
        {
            return;
        }

        var maxX = bestCluster.Max(cell => cell.X);
        var minY = bestCluster.Min(cell => cell.Y);
        var topLeft = TileToWorld(maxX - 2, minY);
        var rect = new Rect2(
            topLeft,
            new Vector2(Map16TileSize * 3.0f, Map16TileSize * 3.0f));
        _pipeEntrances.Add(new PipeEntrance(rect, screen, Horizontal: false, Kind: "diagonal"));
    }

    private static List<(int X, int Y)> FloodDiagonalPipeCluster(
        (int X, int Y) start,
        HashSet<(int X, int Y)> diagonalTiles,
        HashSet<(int X, int Y)> visited)
    {
        var cluster = new List<(int X, int Y)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(start);
        visited.Add(start);

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            cluster.Add(tile);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    var next = (tile.X + dx, tile.Y + dy);
                    if (!diagonalTiles.Contains(next) || !visited.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }
        }

        return cluster;
    }

    private static int MaxTileX(List<(int X, int Y)> cluster)
    {
        return cluster.Max(cell => cell.X);
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
        var height = SmwPhysics.PlayerHeightFor(_state);
        if (_playerHitboxGizmo != null)
        {
            SetLineRect(_playerHitboxGizmo, new Rect2(Vector2.Zero, new Vector2(SmwPhysics.PlayerWidth, height)));
        }
        if (_playerFootGizmo != null)
        {
            _playerFootGizmo.Position = new Vector2(SmwPhysics.PlayerWidth * 0.5f - 1.0f, height - 2.0f);
        }
        if (_playerDebugLabel != null)
        {
            _playerDebugLabel.Text = $"pow={_state.Powerup} h={height} g={(_state.OnGround ? 1 : 0)} duck={(_state.Ducking ? 1 : 0)}";
            _playerDebugLabel.Position = new Vector2(-8.0f, -18.0f);
        }
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

    private bool UpdateSpriteActors(Rect2 previousPlayerRect)
    {
        var playerAdjusted = false;
        if (_playerHurtCooldown > 0)
        {
            _playerHurtCooldown--;
        }

        if (_spriteActors.Count == 0)
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
            UpdateSpriteActorMotion(actor);
            if (IsSolidBlockSprite(actor.SpriteId))
            {
                playerAdjusted |= ResolvePlayerSolidBlockActorCollision(actor, previousPlayerRect);
                actor.Node.Position = new Vector2(actor.X, actor.Y);
                continue;
            }

            var handledPlayerCollision = ResolvePlayerSpriteActorCollision(actor, _physics.PlayerRect(_state));
            actor.Node.Position = new Vector2(actor.X, actor.Y);
            if (handledPlayerCollision)
            {
                return true;
            }
        }

        return playerAdjusted;
    }

    private void UpdateSpriteActorMotion(RuntimeSpriteActor actor)
    {
        var actorRect = actor.Rect;
        if (MathF.Abs(actorRect.GetCenter().X - (_cameraX + LogicalViewportWidth * 0.5f)) > LogicalViewportWidth + 80.0f)
        {
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

        actor.X += actor.XSpeed;
        var rect = actor.Rect;
        if (actor.Behavior.TerrainCollision)
        {
            foreach (var solid in _solids)
            {
                if (!rect.Intersects(solid))
                {
                    continue;
                }

                if (actor.XSpeed > 0)
                {
                    actor.X = solid.Position.X - actor.Behavior.Hitbox.Position.X - actor.Behavior.Hitbox.Size.X;
                }
                else if (actor.XSpeed < 0)
                {
                    actor.X = solid.Position.X + solid.Size.X - actor.Behavior.Hitbox.Position.X;
                }
                actor.XSpeed = -actor.XSpeed;
                rect = actor.Rect;
            }
        }

        if (actor.Behavior.Gravity)
        {
            actor.YSpeed = MathF.Min(SpriteActorMaxFall, actor.YSpeed + SpriteActorGravity);
        }
        actor.Y += actor.YSpeed;
        actor.OnGround = false;
        rect = actor.Rect;
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
                    actor.Y = solid.Position.Y - actor.Behavior.Hitbox.Position.Y - actor.Behavior.Hitbox.Size.Y;
                    actor.YSpeed = 0.0f;
                    actor.OnGround = true;
                }
                else
                {
                    actor.Y = solid.Position.Y + solid.Size.Y - actor.Behavior.Hitbox.Position.Y;
                    actor.YSpeed = 0.0f;
                }
                rect = actor.Rect;
            }
        }

        if (actor.Behavior.TerrainCollision)
        {
            rect = actor.Rect;
            var probeX = rect.GetCenter().X;
            var bottom = rect.Position.Y + rect.Size.Y;
            if (SmwPhysics.TryResolveFloorSlopeFromAbove(
                probeX,
                rect.Position.Y,
                bottom,
                bottom,
                actor.YSpeed,
                _slopes,
                aboveTolerance: 8.0f,
                belowTolerance: 16.0f,
                out var slopeY))
            {
                actor.Y = slopeY - actor.Behavior.Hitbox.Position.Y - actor.Behavior.Hitbox.Size.Y;
                actor.YSpeed = 0.0f;
                actor.OnGround = true;
            }
        }

        if (actor.Y > GetLevelPixelBottom() + 128.0f)
        {
            actor.Alive = false;
        }
    }

    private static void UpdateWingedQuestionBlockMotion(RuntimeSpriteActor actor)
    {
        var frame = actor.MotionFrame % WingedQuestionBlockCycleFrames;
        actor.MotionFrame = (actor.MotionFrame + 1) % WingedQuestionBlockCycleFrames;
        var wave = frame < WingedQuestionBlockCycleFrames / 2
            ? frame
            : WingedQuestionBlockCycleFrames - frame;
        actor.State = wave < WingedQuestionBlockCycleFrames / 4 ? 0 : 1;
        actor.Y = actor.HomeY + (wave - WingedQuestionBlockCycleFrames / 4) / 4.0f;
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

    private bool ResolvePlayerSolidBlockActorCollision(RuntimeSpriteActor actor, Rect2 previousPlayerRect)
    {
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

        if (_state.YSpeed >= 0 && previousBottom <= previousActorTop + 6.0f)
        {
            LandPlayerOnSolidBlockActor(actor, actorTop);
            return true;
        }

        if (_state.YSpeed < 0 && previousTop >= previousActorBottom - 6.0f)
        {
            _state.Y = (int)MathF.Round(actorBottom);
            _state.SubY = 0;
            _state.YSpeed = 8;
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            if (!TriggerSolidBlockActorReward(actor))
            {
                _lastActorEvent = $"block:{actor.SpriteId:X2}:bump";
            }
            _audio?.PlayJump();
            return true;
        }

        if (_state.XSpeed > 0 && previousRight <= actorLeft + 6.0f)
        {
            _state.X = (int)MathF.Round(actorLeft - SmwPhysics.PlayerWidth);
            _state.SubX = 0;
            _state.XSpeed = 0;
            _state.SubXSpeed = 0;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
            return true;
        }

        if (_state.XSpeed < 0 && previousLeft >= actorRight - 6.0f)
        {
            _state.X = (int)MathF.Round(actorRight);
            _state.SubX = 0;
            _state.XSpeed = 0;
            _state.SubXSpeed = 0;
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
            _state.Y = (int)MathF.Round(actorBottom);
            _state.SubY = 0;
            _state.YSpeed = Math.Max(8, _state.YSpeed);
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            if (!TriggerSolidBlockActorReward(actor))
            {
                _lastActorEvent = $"block:{actor.SpriteId:X2}:bump";
            }
        }
        else if (overlapFromLeft < overlapFromRight)
        {
            _state.X = (int)MathF.Round(actorLeft - SmwPhysics.PlayerWidth);
            _state.SubX = 0;
            _state.XSpeed = 0;
            _state.SubXSpeed = 0;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
        }
        else
        {
            _state.X = (int)MathF.Round(actorRight);
            _state.SubX = 0;
            _state.XSpeed = 0;
            _state.SubXSpeed = 0;
            _lastActorEvent = $"block:{actor.SpriteId:X2}:side";
        }

        return true;
    }

    private bool TriggerSolidBlockActorReward(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0x83 || actor.Used)
        {
            return false;
        }

        actor.Used = true;
        var contentIndex = WorldTileXNibble(actor.X) & 0x03;
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
                _coinCount++;
                break;
            case 1:
                _physics.SetPowerup(
                    ref _state,
                    _state.Powerup == SmwPhysics.SmallPowerup ? SmwPhysics.BigPowerup : SmwPhysics.FirePowerup);
                _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
                UpdatePlayerGraphic(force: true);
                break;
            case 2:
                _physics.SetPowerup(ref _state, SmwPhysics.CapePowerup);
                _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
                UpdatePlayerGraphic(force: true);
                break;
            case 3:
                _oneUpCount++;
                break;
        }

        ReplaceSpriteActorVisuals(actor);
        _audio?.PlaySample(9);
        _lastActorEvent = $"block:{actor.SpriteId:X2}:reward:{reward}";
        GD.Print(
            $"smw-runtime: block_reward level={_currentLevelId} sprite={actor.SpriteId:X2} reward={reward} " +
            $"x={actor.X:0.00} y={actor.Y:0.00} coins={_coinCount} oneups={_oneUpCount} pow={_state.Powerup}");
        return true;
    }

    private static int WorldTileXNibble(float x)
    {
        return ((int)MathF.Floor(x / Map16TileSize)) & 0x0F;
    }

    private void LandPlayerOnSolidBlockActor(RuntimeSpriteActor actor, float actorTop)
    {
        _state.Y = (int)MathF.Round(actorTop - SmwPhysics.PlayerHeightFor(_state));
        _state.SubY = 0;
        _state.YSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = true;
        _state.X += (int)MathF.Round(actor.X - actor.PreviousX);
        _lastActorEvent = $"block:{actor.SpriteId:X2}:top";
    }

    private bool ResolvePlayerSpriteActorCollision(RuntimeSpriteActor actor, Rect2 playerRect)
    {
        var actorRect = actor.Rect;
        if (!actor.Alive ||
            !actor.Behavior.CanInteract ||
            (IsJumpingPiranhaSprite(actor.SpriteId) && actor.State == 0) ||
            !playerRect.Intersects(actorRect))
        {
            return false;
        }

        var playerBottom = _state.YFloat + SmwPhysics.PlayerHeightFor(_state);
        var stomped = actor.Behavior.Stompable && _state.YSpeed > 0 && playerBottom <= actorRect.Position.Y + 10.0f;
        if (stomped)
        {
            if (TryStompRex(actor))
            {
                BoostPlayerAfterSpriteStomp();
                return true;
            }

            actor.Alive = false;
            _lastActorEvent = $"stomp:{actor.SpriteId:X2}:dead";
            BoostPlayerAfterSpriteStomp();
            return true;
        }

        HurtPlayerFromActor(actor);
        return true;
    }

    private bool TryStompRex(RuntimeSpriteActor actor)
    {
        if (actor.SpriteId != 0xAB || actor.State != 0)
        {
            return false;
        }

        var oldBottom = actor.Rect.Position.Y + actor.Rect.Size.Y;
        actor.State = 1;
        actor.Behavior = SquishedRexBehavior(actor.XSpeed);
        actor.Y = oldBottom - actor.Behavior.Hitbox.Position.Y - actor.Behavior.Hitbox.Size.Y;
        actor.XSpeed = actor.Behavior.InitialXSpeed;
        actor.Body.Position = actor.Behavior.Hitbox.Position;
        actor.Body.Size = actor.Behavior.Hitbox.Size;
        ReplaceSpriteActorVisuals(actor);
        _lastActorEvent = "stomp:AB:1";
        return true;
    }

    private void BoostPlayerAfterSpriteStomp()
    {
        _state.YSpeed = -48;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _audio?.PlaySpinJump();
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
        var actorRect = actor.Rect;
        if (_playerHurtCooldown > 0)
        {
            return;
        }

        _lastActorEvent = $"hurt:{actor.SpriteId:X2}:{actor.State}";
        _playerHurtCooldown = PlayerHurtCooldownFrames;
        if (_state.Powerup > SmwPhysics.SmallPowerup)
        {
            _physics.SetPowerup(ref _state, SmwPhysics.SmallPowerup);
            UpdatePlayerGraphic(force: true);
        }
        _state.XSpeed = _state.XFloat < actorRect.GetCenter().X ? -24 : 24;
        _state.YSpeed = -32;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _audio?.PlayJump();
    }

    private void CheckCoinPickups()
    {
        if (_coinPickups.Count == 0)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        foreach (var pickup in _coinPickups)
        {
            if (pickup.Collected || !playerRect.Intersects(pickup.Rect))
            {
                continue;
            }

            CollectCoin(pickup);
        }
    }

    private void CollectCoin(CoinPickup pickup)
    {
        pickup.Collected = true;
        foreach (var tile in pickup.Tiles)
        {
            _map16Layer?.HideTile(tile.X, tile.Y);
            _map16TilesByCoord.Remove((tile.X, tile.Y));
        }

        _coinCount++;
        if (pickup.DragonCoin)
        {
            _dragonCoinCount++;
        }

        _audio?.PlaySample(9);
        GD.Print(
            $"smw-runtime: coin_pickup level={_currentLevelId} " +
            $"dragon={(pickup.DragonCoin ? 1 : 0)} coins={_coinCount} dragon_coins={_dragonCoinCount} " +
            $"x={pickup.Rect.Position.X:0.00} y={pickup.Rect.Position.Y:0.00}");
    }

    private void CheckGoalTape()
    {
        if (_courseClear || _goalTapeTriggers.Count == 0)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        foreach (var trigger in _goalTapeTriggers)
        {
            if (!playerRect.Intersects(trigger))
            {
                continue;
            }

            TriggerCourseClear();
            return;
        }
    }

    private void TriggerCourseClear()
    {
        _courseClear = true;
        _state.XSpeed = 0;
        _state.SubXSpeed = 0;
        _audio?.PlayMusicPreview("Credits");
        ShowCourseClearLabel();
        GD.Print($"smw-runtime: course_clear level={_currentLevelId}");
    }

    private void BuildPlayer()
    {
        _playerHitboxGizmo = null;
        _playerFootGizmo = null;
        _playerDebugLabel = null;
        _player = new Node2D
        {
            Name = "MarioPlayer",
            Position = new Vector2(_state.XFloat, _state.YFloat),
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

    private bool TryBuildPlayerSprites()
    {
        const string playerAtlasPath = "res://generated/smw/player/gfx32_player_palette0.png";
        if (_player == null ||
            !HasPlayerOamMetadata() ||
            !FileAccess.FileExists(playerAtlasPath))
        {
            return false;
        }

        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(playerAtlasPath));
        if (image == null || image.IsEmpty())
        {
            return false;
        }

        _playerTexture = ImageTexture.CreateFromImage(image);

        for (var i = 0; i < PlayerOamSpriteSlots; i++)
        {
            var sprite = new Sprite2D
            {
                Texture = _playerTexture,
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
            return;
        }

        var pose = ChoosePlayerPose();
        var nativeFacing = _state.Facing == 0 ? 0 : 1;
        if (!force &&
            pose == _lastPlayerPose &&
            nativeFacing == _lastPlayerFacing &&
            _state.Powerup == _lastPlayerPowerup &&
            _state.Ducking == _lastPlayerDucking)
        {
            return;
        }

        _lastPlayerPose = pose;
        _lastPlayerFacing = nativeFacing;
        _lastPlayerPowerup = _state.Powerup;
        _lastPlayerDucking = _state.Ducking;
        RenderPlayerOamPose(pose, _state.Powerup, nativeFacing);
    }

    private int ChoosePlayerPose()
    {
        if (_playerAnimTimer > 0)
        {
            _playerAnimTimer--;
        }

        if (!_state.OnGround)
        {
            return _state.SpinJump ? 4 : 6;
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

        foreach (var sprite in _playerTileSprites)
        {
            sprite.Visible = false;
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

            var tile = ResolvePlayerDynamicTile(descriptor, headBase, bodyBase);
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
        return powerup == SmwPhysics.SmallPowerup || ducking
            ? SmwPhysics.SmallPlayerHeight - SmwPhysics.BigPlayerHeight
            : 0;
    }

    private static int ResolvePlayerDynamicTile(int descriptor, int headPointer, int bodyPointer)
    {
        return descriptor switch
        {
            0 => PlayerPointerToSourceTile(headPointer),
            1 => PlayerPointerToSourceTile(headPointer) + 1,
            2 => PlayerPointerToSourceTile(bodyPointer),
            3 => PlayerPointerToSourceTile(bodyPointer) + 1,
            _ => descriptor,
        };
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
        sprite.Position = new Vector2(x, y);
        sprite.FlipH = flipH;
        sprite.RegionRect = new Rect2(
            (tile % 16) * 8,
            (tile / 16) * 8,
            spriteSize,
            spriteSize);
        sprite.Visible = true;
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
        var state = _physics.MakeState(
            (int)MathF.Round(entrance.Position.X),
            (int)MathF.Round(entrance.Position.Y),
            DefaultPlayerPowerup);
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
                StartEntranceMotion(entrance.EntranceSettings, 28, new Vector2(0.0f, 1.0f), ref state);
                break;
            case 6:
                state.X |= 8;
                state.Y |= 2;
                StartEntranceMotion(entrance.EntranceSettings, 32, new Vector2(4.0f, -4.0f), ref state);
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
            _state.XSpeed = 0;
            _state.YSpeed = 0;
            _state.SubXSpeed = 0;
            _state.SubYSpeed = 0;
            GD.Print($"smw-runtime: entrance_motion_done action={_entranceMotionAction} x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
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
        entrance = new LevelEntrance(
            FormatLevelId(levelId),
            new Vector2(NativeEntranceX(xIndex), NativeEntranceY(yIndex) + LevelVisualYOffset),
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
        entrance = new LevelEntrance(
            FormatLevelId(targetLevel),
            new Vector2(NativeEntranceX(xIndex), NativeEntranceY(yIndex) + LevelVisualYOffset),
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
        _hud = null;
        _courseClearLabel = null;
        if (!DebugOverlays)
        {
            BuildCourseClearLayer();
            return;
        }

        var layer = new CanvasLayer
        {
            Name = "HudLayer",
        };
        _hudLayer = layer;
        AddChild(layer);
        _hud = new Label { Position = new Vector2(12, 10) };
        _hud.AddThemeFontSizeOverride("font_size", 13);
        layer.AddChild(_hud);
        AddAssetPreviewOverlay(layer);
        UpdateHud();
        BuildCourseClearLabel(layer);
    }

    private void BuildCourseClearLayer()
    {
        var layer = new CanvasLayer
        {
            Name = "CourseClearLayer",
        };
        _courseClearLayer = layer;
        AddChild(layer);
        BuildCourseClearLabel(layer);
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
        if (_hud == null)
        {
            return;
        }

        var footTile = DescribeFootTile();
        _hud.Text = $"x={_state.XFloat:000000.00} y={_state.YFloat:000000.00} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} pow={_state.Powerup} h={SmwPhysics.PlayerHeightFor(_state)} g={(_state.OnGround ? 1 : 0)} d={(_state.Ducking ? 1 : 0)} " +
            $"cam={_cameraX:0000},{_cameraY:0000} tiles={_placedTiles.Count} solids={_solids.Count} slopes={_slopes.Count} " +
            $"coins={_coinCount}/{_dragonCoinCount} tile={footTile} exits={_screenExits.Count} sprites={_levelSprites.Count}/{_spriteActors.Count} player={_playerTileSprites.Count}";
    }

    private string DescribeFootTile()
    {
        var footX = (int)MathF.Floor((_state.XFloat + SmwPhysics.PlayerWidth * 0.5f) / Map16TileSize);
        var footY = (int)MathF.Floor((_state.YFloat + SmwPhysics.PlayerHeightFor(_state) - LevelVisualYOffset + 1.0f) / Map16TileSize);
        if (!_map16TilesByCoord.TryGetValue((footX, footY), out var tile))
        {
            return $"{footX},{footY}:----";
        }

        var role = IsSlopeSurfaceTile(tile) ? "slope" :
            IsCoinMarkerTile(tile) ? "coin" :
            IsSolidMap16Source(tile.Source) ? "solid" :
            "pass";
        return $"{footX},{footY}:{tile.Map16:X3}:{role}:{tile.Source}";
    }

    private void PrintRuntimeState()
    {
        var layer2Bg = FileAccess.FileExists(_levelLayer2BackgroundPath) ? 1 : 0;
        GD.Print($"smw-runtime: level={_currentLevelId} layer1_objects={_levelObjects.Count} layer2_objects={_layer2Objects.Count} layer2_bg={layer2Bg} map16_tiles={_placedTiles.Count} collision_rects={_solids.Count} slope_surfaces={_slopes.Count} pipe_cells={_diagonalPipeBodyCells.Count}/{_diagonalPipeCeilingCells.Count} coin_pickups={_coinPickups.Count} screen_exits={_screenExits.Count} pipe_rects={_pipeEntrances.Count} sprite_spawns={_levelSprites.Count} sprite_actors={_spriteActors.Count} goal_tapes={_goalTapeTriggers.Count} player_sprites={_playerTileSprites.Count}");
    }

    public void DebugEnterLevel(string levelId)
    {
        EnterLevel(levelId);
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
        _entranceMotionFrames = 0;
        _entranceMotionAction = 0;
        _entranceMotionPixelsPerFrame = Vector2.Zero;
        _pipeTransitionLatch = false;
        _state.X = (int)MathF.Round(position.X);
        _state.Y = (int)MathF.Round(position.Y);
        _state.SubX = 0;
        _state.SubY = 0;
        _state.XSpeed = 0;
        _state.YSpeed = 0;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _cameraInitialized = false;
        UpdateCamera();
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-spawn: x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
    }

    public void DebugSetPlayerPowerup(int powerup)
    {
        _physics.SetPowerup(ref _state, powerup);
        _playerWalkingFrame = Math.Min(_playerWalkingFrame, WalkingPoseCountForPowerup(_state.Powerup));
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        UpdatePlayerGraphic(force: true);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-powerup: powerup={_state.Powerup} height={SmwPhysics.PlayerHeightFor(_state)} render_y={PlayerRenderYOffsetForState(_state.Powerup, _state.Ducking)}");
    }

    public void DebugSetPlayerVelocity(int xSpeed, int ySpeed)
    {
        _state.XSpeed = xSpeed;
        _state.YSpeed = ySpeed;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        GD.Print($"smw-test-velocity: xs={_state.XSpeed} ys={_state.YSpeed}");
    }

    public void DebugSetPlayerSpinJump(bool spinJump)
    {
        _state.SpinJump = spinJump;
        GD.Print($"smw-test-spinjump: spin={(spinJump ? 1 : 0)}");
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
            [' ', '\t', ',', ':', ';'],
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
            case "spawn":
            case "pos":
                RequirePartCount(parts, 3);
                DebugSetPlayerPosition(new Vector2(ParseFloat(parts[1]), ParseFloat(parts[2])));
                return BuildDebugState("spawn");
            case "powerup":
            case "pow":
                RequirePartCount(parts, 2);
                DebugSetPlayerPowerup(ParseDebugPowerup(parts[1]));
                return BuildDebugState("powerup");
            case "velocity":
            case "vel":
                RequirePartCount(parts, 3);
                DebugSetPlayerVelocity(
                    int.Parse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture),
                    int.Parse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture));
                return BuildDebugState("velocity");
            case "spinjump":
            case "spinstate":
                RequirePartCount(parts, 2);
                DebugSetPlayerSpinJump(ParseDebugBool(parts[1]));
                return BuildDebugState("spinjump");
            case "level":
                RequirePartCount(parts, 2);
                DebugEnterLevel(parts[1].ToUpperInvariant());
                return BuildDebugState("level");
            case "screen_exit":
            case "exit":
                RequirePartCount(parts, 2);
                DebugEnterScreenExit(ParseHexOrDecimalDebug(parts[1]));
                return BuildDebugState("screen_exit");
            case "script":
                RequirePartCount(parts, 2);
                DebugLoadInputScript(parts[1]);
                return "ok script_loaded";
            case "capture":
                RequirePartCount(parts, 2);
                var frames = parts.Length >= 3 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var captureFrames)
                    ? Math.Max(1, captureFrames)
                    : 1;
                DebugCaptureViewport(parts[1], frames, quitAfterCapture: false);
                return $"ok capture_scheduled={parts[1]} frames={frames}";
            case "state":
                return PrintDebugState(parts.Length >= 2 ? parts[1] : "manual");
            case "quit":
                GD.Print("smw-debug: quit");
                GetTree().Quit();
                return "ok quit";
            default:
                throw new FormatException($"unknown command '{parts[0]}'");
        }
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
        GD.Print($"smw-debug: input frames={frames} left={(input.Left ? 1 : 0)} right={(input.Right ? 1 : 0)} down={(input.Down ? 1 : 0)} jump={(input.Jump ? 1 : 0)} spin={(input.Spin ? 1 : 0)} run={(input.Run ? 1 : 0)}");
    }

    private string PrintDebugState(string tag)
    {
        var state = BuildDebugState(tag);
        GD.Print(state);
        return state;
    }

    private string BuildDebugState(string tag)
    {
        var nearestActor = DescribeNearestActor();
        return
            $"smw-debug-state: tag={tag} frame={_debugFrameCounter} level={_currentLevelId} " +
            $"paused={(_debugPaused ? 1 : 0)} queued={_debugStepFrames} " +
            $"x={_state.XFloat:0.00} y={_state.YFloat:0.00} xs={_state.XSpeed} ys={_state.YSpeed} " +
            $"pow={_state.Powerup} h={SmwPhysics.PlayerHeightFor(_state)} g={(_state.OnGround ? 1 : 0)} duck={(_state.Ducking ? 1 : 0)} " +
            $"cam={_cameraX:0.00},{_cameraY:0.00} tile={DescribeFootTile()} solids={_solids.Count} slopes={_slopes.Count} " +
            $"actors={_spriteActors.Count} near={nearestActor} actor_event={_lastActorEvent} blocks={_blockBreakCount}";
    }

    private string DescribeNearestActor()
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

        return nearest == null
            ? "none"
            : $"{nearest.SpriteId:X2}:{nearest.State}:{nearest.X:0.00},{nearest.Y:0.00}";
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

        var parts = line.Split(
            [' ', '\t', ',', ':', ';'],
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
            case "l":
                input.Left = true;
                break;
            case "right":
            case "r":
                input.Right = true;
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

    private void CaptureViewportNow(string capturePath, bool quitAfterCapture)
    {
        var image = GetViewport().GetTexture()?.GetImage();
        if (image == null)
        {
            GD.PrintErr($"smw-capture: failed path={capturePath} reason=viewport_texture_unavailable level={_currentLevelId}");
            if (quitAfterCapture)
            {
                GetTree().Quit(1);
            }
            return;
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
    }

    private void EnterLevel(string levelId, LevelEntrance? entrance = null)
    {
        if (!LoadLevelData(levelId))
        {
            GD.PrintErr($"smw-runtime: unable to load level {levelId}");
            return;
        }

        _courseClear = false;
        _playerHurtCooldown = 0;
        _lastActorEvent = "none";
        _blockBreakCount = 0;
        _state = MakeInitialPlayerState(entrance);
        ResetPlayerAnimationState();
        _cameraInitialized = false;
        UpdateCamera();
        BuildWorld();
        BuildHud();
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        _lastPlayerPose = -1;
        _lastPlayerPowerup = -1;
        _lastPlayerDucking = false;
        UpdatePlayerGraphic(force: true);
        PrintRuntimeState();
        StartLevelMusic();
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
        _pipeTransitionLatch = true;

        var playerRect = _physics.PlayerRect(_state);
        PipeEntrance? matchedEntrance = null;
        foreach (var pipeEntrance in _pipeEntrances)
        {
            if (pipeEntrance.Horizontal && !sidePressed)
            {
                continue;
            }
            if (!pipeEntrance.Horizontal && !downPressed)
            {
                continue;
            }

            if (playerRect.Intersects(pipeEntrance.Rect))
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
            GD.Print(
                $"pipe-debug screen={screen:X2} target={entrance.LevelId} " +
                $"secondary={(entrance.Secondary ? 1 : 0)} source={entrance.SourceId:X3} kind={matchedEntrance.Value.Kind}");
            EnterLevel(entrance.LevelId, entrance);
        }
        else
        {
            GD.PrintErr($"pipe-debug screen={screen:X2} unresolved level={_currentLevelId}");
        }
    }

    private void UpdateCamera()
    {
        var maxCameraX = MathF.Max(0.0f, GetLevelPixelRight() - LogicalViewportWidth);
        var maxCameraY = MathF.Max(0.0f, GetLevelPixelBottom() - LogicalViewportHeight);
        if (!_cameraInitialized)
        {
            _cameraX = Math.Clamp(_state.XFloat - CameraHorizontalAnchor, 0.0f, maxCameraX);
            _cameraY = Math.Clamp(_state.YFloat - CameraVerticalLower, 0.0f, maxCameraY);
            _cameraInitialized = true;
        }

        var playerScreenX = _state.XFloat - _cameraX;
        if (playerScreenX < CameraHorizontalAnchor - CameraHorizontalBand)
        {
            _cameraX -= CameraHorizontalAnchor - CameraHorizontalBand - playerScreenX;
        }
        else if (playerScreenX > CameraHorizontalAnchor + CameraHorizontalBand)
        {
            _cameraX += playerScreenX - (CameraHorizontalAnchor + CameraHorizontalBand);
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
