# Experimental Godot .NET Web Export

This project can be prepared for the same experimental path used by Raul Santos' Godot 4 Web .NET prototype.

This is not the stock Godot 4.6.3 Mono export path. It requires a custom Godot editor and matching Web export templates built from `godotengine/godot#106125`, plus .NET SDK 9.0 and the `wasm-tools` workload.

This path now covers the first playable browser slice when paired with the `web/` loader: the browser validates a local ROM, Pyodide generates a focused level asset pack, and the custom Godot .NET Web export consumes those files through the web bridge. It is still experimental and does not replace the normal local Godot workflow.

The local Godot fork also carries Web .NET marshalling fixes needed by this project:

- typed `Array<T>` reads copy a native `Variant` before converting it to managed C# instead of reading directly from native array memory
- object `Variant` conversion resolves the object pointer directly from the native `Variant` instead of round-tripping only through the object id

## Local Requirements

- Linux custom Godot binary built from `godotengine/godot#106125`
- matching custom Web export template zip files from the same build
- .NET SDK 9.0+
- `wasm-tools` workload:

```bash
dotnet workload install wasm-tools
```

Check project-side prerequisites:

```bash
GODOT_WEB_DOTNET_BIN=/path/to/custom-godot tools/check-web-dotnet-prototype.sh
```

## Export

Install or pass the matching custom Web export templates, then run:

```bash
GODOT_WEB_DOTNET_BIN=/path/to/custom-godot tools/export-web-dotnet-prototype.sh
```

The export is written to:

```text
web-export/out/
```

That directory is ignored by git. Do not commit exported builds unless a release process explicitly chooses to publish generated web artifacts elsewhere.

## Serve Locally

Use the checked-in server so the preview has the same headers as the Pages/CDN path:

```bash
tools/serve-web-dotnet-prototype.sh
```

Then open the prepared public root, for example:

```text
http://127.0.0.1:8060
```

The server sends:

- `Cross-Origin-Opener-Policy: same-origin`
- `Cross-Origin-Embedder-Policy: require-corp`
- `Access-Control-Allow-Origin: *`

## GitHub Pages Deployment

The normal Pages workflow deploys the source-only browser ROM loader from `web/`.

The experimental workflow, `.github/workflows/experimental-web-dotnet-pages.yml`, can deploy an actual custom Godot .NET Web export when manually dispatched with:

- `godot_archive_url`: archive containing a Linux Godot binary built from `godotengine/godot#106125`
- `godot_binary_path`: optional path to the binary inside that archive
- `web_release_template_url`: optional matching custom Web release template zip
- `web_debug_template_url`: optional matching custom Web debug template zip

The workflow publishes:

```text
https://sabino.pro/open-platformer-runtime/
https://sabino.pro/open-platformer-runtime/experimental-godot/
```

Plain GitHub Pages does not let this repository set arbitrary HTTP response headers. The checked-in loader/export path is currently built without pthreads, but the custom domain/CDN should still preserve COOP/COEP headers for `/open-platformer-runtime/experimental-godot/*` if future templates require `SharedArrayBuffer`.

## Project-Side Changes

- `export_presets.cfg` mirrors the prototype's Web preset and includes the experimental `dotnet/*` options.
- `SmwGodotNative.csproj` keeps normal local builds on `net8.0`, but switches to `net9.0` when `GodotWebDotNetPrototype=true` is set.
- `src/web/MainJS.cs` provides the temporary managed `Main` shim needed by the prototype before .NET 10.
- `tools/patch-godot-web-dotnet-index.py` automates the prototype README's manual `DOTNET.setup` removal step when the generated `index.js` contains it.
