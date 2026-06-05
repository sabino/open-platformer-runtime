# Browser Loader

This directory contains the first browser-facing entrypoint for the runtime.

The page lets a user choose a local ROM file with the browser's native file picker, validates the file entirely in browser memory, probes the ROM tables needed by the importer, and can save a small browser manifest. It does not upload the ROM and it does not write generated assets into the repository.

The playable browser runtime is not connected yet. The current Godot project is a Godot 4 .NET/C# runtime that loads generated files from `res://generated/smw/`; the browser path needs a web-safe importer plus a runtime asset provider before it can actually launch gameplay from the uploaded file.

The public deployment is GitHub Pages:

```text
https://sabino.pro/open-platformer-runtime/
```

Run the static loader locally:

```bash
python3 -m http.server 8765 -d web
```

Then open:

```text
http://localhost:8765
```

Serving from `localhost` keeps the Web Crypto API available for SHA-1 validation.
