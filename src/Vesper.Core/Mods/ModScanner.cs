using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Vesper.Core.Mods;

public static partial class ModScanner
{
    public const string DisabledSuffix = ".disabled";

    public static IReadOnlyList<ModFile> Scan(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
            return [];

        var results = new List<ModFile>();

        foreach (var path in Directory.EnumerateFiles(modsDirectory))
        {
            var name = Path.GetFileName(path);
            var disabled = name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
            var bare = disabled ? name[..^DisabledSuffix.Length] : name;

            if (!bare.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add(Read(path, bare, disabled));
        }

        return results
            .OrderBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ModFile Read(string path, string fileName, bool disabled)
    {
        var mod = new ModFile
        {
            Path = path,
            FileName = fileName,
            IsDisabled = disabled,
            Name = Path.GetFileNameWithoutExtension(fileName),
        };

        try
        {
            mod.SizeBytes = new FileInfo(path).Length;

            using var archive = ZipFile.OpenRead(path);

            if (TryReadFabric(archive, mod))
                return mod;

            if (TryReadQuilt(archive, mod))
                return mod;

            if (TryReadForge(archive, mod))
                return mod;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException)
        {
            mod.Description = "This file could not be read as a mod archive";
        }

        return mod;
    }

    private static bool TryReadFabric(ZipArchive archive, ModFile mod)
    {
        var entry = archive.GetEntry("fabric.mod.json");

        if (entry is null)
            return false;

        using var stream = entry.Open();
        using var document = JsonDocument.Parse(ReadAll(stream), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        var root = document.RootElement;

        mod.Loader = "Fabric";
        mod.ModId = Text(root, "id");
        mod.Version = Text(root, "version");
        mod.Description = Text(root, "description");

        var name = Text(root, "name");
        if (!string.IsNullOrWhiteSpace(name))
            mod.Name = name;

        if (root.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            mod.Authors = string.Join(", ", authors.EnumerateArray().Select(AuthorName).Where(a => a.Length > 0));

        return true;
    }

    private static bool TryReadQuilt(ZipArchive archive, ModFile mod)
    {
        var entry = archive.GetEntry("quilt.mod.json");

        if (entry is null)
            return false;

        using var stream = entry.Open();
        using var document = JsonDocument.Parse(ReadAll(stream), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });

        mod.Loader = "Quilt";

        if (!document.RootElement.TryGetProperty("quilt_loader", out var loader))
            return true;

        mod.ModId = Text(loader, "id");
        mod.Version = Text(loader, "version");

        if (loader.TryGetProperty("metadata", out var metadata))
        {
            var name = Text(metadata, "name");
            if (!string.IsNullOrWhiteSpace(name))
                mod.Name = name;

            mod.Description = Text(metadata, "description");
        }

        return true;
    }

    private static bool TryReadForge(ZipArchive archive, ModFile mod)
    {
        var entry = archive.GetEntry("META-INF/neoforge.mods.toml");
        var loader = "NeoForge";

        if (entry is null)
        {
            entry = archive.GetEntry("META-INF/mods.toml");
            loader = "Forge";
        }

        if (entry is null)
            return false;

        using var stream = entry.Open();
        var toml = ReadAll(stream);

        mod.Loader = loader;
        mod.ModId = TomlValue(toml, "modId");
        mod.Version = TomlValue(toml, "version");
        mod.Description = TomlValue(toml, "description");
        mod.Authors = TomlValue(toml, "authors");

        var name = TomlValue(toml, "displayName");
        if (!string.IsNullOrWhiteSpace(name))
            mod.Name = name;

        if (mod.Version is "${file.jarVersion}" or "")
            mod.Version = string.Empty;

        return true;
    }

    private static string AuthorName(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Object => Text(element, "name"),
        _ => string.Empty,
    };

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string TomlValue(string toml, string key)
    {
        var match = Regex.Match(
            toml,
            key + @"\s*=\s*(?:'''(?<v>[\s\S]*?)'''|""""""(?<v>[\s\S]*?)""""""|""(?<v>[^""]*)""|'(?<v>[^']*)')",
            RegexOptions.IgnoreCase);

        return match.Success ? match.Groups["v"].Value.Trim() : string.Empty;
    }

    private static string ReadAll(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
