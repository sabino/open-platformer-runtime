using Godot;
using System;
using System.Collections.Generic;

public partial class GameScene : Node2D
{
    private const float LevelVisualYOffset = -64.0f;
    private const int Map16TileSize = 16;
    private const int Map16AtlasColumns = 16;
    private const int PlayerOamSpriteSlots = 8;
    private const int BigMarioPowerup = 1;
    private const float LogicalViewportWidth = 256.0f;
    private const float LogicalViewportHeight = 224.0f;

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
    private string _levelLayer2BackgroundPath = "res://generated/smw/levels/level_105_layer2_background.png";
    private float _cameraX;
    private float _cameraY;
    private int _lastPlayerPose = -1;
    private int _lastPlayerFacing = -1;
    private bool _pipeTransitionLatch;

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

        _physics.Step(ref _state, frameInput, _solids, _slopes);
        UpdateCamera();

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
        var slopeTiles = new HashSet<(int X, int Y)>();
        foreach (var tile in _placedTiles)
        {
            if (IsSlopeSurfaceSource(tile.Source))
            {
                slopeTiles.Add((tile.X, tile.Y));
            }
            else if (IsSolidMap16Source(tile.Source))
            {
                solidTiles.Add((tile.X, tile.Y));
            }
        }

        foreach (var rect in BuildMergedSolidRects(solidTiles))
        {
            AddSolid(rect, new Color(0.05f, 0.85f, 0.20f, 0.10f), debugVisible: true);
        }

        foreach (var slope in BuildSlopeSurfaces(slopeTiles))
        {
            AddSlope(slope, debugVisible: true);
        }
    }

    private static List<SmwPhysics.SlopeSurface> BuildSlopeSurfaces(HashSet<(int X, int Y)> slopeTiles)
    {
        var slopes = new List<SmwPhysics.SlopeSurface>();
        foreach (var component in ConnectedTileComponents(slopeTiles))
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            foreach (var tile in component)
            {
                minX = Math.Min(minX, tile.X);
                maxX = Math.Max(maxX, tile.X);
                minY = Math.Min(minY, tile.Y);
                maxY = Math.Max(maxY, tile.Y);
            }

            if (minX == int.MaxValue)
            {
                continue;
            }

            slopes.Add(new SmwPhysics.SlopeSurface(
                minX * Map16TileSize,
                (maxY + 1) * Map16TileSize + LevelVisualYOffset,
                (maxX + 1) * Map16TileSize,
                minY * Map16TileSize + LevelVisualYOffset));
        }

        return slopes;
    }

    private static List<List<(int X, int Y)>> ConnectedTileComponents(HashSet<(int X, int Y)> tiles)
    {
        var pending = new HashSet<(int X, int Y)>(tiles);
        var components = new List<List<(int X, int Y)>>();
        while (pending.Count > 0)
        {
            var start = FirstTile(pending);
            pending.Remove(start);
            var component = new List<(int X, int Y)>();
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                component.Add(tile);
                foreach (var neighbor in NeighborTiles(tile))
                {
                    if (pending.Remove(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
            components.Add(component);
        }

        return components;
    }

    private static (int X, int Y) FirstTile(HashSet<(int X, int Y)> tiles)
    {
        foreach (var tile in tiles)
        {
            return tile;
        }

        return (0, 0);
    }

    private static IEnumerable<(int X, int Y)> NeighborTiles((int X, int Y) tile)
    {
        yield return (tile.X + 1, tile.Y);
        yield return (tile.X - 1, tile.Y);
        yield return (tile.X, tile.Y + 1);
        yield return (tile.X, tile.Y - 1);
        yield return (tile.X + 1, tile.Y + 1);
        yield return (tile.X - 1, tile.Y - 1);
        yield return (tile.X + 1, tile.Y - 1);
        yield return (tile.X - 1, tile.Y + 1);
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

    private static bool IsSlopeSurfaceSource(string source)
    {
        return source.Contains("diagonal_pipe", StringComparison.Ordinal) ||
            source.Contains("diagonal_ledge", StringComparison.Ordinal) ||
            source.Contains("steep_right_slope", StringComparison.Ordinal);
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
        if (!force && pose == _lastPlayerPose && nativeFacing == _lastPlayerFacing)
        {
            return;
        }

        _lastPlayerPose = pose;
        _lastPlayerFacing = nativeFacing;
        RenderPlayerOamPose(pose, BigMarioPowerup, nativeFacing);
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
            $"xs={_state.XSpeed} ys={_state.YSpeed} cam={_cameraX:0000},{_cameraY:0000} tiles={_placedTiles.Count} solids={_solids.Count} " +
            $"exits={_screenExits.Count} sprites={_levelSprites.Count} player={_playerTileSprites.Count}";
    }

    private void PrintRuntimeState()
    {
        var layer2Bg = FileAccess.FileExists(_levelLayer2BackgroundPath) ? 1 : 0;
        GD.Print($"smw-runtime: level={_currentLevelId} layer1_objects={_levelObjects.Count} layer2_objects={_layer2Objects.Count} layer2_bg={layer2Bg} map16_tiles={_placedTiles.Count} collision_rects={_solids.Count} slope_surfaces={_slopes.Count} screen_exits={_screenExits.Count} pipe_rects={_pipeEntrances.Count} sprite_spawns={_levelSprites.Count} player_sprites={_playerTileSprites.Count}");
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
        UpdateCamera();
        if (_player != null)
        {
            _player.Position = new Vector2(_state.XFloat, _state.YFloat);
        }
        UpdateHud();
        GD.Print($"smw-test-spawn: x={_state.XFloat:0.00} y={_state.YFloat:0.00}");
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

        _state = MakeInitialPlayerState();
        UpdateCamera();
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

    private void UpdateCamera()
    {
        var maxCameraX = MathF.Max(0.0f, GetLevelPixelRight() - LogicalViewportWidth);
        var maxCameraY = MathF.Max(0.0f, GetLevelPixelBottom() - LogicalViewportHeight);
        _cameraX = Math.Clamp(_state.XFloat - 160.0f, 0.0f, maxCameraX);
        _cameraY = Math.Clamp(_state.YFloat - 176.0f, 0.0f, maxCameraY);
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
