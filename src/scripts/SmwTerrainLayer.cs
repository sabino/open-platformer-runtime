using Godot;
using System;
using System.Collections.Generic;

namespace OpenPlatformerRuntime;

/// <summary>Godot tile rendering backed by the ROM's decoded Map16 atlas.</summary>
public partial class SmwTerrainLayer : TileMapLayer
{
    private const int TileSize = 16;
    private const int AtlasColumns = 16;
    private const int AtlasSourceId = 0;
    private int _atlasTileCount;

    public void Configure(Texture2D texture, IEnumerable<(int X, int Y, int Map16)> tiles, float visualYOffset)
    {
        if (texture.GetWidth() != AtlasColumns * TileSize || texture.GetHeight() % TileSize != 0)
            throw new ArgumentException("Map16 atlas must contain complete rows of sixteen 16px tiles", nameof(texture));

        Clear();
        Position = new Vector2(0, visualYOffset);
        TextureFilter = TextureFilterEnum.Nearest;
        CollisionEnabled = false;
        NavigationEnabled = false;
        OcclusionEnabled = false;

        var atlas = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = new Vector2I(TileSize, TileSize),
            UseTexturePadding = false,
        };
        _atlasTileCount = AtlasColumns * texture.GetHeight() / TileSize;
        for (var id = 0; id < _atlasTileCount; id++)
            atlas.CreateTile(AtlasCoords(id));

        var tileset = new TileSet { TileSize = new Vector2I(TileSize, TileSize) };
        tileset.AddSource(atlas, AtlasSourceId);
        TileSet = tileset;
        foreach (var tile in tiles) ReplaceTile(tile.X, tile.Y, tile.Map16);
    }

    public void HideTile(int x, int y) => EraseCell(new Vector2I(x, y));

    public void ReplaceTile(int x, int y, int map16)
    {
        var cell = new Vector2I(x, y);
        if ((uint)map16 >= (uint)_atlasTileCount) EraseCell(cell);
        else SetCell(cell, AtlasSourceId, AtlasCoords(map16));
    }

    public int Map16At(int x, int y)
    {
        var cell = new Vector2I(x, y);
        if (GetCellSourceId(cell) != AtlasSourceId) return -1;
        var atlas = GetCellAtlasCoords(cell);
        return atlas.Y * AtlasColumns + atlas.X;
    }

    private static Vector2I AtlasCoords(int map16) => new(map16 % AtlasColumns, map16 / AtlasColumns);
}
