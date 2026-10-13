namespace OpenPlatformerRuntime.SmwAssets;

public sealed partial class SmwMap16Terrain
{
    // $0DAB6E/$0DAC21/$0DAC92 share traversal but use distinct edge and
    // interior writes. The last interior row is drawn without another edge.
    private void LeftSlope(int columns)
    {
        System.ReadOnlySpan<byte> edge = columns switch
        {
            1 => [0xAA], 2 => [0x96, 0x9B], _ => [0x6E, 0x73, 0x78, 0x7D],
        };
        var name = columns switch { 1 => "steep_left_slope", 2 => "normal_left_slope", _ => "gradual_left_slope" };
        var remaining = (_object.SizeOrType >> 4) + 1;
        var width = columns - 1;
        Preserve();
        while (true)
        {
            var pos = _position;
            foreach (var tile in edge) pos = SlopeAir(pos, tile, name + "_edge");
            DrawInterior(pos, width);
            Restore();
            width += columns;
            if (--remaining == 0)
            {
                DrawInterior(Down(), width);
                Restore();
                break;
            }
            SlopeDownLeft(columns);
        }
        void DrawInterior(byte pos, int size)
        {
            if (size < columns) return;
            // Local functions cannot capture spans; select the immediate
            // game-code tile IDs again, without keeping any extracted art.
            for (var col = 0; col < columns; col++)
            {
                var tile = columns switch
                {
                    1 => 0xE2, 2 => col == 0 ? 0xDE : 0xE6,
                    _ => col == 0 ? 0xD8 : col == 1 ? 0xDA : 0xE6,
                };
                pos = WriteRight(pos, 1, (byte)tile, name + "_surface");
            }
            for (var col = size - 2 * columns + 1; col > 0; col--)
                pos = WriteRight(pos, 0, 0x3F, name + "_fill");
        }
    }
    private byte SlopeDownLeft(int columns)
    {
        var delta = 16 - columns;
        var next = (byte)(_position + delta);
        if (_position + delta >= 256) VerticalCarry();
        if ((next & 15) >= delta)
        {
            if (next + 16 >= 256) VerticalCarry();
            next += 16;
            PreviousScreen();
        }
        return _position = next;
    }
    private byte SlopeDownRight(int columns, bool subtractOnBorrow)
    {
        var next = (byte)(_position + 16 + columns);
        if (_position + 16 + columns >= 256) VerticalCarry();
        if ((next & 15) < columns)
        {
            if (next < 16)
            {
                // $0DAFDF really subtracts a high-byte borrow. The normal
                // helper $0DA987 instead adds zero with clear carry.
                if (subtractOnBorrow) _pointer -= 256;
                _backup = (_backup & 255) | (_pointer & ~255);
            }
            next -= 16;
            NextScreen();
        }
        return _position = next;
    }
    // $0DAD44, including the final pair of interior tiles.
    private void NormalRightSlope()
    {
        var remaining = (_object.SizeOrType >> 4) + 1;
        var width = 1;
        var pos = _position;
        Preserve();
        do
        {
            pos = SlopeAir(pos, 0xA0, "normal_right_slope_edge");
            SlopeAir(pos, 0xA5, "normal_right_slope_edge");
            Restore(); Down();
            width += 2;
            remaining--;
            pos = _position;
            for (var col = width; col > 3; col--)
                pos = WriteRight(pos, 0, 0x3F, "normal_right_slope_fill");
            pos = WriteRight(pos, 1, 0xE6, "normal_right_slope_surface");
            pos = WriteRight(pos, 1, 0xE0, "normal_right_slope_surface");
        } while (remaining != 0);
    }
    // $0DAE6D/$0DAF61. After the first downward step, successive rows move
    // right by two/one columns. Only the steep case subtracts on borrow.
    private void UpsideDownLeftSlope(int columns)
    {
        var remaining = _object.SizeOrType >> 4;
        var width = columns * remaining - 1;
        var name = columns == 1 ? "upside_down_steep_left_slope" : "upside_down_normal_left_slope";
        Preserve();
        var pos = _position;
        DrawEdge(pos, width);
        var first = true;
        while (true)
        {
            Restore();
            if (first) { Down(); first = false; }
            else { width -= columns; SlopeDownRight(columns, subtractOnBorrow: columns == 1); }
            if (--remaining < 0) break;
            pos = WriteRight(_position, 1, (byte)(columns == 1 ? 0xC4 : 0xC6), name + "_surface");
            if (columns == 2) pos = WriteRight(pos, 1, 0xC7, name + "_surface");
            if (width - columns >= 0) DrawEdge(pos, width - columns);
        }
        void DrawEdge(byte position, int size)
        {
            position = WriteRight(position, 1, (byte)(columns == 1 ? 0xEC : 0xEE), name + "_edge");
            if (columns == 2) position = WriteRight(position, 1, 0xF0, name + "_edge");
            for (var col = size - columns + 1; col > 0; col--)
                position = WriteRight(position, 1, 0x65, name + "_fill");
        }
    }
    // $0DAEFC/$0DAFEA preserve vertical alignment and shrink the dirt row.
    private void UpsideDownRightSlope(int columns)
    {
        var remaining = _object.SizeOrType >> 4;
        var width = columns * remaining + columns - 1;
        var name = columns == 1 ? "upside_down_steep_right_slope" : "upside_down_normal_right_slope";
        Preserve();
        var first = true;
        do
        {
            var pos = _position;
            var count = width;
            while (count >= columns * 2)
            {
                pos = WriteRight(pos, 1, 0x65, name + "_fill");
                count--;
            }
            if (count >= columns)
            {
                pos = WriteRight(pos, 1, (byte)(columns == 1 ? 0xED : 0xF0), name + "_edge");
                if (columns == 2) pos = WriteRight(pos, 1, 0xEF, name + "_edge");
            }
            if (count < columns || !first)
            {
                pos = WriteRight(pos, 1, (byte)(columns == 1 ? 0xC5 : 0xC8), name + "_surface");
                if (columns == 2) WriteRight(pos, 1, 0xC9, name + "_surface");
            }
            Restore(); Down();
            width -= columns;
            first = false;
        } while (--remaining >= 0);
    }

    // $0DABFD. The fill variants depend on the previous low byte.
    private byte SlopeAir(byte position, byte tile, string source)
    {
        var previous = PreviousLow(position);
        for (var i = 2; i >= 0; i--)
            if (previous == _rom.ReadByte(0x0DABF7 + i))
            {
                tile += _rom.ReadByte(0x0DABFA + i);
                break;
            }
        return WriteRight(position, 1, tile, source);
    }
    // $0DADA3, used by 105. The final row contains an interior edge without
    // another slope surface; the next loop test stops before drawing it.
    private void SteepRightSlope()
    {
        var remaining = (_object.SizeOrType >> 4) + 1;
        var width = 0;
        var pos = _position;
        Preserve();
        while (remaining != 0)
        {
            SlopeAir(pos, 0xAF, "steep_right_slope_edge");
            Restore(); Down();
            width++;
            remaining--;
            pos = _position;
            for (var col = width; col > 1; col--)
                pos = WriteRight(pos, 0, 0x3F, "steep_right_slope_fill");
            pos = WriteRight(pos, 1, 0xE4, "steep_right_slope_surface");
        }
    }
    // $0DADEB. Each surface row has four quarter-height tiles, followed
    // on the next row by four interior tiles. The last interior row remains.
    private void GradualRightSlope()
    {
        var remaining = (_object.SizeOrType >> 4) + 1;
        var width = 3;
        var pos = _position;
        Preserve();
        do
        {
            pos = SlopeAir(pos, 0x82, "gradual_right_slope_edge");
            pos = SlopeAir(pos, 0x87, "gradual_right_slope_edge");
            pos = SlopeAir(pos, 0x8C, "gradual_right_slope_edge");
            SlopeAir(pos, 0x91, "gradual_right_slope_edge");
            Restore(); Down();
            width += 4;
            remaining--;
            pos = _position;
            for (var col = width; col > 7; col--)
                pos = WriteRight(pos, 0, 0x3F, "gradual_right_slope_fill");
            pos = WriteRight(pos, 1, 0xE6, "gradual_right_slope_surface");
            pos = WriteRight(pos, 1, 0xE6, "gradual_right_slope_surface");
            pos = WriteRight(pos, 1, 0xDB, "gradual_right_slope_surface");
            pos = WriteRight(pos, 1, 0xDC, "gradual_right_slope_surface");
        } while (remaining != 0);
    }
}
