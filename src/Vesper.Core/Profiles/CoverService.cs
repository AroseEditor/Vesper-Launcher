using Vesper.Core.Storage;

namespace Vesper.Core.Profiles;

public enum CoverSource
{
    UserFile,
    RemoteIcon,
    Procedural,
}

public sealed record CoverResolution(CoverSource Source, string? FilePath, string? Url, int Seed);

public sealed class CoverService
{
    public const string CoversFolder = "covers";

    private readonly VesperPaths _paths;

    public CoverService(VesperPaths paths) => _paths = paths;

    public string CoversDirectory => Path.Combine(_paths.Root, CoversFolder);

    public string CoverPathFor(string profileId) =>
        Path.Combine(CoversDirectory, profileId + ".png");

    public CoverResolution Resolve(Profile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.CoverPath) && File.Exists(profile.CoverPath))
            return new CoverResolution(CoverSource.UserFile, profile.CoverPath, null, 0);

        var stored = CoverPathFor(profile.Id);
        if (File.Exists(stored))
            return new CoverResolution(CoverSource.UserFile, stored, null, 0);

        if (!string.IsNullOrWhiteSpace(profile.IconUrl))
            return new CoverResolution(CoverSource.RemoteIcon, null, profile.IconUrl, 0);

        return new CoverResolution(CoverSource.Procedural, null, null, SeedFor(profile));
    }

    public string SaveUserCover(string profileId, byte[] png)
    {
        Directory.CreateDirectory(CoversDirectory);
        var path = CoverPathFor(profileId);
        File.WriteAllBytes(path, png);
        return path;
    }

    public void ClearUserCover(string profileId)
    {
        var path = CoverPathFor(profileId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static int SeedFor(Profile profile)
    {
        var key = profile.MinecraftVersion + "|" + profile.Loader;
        var hash = 17;

        foreach (var c in key)
            hash = hash * 31 + c;

        return Math.Abs(hash);
    }
}
