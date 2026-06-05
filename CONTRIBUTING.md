# Contributing

This repository is source-only. Contributions must not include ROMs, extracted assets, generated asset packs, screenshots or videos derived from third-party assets, saves, TAS downloads, or machine-local paths.

## Before Opening a Change

1. Keep generated output under `generated/` and leave it untracked.
2. Use placeholders such as `/path/to/compatible-rom.sfc` in documentation and scripts.
3. Run source-only checks when possible:

   ```bash
   tools/check-dotnet.sh
   ```

4. If your change depends on a local ROM dump, run the importer checks locally but do not commit the output:

   ```bash
   SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-importer.sh
   SMW_ROM_PATH=/path/to/compatible-rom.sfc tools/check-native-extractor.sh
   ```

5. Review `docs/RELEASE-HYGIENE.md` before publishing branches, tags, screenshots, or release artifacts.

## Implementation Expectations

- Prefer small, reference-backed runtime slices over broad rewrites.
- Mark incomplete behavior explicitly in docs and tests.
- Preserve deterministic input, trace, and headless repro paths for physics, collision, sprite, and block changes.
- Keep compatibility claims narrow. A level that boots is not necessarily playable or faithful.
