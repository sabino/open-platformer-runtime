using System;

namespace OpenPlatformerRuntime.Audio;

public sealed partial class SnesDsp
{
    // Observation taps are outside the authoritative DSP state. No tap changes
    // the integer pipeline, register events, echo feedback or raw PCM.
    private readonly int[] _monitorLeft = new int[8], _monitorRight = new int[8];
    private readonly byte[] _monitorSource = new byte[8];
    private int _monitorEchoLeft, _monitorEchoRight;
    public ReadOnlySpan<int> MonitorVoiceLeft => _monitorLeft;
    public ReadOnlySpan<int> MonitorVoiceRight => _monitorRight;
    public ReadOnlySpan<byte> MonitorSourceIds => _monitorSource;
    public int MonitorEchoLeft => _monitorEchoLeft;
    public int MonitorEchoRight => _monitorEchoRight;
    private void ClearMonitorTaps()
    {
        Array.Clear(_monitorLeft); Array.Clear(_monitorRight); Array.Clear(_monitorSource);
        _monitorEchoLeft = _monitorEchoRight = 0;
    }
}
