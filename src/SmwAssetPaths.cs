using Godot;
using System;

namespace OpenPlatformerRuntime;

public static class SmwAssetPaths
{
    public const string ResourceBasePath = "res://generated/smw";
    public const string UserBasePath = "user://generated/smw";

    private static string? _forcedBasePath;

    public static string BasePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_forcedBasePath))
            {
                return _forcedBasePath;
            }

            return FileAccess.FileExists(UserPath("manifest.json"))
                ? UserBasePath
                : ResourceBasePath;
        }
    }

    public static string ManifestPath => Path("manifest.json");

    public static string Path(string relativePath)
    {
        return $"{BasePath}/{NormalizeRelativePath(relativePath)}";
    }

    public static string UserPath(string relativePath)
    {
        return $"{UserBasePath}/{NormalizeRelativePath(relativePath)}";
    }

    public static void PreferUserAssetPack()
    {
        _forcedBasePath = UserBasePath;
    }

    public static void ClearForcedBasePath()
    {
        _forcedBasePath = null;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', '/')
            .TrimStart('/');
    }
}
