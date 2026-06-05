# Experimental Godot .NET Web Export

`web-export/out/` is ignored by git and is reserved for browser builds created with a custom Godot editor/export-template build that includes `godotengine/godot#106125`.

Build locally:

```bash
GODOT_WEB_DOTNET_BIN=/path/to/custom-godot tools/export-web-dotnet-prototype.sh
```

Serve locally with the headers required by the prototype:

```bash
tools/serve-web-dotnet-prototype.sh
```
