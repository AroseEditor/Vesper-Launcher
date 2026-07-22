using Vesper.Core.Accounts;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Skins;

public sealed class SkinSync
{
    public const string GameFolder = "vesper";
    public const string SkinsFolder = "skins";
    public const string SkinFile = "skin.png";
    public const string CapeFile = "cape.png";
    public const string ModelFile = "model.txt";

    private readonly VesperPaths _paths;
    private readonly SkinStore _store;

    public SkinSync(VesperPaths paths)
    {
        _paths = paths;
        _store = new SkinStore(paths);
    }

    public string TargetDirectory(string profileId) =>
        Path.Combine(_paths.ProfileGameDir(profileId), GameFolder, SkinsFolder);

    public bool Apply(Profile profile, Account account)
    {
        var target = TargetDirectory(profile.Id);
        Directory.CreateDirectory(target);

        var wrote = false;
        var skin = _store.ReadSkin(account.Id);

        if (skin is not null)
        {
            File.WriteAllBytes(Path.Combine(target, SkinFile), skin);
            wrote = true;
        }
        else
        {
            Remove(Path.Combine(target, SkinFile));
        }

        var capePath = _store.CapePath(account.Id);

        if (File.Exists(capePath))
        {
            File.Copy(capePath, Path.Combine(target, CapeFile), overwrite: true);
            wrote = true;
        }
        else
        {
            Remove(Path.Combine(target, CapeFile));
        }

        File.WriteAllText(
            Path.Combine(target, ModelFile),
            account.SkinModel == SkinModel.Slim ? "slim" : "classic");

        return wrote;
    }

    private static void Remove(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
