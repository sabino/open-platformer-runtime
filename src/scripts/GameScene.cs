using Godot;
using System;
using System.Collections.Generic;

public partial class GameScene : Node2D
{
    private const float LevelVisualYOffset = -64.0f;
    private const int Map16TileSize = 16;
    private const int Map16AtlasColumns = 16;

    private readonly SmwPhysics _physics = new();
    private readonly List<Rect2> _solids = [];
    private readonly List<Godot.Collections.Dictionary> _screenExits = [];
    private readonly List<Godot.Collections.Dictionary> _levelObjects = [];
    private readonly List<SpriteSpawn> _levelSprites = [];
    private readonly List<PlacedMap16Tile> _placedTiles = [];
    private readonly List<PipeEntrance> _pipeEntrances = [];
    private readonly List<int> _headTilePointers = [];
    private readonly List<int> _bodyTilePointers = [];
    private readonly List<Sprite2D> _playerTileSprites = [];

    private SmwPhysics.PlayerState _state;
    private Node2D? _player;
    private Label? _hud;
    private CanvasLayer? _hudLayer;
    private Node2D? _worldRoot;
    private SmwAudio? _audio;
    private ImageTexture? _playerTexture;
    private string _currentLevelId = "105";
    private string _levelGfxAtlasPath = "res://generated/smw/tilesets/level_105_tileset7_8x8.png";
    private string _levelMap16AtlasPath = "res://generated/smw/tilesets/level_105_tileset7_map16_preview.png";
    private string _levelSpriteAtlasPath = "res://generated/smw/spritesets/level_105_spritegfx8_8x8.png";
    private string _levelLayoutPreviewPath = "res://generated/smw/levels/level_105_partial_layout.png";
    private string _levelTilemapPath = "res://generated/smw/levels/level_105_partial_tilemap.json";
    private float _cameraX;
    private int _lastPlayerPose = -1;
    private bool _pipeTransitionLatch;

    public override void _Ready()
    {
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
        var frameInput = new SmwPhysics.FrameInput
        {
            Left = Input.IsActionPressed("smw_left"),
            Right = Input.IsActionPressed("smw_right"),
            Down = Input.IsActionPressed("smw_down"),
            Jump = Input.IsActionPressed("smw_jump"),
            JumpPressed = Input.IsActionJustPressed("smw_jump"),
            SpinPressed = Input.IsActionJustPressed("smw_spin"),
            Run = Input.IsActionPressed("smw_run"),
        };

        if (_state.OnGround && frameInput.JumpPressed)
        {
            _audio?.PlayJump();
        }
        else if (_state.OnGround && frameInput.SpinPressed)
        {
            _audio?.PlaySpinJump();
        }

        _physics.Step(ref _state, frameInput, _solids);
        _cameraX = MathF.Max(0.0f, _state.XFloat - 160.0f);
        Position = new Vector2(-MathF.Round(_cameraX), 0);

        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        UpdatePlayerGraphic();
        UpdateHud();
        CheckPipeDebug();
    }

    private readonly record struct PlacedMap16Tile(int X, int Y, int Map16, string Source);
    private readonly record struct SpriteSpawn(int X, int Y, int Screen, int SpriteId, int ExtraBits, int Offset);
    private readonly record struct PipeEntrance(Rect2 Rect, int Screen);

    private void LoadAssetPack()
    {
        LoadPlayerGraphicsMetadata();
        LoadLevelData(_currentLevelId);
    }

    private bool LoadLevelData(string levelId)
    {
        _screenExits.Clear();
        _levelObjects.Clear();
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
            var screen = (screenY >> 4) & 0x0F;
            var y = (screenY & 0x0F) * 16 + (int)LevelVisualYOffset;
            var x = screen * 256 + xId;
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
            _placedTiles.Add(new PlacedMap16Tile(x, y, map16, source));
        }
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
        StartWorldRoot();

        if (AddGeneratedMap16Tiles())
        {
            AddGeneratedCollision();
        }
        else
        {
            AddGeneratedLevelPreview();
            AddSolid(new Rect2(0, 192, 3584, 64), new Color(0.20f, 0.55f, 0.25f, 0.22f), debugVisible: true);
            AddSolid(new Rect2(240, 160, 48, 32), new Color(0.55f, 0.42f, 0.20f, 0.22f), debugVisible: true);
            AddSolid(new Rect2(368, 144, 64, 48), new Color(0.20f, 0.48f, 0.22f, 0.22f), debugVisible: true);
        }

        RebuildPipeEntrances();
        AddPipeMarkers();
        AddObjectMarkers();
        AddSpriteMarkers();

        for (var i = 0; i < 20; i++)
        {
            AddScreenLine(i);
        }
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
        foreach (var tile in _placedTiles)
        {
            if (IsSolidMap16Source(tile.Source))
            {
                solidTiles.Add((tile.X, tile.Y));
            }
        }

        foreach (var rect in BuildMergedSolidRects(solidTiles))
        {
            AddSolid(rect, new Color(0.05f, 0.85f, 0.20f, 0.10f), debugVisible: true);
        }
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
        return source.Contains("ledge", StringComparison.Ordinal) ||
            source.Contains("ground", StringComparison.Ordinal) ||
            source.Contains("mushroom", StringComparison.Ordinal) ||
            source.Contains("pipe", StringComparison.Ordinal) ||
            source.Contains("slope", StringComparison.Ordinal) ||
            source.StartsWith("std_generic_", StringComparison.Ordinal);
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
            Color = color,
            Position = rect.Position,
            Size = rect.Size,
        };
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
            foreach (var tile in _placedTiles)
            {
                if (tile.X / 16 != screen || !tile.Source.Contains("vertical_pipe_top_left", StringComparison.Ordinal))
                {
                    continue;
                }

                var topLeft = TileToWorld(tile.X, tile.Y);
                _pipeEntrances.Add(new PipeEntrance(new Rect2(topLeft.X, topLeft.Y - 32, 32, 48), screen));
                break;
            }
        }
    }

    private void AddScreenLine(int index)
    {
        var line = new ColorRect
        {
            Color = new Color(1, 1, 1, 0.14f),
            Position = new Vector2(index * 256, 0),
            Size = new Vector2(1, 224),
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

    private void BuildPlayer()
    {
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
                Size = new Vector2(SmwPhysics.PlayerWidth, SmwPhysics.PlayerHeight),
            });
            return;
        }

        UpdatePlayerGraphic(force: true);
    }

    private bool TryBuildPlayerSprites()
    {
        const string playerAtlasPath = "res://generated/smw/player/gfx32_player_palette0.png";
        if (_player == null ||
            _headTilePointers.Count == 0 ||
            _bodyTilePointers.Count == 0 ||
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
        var offsets = new[]
        {
            new Vector2(-1, 0),
            new Vector2(7, 0),
            new Vector2(-1, 8),
            new Vector2(7, 8),
            new Vector2(-1, 16),
            new Vector2(7, 16),
            new Vector2(-1, 24),
            new Vector2(7, 24),
        };

        foreach (var offset in offsets)
        {
            var sprite = new Sprite2D
            {
                Texture = _playerTexture,
                RegionEnabled = true,
                Centered = false,
                Position = offset,
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
        if (!force && pose == _lastPlayerPose)
        {
            UpdatePlayerFacing();
            return;
        }

        _lastPlayerPose = pose;
        var headTile = _headTilePointers[Math.Clamp(pose, 0, _headTilePointers.Count - 1)];
        var bodyTile = _bodyTilePointers[Math.Clamp(pose, 0, _bodyTilePointers.Count - 1)];
        SetPlayerTileBlock(0, headTile);
        SetPlayerTileBlock(4, bodyTile);
        UpdatePlayerFacing();
    }

    private int ChoosePlayerPose()
    {
        if (!_state.OnGround)
        {
            return _state.SpinJump ? 4 : 6;
        }

        if (Math.Abs(_state.XSpeed) >= 4)
        {
            return 1 + (int)((Time.GetTicksMsec() / 110) % 3);
        }

        return 0;
    }

    private void SetPlayerTileBlock(int spriteIndex, int topLeftTile)
    {
        SetPlayerTile(spriteIndex, topLeftTile);
        SetPlayerTile(spriteIndex + 1, topLeftTile + 1);
        SetPlayerTile(spriteIndex + 2, topLeftTile + 16);
        SetPlayerTile(spriteIndex + 3, topLeftTile + 17);
    }

    private void SetPlayerTile(int spriteIndex, int tile)
    {
        if (spriteIndex < 0 || spriteIndex >= _playerTileSprites.Count)
        {
            return;
        }

        _playerTileSprites[spriteIndex].RegionRect = new Rect2(
            (tile % 16) * 8,
            (tile / 16) * 8,
            8,
            8);
    }

    private void UpdatePlayerFacing()
    {
        foreach (var sprite in _playerTileSprites)
        {
            sprite.FlipH = _state.Facing == 0;
        }
    }

    private SmwPhysics.PlayerState MakeInitialPlayerState()
    {
        foreach (var tile in _placedTiles)
        {
            if (tile.X >= 8 &&
                (tile.Source.Contains("ledge_top", StringComparison.Ordinal) ||
                    tile.Source.Contains("mushroom_top", StringComparison.Ordinal) ||
                    tile.Source.Contains("horizontal_pipe", StringComparison.Ordinal)))
            {
                return _physics.MakeState(
                    tile.X * Map16TileSize + Map16TileSize,
                    (int)(tile.Y * Map16TileSize + LevelVisualYOffset - SmwPhysics.PlayerHeight));
            }
        }

        return _physics.MakeState(64, 64);
    }

    private static Vector2 TileToWorld(int x, int y)
    {
        return new Vector2(x * Map16TileSize, y * Map16TileSize + LevelVisualYOffset);
    }

    private void BuildHud()
    {
        _hudLayer?.QueueFree();
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
            $"xs={_state.XSpeed} ys={_state.YSpeed} tiles={_placedTiles.Count} solids={_solids.Count} " +
            $"exits={_screenExits.Count} sprites={_levelSprites.Count} player={_playerTileSprites.Count}";
    }

    private void PrintRuntimeState()
    {
        GD.Print($"smw-runtime: level={_currentLevelId} map16_tiles={_placedTiles.Count} collision_rects={_solids.Count} screen_exits={_screenExits.Count} pipe_rects={_pipeEntrances.Count} sprite_spawns={_levelSprites.Count} player_sprites={_playerTileSprites.Count}");
    }

    public void DebugEnterLevel(string levelId)
    {
        EnterLevel(levelId);
    }

    private void EnterLevel(string levelId)
    {
        if (!LoadLevelData(levelId))
        {
            GD.PrintErr($"smw-runtime: unable to load level {levelId}");
            return;
        }

        _state = MakeInitialPlayerState();
        _cameraX = MathF.Max(0.0f, _state.XFloat - 160.0f);
        Position = new Vector2(-MathF.Round(_cameraX), 0);
        BuildWorld();
        BuildHud();
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }

        _lastPlayerPose = -1;
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
}
