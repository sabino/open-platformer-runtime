using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using OpenPlatformerRuntime.Audio;
using OpenPlatformerRuntime.SmwAssets;
using System.Collections.Generic;

if (args.Length == 1 && args[0] == "self-test")
{
    var memory = new byte[65536]; var registers = new byte[128];
    memory[512 + 1] = memory[514 + 1] = 0x20; memory[0x2000] = 0x83;
    for (var i = 1; i < 9; i++) memory[0x2000+i] = (byte)(i*31);
    registers[0x5D]=2; registers[0x0C]=registers[0x1C]=127;
    registers[0]=registers[1]=87; registers[3]=16; registers[7]=127; registers[0x6C]=32;
    var one = new SnesDsp(); var split = new SnesDsp();
    one.Initialize(memory,registers); split.Initialize(memory,registers);
    one.WriteRegister(0x4C,1); split.WriteRegister(0x4C,1);
    var expected = new short[8192]; var actual = new short[8192];
    one.Generate(expected);
    for (var i=0;i<actual.Length;i+=2) split.Generate(actual.AsSpan(i,2));
    if (!expected.AsSpan().SequenceEqual(actual) || !one.CaptureState().AsSpan().SequenceEqual(split.CaptureState()))
        throw new InvalidOperationException("DSP block size changes output or state");
    var saved = one.CaptureState(); one.Generate(expected);
    split.RestoreState(saved); split.Generate(actual);
    if (!expected.AsSpan().SequenceEqual(actual)) throw new InvalidOperationException("DSP checkpoint changes PCM");
    var allocated=GC.GetAllocatedBytesForCurrentThread();
    for (var i=0;i<8;i++) one.Generate(actual);
    if (GC.GetAllocatedBytesForCurrentThread()!=allocated) throw new InvalidOperationException("DSP hot path allocates");
    if (NativeSmwAudio.FrameToClock(60)!=1024000 || NativeSmwAudio.FrameToClock(3600)!=61440000)
        throw new InvalidOperationException("Frame/audio clock mapping is not rational and deterministic");
    var romPath = Environment.GetEnvironmentVariable("SMW_ROM_PATH");
    if (!string.IsNullOrWhiteSpace(romPath))
    {
        var data = SmwAudioData.FromRom(SmwRom.FromBytes(File.ReadAllBytes(romPath)));
        var cached = SmwAudioData.FromBinary(data.ToBinary());
        if (!data.CreateDataImage(SmwMusicBank.Level).AsSpan().SequenceEqual(cached.CreateDataImage(SmwMusicBank.Level)))
            throw new InvalidOperationException("Native ROM audio cache changes data");
        var native = new NativeSmwAudio(data);
        native.ScheduleCommand(0,2,2); native.ScheduleCommand(32768,1,1);
        native.Generate(expected); var checkpoint=native.CaptureState();
        native.Generate(expected); native.RestoreState(checkpoint); native.Generate(actual);
        if(!expected.AsSpan().SequenceEqual(actual)) throw new InvalidOperationException("Native sequencer/DSP checkpoint changes PCM");
        var whole = new NativeSmwAudio(data); var pieces = new NativeSmwAudio(data);
        whole.ScheduleCommand(0,2,2); pieces.ScheduleCommand(0,2,2);
        whole.Generate(expected);
        for(var i=0;i<actual.Length;i+=2) pieces.Generate(actual.AsSpan(i,2));
        if(!expected.AsSpan().SequenceEqual(actual) || !whole.CaptureState().AsSpan().SequenceEqual(pieces.CaptureState()))
            throw new InvalidOperationException("Native sequencing depends on the render block size");
        var referenceMix = new NativeSmwAudio(data); var monitored = new NativeSmwAudio(data);
        referenceMix.ScheduleCommand(0,2,2); monitored.ScheduleCommand(0,2,2);
        var listening = new short[expected.Length]; var monitor = new SnesDspMonitor();
        referenceMix.Generate(expected); monitored.Generate(actual,listening,monitor);
        if (!expected.AsSpan().SequenceEqual(actual) || !actual.AsSpan().SequenceEqual(listening))
            throw new InvalidOperationException("Bypassed monitor changes authentic PCM");
        monitor.Enabled = true;
        for (var voice=0;voice<8;voice++) monitor.SetMuted(false,voice,true);
        referenceMix.Generate(expected); monitored.Generate(actual,listening,monitor);
        if (!expected.AsSpan().SequenceEqual(actual) || !referenceMix.CaptureState().AsSpan().SequenceEqual(monitored.CaptureState()))
            throw new InvalidOperationException("Listening mixer changes native PCM or simulation state");
        foreach (var sample in listening) if (sample!=0) throw new InvalidOperationException("Muted dry monitor is not silent");
        monitor.Reset(); monitor.Enabled=true;
        for(var sample=0;sample<20;sample++) monitor.SetSideGain(true,sample,true,0);
        monitored.Generate(actual,listening,monitor);
        for(var i=1;i<listening.Length;i+=2) if(listening[i]!=0) throw new InvalidOperationException("Instrument right-channel gain leaks");
    }
    Console.WriteLine("native audio self-test: block independence, full DSP checkpoint, rational clock mapping, zero hot-path allocation passed");
    return;
}
if (args.Length == 7 && args[0] == "sfx-case")
{
    var data=SmwAudioData.FromRom(SmwRom.FromBytes(File.ReadAllBytes(args[1])));
    var port=int.Parse(args[2]); var command=byte.Parse(args[3]); var frames=int.Parse(args[4]); var root=args[5];
    if(args[6]!="--isolated") throw new ArgumentException("Expected isolated SFX fixture");
    Directory.CreateDirectory(root); var writes=new List<Write>();
    var engine=new NativeSmwAudio(data,trace:(clock,register,value)=>writes.Add(new Write(clock,register,value)));
    engine.ScheduleCommand(0,port,command); var pcm=new short[frames*2]; engine.Generate(pcm);
    var bytes=new byte[pcm.Length*2];
    for(var i=0;i<pcm.Length;i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i*2,2),pcm[i]);
    File.WriteAllBytes(Path.Combine(root,"native.pcm"),bytes);
    File.WriteAllText(Path.Combine(root,"writes.json"),JsonSerializer.Serialize(writes));
    Console.WriteLine($"native isolated SFX: port={port} command={command:X2} frames={frames} writes={writes.Count}");
    return;
}
if (args.Length == 4 && args[0] == "driver-cases")
{
    var data = SmwAudioData.FromRom(SmwRom.FromBytes(File.ReadAllBytes(args[1])));
    var frames = int.Parse(args[2]); var root = args[3]; Directory.CreateDirectory(root);
    var cases = new List<object>();
    foreach (var bank in Enum.GetValues<SmwMusicBank>())
    {
        var count = bank == SmwMusicBank.Level ? 29 : bank == SmwMusicBank.Overworld ? 9 : 12;
        for (byte song = 1; song <= count; song++)
        {
            var name = $"{bank}-{song:00}"; var output = Path.Combine(root, name); Directory.CreateDirectory(output);
            var writes = new List<Write>();
            var engine = new NativeSmwAudio(data, bank, (clock, register, value) => writes.Add(new Write(clock, register, value)));
            var image = engine.Dsp.SampleAndEchoMemory.ToArray();
            File.WriteAllBytes(Path.Combine(output, "initial.memory"), image);
            var registers = new byte[128]; registers[0x6C] = 0xE0;
            File.WriteAllBytes(Path.Combine(output, "initial.registers"), registers);
            var commands = new[] { new SmwAudioCommand(0,2,song), new SmwAudioCommand(32768,1,1),
                new SmwAudioCommand(65536,1,0), new SmwAudioCommand(98304,3,1), new SmwAudioCommand(131072,3,0) };
            foreach (var command in commands) engine.ScheduleCommand(command.Clock, command.Port, command.Value);
            var samples = new short[frames * 2]; engine.Generate(samples);
            var bytes = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i*2,2), samples[i]);
            File.WriteAllBytes(Path.Combine(output,"native.pcm"),bytes);
            File.WriteAllText(Path.Combine(output,"writes.json"),JsonSerializer.Serialize(writes));
            File.WriteAllText(Path.Combine(output,"state.txt"),engine.Driver.DescribeState()+"\n"+engine.Dsp.DescribeState());
            cases.Add(new { name, bank=bank.ToString(), song, frames, ticks=engine.Driver.SequencerTicks, commands });
        }
    }
    File.WriteAllText(Path.Combine(root,"cases.json"),JsonSerializer.Serialize(cases));
    Console.WriteLine($"native SMW driver: rendered {cases.Count} ROM song cases");
    return;
}
if ((args.Length == 6 || args.Length == 7 && args[6] == "--music-only") && args[0] == "render")
{
    var rom = SmwRom.FromBytes(File.ReadAllBytes(args[1]));
    var data = SmwAudioData.FromRom(rom);
    var bank = Enum.Parse<SmwMusicBank>(args[2], ignoreCase: true);
    var song = byte.Parse(args[3]); var frames = int.Parse(args[4]);
    var output = args[5]; Directory.CreateDirectory(output);
    var writes = new List<Write>();
    var engine = new NativeSmwAudio(data, bank, (clock, register, value) => writes.Add(new Write(clock, register, value)));
    File.WriteAllBytes(Path.Combine(output, "initial.memory"), engine.Dsp.SampleAndEchoMemory.ToArray());
    var initialRegisters = new byte[128]; initialRegisters[0x6C] = 0xE0;
    File.WriteAllBytes(Path.Combine(output, "initial.registers"), initialRegisters);
    engine.ScheduleCommand(0, 2, song);
    if (args.Length == 6)
    {
        engine.ScheduleCommand(32768, 1, 1); engine.ScheduleCommand(65536, 1, 0);
        engine.ScheduleCommand(98304, 3, 1); engine.ScheduleCommand(131072, 3, 0);
    }
    var pcm = new short[frames * 2]; engine.Generate(pcm);
    var bytes = new byte[pcm.Length * 2];
    for (var i = 0; i < pcm.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), pcm[i]);
    File.WriteAllBytes(Path.Combine(output, "native.pcm"), bytes);
    File.WriteAllText(Path.Combine(output, "writes.json"), JsonSerializer.Serialize(writes));
    File.WriteAllText(Path.Combine(output, "state.txt"), engine.Driver.DescribeState() + "\n" + engine.Dsp.DescribeState());
    File.WriteAllBytes(Path.Combine(output, "native-audio.bin"), data.ToBinary());
    Console.WriteLine($"native SMW audio: bank={bank} song={song:X2} frames={frames} writes={writes.Count} pcm_sha256={Convert.ToHexString(SHA256.HashData(bytes))}");
    return;
}
if (args.Length == 3 && args[0] == "dsp-batch")
{
    var cases = JsonSerializer.Deserialize<Fixture[]>(File.ReadAllText(args[1]))!;
    Directory.CreateDirectory(args[2]);
    foreach (var fixture in cases)
    {
        var dsp = new SnesDsp();
        dsp.Initialize(File.ReadAllBytes(fixture.memory), File.ReadAllBytes(fixture.registers));
        var pcm = new short[fixture.frames * 2];
        var writes = fixture.events;
        var next = 0; var output = 0;
        long previous = -1;
        foreach (var item in writes)
        {
            if (item.clock < previous || item.clock < 0 || item.clock >= fixture.frames * 32L || (uint)item.register >= 128 || (uint)item.value > 255)
                throw new InvalidDataException("Invalid or unordered DSP event");
            previous = item.clock;
        }
        while (dsp.Clock < fixture.frames * 32L)
        {
            while (next < writes.Length && writes[next].clock == dsp.Clock)
            {
                var e = writes[next++]; dsp.WriteRegister(e.register, (byte)e.value);
            }
            if (!dsp.StepClock()) continue;
            pcm[output++] = dsp.LastLeft; pcm[output++] = dsp.LastRight;
        }
        if (output != pcm.Length) throw new InvalidOperationException("DSP output cadence mismatch");
        var bytes = new byte[pcm.Length * 2];
        for (var i = 0; i < pcm.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2, 2), pcm[i]);
        File.WriteAllBytes(Path.Combine(args[2], fixture.name + ".pcm"), bytes);
        File.WriteAllBytes(Path.Combine(args[2], fixture.name + ".memory"), dsp.SampleAndEchoMemory.ToArray());
        File.WriteAllBytes(Path.Combine(args[2], fixture.name + ".registers"), dsp.Registers.ToArray());
        File.WriteAllText(Path.Combine(args[2], fixture.name + ".state"), dsp.DescribeState());
    }
    Console.WriteLine($"managed DSP: rendered {cases.Length} cases");
    return;
}
if (args.Length == 1 && args[0] == "benchmark")
{
    var dsp = new SnesDsp();
    var memory = new byte[65536]; var registers = new byte[128];
    registers[0x0C] = registers[0x1C] = 127; registers[0x5D] = 2; registers[0x6D] = 0x60;
    registers[0x7D] = 2; registers[0x4D] = 255; registers[0x0D] = 96; registers[0x2C] = registers[0x3C] = 22;
    registers[0x0F] = 127;
    for (var v = 0; v < 8; v++)
    {
        memory[512 + v * 4 + 1] = memory[514 + v * 4 + 1] = 0x20;
        registers[v * 16] = registers[v * 16 + 1] = 64;
        registers[v * 16 + 3] = 16;
        registers[v * 16 + 4] = (byte)v; registers[v * 16 + 5] = 0xFF; registers[v * 16 + 6] = 0xE0;
    }
    memory[0x2000] = 0x83;
    for (var i = 1; i < 9; i++) memory[0x2000 + i] = (byte)(i * 17);
    dsp.Initialize(memory, registers); dsp.WriteRegister(0x4C, 255);
    var output = new short[64000];
    for (var i = 0; i < 5; i++) dsp.Generate(output);
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var started = Stopwatch.GetTimestamp();
    for (var i = 0; i < 20; i++) dsp.Generate(output);
    var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds / 20;
    var allocations = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine(JsonSerializer.Serialize(new { seconds_generated = 20, ms_per_audio_second = elapsed,
        speed_realtime = 1000 / elapsed, allocated_bytes = allocations, voices = 8, echo = true,
        raw_boundary = "signed 16-bit stereo, 32000 Hz" }));
    return;
}
throw new ArgumentException("usage: SmwAudioTool dsp-batch CASES.json OUT | benchmark");

sealed record Write(long clock, int register, int value);
sealed record Fixture(string name, string memory, string registers, int frames, Write[] events);
