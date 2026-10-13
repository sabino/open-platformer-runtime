using System;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Native ROM-driven texture inputs for the current development level entry.</summary>
public sealed class SmwLevelVisuals
{
    private readonly SmwPaletteDecoder _palettes;
    private readonly byte[] _playerPixels;
    private readonly byte[] _spritePixels;
    private readonly byte[] _foreground;
    private readonly ushort[] _palette;
    private readonly SmwLevelGraphics _level;

    public SmwLevelVisuals(SmwRom rom, int level)
    {
        _level = new SmwLevelGraphics(rom, level);
        _palettes = new SmwPaletteDecoder(rom);
        _palette = _palettes.CreateLevelEntry(_level.Header.Palette);
        _foreground = _level.ReadBackgroundWindow();
        _spritePixels = SnesGraphics.DecodePlanar(_level.ReadSpriteWindow(), 4);
        _playerPixels = new SmwGraphicsDecoder(rom).Decode(0x32).DecodePixels();
        Sky = _palettes.ApplyLevelBase(new ushort[256], _level.Header.Palette);
    }

    public SmwLevelHeader Header => _level.Header;
    public ReadOnlySpan<ushort> Palette => _palette;
    public ushort Sky { get; }
    public SmwRgbaImage ForegroundImage() => SmwGraphicsImages.CreateMap16Atlas(_level.ReadForegroundMap16(), _foreground, _palette);
    public SmwRgbaImage? BackgroundImage()
    {
        var tiles = _level.ReadBackgroundTiles();
        return tiles == null ? null : SmwGraphicsImages.CreateLayer2(tiles, _level.ReadBackgroundMap16(), _foreground, _palette);
    }

    public SmwRgbaImage SpriteImage(int palette)
    {
        if ((uint)palette >= 8) throw new ArgumentOutOfRangeException(nameof(palette));
        return SmwGraphicsImages.CreateAtlas(_spritePixels, _palette.AsSpan((8 + palette) * 16, 16));
    }

    public SmwRgbaImage PlayerImage(int variant) => SmwGraphicsImages.CreateAtlas(_playerPixels, _palettes.CreatePlayerRow(variant));
}
