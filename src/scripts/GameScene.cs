using Godot;
using System;
using System.Collections.Generic;

public partial class GameScene : Node2D
{
    private const float LevelVisualYOffset = -64.0f;
    private const int Map16TileSize = 16;
    private const int Map16AtlasColumns = 16;
    private const int PlayerOamSpriteSlots = 8;
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
    private const int GoalTapeSpriteId = 0x7B;
    private const int DefaultPlayerPowerup = SmwPhysics.BigPowerup;

    private readonly SmwPhysics _physics = new();
    private readonly List<Rect2> _solids = [];
    private readonly List<SmwPhysics.SlopeSurface> _slopes = [];
    private readonly List<Godot.Collections.Dictionary> _screenExits = [];
    private readonly List<Godot.Collections.Dictionary> _levelObjects = [];
    private readonly List<Godot.Collections.Dictionary> _layer2Objects = [];
    private readonly List<SpriteSpawn> _levelSprites = [];
    private readonly List<PlacedMap16Tile> _placedTiles = [];
    private readonly List<PipeEntrance> _pipeEntrances = [];
    private readonly List<int> _headTilePointers = [];
    private readonly List<int> _bodyTilePointers = [];
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
    private readonly List<Rect2> _goalTapeTriggers = [];

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
    private string _currentLevelId = "105";
    private string _levelGfxAtlasPath = "res://generated/smw/tilesets/level_105_tileset7_8x8.png";
    private string _levelMap16AtlasPath = "res://generated/smw/tilesets/level_105_tileset7_map16_preview.png";
    private string _levelSpriteAtlasPath = "res://generated/smw/spritesets/level_105_spritegfx8_8x8.png";
    private string _levelLayoutPreviewPath = "res://generated/smw/levels/level_105_partial_layout.png";
    private string _levelTilemapPath = "res://generated/smw/levels/level_105_partial_tilemap.json";
    private string _levelLayer2BackgroundPath = "res://generated/smw/levels/level_105_layer2_background.png";
    private float _cameraX;
    private float _cameraY;
    private bool _cameraInitialized;
    private int _lastPlayerPose = -1;
    private int _lastPlayerFacing = -1;
    private int _lastPlayerPowerup = -1;
    private bool _pipeTransitionLatch;
    private int _playerHurtCooldown;
    private bool _courseClear;

    public bool DebugOverlays { get; set; }

    public override void _Ready()
    {
        GetViewport().TransparentBg = false;
        RenderingServer.SetDefaultClearColor(new Color(0.0f, 0.39f, 0.74f, 1.0f));
        _audio = new SmwAudio { Name = "SmwAudio" };
        AddChild(_audio);
        LoadAssetPack();
        _state = MakeInitialPlayerState();
        BuildWorld();
        BuildPlayer();
        BuildHud();
        PrintRuntimeState();
    }

    public override void _PhysicsProcess(double delta)
    {
        var frameInput = _courseClear
            ? new SmwPhysics.FrameInput()
            : new SmwPhysics.FrameInput
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

        if (_state.OnGround && frameInput.SpinPressed)
        {
            _audio?.PlaySpinJump();
        }
        else if (_state.OnGround && frameInput.JumpPressed)
        {
            _audio?.PlayJump();
        }

        _physics.Step(ref _state, frameInput, _solids, _slopes, 0, (int)MathF.Round(GetLevelPixelRight()));
        UpdateCamera();

        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        UpdatePlayerGraphic();
        UpdateSpriteActors();
        CheckGoalTape();
        UpdateHud();
        UpdateDebugGizmos();
        CheckPipeDebug();
    }

    private readonly record struct PlacedMap16Tile(int X, int Y, int Map16, string Source);
    private readonly record struct SpriteSpawn(int X, int Y, int Screen, int SpriteId, int ExtraBits, int Offset);
    private readonly record struct PipeEntrance(Rect2 Rect, int Screen);
    private sealed class RuntimeSpriteActor
    {
        public required Node2D Node { get; init; }
        public required ColorRect Body { get; init; }
        public int SpriteId { get; init; }
        public float X { get; set; }
        public float Y { get; set; }
        public float XSpeed { get; set; }
        public float YSpeed { get; set; }
        public bool Alive { get; set; } = true;
        public bool OnGround { get; set; }
        public int WakeScreen { get; init; }
        public Rect2 Rect => new(X, Y, SpriteActorWidth, SpriteActorHeight);
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
            if (tileset.TryGetValue("atlas_png", out var atlasVariant))
            {
                _levelGfxAtlasPath = $"res://generated/smw/{atlasVariant.AsString()}";
            }
            if (tileset.TryGetValue("map16_preview_png", out var map16Variant))
            {
                _levelMap16AtlasPath = $"res://generated/smw/{map16Variant.AsString()}";
            }
        }

        if (level.TryGetValue("sprite_tileset_assets", out var spriteTilesetVariant) &&
            spriteTilesetVariant.VariantType == Variant.Type.Dictionary)
        {
            var spriteTileset = spriteTilesetVariant.AsGodotDictionary();
            if (spriteTileset.TryGetValue("atlas_png", out var atlasVariant))
            {
                _levelSpriteAtlasPath = $"res://generated/smw/{atlasVariant.AsString()}";
            }
        }

        if (level.TryGetValue("layout_preview", out var layoutVariant) && layoutVariant.VariantType == Variant.Type.Dictionary)
        {
            var layout = layoutVariant.AsGodotDictionary();
            if (layout.TryGetValue("file", out var tilemapVariant))
            {
                _levelTilemapPath = $"res://generated/smw/{tilemapVariant.AsString()}";
            }
            if (layout.TryGetValue("preview_png", out var previewVariant))
            {
                _levelLayoutPreviewPath = $"res://generated/smw/{previewVariant.AsString()}";
            }
        }

        if (level.TryGetValue("layer2_background", out var layer2Variant) &&
            layer2Variant.VariantType == Variant.Type.Dictionary)
        {
            var layer2 = layer2Variant.AsGodotDictionary();
            if (layer2.TryGetValue("preview_png", out var previewVariant))
            {
                _levelLayer2BackgroundPath = $"res://generated/smw/{previewVariant.AsString()}";
            }
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
        _slopes.Clear();
        _spriteActors.Clear();
        _goalTapeTriggers.Clear();
        _cameraGizmo?.QueueFree();
        _cameraGizmo = null;
        StartWorldRoot();
        AddWorldBackground();
        AddLayer2BackgroundPreview();

        if (AddGeneratedMap16Tiles())
        {
            AddGeneratedCollision();
        }
        else
        {
            AddGeneratedLevelPreview();
            AddSolid(new Rect2(0, 192, 3584, 64), new Color(0.20f, 0.55f, 0.25f, 0.22f), DebugOverlays);
            AddSolid(new Rect2(240, 160, 48, 32), new Color(0.55f, 0.42f, 0.20f, 0.22f), DebugOverlays);
            AddSolid(new Rect2(368, 144, 64, 48), new Color(0.20f, 0.48f, 0.22f, 0.22f), DebugOverlays);
        }

        RebuildPipeEntrances();
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

    private static bool IsRuntimeEnemySprite(int spriteId)
    {
        return spriteId is 0x4F or 0x83 or 0x8E or 0x9F or 0xAB or 0xB9 or 0xBD or 0xC7;
    }

    private static RuntimeSpriteActor CreateRuntimeSpriteActor(SpriteSpawn spawn, bool debugOverlays)
    {
        var color = SpriteActorColor(spawn.SpriteId);
        var node = new Node2D
        {
            Name = $"Sprite_{spawn.SpriteId:X2}_{spawn.Offset:X2}",
            Position = new Vector2(spawn.X, spawn.Y - SpriteActorHeight),
            ZIndex = 6,
        };
        var body = new ColorRect
        {
            Color = color,
            Position = Vector2.Zero,
            Size = new Vector2(SpriteActorWidth, SpriteActorHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        node.AddChild(body);
        if (debugOverlays)
        {
            AddRectOutline(
                node,
                new Rect2(Vector2.Zero, new Vector2(SpriteActorWidth, SpriteActorHeight)),
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
            XSpeed = InitialSpriteActorSpeed(spawn.SpriteId),
            WakeScreen = spawn.Screen,
        };
    }

    private static float InitialSpriteActorSpeed(int spriteId)
    {
        return spriteId switch
        {
            0x4F or 0x8E or 0x9F or 0xB9 or 0xBD or 0xC7 => -0.58f,
            0xAB => -0.42f,
            _ => 0.0f,
        };
    }

    private static Color SpriteActorColor(int spriteId)
    {
        return spriteId switch
        {
            0x4F => new Color(0.92f, 0.88f, 0.18f, 1.0f),
            0x83 => new Color(0.74f, 0.20f, 0.18f, 1.0f),
            0x8E => new Color(0.18f, 0.76f, 0.28f, 1.0f),
            0x9F => new Color(0.20f, 0.38f, 0.88f, 1.0f),
            0xAB => new Color(0.76f, 0.32f, 0.16f, 1.0f),
            0xB9 => new Color(0.88f, 0.30f, 0.80f, 1.0f),
            0xBD => new Color(0.16f, 0.50f, 0.96f, 1.0f),
            0xC7 => new Color(0.90f, 0.90f, 0.90f, 1.0f),
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
        var container = new Node2D
        {
            Name = "GeneratedMap16Tiles",
            ZIndex = -10,
        };
        AddWorldChild(container);

        foreach (var tile in _placedTiles)
        {
            if (tile.Map16 < 0)
            {
                continue;
            }

            var region = new Rect2(
                (tile.Map16 % Map16AtlasColumns) * Map16TileSize,
                (tile.Map16 / Map16AtlasColumns) * Map16TileSize,
                Map16TileSize,
                Map16TileSize);
            if (region.Position.Y + Map16TileSize > image.GetHeight())
            {
                continue;
            }

            var sprite = new Sprite2D
            {
                Texture = texture,
                RegionEnabled = true,
                RegionRect = region,
                Position = TileToWorld(tile.X, tile.Y),
                Centered = false,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            container.AddChild(sprite);
        }

        return true;
    }

    private void AddGeneratedCollision()
    {
        var solidTiles = new HashSet<(int X, int Y)>();
        var slopeTiles = new List<PlacedMap16Tile>();
        foreach (var tile in _placedTiles)
        {
            if (IsSlopeSurfaceTile(tile))
            {
                slopeTiles.Add(tile);
            }
            else if (IsSolidMap16Source(tile.Source))
            {
                solidTiles.Add((tile.X, tile.Y));
            }
        }

        foreach (var rect in BuildMergedSolidRects(solidTiles))
        {
            AddSolid(rect, new Color(0.05f, 0.85f, 0.20f, 0.10f), DebugOverlays);
        }

        foreach (var slope in BuildSlopeSurfaces(slopeTiles))
        {
            AddSlope(slope, DebugOverlays);
        }
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

    private static bool IsSlopeUpRightTile(PlacedMap16Tile tile)
    {
        return tile.Source switch
        {
            "left_diagonal_ledge_edge" => tile.Map16 == 0x01AA,
            "right_diagonal_pipe" => tile.Map16 is 0x01C4 or 0x01C7 or 0x01EB,
            _ => false,
        };
    }

    private static bool IsSlopeDownRightTile(PlacedMap16Tile tile)
    {
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
            source.Contains("steep_right_slope", StringComparison.Ordinal);
    }

    private static bool IsSlopeSurfaceTile(PlacedMap16Tile tile)
    {
        return tile.Source switch
        {
            "right_diagonal_pipe" => tile.Map16 is 0x01C4 or 0x01C7 or 0x01EB,
            "left_diagonal_ledge_edge" => tile.Map16 == 0x01AA,
            "steep_right_slope_edge" => tile.Map16 == 0x01AF,
            _ => false,
        };
    }

    private static bool IsCoinMarkerTile(PlacedMap16Tile tile)
    {
        return tile.Source.Contains("coin", StringComparison.OrdinalIgnoreCase) ||
            tile.Map16 is 0x002B or 0x002C or 0x002D or 0x002E;
    }

    private static bool IsDebugBlockMarkerTile(PlacedMap16Tile tile)
    {
        return tile.Source.StartsWith("std_generic_", StringComparison.Ordinal) ||
            tile.Source.Contains("switch", StringComparison.OrdinalIgnoreCase) ||
            tile.Source.Contains("goal_marker", StringComparison.OrdinalIgnoreCase) ||
            tile.Source.Contains("midway", StringComparison.OrdinalIgnoreCase);
    }

    private void AddSolid(Rect2 rect, Color color, bool debugVisible)
    {
        _solids.Add(rect);
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
            DefaultColor = new Color(1.0f, 0.15f, 0.65f, 0.75f),
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
            var node = new ColorRect
            {
                Name = "PipeDebug",
                Color = new Color(0.10f, 0.75f, 0.22f, 0.65f),
                Position = entrance.Rect.Position,
                Size = entrance.Rect.Size,
            };
            AddWorldChild(node);
            AddRectOutline(
                _worldRoot ?? this,
                entrance.Rect,
                new Color(0.20f, 1.0f, 0.40f, 0.9f),
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
                _pipeEntrances.Add(new PipeEntrance(new Rect2(topLeft.X, topLeft.Y - 32, 32, 48), screen));
            }
        }
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
            _playerDebugLabel.Text = $"pow={_state.Powerup} h={height} g={(_state.OnGround ? 1 : 0)}";
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

    private void UpdateSpriteActors()
    {
        if (_playerHurtCooldown > 0)
        {
            _playerHurtCooldown--;
        }

        if (_spriteActors.Count == 0)
        {
            return;
        }

        var playerRect = _physics.PlayerRect(_state);
        for (var i = _spriteActors.Count - 1; i >= 0; i--)
        {
            var actor = _spriteActors[i];
            if (!actor.Alive)
            {
                actor.Node.QueueFree();
                _spriteActors.RemoveAt(i);
                continue;
            }

            UpdateSpriteActorMotion(actor);
            ResolvePlayerSpriteActorCollision(actor, playerRect);
            actor.Node.Position = new Vector2(actor.X, actor.Y);
        }
    }

    private void UpdateSpriteActorMotion(RuntimeSpriteActor actor)
    {
        if (MathF.Abs((actor.X + SpriteActorWidth * 0.5f) - (_cameraX + LogicalViewportWidth * 0.5f)) > LogicalViewportWidth + 48.0f)
        {
            return;
        }

        actor.X += actor.XSpeed;
        var rect = actor.Rect;
        foreach (var solid in _solids)
        {
            if (!rect.Intersects(solid))
            {
                continue;
            }

            if (actor.XSpeed > 0)
            {
                actor.X = solid.Position.X - SpriteActorWidth;
            }
            else if (actor.XSpeed < 0)
            {
                actor.X = solid.Position.X + solid.Size.X;
            }
            actor.XSpeed = -actor.XSpeed;
            rect = actor.Rect;
        }

        actor.YSpeed = MathF.Min(SpriteActorMaxFall, actor.YSpeed + SpriteActorGravity);
        actor.Y += actor.YSpeed;
        actor.OnGround = false;
        rect = actor.Rect;
        foreach (var solid in _solids)
        {
            if (!rect.Intersects(solid))
            {
                continue;
            }

            if (actor.YSpeed >= 0)
            {
                actor.Y = solid.Position.Y - SpriteActorHeight;
                actor.YSpeed = 0.0f;
                actor.OnGround = true;
            }
            else
            {
                actor.Y = solid.Position.Y + solid.Size.Y;
                actor.YSpeed = 0.0f;
            }
            rect = actor.Rect;
        }

        if (actor.Y > GetLevelPixelBottom() + 128.0f)
        {
            actor.Alive = false;
        }
    }

    private void ResolvePlayerSpriteActorCollision(RuntimeSpriteActor actor, Rect2 playerRect)
    {
        if (!actor.Alive || !playerRect.Intersects(actor.Rect))
        {
            return;
        }

        var playerBottom = _state.YFloat + SmwPhysics.PlayerHeightFor(_state);
        var stomped = _state.YSpeed > 0 && playerBottom <= actor.Y + 10.0f;
        if (stomped)
        {
            actor.Alive = false;
            _state.YSpeed = -48;
            _state.SubYSpeed = 0;
            _state.OnGround = false;
            _audio?.PlaySpinJump();
            return;
        }

        if (_playerHurtCooldown > 0)
        {
            return;
        }

        _playerHurtCooldown = PlayerHurtCooldownFrames;
        if (_state.Powerup > SmwPhysics.SmallPowerup)
        {
            _physics.SetPowerup(ref _state, SmwPhysics.SmallPowerup);
            UpdatePlayerGraphic(force: true);
        }
        _state.XSpeed = _state.XFloat < actor.X ? -24 : 24;
        _state.YSpeed = -32;
        _state.SubXSpeed = 0;
        _state.SubYSpeed = 0;
        _state.OnGround = false;
        _audio?.PlayJump();
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
        if (!force && pose == _lastPlayerPose && nativeFacing == _lastPlayerFacing && _state.Powerup == _lastPlayerPowerup)
        {
            return;
        }

        _lastPlayerPose = pose;
        _lastPlayerFacing = nativeFacing;
        _lastPlayerPowerup = _state.Powerup;
        RenderPlayerOamPose(pose, _state.Powerup, nativeFacing);
    }

    private int ChoosePlayerPose()
    {
        if (!_state.OnGround)
        {
            return _state.SpinJump ? 4 : 6;
        }

        if (Math.Abs(_state.XSpeed) >= 4)
        {
            var walkFrame = (int)((Time.GetTicksMsec() / 110) % 3);
            return 2 - walkFrame;
        }

        return 0;
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
            SetPlayerOamSprite(slot, tile, _playerXDisp[dispIndex], _playerYDisp[dispIndex], large, flipH);
        }
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

    private SmwPhysics.PlayerState MakeInitialPlayerState()
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

        _hud.Text = $"x={_state.XFloat:000000.00} y={_state.YFloat:000000.00} " +
            $"xs={_state.XSpeed} ys={_state.YSpeed} pow={_state.Powerup} h={SmwPhysics.PlayerHeightFor(_state)} g={(_state.OnGround ? 1 : 0)} " +
            $"cam={_cameraX:0000},{_cameraY:0000} tiles={_placedTiles.Count} solids={_solids.Count} slopes={_slopes.Count} " +
            $"exits={_screenExits.Count} sprites={_levelSprites.Count}/{_spriteActors.Count} player={_playerTileSprites.Count}";
    }

    private void PrintRuntimeState()
    {
        var layer2Bg = FileAccess.FileExists(_levelLayer2BackgroundPath) ? 1 : 0;
        GD.Print($"smw-runtime: level={_currentLevelId} layer1_objects={_levelObjects.Count} layer2_objects={_layer2Objects.Count} layer2_bg={layer2Bg} map16_tiles={_placedTiles.Count} collision_rects={_solids.Count} slope_surfaces={_slopes.Count} screen_exits={_screenExits.Count} pipe_rects={_pipeEntrances.Count} sprite_spawns={_levelSprites.Count} sprite_actors={_spriteActors.Count} goal_tapes={_goalTapeTriggers.Count} player_sprites={_playerTileSprites.Count}");
    }

    public void DebugEnterLevel(string levelId)
    {
        EnterLevel(levelId);
    }

    public void DebugSetPlayerPosition(Vector2 position)
    {
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
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        UpdatePlayerGraphic(force: true);
        UpdateHud();
        UpdateDebugGizmos();
        GD.Print($"smw-test-powerup: powerup={_state.Powerup} height={SmwPhysics.PlayerHeightFor(_state)}");
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

    private void EnterLevel(string levelId)
    {
        if (!LoadLevelData(levelId))
        {
            GD.PrintErr($"smw-runtime: unable to load level {levelId}");
            return;
        }

        _courseClear = false;
        _playerHurtCooldown = 0;
        _state = MakeInitialPlayerState();
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
        UpdatePlayerGraphic(force: true);
        PrintRuntimeState();
    }

    private void CheckPipeDebug()
    {
        if (!Input.IsActionPressed("smw_down"))
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
        foreach (var entrance in _pipeEntrances)
        {
            if (playerRect.Intersects(entrance.Rect))
            {
                matchedEntrance = entrance;
                break;
            }
        }
        if (matchedEntrance == null)
        {
            return;
        }

        var screen = matchedEntrance.Value.Screen;
        Godot.Collections.Dictionary? exitData = null;
        foreach (var entry in _screenExits)
        {
            if (entry.TryGetValue("screen", out var screenVariant) && screenVariant.AsInt32() == screen)
            {
                exitData = entry;
                break;
            }
        }

        GD.Print($"pipe-debug screen={screen:X2} exit={Json.Stringify(exitData ?? new Godot.Collections.Dictionary())}");
        if (exitData != null &&
            exitData.TryGetValue("vanilla_destination", out var destinationVariant))
        {
            EnterLevel($"{destinationVariant.AsInt32():X3}");
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
