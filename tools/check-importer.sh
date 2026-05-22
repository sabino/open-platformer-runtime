#!/usr/bin/env bash
set -euo pipefail

ROM_PATH="${1:-${SMW_ROM_PATH:-/path/to/compatible-rom.sfc}}"
OUT_DIR="${2:-generated/smw}"

tools/import-smw.sh "$ROM_PATH" "$OUT_DIR"

python3 - "$OUT_DIR" <<'PY'
import json
import sys
from pathlib import Path

out_dir = Path(sys.argv[1])
manifest = json.loads((out_dir / "manifest.json").read_text())
levels = manifest["levels"]
assert "105" in levels, "missing Yoshi Island 1 level 105"
assert "0CB" in levels, "missing direct pipe target level 0CB"

level_105 = json.loads((out_dir / levels["105"]["file"]).read_text())
exits = level_105["screen_exits"]
assert exits, "level 105 should contain at least one screen exit"
pipe_exit = next((exit for exit in exits if exit["screen"] == 7), None)
assert pipe_exit is not None, "level 105 screen 07 pipe exit missing"
assert pipe_exit["exit_low"] == 0xCB, pipe_exit
assert pipe_exit["vanilla_destination"] == 0x0CB, pipe_exit
assert pipe_exit["vanilla_secondary"] == 0, pipe_exit
assert pipe_exit["raw_r11"] == 0, pipe_exit

for png in [
    out_dir / "player" / "gfx32_player_palette0.png",
    out_dir / "player" / "gfx33_player_palette0.png",
    out_dir / "tilesets" / "level_105_tileset7_8x8.png",
    out_dir / "tilesets" / "level_105_tileset7_map16_preview.png",
    out_dir / "tilesets" / "level_0CB_tileset8_8x8.png",
    out_dir / "tilesets" / "level_0CB_tileset8_map16_preview.png",
    out_dir / "spritesets" / "level_105_spritegfx8_8x8.png",
    out_dir / "spritesets" / "level_0CB_spritegfx4_8x8.png",
    out_dir / "levels" / "level_105_partial_layout.png",
    out_dir / "levels" / "level_0CB_partial_layout.png",
]:
    data = png.read_bytes()
    assert data.startswith(b"\x89PNG\r\n\x1a\n"), png
    assert data[12:16] == b"IHDR", png
    width = int.from_bytes(data[16:20], "big")
    height = int.from_bytes(data[20:24], "big")
    assert width > 0 and height > 0, (png, width, height)

for palette_json in [
    out_dir / "palettes" / "level_105_palette.json",
    out_dir / "palettes" / "level_0CB_palette.json",
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
assert tilemap["placed_tile_count"] > 1000, tilemap["placed_tile_count"]
assert tilemap["preview_png"]["file"] == "levels/level_105_partial_layout.png"
assert tilemap["preview_png"]["rendered_tile_count"] == tilemap["placed_tile_count"]
assert any("Map16 tile word's palette" in note for note in tilemap["notes"]), tilemap["notes"]
assert tilemap["palette_assets"]["file"] == "palettes/level_105_palette.json"
assert tilemap["map16_pointer_source"] == {"source": "native_initialize_map16_pointers", "tileset": 7}
assert tilemap["gfx_source"]["source"] == "vanilla_fg_bg_gfx_list"

pipe_target_tilemap = json.loads((out_dir / "levels" / "level_0CB_partial_tilemap.json").read_text())
assert pipe_target_tilemap["placed_tile_count"] > 100, pipe_target_tilemap["placed_tile_count"]
assert pipe_target_tilemap["unsupported_object_counts"] == {}, pipe_target_tilemap["unsupported_object_counts"]
target_sources = {tile["source"] for tile in pipe_target_tilemap["placed_tiles"]}
assert "horizontal_pipe_end" in target_sources, target_sources
assert "rope_mushroom_top_left" in target_sources, target_sources
assert "rope_mushroom_column_left" in target_sources, target_sources

pipe_target_sprite_tileset = json.loads((out_dir / "spritesets" / "level_0CB_spritegfx4.json").read_text())
assert pipe_target_sprite_tileset["sprite_graphics"] == 4, pipe_target_sprite_tileset
assert [entry["gfx_id"] for entry in pipe_target_sprite_tileset["uploads"]] == ["06", "13", "01", "00"], pipe_target_sprite_tileset["uploads"]
assert pipe_target_sprite_tileset["gfx_source"]["source"] == "vanilla_sprite_gfx_list"

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

print("smw-import check: YI1 pipe route 105 screen 07 -> 0CB secondary=0")
print("smw-import check: player GFX32/GFX33 PNG atlases and categorization manifest present")
print("smw-import check: per-level full CGRAM palettes generated for 105 and 0CB")
print("smw-import check: level 105 tileset 7 GFX atlas and Map16 preview use level CGRAM rows")
print("smw-import check: level 105 and 0CB sprite GFX VRAM atlases use level sprite palette rows")
print("smw-import check: level 105 partial layout preview uses Map16 palette bits against full CGRAM")
print("smw-import check: level 0CB rope pipe target layout expands horizontal pipes and mushroom platforms")
print("smw-import check: SPC banks and BRR preview WAVs present")
PY
