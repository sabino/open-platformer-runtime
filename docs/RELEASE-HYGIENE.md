# Release Hygiene

This repository is intended to publish source code and tooling only. Keep the public tree free of proprietary game assets, generated extraction output, save data, captures, and machine-local paths.

## Required Checks

Before publishing a branch or tag:

0. Confirm the source license is present:

   ```bash
   test -f LICENSE
   ```

1. Confirm ignored generated data is not tracked:

   ```bash
   git ls-files generated .godot '*.sfc' '*.smc' '*.sav' '*.srm'
   git rev-list --objects --all | rg -i '\\.(sfc|smc|sav|srm|spc|brr|png|wav|ogg|mp3|flac|bin|mp4|webm|mov|zip|7z|rar|smv|bk2|lsmv|json)$|generated/|\\.godot/'
   ```

2. Confirm host-local paths and old public names are absent from current source and history. Add any project-specific trademark or title patterns locally before running:

   ```bash
   rg -n -i "$HOME|/media/$USER|<external-volume-name>" . --glob '!generated/**' --glob '!.godot/**'
   git grep -I -n -i -e "$HOME" -e "/media/$USER" -e '<external-volume-name>' $(git rev-list --all) -- .
   ```

3. Check that generated/cached binary output is not staged:

   ```bash
   git status --short
   git diff --cached --name-only | rg -i '(^generated/|^\\.godot/|\\.(sfc|smc|sav|srm|spc|brr|png|wav|ogg|mp3|flac|bin|mp4|webm|mov|zip|7z|rar|smv|bk2|lsmv)$)'
   ```

4. Build without requiring a local ROM path:

   ```bash
   tools/check-dotnet.sh
   ```

5. Run importer and ROM verification only with a user-provided local dump:

   ```bash
   SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-importer.sh
   SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-native-extractor.sh
   ```

6. Review the public status document and make sure it still matches the code:

   ```bash
   sed -n '1,220p' docs/STATUS.md
   ```

## Publishing Rules

- Do not commit `generated/`, `.godot/`, ROMs, saves, screenshots, videos, TAS downloads, or extracted binary/audio/PNG/JSON asset packs.
- Do not hardcode local filesystem paths. Use `SMW_ROM_PATH`, `SMW_NATIVE_ROOT`, and `GODOT_BIN`.
- Keep screenshots and videos out of the main public page unless they use clean placeholder assets.
- Keep the project title and README framing generic. Use the game name only where it is technically necessary to identify compatibility requirements.
- Treat generated manifests as local artifacts; they should record hashes and file names, not absolute user paths.
- Keep `docs/STATUS.md` honest. Do not imply broad level compatibility, complete physics, complete sprite behavior, complete audio, overworld progression, or save support until those systems are verified.
