using CmlLib.Core;

namespace Vesper.Core.Storage;

public sealed class VesperMinecraftPath : MinecraftPath
{
    public VesperMinecraftPath(VesperPaths paths, string instanceId)
        : base(paths.InstanceGameDir(instanceId))
    {
        Library = paths.SharedLibrariesDir;
        Versions = paths.SharedVersionsDir;
        Assets = paths.SharedAssetsDir;
        Runtime = paths.RuntimeDir;
    }

    public override string GetNativePath(string id) =>
        Path.GetFullPath(Path.Combine(BasePath, "natives", id));
}
