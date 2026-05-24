#!/usr/bin/env bash
set -euo pipefail

ROM_PATH="${1:-${SMW_ROM_PATH:-/path/to/compatible-rom.sfc}}"
OUT_DIR="${2:-generated/smw}"

tools/import-smw.sh "$ROM_PATH" "$OUT_DIR"

dotnet run --project tests/SmwAssetCheck/SmwAssetCheck.csproj -- "$OUT_DIR"
dotnet run --project tools/SmwAssetTool/SmwAssetTool.csproj -- verify-core "$ROM_PATH" "$OUT_DIR"

python3 - "$OUT_DIR" "$ROM_PATH" <<'PY'
import importlib.util
import json
import sys
from collections import Counter
from pathlib import Path

out_dir = Path(sys.argv[1])
rom_path = Path(sys.argv[2])
manifest = json.loads((out_dir / "manifest.json").read_text())
levels = manifest["levels"]
assert "105" in levels, "missing Yoshi Island 1 level 105"
assert "1CB" in levels, "missing vanilla direct pipe target level 1CB"

level_105 = json.loads((out_dir / levels["105"]["file"]).read_text())
exits = level_105["screen_exits"]
assert exits, "level 105 should contain at least one screen exit"
pipe_exit = next((exit for exit in exits if exit["screen"] == 7), None)
assert pipe_exit is not None, "level 105 screen 07 pipe exit missing"
assert pipe_exit["exit_low"] == 0xCB, pipe_exit
assert pipe_exit["vanilla_destination"] == 0x1CB, pipe_exit
assert pipe_exit["vanilla_destination_low"] == 0xCB, pipe_exit
assert pipe_exit["vanilla_source_map_high"] == 1, pipe_exit
assert pipe_exit["vanilla_secondary"] == 0, pipe_exit
assert pipe_exit["raw_r11"] == 0, pipe_exit

player_pngs = [
    out_dir / "player" / f"gfx32_player_palette{i}.png" for i in range(4)
] + [
    out_dir / "player" / f"gfx33_player_palette{i}.png" for i in range(4)
]

for png in player_pngs + [
    out_dir / "tilesets" / "level_105_tileset7_8x8.png",
    out_dir / "tilesets" / "level_105_tileset7_map16_preview.png",
    out_dir / "tilesets" / "level_1CB_tileset3_8x8.png",
    out_dir / "tilesets" / "level_1CB_tileset3_map16_preview.png",
    out_dir / "spritesets" / "level_105_spritegfx8_8x8.png",
    out_dir / "spritesets" / "level_1CB_spritegfx8_8x8.png",
    out_dir / "levels" / "level_105_partial_layout.png",
    out_dir / "levels" / "level_105_layer2_background.png",
    out_dir / "levels" / "level_1CB_partial_layout.png",
    out_dir / "levels" / "level_1CB_layer2_background.png",
]:
    data = png.read_bytes()
    assert data.startswith(b"\x89PNG\r\n\x1a\n"), png
    assert data[12:16] == b"IHDR", png
    width = int.from_bytes(data[16:20], "big")
    height = int.from_bytes(data[20:24], "big")
    assert width > 0 and height > 0, (png, width, height)

for palette_json in [
    out_dir / "palettes" / "level_105_palette.json",
    out_dir / "palettes" / "level_1CB_palette.json",
]:
    level_palette = json.loads(palette_json.read_text())
    assert level_palette["status"] == "preview", level_palette
    assert level_palette["source"] == "vanilla_header_tables", level_palette
    assert len(level_palette["snes_bgr555"]) == 256, palette_json
    assert len(level_palette["rgb888"]) == 256, palette_json
    assert level_palette["layout"]["tilemap_palette_bits"].startswith("bits 10-12"), level_palette["layout"]

player_graphics = json.loads((out_dir / "player" / "player_graphics.json").read_text())
assert player_graphics["status"] == "partial"
assert player_graphics["categories"]["states_pending_direct_oam_port"], player_graphics
assert player_graphics["palette"]["layout"].startswith("full OBJ palette row 8"), player_graphics["palette"]
assert player_graphics["palette"]["snes_bgr555"][:8] == [0x0000, 0x7FDD, 0x0000, 0x0D71, 0x1E9B, 0x3B7F, 0x635F, 0x581D]
assert [variant["name"] for variant in player_graphics["palette"]["variants"]] == ["mario", "luigi", "fire_mario", "fire_luigi"]
assert player_graphics["palette"]["variants"][2]["snes_bgr555"][6:16] != player_graphics["palette"]["snes_bgr555"][6:16]
assert player_graphics["tile_pointer_tables"]["walking_pose_count"] == [1, 2, 2, 2]
assert player_graphics["tile_pointer_tables"]["animation_speed_table"][:16] == [10, 8, 6, 4, 3, 2, 1, 1, 10, 8, 6, 4, 3, 2, 1, 1]
assert len(player_graphics["tile_pointer_tables"]["animation_speed_table"]) == 112
oam_tables = player_graphics["oam_tables"]
assert oam_tables["player_xy_disp_index_index"]["source_addr"] == "0x00DCEC"
assert oam_tables["player_xy_disp_index"]["source_addr"] == "0x00DD32"
assert oam_tables["x_disp"]["source_addr"] == "0x00DD4E"
assert oam_tables["y_disp"]["source_addr"] == "0x00DE32"
assert oam_tables["powerup_tileset_index"]["source_addr"] == "0x00DF16"
assert oam_tables["tiles_index"]["source_addr"] == "0x00DF4C"
assert oam_tables["tiles"]["source_addr"] == "0x00DFDA"
assert oam_tables["head_tile_pointer_index"]["source_addr"] == "0x00E00C"
assert oam_tables["body_tile_pointer_index"]["source_addr"] == "0x00E0CC"
assert oam_tables["tile_x_flip"]["values"] == [0, 64]
assert oam_tables["powerup_tileset_index"]["values"] == [0, 70, 131, 70]
assert oam_tables["tiles"]["values"][:8] == [0, 2, 128, 128, 0, 2, 12, 128]
assert len(oam_tables["x_disp"]["values"]) == 114
assert len(oam_tables["tiles_index"]["values"]) == 192

tileset = json.loads((out_dir / "tilesets" / "level_105_tileset7.json").read_text())
assert tileset["status"] == "preview"
assert [entry["gfx_id"] for entry in tileset["uploads"]] == ["15", "1B", "17", "14"], tileset["uploads"]
assert tileset["atlas_png"]["file"] == "tilesets/level_105_tileset7_8x8.png"
assert tileset["map16_preview_png"]["file"] == "tilesets/level_105_tileset7_map16_preview.png"
assert tileset["palette_mapping"]["tile_word_palette_bits"] == "bits 10-12"
assert tileset["palette_mapping"]["cgram_row_indexing"] is True
assert tileset["palette_assets"]["file"] == "palettes/level_105_palette.json"
assert tileset["map16_pointer_source"] == {"source": "native_initialize_map16_pointers", "tileset": 7}
assert tileset["gfx_source"]["source"] == "vanilla_fg_bg_gfx_list"
assert tileset["vram"]["tile_count"] == 512, tileset["vram"]
assert [entry["tile_start"] for entry in tileset["uploads"]] == [384, 256, 128, 0], tileset["uploads"]
assert all(entry["tile_count"] == 128 for entry in tileset["uploads"]), tileset["uploads"]
assert tileset["map16_preview_png"]["map16_tile_count"] == 512, tileset["map16_preview_png"]

spec = importlib.util.spec_from_file_location("smw_import", Path("tools/smw_import.py"))
smw_import = importlib.util.module_from_spec(spec)
sys.modules["smw_import"] = smw_import
spec.loader.exec_module(smw_import)
rom = smw_import.Rom.load(rom_path)
level_1cb_map16 = smw_import.level_map16_words(rom, 3)
pipe_words = smw_import.map16_tile_words(level_1cb_map16, 0x133)
assert [word & 0x03FF for word in pipe_words] == [0, 1, 16, 17], pipe_words

sprite_tileset = json.loads((out_dir / "spritesets" / "level_105_spritegfx8.json").read_text())
assert sprite_tileset["status"] == "preview"
assert sprite_tileset["sprite_graphics"] == 8, sprite_tileset
assert [entry["gfx_id"] for entry in sprite_tileset["uploads"]] == ["20", "13", "01", "00"], sprite_tileset["uploads"]
assert sprite_tileset["atlas_png"]["file"] == "spritesets/level_105_spritegfx8_8x8.png"
assert sprite_tileset["vram"]["format"] == "snes_4bpp_tiles_in_sprite_vram_order_0x6000_to_0x7fff"
assert sprite_tileset["palette_mapping"]["final_oam_palette_selection_pending"] is True
assert sprite_tileset["palette_mapping"]["preview_row"] == 14
assert sprite_tileset["palette_assets"]["file"] == "palettes/level_105_palette.json"
assert sprite_tileset["gfx_source"]["source"] == "vanilla_sprite_gfx_list"
assert sprite_tileset["vram"]["tile_count"] == 512, sprite_tileset["vram"]
assert [entry["tile_start"] for entry in sprite_tileset["uploads"]] == [384, 256, 128, 0], sprite_tileset["uploads"]
assert [entry["vram_addr"] for entry in sprite_tileset["uploads"]] == ["0x7800", "0x7000", "0x6800", "0x6000"], sprite_tileset["uploads"]
assert all(entry["tile_count"] == 128 for entry in sprite_tileset["uploads"]), sprite_tileset["uploads"]

tilemap = json.loads((out_dir / "levels" / "level_105_partial_tilemap.json").read_text())
assert tilemap["status"] == "partial"
assert tilemap["placed_tile_count"] == 1616, tilemap["placed_tile_count"]
assert tilemap["unsupported_object_counts"] == {}, tilemap["unsupported_object_counts"]
assert tilemap["preview_png"]["file"] == "levels/level_105_partial_layout.png"
assert tilemap["preview_png"]["rendered_tile_count"] == tilemap["placed_tile_count"]
assert any("Map16 tile word's palette" in note for note in tilemap["notes"]), tilemap["notes"]
assert tilemap["palette_assets"]["file"] == "palettes/level_105_palette.json"
assert tilemap["map16_pointer_source"] == {"source": "native_initialize_map16_pointers", "tileset": 7}
assert tilemap["gfx_source"]["source"] == "vanilla_fg_bg_gfx_list"
first_object = level_105["layer1"]["objects"][0]
assert first_object["raw"] == [0x58, 0x10, 0xBF], first_object
assert first_object["placement"]["x_tile"] == 0, first_object["placement"]
assert first_object["placement"]["y_tile"] == 24, first_object["placement"]
assert first_object["placement"]["map16_offset"] == 0x180, first_object["placement"]
screen_jump_object = level_105["layer1"]["objects"][34]
post_jump_object = level_105["layer1"]["objects"][35]
assert screen_jump_object["raw"] == [0x0A, 0x00, 0x01], screen_jump_object
assert post_jump_object["placement"]["screen_cursor"] == 0x0B, post_jump_object["placement"]
assert post_jump_object["placement"]["x_tile"] == 0x0B2, post_jump_object["placement"]
tile_sources = {tile["source"] for tile in tilemap["placed_tiles"]}
assert "right_diagonal_pipe" in tile_sources, tile_sources
assert "left_diagonal_ledge_edge" in tile_sources, tile_sources
assert "extended_goal_marker" in tile_sources, tile_sources
assert "extended_midway_bar" in tile_sources, tile_sources
assert "extended_3up_moon" in tile_sources, tile_sources
assert "extended_invisible_1up" in tile_sources, tile_sources
assert "extended_question_block_flower" in tile_sources, tile_sources
assert "extended_yellow_switch_block" in tile_sources, tile_sources
assert "steep_right_slope_surface" in tile_sources, tile_sources
placed_by_coord = {(tile["x"], tile["y"], tile["source"]): tile["map16"] for tile in tilemap["placed_tiles"]}
assert placed_by_coord[(56, 18, "right_diagonal_pipe")] == 0x01C4, placed_by_coord.get((56, 18, "right_diagonal_pipe"))
assert placed_by_coord[(55, 19, "right_diagonal_pipe")] == 0x01C7, placed_by_coord.get((55, 19, "right_diagonal_pipe"))
assert placed_by_coord[(50, 24, "wide_scale_ledge_top")] == 0x0100, placed_by_coord.get((50, 24, "wide_scale_ledge_top"))
assert placed_by_coord[(13, 18, "left_diagonal_ledge_edge")] == 0x01AA, placed_by_coord.get((13, 18, "left_diagonal_ledge_edge"))
assert placed_by_coord[(11, 21, "left_diagonal_ledge_bottom")] == 0x01F7, placed_by_coord.get((11, 21, "left_diagonal_ledge_bottom"))
assert placed_by_coord[(17, 21, "left_diagonal_ledge_edge")] == 0x00A6, placed_by_coord.get((17, 21, "left_diagonal_ledge_edge"))
assert all(tile["x"] != 11 or tile["y"] != 22 for tile in tilemap["placed_tiles"]), placed_by_coord.get((11, 22, "left_diagonal_ledge_fill"))
assert placed_by_coord[(12, 22, "left_diagonal_ledge_fill")] == 0x00A3, placed_by_coord.get((12, 22, "left_diagonal_ledge_fill"))
assert placed_by_coord[(18, 22, "left_diagonal_ledge_edge")] == 0x00A6, placed_by_coord.get((18, 22, "left_diagonal_ledge_edge"))
assert placed_by_coord[(294, 22, "extended_goal_marker")] == 0x0066, placed_by_coord.get((294, 22, "extended_goal_marker"))
assert placed_by_coord[(295, 22, "extended_goal_marker")] == 0x0067, placed_by_coord.get((295, 22, "extended_goal_marker"))
assert placed_by_coord[(294, 23, "extended_goal_marker")] == 0x0068, placed_by_coord.get((294, 23, "extended_goal_marker"))
assert placed_by_coord[(295, 23, "extended_goal_marker")] == 0x0069, placed_by_coord.get((295, 23, "extended_goal_marker"))
assert placed_by_coord[(150, 21, "extended_midway_bar")] == 0x0035, placed_by_coord.get((150, 21, "extended_midway_bar"))
assert placed_by_coord[(151, 21, "extended_midway_bar")] == 0x0038, placed_by_coord.get((151, 21, "extended_midway_bar"))
assert placed_by_coord[(198, 4, "extended_3up_moon")] == 0x006E, placed_by_coord.get((198, 4, "extended_3up_moon"))
assert placed_by_coord[(209, 15, "extended_invisible_1up")] == 0x011A, placed_by_coord.get((209, 15, "extended_invisible_1up"))
assert placed_by_coord[(243, 17, "extended_question_block_flower")] == 0x0124, placed_by_coord.get((243, 17, "extended_question_block_flower"))
assert placed_by_coord[(156, 20, "extended_yellow_switch_block")] == 0x006B, placed_by_coord.get((156, 20, "extended_yellow_switch_block"))
assert all(tile["source"] != "right_diagonal_pipe" for tile in tilemap["placed_tiles"] if (tile["x"], tile["y"]) == (50, 24))

synthetic_right_ledge = smw_import.build_partial_level_tilemap(
    {"width_tiles": 32, "height_tiles": 32, "tileset": 7},
    [{
        "object_id": 0x3B,
        "size_or_type": 0x22,
        "placement": {"x_tile": 8, "y_tile": 8},
    }],
)
right_ledge_by_coord = {(tile["x"], tile["y"], tile["source"]): tile["map16"] for tile in synthetic_right_ledge["placed_tiles"]}
assert right_ledge_by_coord[(8, 8, "right_diagonal_ledge_edge")] == 0x00AF, right_ledge_by_coord
assert right_ledge_by_coord[(9, 8, "right_diagonal_ledge_edge")] == 0x01AF, right_ledge_by_coord
assert right_ledge_by_coord[(7, 9, "right_diagonal_ledge_edge")] == 0x00A9, right_ledge_by_coord
assert right_ledge_by_coord[(10, 9, "right_diagonal_ledge_edge")] == 0x01AF, right_ledge_by_coord
assert right_ledge_by_coord[(11, 11, "right_diagonal_ledge_bottom")] == 0x01F9, right_ledge_by_coord
assert right_ledge_by_coord[(4, 12, "right_diagonal_ledge_edge")] == 0x00A9, right_ledge_by_coord
assert right_ledge_by_coord[(10, 12, "right_diagonal_ledge_edge")] == 0x00AC, right_ledge_by_coord

layer2_bg = json.loads((out_dir / "levels" / "level_105_layer2_background.json").read_text())
assert layer2_bg["kind"] == "rle_background", layer2_bg
assert layer2_bg["placed_tile_count"] == 864, layer2_bg["placed_tile_count"]
assert layer2_bg["map16_page"] == 0, layer2_bg["map16_page"]
assert layer2_bg["preview_png"]["file"] == "levels/level_105_layer2_background.png"
assert layer2_bg["map16_pointer_source"]["base_addr"] == "0x0D9100"
assert levels["105"]["layer2_background"]["preview_png"] == "levels/level_105_layer2_background.png"

sprites = level_105["sprite_layer"]["sprites"]
assert sprites[0]["format"] == "yyyyEESY_XXXXssss_NNNNNNNN", sprites[0]
assert sprites[0]["screen"] == 0 and sprites[0]["x_px"] == 208 and sprites[0]["y_px"] == 272, sprites[0]
assert sprites[0]["sprite_id"] == 0xBD, sprites[0]
assert sprites[1]["screen"] == 1 and sprites[1]["x_px"] == 496 and sprites[1]["y_px"] == 304, sprites[1]
assert sprites[1]["sprite_id"] == 0x9F, sprites[1]
sprite_counts = Counter(sprite["sprite_id"] for sprite in sprites)
assert sprite_counts[0xAB] == 18, sprite_counts
assert sprite_counts[0x9F] == 4, sprite_counts
assert sprite_counts[0x4F] == 3, sprite_counts
assert sprite_counts[0xB9] == 2, sprite_counts

pipe_target_tilemap = json.loads((out_dir / "levels" / "level_1CB_partial_tilemap.json").read_text())
assert pipe_target_tilemap["placed_tile_count"] == 588, pipe_target_tilemap["placed_tile_count"]
assert pipe_target_tilemap["unsupported_object_counts"] == {}, pipe_target_tilemap["unsupported_object_counts"]
target_sources = {tile["source"] for tile in pipe_target_tilemap["placed_tiles"]}
assert "horizontal_pipe_end" in target_sources, target_sources
assert "underground_ceiling_ledge_fill" in target_sources, target_sources
assert "underground_ceiling_edge" in target_sources, target_sources
assert "extended_generic" in target_sources, target_sources
target_by_coord = {(tile["x"], tile["y"]): tile for tile in pipe_target_tilemap["placed_tiles"]}
assert target_by_coord[(31, 13)]["source"] == "extended_generic", target_by_coord[(31, 13)]
assert target_by_coord[(31, 13)]["map16"] == 0x01EC, target_by_coord[(31, 13)]
assert target_by_coord[(31, 22)]["source"] == "ground_edge_middle", target_by_coord[(31, 22)]
assert target_by_coord[(31, 23)]["source"] == "ground_edge_middle", target_by_coord[(31, 23)]

pipe_target_layer2_bg = json.loads((out_dir / "levels" / "level_1CB_layer2_background.json").read_text())
assert pipe_target_layer2_bg["placed_tile_count"] == 864, pipe_target_layer2_bg["placed_tile_count"]
assert pipe_target_layer2_bg["map16_page"] == 1, pipe_target_layer2_bg["map16_page"]

pipe_target_sprite_tileset = json.loads((out_dir / "spritesets" / "level_1CB_spritegfx8.json").read_text())
assert pipe_target_sprite_tileset["sprite_graphics"] == 8, pipe_target_sprite_tileset
assert [entry["gfx_id"] for entry in pipe_target_sprite_tileset["uploads"]] == ["20", "13", "01", "00"], pipe_target_sprite_tileset["uploads"]
assert pipe_target_sprite_tileset["gfx_source"]["source"] == "vanilla_sprite_gfx_list"

secondary_tables = json.loads((out_dir / "levels" / "secondary_tables.json").read_text())
assert secondary_tables["secondary_level_low_05f800"][0x1CB] == 0x05
assert secondary_tables["secondary_y_05fa00"][0x1CB] == 0xA9
assert secondary_tables["secondary_x_05fc00"][0x1CB] == 0x08
assert secondary_tables["secondary_entrance_type_05fe00"][0x1CB] == 0x06

audio = manifest["assets"]["audio"]
assert audio["status"] == "partial", audio
assert audio["sample_rate"] == 32000, audio
expected_banks = {
    "spc_engine": 6323,
    "spc_samples": 28538,
    "spc_level_music_bank": 16899,
    "spc_overworld_music_bank": 5667,
    "spc_credits_music_bank": 6624,
}
for name, expected_size in expected_banks.items():
    bank = audio["banks"][name]
    bank_path = out_dir / bank["file"]
    assert bank["length"] == expected_size, (name, bank["length"])
    assert bank_path.read_bytes(), bank_path
    assert bank_path.stat().st_size == expected_size, (bank_path, bank_path.stat().st_size)

audio_manifest = json.loads((out_dir / "audio" / "audio_manifest.json").read_text())
decoded_ids = [sample["id"] for sample in audio_manifest["decoded_samples"]]
assert decoded_ids == [9, 14, 16], decoded_ids
for sample in audio_manifest["decoded_samples"]:
    assert sample["sample_rate"] == 32000, sample
    assert sample["sample_count"] > 4000, sample
    wav_data = (out_dir / sample["file"]).read_bytes()
    assert wav_data[:4] == b"RIFF", sample["file"]
    assert wav_data[8:12] == b"WAVE", sample["file"]

print("smw-import check: YI1 pipe route 105 screen 07 -> 1CB secondary=0")
print("smw-import check: player GFX32/GFX33 PNG atlases and categorization manifest present")
print("smw-import check: per-level full CGRAM palettes generated for 105 and 1CB")
print("smw-import check: level 105 tileset 7 GFX atlas and Map16 preview use level CGRAM rows")
print("smw-import check: per-level Map16 quadrants reorder raw TL/BL/TR/BR words into render TL/TR/BL/BR order")
print("smw-import check: level 105 and 1CB sprite GFX VRAM atlases use level sprite palette rows")
print("smw-import check: level 105 partial layout preview uses Map16 palette bits against full CGRAM")
print("smw-import check: level 105 object placement follows Lunar Magic x=b1/y=b0 tile decode")
print("smw-import check: level 105 and 1CB Layer 2 RLE backgrounds decode to preview layers")
print("smw-import check: level 105 sprite positions decode with native screen/x/y bit layout")
print("smw-import check: level 1CB underground pipe target layout expands ceiling ledges, edges, and pipes")
print("smw-import check: SPC banks and BRR preview WAVs present")
PY
