using System;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Validated vanilla ROM data shared by CLI, desktop and browser hosts.</summary>
public sealed class SmwRom
{
    private SmwRom(LoRomData data, SmwRomInspection inspection)
    {
        Data = data;
        Inspection = inspection;
    }

    public LoRomData Data { get; }
    public SmwRomInspection Inspection { get; }

    public static SmwRom FromBytes(ReadOnlySpan<byte> bytes)
    {
        var inspection = SmwRomInspector.Inspect(bytes);
        if (!inspection.IsSupported)
        {
            throw new ArgumentException($"Unsupported SMW ROM: {inspection.Status}, size={inspection.Size}, sha1={inspection.Sha1}", nameof(bytes));
        }
        return new SmwRom(new LoRomData(bytes), inspection);
    }
}
