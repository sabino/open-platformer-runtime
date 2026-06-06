# Browser Loader

This directory contains the browser-facing entrypoint for the runtime.

The page lets a user choose a local ROM file with the browser's native file picker, validates the file entirely in browser memory, reads the available level index and names from the ROM, and streams generated assets into the experimental Godot Web runtime. Level search and selection happen inside the running Godot course selector, not in the surrounding HTML page. It does not upload the ROM and it does not write generated assets into the repository.

The playable browser runtime uses a custom experimental Godot 4 .NET/Web build documented in `docs/WEB-DOTNET-PROTOTYPE.md`.

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
