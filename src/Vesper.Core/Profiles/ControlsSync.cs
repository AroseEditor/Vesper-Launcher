using Vesper.Core.Storage;

namespace Vesper.Core.Profiles;

public sealed class ControlsSync
{
    public const string MasterFileName = "controls.txt";
    public const string KeyPrefix = "key_";

    private static readonly string[] VanillaBindings =
    [
        "key.attack", "key.use", "key.forward", "key.left", "key.back", "key.right",
        "key.jump", "key.sneak", "key.sprint", "key.drop", "key.inventory", "key.chat",
        "key.playerlist", "key.pickItem", "key.command", "key.socialInteractions",
        "key.screenshot", "key.togglePerspective", "key.smoothCamera", "key.fullscreen",
        "key.spectatorOutlines", "key.swapOffhand", "key.saveToolbarActivator",
        "key.loadToolbarActivator", "key.advancements",
        "key.hotbar.1", "key.hotbar.2", "key.hotbar.3", "key.hotbar.4", "key.hotbar.5",
        "key.hotbar.6", "key.hotbar.7", "key.hotbar.8", "key.hotbar.9",
    ];

    private static readonly string[] ControlSettings =
    [
        "mouseSensitivity", "invertYMouse", "autoJump", "toggleCrouch", "toggleSprint",
        "mouseWheelSensitivity", "rawMouseInput", "discrete_mouse_scroll",
    ];

    private readonly VesperPaths _paths;

    public ControlsSync(VesperPaths paths) => _paths = paths;

    public string MasterFile => Path.Combine(_paths.Root, MasterFileName);

    public static IReadOnlySet<string> VanillaKeys { get; } =
        VanillaBindings.Select(b => KeyPrefix + b).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> SyncedSettings { get; } =
        ControlSettings.ToHashSet(StringComparer.Ordinal);

    public static bool IsSynced(string option) =>
        VanillaKeys.Contains(option) || SyncedSettings.Contains(option);

    public static bool IsModBinding(string option) =>
        option.StartsWith(KeyPrefix, StringComparison.Ordinal) && !VanillaKeys.Contains(option);

    public Dictionary<string, string> LoadMaster() => ReadOptions(MasterFile);

    public void SaveMaster(IReadOnlyDictionary<string, string> values)
    {
        Directory.CreateDirectory(_paths.Root);

        var lines = values
            .Where(pair => IsSynced(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}:{pair.Value}");

        File.WriteAllLines(MasterFile, lines);
    }

    public int CaptureFrom(string optionsPath)
    {
        if (!File.Exists(optionsPath))
            return 0;

        var options = ReadOptions(optionsPath);
        var master = LoadMaster();
        var captured = 0;

        foreach (var pair in options)
        {
            if (!IsSynced(pair.Key))
                continue;

            if (!master.TryGetValue(pair.Key, out var existing) || existing != pair.Value)
                captured++;

            master[pair.Key] = pair.Value;
        }

        if (captured > 0 || master.Count > 0)
            SaveMaster(master);

        return captured;
    }

    public int ApplyTo(string optionsPath)
    {
        var master = LoadMaster();

        if (master.Count == 0)
            return 0;

        var applied = 0;

        if (!File.Exists(optionsPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(optionsPath)!);
            File.WriteAllLines(
                optionsPath,
                master.Where(p => IsSynced(p.Key)).Select(p => $"{p.Key}:{p.Value}"));

            return master.Count;
        }

        var lines = File.ReadAllLines(optionsPath).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < lines.Count; i++)
        {
            var separator = lines[i].IndexOf(':');

            if (separator <= 0)
                continue;

            var key = lines[i][..separator];

            if (!IsSynced(key) || !master.TryGetValue(key, out var value))
                continue;

            seen.Add(key);

            var replacement = $"{key}:{value}";

            if (lines[i] != replacement)
            {
                lines[i] = replacement;
                applied++;
            }
        }

        foreach (var pair in master)
        {
            if (IsSynced(pair.Key) && seen.Add(pair.Key))
            {
                lines.Add($"{pair.Key}:{pair.Value}");
                applied++;
            }
        }

        File.WriteAllLines(optionsPath, lines);
        return applied;
    }

    public string OptionsFileFor(string profileId) =>
        Path.Combine(_paths.ProfileGameDir(profileId), "options.txt");

    private static Dictionary<string, string> ReadOptions(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!File.Exists(path))
            return values;

        foreach (var line in File.ReadAllLines(path))
        {
            var separator = line.IndexOf(':');

            if (separator <= 0)
                continue;

            values[line[..separator]] = line[(separator + 1)..];
        }

        return values;
    }
}
