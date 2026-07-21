using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Servers;

public sealed class ServerManager
{
    private readonly VesperPaths _paths;
    private readonly PaperApi _paper;

    public ServerManager(VesperPaths paths, PaperApi? paper = null)
    {
        _paths = paths;
        _paper = paper ?? new PaperApi();
    }

    public PaperApi Paper => _paper;

    public IReadOnlyList<ServerDefinition> LoadAll()
    {
        if (!Directory.Exists(_paths.ServersDir))
            return [];

        var result = new List<ServerDefinition>();

        foreach (var directory in Directory.EnumerateDirectories(_paths.ServersDir))
        {
            var definition = Load(Path.GetFileName(directory));

            if (definition is not null)
                result.Add(definition);
        }

        return result
            .OrderByDescending(s => s.LastStartedAt ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ServerDefinition? Load(string id)
    {
        var definition = VesperJson.Read<ServerDefinition>(DefinitionFile(id));

        if (definition is null)
            return null;

        definition.Id = id;
        return definition;
    }

    public ServerDefinition Create(string name, string minecraftVersion, string project = "paper")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Server name cannot be empty", nameof(name));

        var definition = new ServerDefinition
        {
            Id = AllocateId(name),
            Name = name.Trim(),
            Project = project,
            MinecraftVersion = minecraftVersion,
        };

        Directory.CreateDirectory(DirectoryFor(definition.Id));
        Save(definition);
        return definition;
    }

    public void Save(ServerDefinition definition) =>
        VesperJson.Write(DefinitionFile(definition.Id), definition);

    public void Delete(string id)
    {
        var directory = DirectoryFor(id);

        if (System.IO.Directory.Exists(directory))
            System.IO.Directory.Delete(directory, recursive: true);
    }

    public string DirectoryFor(string id) => _paths.ServerDir(id);

    public string JarPath(ServerDefinition definition) =>
        Path.Combine(DirectoryFor(definition.Id), definition.JarFileName);

    public bool IsInstalled(ServerDefinition definition) => File.Exists(JarPath(definition));

    public async Task InstallAsync(
        ServerDefinition definition,
        PaperBuild build,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        definition.Build = build.Id;
        definition.JarFileName = "server.jar";

        await _paper.DownloadAsync(build, JarPath(definition), progress, cancellationToken);

        AcceptEula(definition);
        WriteProperties(definition);
        Save(definition);
    }

    public void AcceptEula(ServerDefinition definition) =>
        File.WriteAllText(
            Path.Combine(DirectoryFor(definition.Id), "eula.txt"),
            "eula=true" + Environment.NewLine);

    public void WriteProperties(ServerDefinition definition)
    {
        var path = Path.Combine(DirectoryFor(definition.Id), "server.properties");
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["server-port"] = definition.Port.ToString(),
            ["max-players"] = definition.MaxPlayers.ToString(),
            ["motd"] = definition.Motd,
            ["online-mode"] = definition.OnlineMode ? "true" : "false",
        };

        if (File.Exists(path))
        {
            var merged = new List<string>();

            foreach (var line in File.ReadAllLines(path))
            {
                var separator = line.IndexOf('=');

                if (separator <= 0 || line.StartsWith('#'))
                {
                    merged.Add(line);
                    continue;
                }

                var key = line[..separator];

                if (values.TryGetValue(key, out var replacement))
                {
                    merged.Add($"{key}={replacement}");
                    values.Remove(key);
                }
                else
                {
                    merged.Add(line);
                }
            }

            merged.AddRange(values.Select(pair => $"{pair.Key}={pair.Value}"));
            File.WriteAllLines(path, merged);
            return;
        }

        File.WriteAllLines(path, values.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    public void MarkStarted(ServerDefinition definition)
    {
        definition.LastStartedAt = DateTimeOffset.UtcNow;
        Save(definition);
    }

    private string DefinitionFile(string id) => Path.Combine(DirectoryFor(id), "server.json");

    private string AllocateId(string name)
    {
        var slug = ProfileManager.Slugify(name);

        if (!System.IO.Directory.Exists(DirectoryFor(slug)))
            return slug;

        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{slug}-{n}";

            if (!System.IO.Directory.Exists(DirectoryFor(candidate)))
                return candidate;
        }

        return $"{slug}-{Guid.NewGuid():N}"[..32];
    }
}
