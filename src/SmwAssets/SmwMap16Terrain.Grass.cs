using System;
using System.IO;

namespace OpenPlatformerRuntime.SmwAssets;

public sealed partial class SmwMap16Terrain
{
    // $0DA71B/$0DA760/$0DA78D. Transparent entries still clear the high
    // byte but retain the existing low byte. Bush edges merge with the low
    // byte underneath; an ordinary transparent rectangle is not equivalent.
    private void LargeBush()
    {
        var big = _routineAddress == 0x0DA71B;
        var table = big ? 0x0DA6EE : 0x0DA748;
        var width = big ? 9 : 6;
        var height = big ? 5 : 4;
        var index = 0;
        Preserve();
        for (var row = 0; row < height; row++)
        {
            var pos = _position;
            for (var col = 0; col < width; col++)
            {
                Page(pos, 0);
                var tile = _rom.ReadByte(table + index++);
                if (tile != 0x25)
                {
                    var previous = PreviousLow(pos);
                    if (tile >= 0x54 && previous != 0x25)
                    {
                        if (previous != 0x49) tile++;
                        tile++;
                    }
                    Low(pos, tile, big ? "large_bush" : "large_bush_small");
                }
                pos = Right(pos);
            }
            Restore(); Down();
        }
    }

    // $0DB5B7. Length is the distance to the right edge, not a tile count.
    private void SmallBush()
    {
        var kind = _object.SizeOrType >> 4;
        if (kind >= 5) throw new InvalidDataException("Invalid small bush kind");
        var width = _object.SizeOrType & 15;
        if (width == 0) width = 256;
        var pos = WriteRight(_position, 0, _rom.ReadByte(0x0DB5A8 + kind), "small_bush_left");
        for (var col = 1; col < width; col++)
            pos = WriteRight(pos, 0, _rom.ReadByte(0x0DB5AD + kind), "small_bush_middle");
        Write(pos, 0, _rom.ReadByte(0x0DB5B2 + kind), "small_bush_right");
    }
    // $0DB73F. The first two rows have two and four tiles. Later rows
    // repeat the last five table entries and shift left across screens.
    private void DiagonalPipe()
    {
        var remaining = _object.SizeOrType >> 4;
        var widthMinusOne = 1;
        var index = 0;
        Preserve();
        while (true)
        {
            var pos = _position;
            for (var col = 0; col <= widthMinusOne; col++)
                pos = WriteRight(pos, 1, _rom.ReadByte(0x0DB72F + index++), "right_diagonal_pipe");
            Restore(); DownLeft();
            widthMinusOne += 2;
            if (--remaining < 0) break;
            if (index == 6)
            {
                widthMinusOne--;
                do
                {
                    pos = _position;
                    for (var col = 0; col <= widthMinusOne; col++)
                        pos = WriteRight(pos, 1, _rom.ReadByte(0x0DB72F + index++), "right_diagonal_pipe");
                    Restore(); DownLeft();
                    if (index == 16) index = 11;
                } while (--remaining >= 0);
                break;
            }
        }
        Write(Right(_position), 1, 0xEB, "right_diagonal_pipe");
    }
    // $0DB84E only changes the low byte. In particular, both ledge entry
    // routines have an LDA where a page-reset JSR appears intended. Preserve
    // the actual original write sequence and the previous page at that cell.
    private byte DirtLine(byte position, byte tile, string source)
    {
        var previous = PreviousLow(position);
        if (previous != 0x25)
        {
            if (previous != 0x3F) tile++;
            tile++;
        }
        Low(position, tile, source);
        return Right(position);
    }
    // $0DB7AA.
    private void DiagonalLedgeLeft()
    {
        var rows = _object.SizeOrType & 15;
        var lowerRows = _object.SizeOrType >> 4;
        var span = 1;
        Preserve();
        var pos = SlopeAir(_position, 0xAA, "left_diagonal_ledge_edge");
        DirtLine(pos, 0xA1, "left_diagonal_ledge_edge");
        while (true)
        {
            Restore(); DownLeft();
            span += 2;
            if (--rows < 0) break;
            pos = SlopeAir(_position, 0xAA, "left_diagonal_ledge_edge");
            pos = WriteRight(pos, 1, 0xE2, "left_diagonal_ledge_assist");
            for (var col = span - 2; col > 0; col--)
                pos = WriteRight(pos, 0, 0x3F, "left_diagonal_ledge_fill");
            Page(pos, 0);
            DirtLine(pos, 0xA6, "left_diagonal_ledge_edge");
        }
        _position = Right(_position);
        Preserve();
        var width = span - 1;
        pos = SlopeAir(_position, 0xF7, "left_diagonal_ledge_bottom");
        while (true)
        {
            for (var col = width - 1; col > 0; col--)
                pos = WriteRight(pos, 0, 0x3F, "left_diagonal_ledge_fill");
            Page(pos, 0);
            DirtLine(pos, 0xA6, "left_diagonal_ledge_edge");
            Restore(); DownRight();
            if (--lowerRows < 0) break;
            Page(_position, 0);
            pos = DirtLine(_position, 0xA3, "left_diagonal_ledge_edge");
        }
    }
    // $0DB863, including the unchanged high byte of the first dirt line.
    private void DiagonalLedgeRight()
    {
        var rows = _object.SizeOrType & 15;
        var lowerRows = _object.SizeOrType >> 4;
        var span = 1;
        Preserve();
        var pos = DirtLine(_position, 0xAF, "right_diagonal_ledge_edge");
        SlopeAir(pos, 0xAF, "right_diagonal_ledge_edge");
        while (true)
        {
            Restore(); DownLeft();
            span += 2;
            if (--rows < 0) break;
            Page(_position, 0);
            pos = DirtLine(_position, 0xA9, "right_diagonal_ledge_edge");
            for (var col = span - 2; col > 0; col--)
                pos = WriteRight(pos, 0, 0x3F, "right_diagonal_ledge_fill");
            pos = WriteRight(pos, 1, 0xE4, "right_diagonal_ledge_assist");
            SlopeAir(pos, 0xAF, "right_diagonal_ledge_edge");
        }
        var width = span - 1;
        Page(_position, 0);
        pos = DirtLine(_position, 0xA9, "right_diagonal_ledge_edge");
        for (var col = width - 1; col > 0; col--)
            pos = WriteRight(pos, 0, 0x3F, "right_diagonal_ledge_fill");
        Page(pos, 1);
        DirtLine(pos, 0xF9, "right_diagonal_ledge_bottom");
        while (true)
        {
            Restore(); DownLeft();
            if (--lowerRows < 0) break;
            Page(_position, 0);
            pos = DirtLine(_position, 0xA9, "right_diagonal_ledge_edge");
            for (var col = width - 1; col > 0; col--)
                pos = WriteRight(pos, 0, 0x3F, "right_diagonal_ledge_fill");
            Page(pos, 0);
            DirtLine(pos, 0xAC, "right_diagonal_ledge_edge");
        }
    }
}
