using System;
using System.Collections.Generic;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Original level streams and background data from the vanilla pointer tables.</summary>
public sealed class SmwLevelData
{
    public SmwLevelData(SmwRom rom, int level)
    {
        Header = SmwLevelHeader.Read(rom, level);
        Layer1 = SmwLevelStreams.ReadObjects(rom.Data, Header.SourceAddress, Header.LayoutFlags);
        var layer2 = rom.Data.ReadUInt24(0x05E600 + level * 3);
        if (layer2 >> 16 == 0xFF)
        {
            Layer2Address = 0x0C0000 | (layer2 & 0xFFFF);
            Background = Rle1Decoder.Decode(rom.Data.ReadFrom(Layer2Address));
        }
        else
        {
            Layer2Address = layer2;
            Layer2 = SmwLevelStreams.ReadObjects(rom.Data, layer2, Header.LayoutFlags, 1);
        }
        var sprites = 0x070000 | rom.Data.ReadUInt16(0x05EC00 + level * 2);
        Sprites = SmwLevelStreams.ReadSprites(rom.Data, sprites, Header.Vertical);
    }

    public SmwLevelHeader Header { get; }
    public SmwObjectStream Layer1 { get; }
    public SmwObjectStream? Layer2 { get; }
    public int Layer2Address { get; }
    public Rle1Decoded? Background { get; }
    public SmwSpriteStream Sprites { get; }

    // ExtObj00 writes a global selector, not a per-screen secondary bit.
    // The final processed exit record wins. These modes skip object loading
    // or stop after layer 1 in the original level loading routine.
    public byte ExitSecondarySelector
    {
        get
        {
            byte selector = 0;
            foreach (var exit in LoadedExits) selector = (byte)(exit.Properties >> 1);
            return selector;
        }
    }

    public IEnumerable<SmwScreenExit> LoadedExits
    {
        get
        {
            if (Header.Mode is 9 or 11 or 16) yield break;
            foreach (var exit in Layer1.Exits) yield return exit;
            if (Layer2 != null && Header.Mode is not (0 or 10 or 12 or 13 or 14 or 17 or 30))
                foreach (var exit in Layer2.Exits) yield return exit;
        }
    }
}
