using System;
using System.Collections.Generic;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

/// <summary>Declared persistent state used by the original object loading routines.</summary>
public sealed class SmwTerrainState
{
    // The original game maintains three 128-byte item-memory banks.
    // Keep a private copy so object loading cannot alter its initial state.
    private readonly byte[] _itemMemory;
    public SmwTerrainState(ReadOnlySpan<byte> itemMemory = default)
    {
        if (!itemMemory.IsEmpty && itemMemory.Length != 384)
            throw new ArgumentException("Terrain item memory must contain 384 bytes", nameof(itemMemory));
        _itemMemory = itemMemory.IsEmpty ? new byte[384] : itemMemory.ToArray();
    }
    public bool CollectedMoon { get; init; }
    public bool Collected1UpCheckpoints { get; init; }
    public bool CollectedFiveYoshiCoins { get; init; }
    public bool GotMidpoint { get; init; }
    public bool OverworldMidpoint { get; init; }
    public bool YellowSwitch { get; init; }
    public bool GreenSwitch { get; init; }
    internal byte ItemMemory(int index) => _itemMemory[index];
}

public readonly record struct SmwTerrainTile(int Layer, int X, int Y, ushort Map16, int ObjectSequence, string Source);

/// <summary>
/// Original Map16 object expansion. This contains game routines and their
/// tile buffers, not a CPU, instruction interpreter, or emulated memory bus.
/// Unsupported objects fail explicitly rather than inventing geometry.
/// </summary>
public sealed partial class SmwMap16Terrain
{
    public const int BufferSize = 0x3800;
    private const int BufferBase = 0xC800;
    private readonly SmwLevelData _level;
    private readonly LoRomData _rom;
    private readonly SmwTerrainState _state;
    private readonly byte[] _low = new byte[BufferSize];
    private readonly byte[] _high = new byte[BufferSize];
    private readonly int[] _owner = new int[BufferSize];
    private readonly string?[] _source = new string?[BufferSize];
    private SmwLevelObject _object = null!;
    private int _pointer, _backup, _routineAddress;
    private byte _position, _nextScreen;

    // Ported from the pinned snesrev/smw object routines, checked against the
    // vanilla galaxyhaxz/smw-src disassembly. See THIRD-PARTY-NOTICES.md.
    public SmwMap16Terrain(SmwRom rom, SmwLevelData level, SmwTerrainState? state = null)
    {
        _rom = rom.Data;
        _level = level;
        _state = state ?? new SmwTerrainState();
        // No vanilla USA header selects 3. Its original address overlaps
        // other game state, so it cannot be treated as a fourth empty bank.
        if (level.Header.ItemMemory == 3)
            throw new NotSupportedException("Item-memory selection 3 requires additional game state");
        Array.Fill(_low, (byte)0x25);
        Array.Fill(_owner, -1);
        if (level.Header.Mode is 9 or 11 or 16) return;
        Load(level.Layer1);
        if (level.Layer2 != null && level.Header.Mode is not (0 or 10 or 12 or 13 or 14 or 17 or 30))
            Load(level.Layer2);
    }

    public ReadOnlySpan<byte> LowBytes => _low;
    public ReadOnlySpan<byte> HighBytes => _high;

    public IEnumerable<SmwTerrainTile> LayerTiles(int layer = 1, bool includeEmpty = false)
    {
        if (layer is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(layer));
        if (_level.Header.Mode is 9 or 11 or 16) yield break;
        var vertical = ((_level.Header.LayoutFlags >> (layer - 1)) & 1) != 0;
        var rows = vertical ? 32 : 27;
        for (var screen = 0; screen < _level.Header.Screens; screen++)
        {
            var start = ScreenPointer(layer, screen);
            for (var row = 0; row < rows; row++)
                for (var column = 0; column < 16; column++)
                {
                    var offset = start + row * 16 + column;
                    if ((uint)offset >= BufferSize) throw new InvalidDataException("Layer layout extends outside the Map16 buffer");
                    var tile = (ushort)(_low[offset] | _high[offset] << 8);
                    if (!includeEmpty && tile == 0x25) continue;
                    yield return new SmwTerrainTile(layer, vertical ? row : screen * 16 + column,
                        vertical ? screen * 16 + column : row, tile, _owner[offset], _source[offset] ?? "empty");
                }
        }
    }

    private int ScreenPointer(int layer, int screen)
    {
        var table = _rom.ReadUInt16((layer == 1 ? 0x00BDA8 : 0x00BDE8) + _level.Header.Mode * 2);
        var address = _rom.ReadUInt24(table + screen * 3);
        if (address >> 16 != 0x7E) throw new InvalidDataException("Object layout does not select the original low Map16 buffer");
        return (address & 0xFFFF) - BufferBase;
    }

    private void Load(SmwObjectStream stream)
    {
        foreach (var obj in stream.Objects)
        {
            _object = obj;
            if (obj.IsExit || obj.IsScreenJump) continue;
            _pointer = ScreenPointer(obj.Placement.Layer, obj.Placement.Screen) + (obj.Placement.HighSubscreen ? 256 : 0);
            _backup = _pointer;
            _position = (byte)(obj.Placement.SubY << 4 | obj.Placement.SubX);
            _nextScreen = (byte)obj.Placement.Screen;
            _routineAddress = SmwObjectDispatch.Routine(_rom, _level.Header.Tileset, obj);
            switch (_routineAddress)
            {
                case 0x0DA57B or 0x0DA64D: GenericSingle(); break;
                case 0x0DB2CA: YoshiCoin(); break;
                case 0x0DA68E: MidwayBar(); break;
                case 0x0DA7E7: GoalSign(); break;
                case 0x0DB583 or 0x0DB58B: SwitchBlock(); break;
                case 0x0DA71B or 0x0DA760: LargeBush(); break;
                case 0x0DA656: ExtendedSlopeTop(); break;
                case 0x0DA673: PurpleTriangle(); break;
                case 0x0DA6D1: Door(); break;
                case 0x0DA80D or 0x0DA846 or 0x0DA87D: VerticalSlopePiece(); break;
                case 0x0DA8C3: Repeated(); break;
                case 0x0DAA26: VerticalPipe(); break;
                case 0x0DAAB4: HorizontalPipe(); break;
                case 0x0DAB0D: BulletShooter(); break;
                case 0x0DADA3: SteepRightSlope(); break;
                case 0x0DADEB: GradualRightSlope(); break;
                case 0x0DAB6E: LeftSlope(2); break;
                case 0x0DAC21: LeftSlope(1); break;
                case 0x0DAC92: LeftSlope(4); break;
                case 0x0DAD44: NormalRightSlope(); break;
                case 0x0DAE6D: UpsideDownLeftSlope(2); break;
                case 0x0DAEFC: UpsideDownRightSlope(2); break;
                case 0x0DAF61: UpsideDownLeftSlope(1); break;
                case 0x0DAFEA: UpsideDownRightSlope(1); break;
                case 0x0DB075: GroundEdge(); break;
                case 0x0DB1D4: GroundLedge(obj.SizeOrType & 15, obj.SizeOrType >> 4, false); break;
                case 0x0DB224: GoalGate(); break;
                case 0x0DB336: PurpleCoins(); break;
                case 0x0DB3BD: RopeCloud(); break;
                case 0x0DB3E3: SurfaceRectangle(); break;
                case 0x0DB42D: DonutBridge(); break;
                case 0x0DB461: NetBottom(); break;
                case 0x0DB49E: NetSide(); break;
                case 0x0DB51F: SkinnyPipe(vertical: true); break;
                case 0x0DB547: SkinnyPipe(vertical: false); break;
                case 0x0DB1C8: GroundLedge(obj.SizeOrType, 2, true); break;
                case 0x0DDCEA: CeilingLedge(); break;
                case 0x0DDD2E: CeilingEdge(); break;
                case 0x0DB73F: DiagonalPipe(); break;
                case 0x0DB7AA: DiagonalLedgeLeft(); break;
                case 0x0DB863: DiagonalLedgeRight(); break;
                case 0x0DB5B7: SmallBush(); break;
                default: Unsupported(); break;
            }
        }
    }

    private void Unsupported() => throw new SmwTerrainUnsupportedObjectException(_level.Header.Id, _level.Header.Tileset, _object, _routineAddress);
    private int Index(int position)
    {
        var index = _pointer + position;
        if ((uint)index >= BufferSize) throw new InvalidDataException($"Object {_object.Sequence} writes outside the Map16 buffer");
        return index;
    }
    private byte PreviousLow(int position) => _low[Index(position)];
    private void Page(int position, byte page) => _high[Index(position)] = page;
    private void Low(int position, byte value, string source)
    {
        var index = Index(position);
        _low[index] = value;
        _owner[index] = _object.Sequence;
        _source[index] = source;
    }
    private byte WriteRight(byte position, byte page, byte value, string source)
    {
        Page(position, page);
        Low(position, value, source);
        return Right(position);
    }
    private void Write(byte position, byte page, byte value, string source)
    {
        Page(position, page);
        Low(position, value, source);
    }
    // $0DA95D: the horizontal crossing uses the saved row nibble.
    private byte Right(byte position)
    {
        var next = (byte)(position + 1);
        if ((next & 15) == 0)
        {
            _pointer += 0x1B0;
            _nextScreen++;
            next = (byte)(_position & 0xF0);
        }
        return next;
    }
    private void Preserve() => _backup = _pointer;
    private void Restore()
    {
        _pointer = _backup;
        _nextScreen = (byte)_object.Placement.Screen;
    }
    // $0DA97D, specifically the helper used by horizontal object routines.
    private byte Down()
    {
        if (_position + 16 >= 256) { _pointer += 256; _backup = _pointer; }
        _position += 16;
        return _position;
    }
    // $0DA987 updates the saved pointer's high byte along with the live one.
    private void VerticalCarry()
    {
        _pointer += 256;
        _backup = (_backup & 255) | (_pointer & ~255);
    }
    // $0DA992 and $0DA9B4. The second horizontal crossing correction has
    // its own carry behavior, including the original zero-add on borrow.
    private byte DownLeft()
    {
        var next = (byte)(_position + 15);
        if (_position + 15 >= 256) VerticalCarry();
        if ((next & 15) == 15)
        {
            if (next + 16 >= 256) VerticalCarry();
            next += 16;
            PreviousScreen();
        }
        return _position = next;
    }
    private byte DownRight()
    {
        var next = (byte)(_position + 17);
        if (_position + 17 >= 256) VerticalCarry();
        if ((next & 15) == 0)
        {
            // $0DA987 receives clear carry when the subtraction borrows.
            // It saves the high byte but adds zero rather than subtracting.
            if (next < 16) _backup = (_backup & 255) | (_pointer & ~255);
            next -= 16;
            NextScreen();
        }
        return _position = next;
    }
    private void PreviousScreen()
    {
        _pointer -= 0x1B0;
        _backup = _pointer;
        _nextScreen--;
    }
    private void NextScreen()
    {
        _pointer += 0x1B0;
        _backup = _pointer;
        _nextScreen++;
    }
    private bool ItemCollected(byte position)
    {
        var columnByte = (byte)(4 * _nextScreen);
        if ((_object.B0 & 16) != 0) columnByte |= 2;
        if ((position & 8) != 0) columnByte |= 1;
        return (_state.ItemMemory(_level.Header.ItemMemory * 128 + columnByte) & (0x80 >> (position & 7))) != 0;
    }

    // $0DA8C3, repeated generic tiles, including per-column coin memory.
    private void Repeated()
    {
        var kind = _object.Id - 1;
        var width = (_object.SizeOrType & 15) + 1;
        var height = (_object.SizeOrType >> 4) + 1;
        var tile = _rom.ReadByte(0x0DA8B4 + kind);
        Preserve();
        for (var row = 0; row < height; row++)
        {
            var pos = _position;
            for (var col = 0; col < width; col++)
                pos = kind == 4 && ItemCollected(pos) ? Right(pos) : WriteRight(pos, (byte)(kind < 7 ? 0 : 1), tile, $"std_generic_{kind:X2}");
            Restore();
            Down();
        }
    }
    // $0DA57B and the special green star block entry at $0DA64D.
    private void GenericSingle()
    {
        var type = _object.SizeOrType;
        var kind = _routineAddress == 0x0DA64D ? 50 : type - 16;
        if (type is >= 0x18 and < 0x1D && (kind == 8 ? _state.CollectedMoon : _state.Collected1UpCheckpoints)) return;
        Page(_position, (byte)(kind < 19 ? 0 : 1));
        var tile = _rom.ReadByte(0x0DA548 + kind);
        var x = _position & 15;
        if ((kind is 1 or 7 or 50 or 38 || kind == 27 && x is 1 or 4 or 7 or 10 or 13) && ItemCollected(_position))
        {
            if (kind == 7) return;
            Page(_position, 1);
            tile = 0x32;
        }
        Low(_position, tile, "extended_generic");
    }
    // $0DB2CA.
    private void YoshiCoin()
    {
        if (_state.CollectedFiveYoshiCoins || ItemCollected(_position)) return;
        Write(_position, 0, 0x2D, "yoshi_coin_top");
        Write(Down(), 0, 0x2E, "yoshi_coin_bottom");
    }
    // $0DB075: these overlap decisions read the previous low byte, even
    // when it belongs to a different page. Object order is significant.
    private void GroundEdge()
    {
        var kind = _object.SizeOrType & 15;
        if (kind >= 15) throw new InvalidDataException("Invalid ground edge kind");
        var height = _object.SizeOrType >> 4;
        Page(_position, (byte)(kind < 3 ? 0 : 1));
        var tile = _rom.ReadByte(0x0DB039 + kind);
        if ((kind < 9 || kind >= 11) && kind != 2)
        {
            tile = Overlap(_position, tile, 0x0DB0F0, 0x0DB102, 18, out var replaced);
            if (!replaced && PreviousLow(_position) != 0x25 && tile is 1 or 3 or 0x45 or 0x48) tile++;
        }
        Low(_position, tile, "ground_edge_top");
        Down();
        for (var row = 0; row < height; row++)
        {
            Page(_position, (byte)(kind >= 9 || kind is >= 3 and < 7 ? 1 : 0));
            tile = _rom.ReadByte((row == 0 ? 0x0DB048 : 0x0DB057) + kind);
            if ((kind < 3 || kind is >= 7 and < 9) && kind != 2)
                tile = Overlap(_position, tile, 0x0DB15C, 0x0DB17A, 30, out _);
            Low(_position, tile, "ground_edge_middle");
            Down();
        }
        if (kind >= 11) Write(_position, 1, _rom.ReadByte(0x0DB066 + kind), "ground_edge_bottom");
    }
    private byte Overlap(byte position, byte tile, int from, int to, int length, out bool replaced)
    {
        for (var i = length - 1; i >= 0; i--)
            if (PreviousLow(position) == _rom.ReadByte(from + i))
            {
                Page(position, 1);
                replaced = true;
                return _rom.ReadByte(to + i);
            }
        replaced = false;
        return tile;
    }
    // $0DB1C8/$0DB1D4.
    private void GroundLedge(int widthMinusOne, int lowerRows, bool wide)
    {
        Preserve();
        for (var row = 0; row <= lowerRows; row++)
        {
            var pos = _position;
            for (var col = 0; col <= widthMinusOne; col++)
                pos = WriteRight(pos, (byte)(row == 0 ? 1 : 0), (byte)(row == 0 ? 0 : 0x3F),
                    wide ? row == 0 ? "wide_scale_ledge_top" : "wide_scale_ledge_fill" : row == 0 ? "standard_ledge_top" : "standard_ledge_fill");
            Restore();
            Down();
        }
    }
    // $0DAA26. Pipe kinds have distinct row counts; there is no generic
    // top/body/bottom rectangle rule in the original loader.
    private void VerticalPipe()
    {
        var kind = _object.SizeOrType & 15;
        if (kind > 5) throw new InvalidDataException("Invalid vertical pipe kind");
        byte remaining = (byte)(_object.SizeOrType >> 4);
        Preserve();
        void Row(byte left, byte right, string source)
        {
            var pos = WriteRight(_position, 1, left, source + "_left");
            Write(pos, 1, right, source + "_right");
        }
        if (kind < 3)
        {
            Row(_rom.ReadByte(0x0DAA12 + kind), _rom.ReadByte(0x0DAA17 + kind), "vertical_pipe_top");
            Restore(); Down();
            if (kind < 2 && (--remaining & 0x80) != 0) return;
            if (kind == 2 && --remaining == 0)
            {
                Row(_rom.ReadByte(0x0DAA1C + kind), _rom.ReadByte(0x0DAA21 + kind), "vertical_pipe_bottom");
                return;
            }
        }
        for (var guard = 0; guard < 256; guard++)
        {
            Row((byte)(kind == 5 ? 0x68 : 0x35), (byte)(kind == 5 ? 0x69 : 0x36), "vertical_pipe_shaft");
            Restore(); Down();
            remaining--;
            if (kind is 2 or 3 or 4)
            {
                if (remaining != 0) continue;
                Row(_rom.ReadByte(0x0DAA1C + kind), _rom.ReadByte(0x0DAA21 + kind), "vertical_pipe_bottom");
                return;
            }
            if ((remaining & 0x80) != 0) return;
        }
        throw new InvalidDataException("Vertical pipe has an invalid height");
    }
    // $0DAAB4.
    private void HorizontalPipe()
    {
        var kind = (_object.SizeOrType & 0xF0) >> 3;
        if (kind > 6) throw new InvalidDataException("Invalid horizontal pipe kind");
        var count = _object.SizeOrType & 15;
        Preserve();
        for (var row = 0; row < 2; row++)
        {
            var pos = _position;
            var tileKind = kind + row;
            if (kind < 4)
            {
                pos = WriteRight(pos, 1, _rom.ReadByte(0x0DAAA4 + tileKind), "horizontal_pipe_end");
                for (var col = 0; col < count; col++)
                    pos = WriteRight(pos, 1, _rom.ReadByte(0x0DAAAC + tileKind), "horizontal_pipe_shaft");
            }
            else
            {
                var shaftCount = count == 0 ? 256 : count;
                for (var col = 0; col < shaftCount; col++)
                    pos = WriteRight(pos, 1, _rom.ReadByte(0x0DAAAC + tileKind), "horizontal_pipe_shaft");
                Write(pos, 1, _rom.ReadByte(0x0DAAA4 + tileKind), "horizontal_pipe_end");
            }
            Restore(); Down();
        }
    }
    // $0DDCEA and $0DDD2E.
    private void CeilingLedge()
    {
        var width = (_object.SizeOrType & 15) + 1;
        var height = _object.SizeOrType >> 4;
        Preserve();
        for (var row = 0; row <= height; row++)
        {
            var pos = _position;
            for (var col = 0; col < width; col++)
                pos = WriteRight(pos, 1, (byte)(row < height ? 0x65 : 0x4E), row < height ? "underground_ceiling_ledge_fill" : "underground_ceiling_ledge_bottom");
            Restore(); Down();
        }
    }
    private void CeilingEdge()
    {
        var kind = _object.SizeOrType & 15;
        if (kind > 3) throw new InvalidDataException("Invalid ceiling edge kind");
        var height = _object.SizeOrType >> 4;
        for (var row = 0; row < height; row++)
        {
            Write(_position, 1, _rom.ReadByte(0x0DDD26 + kind), "underground_ceiling_edge");
            Down();
        }
        WriteRight(_position, 1, _rom.ReadByte(0x0DDD2A + kind), "underground_ceiling_edge_bottom");
    }
}
