using System;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed partial class SmwMap16Terrain
{
    // $0DA656/$0DA673/$0DA6D1.
    private void ExtendedSlopeTop()
    {
        var index = _object.SizeOrType - 0x42;
        var pos = WriteRight(_position, 1, _rom.ReadByte(0x0DA652 + index), "extended_slope_top");
        Write(pos, 1, _rom.ReadByte(0x0DA654 + index), "extended_slope_top");
    }
    private void PurpleTriangle()
    {
        Write(_position, 1, _rom.ReadByte(0x0DA671 + _object.SizeOrType - 0x44), "extended_purple_triangle");
        Write(Down(), 1, 0xEB, "extended_purple_triangle_base");
    }
    private void Door()
    {
        var index = _object.SizeOrType - 0x47;
        Write(_position, 0, _rom.ReadByte(0x0DA6CD + index), "extended_door_top");
        Write(Down(), 0, _rom.ReadByte(0x0DA6CF + index), "extended_door_bottom");
    }
    private byte DownVertical()
    {
        if (_position + 16 >= 256) _pointer += 0x200;
        _position += 16;
        return _position;
    }
    // $0DA80D/$0DA846/$0DA87D use the vertical cursor helper. The normal
    // piece does not restore its pointer between its two rows.
    private void VerticalSlopePiece()
    {
        if (_routineAddress == 0x0DA80D)
        {
            var index = _object.SizeOrType - 0x91;
            Write(_position, 1, _rom.ReadByte(0x0DA809 + index), "vertical_steep_slope_top");
            Write(DownVertical(), 1, _rom.ReadByte(0x0DA80B + index), "vertical_steep_slope_bottom");
        }
        else if (_routineAddress == 0x0DA846)
        {
            var index = _object.SizeOrType - 0x93;
            var pos = WriteRight(_position, 1, _rom.ReadByte(0x0DA83E + index), "vertical_normal_slope_top");
            Write(pos, 1, _rom.ReadByte(0x0DA840 + index), "vertical_normal_slope_top");
            pos = WriteRight(DownVertical(), 1, _rom.ReadByte(0x0DA842 + index), "vertical_normal_slope_bottom");
            Write(pos, 1, _rom.ReadByte(0x0DA844 + index), "vertical_normal_slope_bottom");
        }
        else
        {
            var index = _object.SizeOrType - 0x95;
            Write(_position, 1, _rom.ReadByte(0x0DA877 + index), "vertical_very_steep_slope_top");
            Write(DownVertical(), 1, _rom.ReadByte(0x0DA879 + index), "vertical_very_steep_slope_middle");
            Write(DownVertical(), 1, _rom.ReadByte(0x0DA87B + index), "vertical_very_steep_slope_bottom");
        }
    }
    // $0DAB0D.
    private void BulletShooter()
    {
        var height = _object.SizeOrType >> 4;
        Write(_position, 1, 0x41, "bullet_shooter_top");
        Down();
        if (height == 0) return;
        Write(_position, 1, 0x42, "bullet_shooter_middle");
        Down();
        for (var row = 1; row < height; row++)
        {
            Write(_position, 1, 0x43, "bullet_shooter_fill");
            Down();
        }
    }
    // $0DB336 preserves the original collected-coin behavior: no advance
    // on a collected column, but a page-zero write and saved high byte.
    private void PurpleCoins()
    {
        var width = (_object.SizeOrType & 15) + 1;
        var height = (_object.SizeOrType >> 4) + 1;
        Preserve();
        for (var row = 0; row < height; row++)
        {
            var pos = _position;
            for (var col = 0; col < width; col++)
            {
                Page(pos, 0);
                if (ItemCollected(pos)) _backup = (_backup & 255) | (_pointer & ~255);
                else { Low(pos, 0x2C, "purple_coin"); pos = Right(pos); }
            }
            Restore(); Down();
        }
    }
    // $0DB3E3: water/lava surfaces and net tops share this routine. The
    // original dispatch also sends unused standard IDs here without clamping.
    private void SurfaceRectangle()
    {
        var kind = _object.Id - 0x18;
        var width = (_object.SizeOrType & 15) + 1;
        var lowerRows = _object.SizeOrType >> 4;
        Preserve();
        for (var row = 0; row <= lowerRows; row++)
        {
            var pos = _position;
            var tile = _rom.ReadByte((row == 0 ? 0x0DB3DB : 0x0DB3DF) + kind);
            for (var col = 0; col < width; col++)
                pos = WriteRight(pos, 0, tile, row == 0 ? "surface_top" : "surface_fill");
            Restore(); Down();
        }
    }
    // $0DB42D.
    private void DonutBridge()
    {
        Preserve();
        for (var row = 0; row < 2; row++)
        {
            var pos = _position;
            for (var col = 0; col <= (_object.SizeOrType & 15); col++)
                pos = WriteRight(pos, (byte)row, _rom.ReadByte(0x0DB42B + row), row == 0 ? "donut_bridge_top" : "donut_bridge_bottom");
            Restore(); Down();
        }
    }
    // $0DB461/$0DB49E, including net corner overlap tests against low bytes.
    private void NetBottom()
    {
        Preserve();
        var height = _object.SizeOrType >> 4;
        for (var row = 0; row <= height; row++)
        {
            var pos = _position;
            for (var col = 0; col <= (_object.SizeOrType & 15); col++)
                pos = WriteRight(pos, 0, (byte)(row == height ? 0x0E : 0x0B), row == height ? "net_bottom" : "net_fill");
            if (row == height) break;
            Restore(); Down();
        }
    }
    private void NetSide()
    {
        var kind = _object.SizeOrType & 15;
        var height = _object.SizeOrType >> 4;
        if (height == 0) height = 256;
        var pos = _position;
        var side = _rom.ReadByte(0x0DB49C + kind);
        var previous = PreviousLow(pos);
        Write(pos, 0, previous == 8 ? _rom.ReadByte(0x0DB4D5 + kind) : previous == 14 ? _rom.ReadByte(0x0DB4D7 + kind) : side, "net_side_top");
        for (var row = 1; row <= height; row++)
        {
            if (pos + 16 >= 256) VerticalCarry();
            pos += 16;
            if (row < height) Write(pos, 0, side, "net_side");
        }
        previous = PreviousLow(pos);
        Write(pos, 0, previous == 14 ? _rom.ReadByte(0x0DB4FA + kind) : previous == 8 ? _rom.ReadByte(0x0DB4FC + kind) : side, "net_side_bottom");
    }

    // $0DA68E. Its first tile is one byte before the encoded position.
    private void MidwayBar()
    {
        if (_state.OverworldMidpoint || _state.GotMidpoint) return;
        var pos = WriteRight((byte)(_position - 1), 0, 0x35, "extended_midway_bar");
        Write(pos, 0, 0x38, "extended_midway_bar");
    }
    // $0DA7E7: two columns by two rows, with the original cursor helpers.
    private void GoalSign()
    {
        Preserve();
        var index = 0;
        for (var row = 0; row < 2; row++)
        {
            var pos = _position;
            for (var col = 0; col < 2; col++)
                pos = WriteRight(pos, 0, _rom.ReadByte(0x0DA7E3 + index++), "extended_goal_marker");
            Restore(); Down();
        }
    }
    // $0DB224. Column traversal advances the live pointer, not the saved
    // pointer or row position. Each next column restores the original row.
    private void GoalGate()
    {
        var goal = (_object.SizeOrType & 15) != 0;
        var height = _object.SizeOrType >> 4;
        if (height == 0) throw new InvalidDataException("Goal gate has a wrapping zero height");
        var table = goal ? 0x0DB21B : 0x0DB212;
        var source = goal ? "goal" : "midway";
        Preserve();
        for (var col = 0; col < 3; col++)
        {
            var pos = _position;
            Write(pos, 0, _rom.ReadByte(table + col), source + "_top");
            for (var row = 1; row <= height; row++)
            {
                if (pos + 16 >= 256) _pointer += 256;
                pos += 16;
                Write(pos, 0, _rom.ReadByte(table + (row == height ? 6 : 3) + col),
                    source + (row == height ? "_bottom" : "_middle"));
            }
            Restore();
            var next = (byte)(_position + 1);
            if ((next & 15) == 0)
            {
                NextScreen();
                next = (byte)(_position & 0xF0);
            }
            _position = next;
        }
    }
    // $0DB3BD.
    private void RopeCloud()
    {
        var kind = _object.SizeOrType >> 4;
        if (kind > 1) throw new InvalidDataException("Invalid rope/cloud tile kind");
        var pos = _position;
        for (var col = 0; col <= (_object.SizeOrType & 15); col++)
            pos = WriteRight(pos, 1, _rom.ReadByte(0x0DB3BB + kind), "rope_cloud_line");
    }
    // $0DB51F/$0DB547. Zero width/height is byte-wrap behavior, not an
    // empty rectangle. Vanilla streams use valid bounded dimensions.
    private void SkinnyPipe(bool vertical)
    {
        var remaining = vertical ? _object.SizeOrType >> 4 : _object.SizeOrType & 15;
        if (remaining == 0) remaining = 256;
        var pos = _position;
        var tile = vertical ? 0x53 : 0x56;
        var source = vertical ? "skinny_vertical" : "skinny_horizontal";
        for (var i = 0; i < remaining; i++)
        {
            Write(pos, 1, (byte)(tile + (i == 0 ? 0 : 1)), source + (i == 0 ? "_top" : "_middle"));
            pos = vertical ? Down() : Right(pos);
        }
        Write(pos, 1, (byte)(tile + 2), source + "_bottom");
    }
    // $0DB583/$0DB58B.
    private void SwitchBlock()
    {
        var yellow = _routineAddress == 0x0DB583;
        var index = yellow ? 1 : 0;
        var active = yellow ? _state.YellowSwitch : _state.GreenSwitch;
        Write(_position, (byte)(active ? 1 : 0), _rom.ReadByte((active ? 0x0DB587 : 0x0DB589) + index),
            yellow ? "extended_yellow_switch_block" : "extended_green_switch_block");
    }
}
