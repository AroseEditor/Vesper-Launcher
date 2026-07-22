namespace Vesper.Core.Servers;

public enum ServerPropertyKind
{
    Toggle,
    Number,
    Choice,
    Text,
}

public sealed record ServerPropertySpec(
    string Key,
    string Label,
    ServerPropertyKind Kind,
    string Default,
    string Group,
    IReadOnlyList<string>? Options = null,
    int Minimum = 0,
    int Maximum = 1000000);

public static class ServerPropertySchema
{
    public const string General = "General";
    public const string World = "World";
    public const string Players = "Players";
    public const string Performance = "Performance";
    public const string ResourcePack = "Resource pack";
    public const string Network = "Network";

    public static IReadOnlyList<ServerPropertySpec> All { get; } =
    [
        new("motd", "Message of the day", ServerPropertyKind.Text, "A Vesper server", General),
        new("max-players", "Slots", ServerPropertyKind.Number, "20", General, Minimum: 1, Maximum: 2000),
        new("gamemode", "Gamemode", ServerPropertyKind.Choice, "survival", General,
            ["survival", "creative", "adventure", "spectator"]),
        new("difficulty", "Difficulty", ServerPropertyKind.Choice, "easy", General,
            ["peaceful", "easy", "normal", "hard"]),
        new("force-gamemode", "Force gamemode", ServerPropertyKind.Toggle, "false", General),
        new("hardcore", "Hardcore", ServerPropertyKind.Toggle, "false", General),
        new("pvp", "PvP", ServerPropertyKind.Toggle, "true", General),

        new("level-name", "World name", ServerPropertyKind.Text, "world", World),
        new("level-seed", "World seed", ServerPropertyKind.Text, "", World),
        new("level-type", "World type", ServerPropertyKind.Choice, "minecraft:normal", World,
            ["minecraft:normal", "minecraft:flat", "minecraft:large_biomes", "minecraft:amplified"]),
        new("allow-nether", "Allow the Nether", ServerPropertyKind.Toggle, "true", World),
        new("generate-structures", "Generate structures", ServerPropertyKind.Toggle, "true", World),
        new("spawn-monsters", "Spawn monsters", ServerPropertyKind.Toggle, "true", World),
        new("spawn-animals", "Spawn animals", ServerPropertyKind.Toggle, "true", World),
        new("spawn-npcs", "Spawn villagers", ServerPropertyKind.Toggle, "true", World),
        new("spawn-protection", "Spawn protection", ServerPropertyKind.Number, "0", World, Maximum: 512),
        new("max-world-size", "Max world size", ServerPropertyKind.Number, "29999984", World, Maximum: 29999984),

        new("online-mode", "Online mode", ServerPropertyKind.Toggle, "true", Players),
        new("white-list", "Whitelist", ServerPropertyKind.Toggle, "false", Players),
        new("enforce-whitelist", "Enforce whitelist", ServerPropertyKind.Toggle, "false", Players),
        new("allow-flight", "Allow flight", ServerPropertyKind.Toggle, "false", Players),
        new("enable-command-block", "Command blocks", ServerPropertyKind.Toggle, "false", Players),
        new("op-permission-level", "Operator level", ServerPropertyKind.Number, "4", Players, Maximum: 4),
        new("player-idle-timeout", "Idle timeout in minutes", ServerPropertyKind.Number, "0", Players, Maximum: 1440),

        new("view-distance", "View distance", ServerPropertyKind.Number, "10", Performance, Minimum: 2, Maximum: 32),
        new("simulation-distance", "Simulation distance", ServerPropertyKind.Number, "10", Performance, Minimum: 2, Maximum: 32),
        new("max-tick-time", "Max tick time", ServerPropertyKind.Number, "60000", Performance, Maximum: 600000),
        new("sync-chunk-writes", "Sync chunk writes", ServerPropertyKind.Toggle, "true", Performance),

        new("require-resource-pack", "Resource pack required", ServerPropertyKind.Toggle, "false", ResourcePack),
        new("resource-pack", "Resource pack URL", ServerPropertyKind.Text, "", ResourcePack),
        new("resource-pack-prompt", "Resource pack prompt", ServerPropertyKind.Text, "", ResourcePack),
        new("resource-pack-sha1", "Resource pack SHA1", ServerPropertyKind.Text, "", ResourcePack),

        new("server-port", "Port", ServerPropertyKind.Number, "25565", Network, Minimum: 1, Maximum: 65535),
        new("server-ip", "Bind address", ServerPropertyKind.Text, "", Network),
        new("enable-rcon", "Enable RCON", ServerPropertyKind.Toggle, "false", Network),
        new("rcon.port", "RCON port", ServerPropertyKind.Number, "25575", Network, Minimum: 1, Maximum: 65535),
        new("rcon.password", "RCON password", ServerPropertyKind.Text, "", Network),
        new("enable-query", "Enable query", ServerPropertyKind.Toggle, "false", Network),
        new("network-compression-threshold", "Compression threshold", ServerPropertyKind.Number, "256", Network, Maximum: 65535),
    ];

    public static IReadOnlyList<string> Groups { get; } =
        [General, World, Players, Performance, ResourcePack, Network];
}

public sealed class ServerProperties
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    public IReadOnlyDictionary<string, string> Values => _values;

    public static ServerProperties Load(string path)
    {
        var properties = new ServerProperties();

        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith('#') || line.Length == 0)
                    continue;

                var separator = line.IndexOf('=');

                if (separator <= 0)
                    continue;

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..];

                properties._values[key] = value;
                properties._order.Add(key);
            }
        }

        foreach (var spec in ServerPropertySchema.All)
        {
            if (!properties._values.ContainsKey(spec.Key))
                properties._values[spec.Key] = spec.Default;
        }

        return properties;
    }

    public string Get(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;

    public void Set(string key, string value) => _values[key] = value;

    public bool GetBool(string key) =>
        bool.TryParse(Get(key), out var value) && value;

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(Get(key), out var value) ? value : fallback;

    public void Save(string path)
    {
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        foreach (var key in _order)
        {
            if (!written.Add(key))
                continue;

            lines.Add($"{key}={Get(key)}");
        }

        foreach (var pair in _values.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (written.Add(pair.Key))
                lines.Add($"{pair.Key}={pair.Value}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }
}
