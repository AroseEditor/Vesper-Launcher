using System.Text;
using Vesper.Core.Storage;

namespace Vesper.Core.Profiles;

public sealed class ProfileManager
{
    private readonly VesperPaths _paths;

    public ProfileManager(VesperPaths paths) => _paths = paths;

    public IReadOnlyList<Profile> LoadAll()
    {
        if (!Directory.Exists(_paths.ProfilesDir))
            return [];

        var result = new List<Profile>();
        foreach (var dir in Directory.EnumerateDirectories(_paths.ProfilesDir))
        {
            var id = Path.GetFileName(dir);
            var profile = Load(id);
            if (profile is not null)
                result.Add(profile);
        }

        return result
            .OrderByDescending(i => i.LastPlayedAt ?? DateTimeOffset.MinValue)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Profile? Load(string id)
    {
        var profile = VesperJson.Read<Profile>(_paths.ProfileFile(id));
        if (profile is null)
            return null;

        profile.Id = id;
        return profile;
    }

    public Profile Create(
        string name,
        string minecraftVersion,
        LoaderKind loader = LoaderKind.Vanilla,
        string? loaderVersion = null,
        bool isVesperProfile = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Profile name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            throw new ArgumentException("Minecraft version cannot be empty", nameof(minecraftVersion));

        if (isVesperProfile && !loader.SupportsVesperProfile())
            throw new ArgumentException("Vesper profiles require Fabric or Forge", nameof(loader));

        var profile = new Profile
        {
            Id = AllocateId(name),
            Name = name.Trim(),
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            LoaderVersion = loaderVersion,
            IsVesperProfile = isVesperProfile,
        };

        Directory.CreateDirectory(_paths.ProfileGameDir(profile.Id));
        Directory.CreateDirectory(_paths.ProfileModsDir(profile.Id));
        Save(profile);
        return profile;
    }

    public void Save(Profile profile) =>
        VesperJson.Write(_paths.ProfileFile(profile.Id), profile);

    public void MarkPlayed(Profile profile)
    {
        profile.LastPlayedAt = DateTimeOffset.UtcNow;
        Save(profile);
    }

    public void Delete(string id)
    {
        var dir = _paths.ProfileDir(id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    private string AllocateId(string name)
    {
        var slug = Slugify(name);
        if (!Directory.Exists(_paths.ProfileDir(slug)))
            return slug;

        for (var n = 2; n < 1000; n++)
        {
            var candidate = $"{slug}-{n}";
            if (!Directory.Exists(_paths.ProfileDir(candidate)))
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
        return slug.Length == 0 ? "profile" : slug;
    }
}
