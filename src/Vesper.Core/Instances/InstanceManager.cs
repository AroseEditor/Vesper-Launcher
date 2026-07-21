using System.Text;
using Vesper.Core.Storage;

namespace Vesper.Core.Instances;

public sealed class InstanceManager
{
    private readonly VesperPaths _paths;

    public InstanceManager(VesperPaths paths) => _paths = paths;

    public IReadOnlyList<Instance> LoadAll()
    {
        if (!Directory.Exists(_paths.InstancesDir))
            return [];

        var result = new List<Instance>();
        foreach (var dir in Directory.EnumerateDirectories(_paths.InstancesDir))
        {
            var id = Path.GetFileName(dir);
            var instance = Load(id);
            if (instance is not null)
                result.Add(instance);
        }

        return result
            .OrderByDescending(i => i.LastPlayedAt ?? DateTimeOffset.MinValue)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Instance? Load(string id)
    {
        var instance = VesperJson.Read<Instance>(_paths.InstanceFile(id));
        if (instance is null)
            return null;

        instance.Id = id;
        return instance;
    }

    public Instance Create(
        string name,
        string minecraftVersion,
        LoaderKind loader = LoaderKind.Vanilla,
        string? loaderVersion = null,
        bool isVesperProfile = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Instance name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            throw new ArgumentException("Minecraft version cannot be empty", nameof(minecraftVersion));

        if (isVesperProfile && !loader.SupportsVesperProfile())
            throw new ArgumentException("Vesper profiles require Fabric or Forge", nameof(loader));

        var instance = new Instance
        {
            Id = AllocateId(name),
            Name = name.Trim(),
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            IsVesperProfile = isVesperProfile,
        };

        Directory.CreateDirectory(_paths.InstanceGameDir(instance.Id));
        Directory.CreateDirectory(_paths.InstanceModsDir(instance.Id));
        Save(instance);
        return instance;
    }

    public void Save(Instance instance) =>
        VesperJson.Write(_paths.InstanceFile(instance.Id), instance);

    public void MarkPlayed(Instance instance)
    {
        instance.LastPlayedAt = DateTimeOffset.UtcNow;
        Save(instance);
    }

    public void Delete(string id)
    {
        var dir = _paths.InstanceDir(id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private string AllocateId(string name)
    {
        var slug = Slugify(name);
        if (!Directory.Exists(_paths.InstanceDir(slug)))
            return slug;

        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{slug}-{n}";
            if (!Directory.Exists(_paths.InstanceDir(candidate)))
                return candidate;
        }

        return $"{slug}-{Guid.NewGuid():N}"[..32];
    }

    public static string Slugify(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasDash = false;

        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length == 0 ? "instance" : slug;
    }
}
