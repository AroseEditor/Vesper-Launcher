using Avalonia.Media.Imaging;
using Vesper.App.Controls;
using Vesper.Core.Versions;

namespace Vesper.App.ViewModels;

public sealed class VersionCardViewModel
{
    public VersionCardViewModel(VersionGroup group)
    {
        Group = group;
        UpdateName = GetUpdateName(group.Name);
        Banner = BannerFactory.GetBannerForGroup(group.Name);
    }

    public VersionGroup Group { get; }

    public string Name => Group.Name;

    public string Subtitle => string.IsNullOrEmpty(UpdateName) || UpdateName == Name
        ? Group.Subtitle
        : $"{UpdateName} · {Group.Subtitle}";

    public string Newest => Group.Newest.Id;

    public string UpdateName { get; }

    public Bitmap Banner { get; }

    public static string GetUpdateName(string groupName) => groupName switch
    {
        "26.2" or "26.1" or "26.0" => "Winter Drop",
        "1.21" => "Tricky Trials",
        "1.20" => "Trails & Tales",
        "1.19" => "The Wild Update",
        "1.18" => "Caves & Cliffs II",
        "1.17" => "Caves & Cliffs I",
        "1.16" => "Nether Update",
        "1.15" => "Buzzy Bees",
        "1.14" => "Village & Pillage",
        "1.13" => "Update Aquatic",
        "1.12" => "World of Color",
        "1.11" => "Exploration Update",
        "1.10" => "Frostburn Update",
        "1.9" => "Combat Update",
        "1.8" => "Bountiful Update",
        "1.7" => "Update That Changed The World",
        "1.6" => "Horse Update",
        "1.5" => "Redstone Update",
        "1.4" => "Pretty Scary Update",
        "1.3" or "1.2" or "1.1" or "1.0" => "Release Era",
        "Snapshots" => "Snapshots",
        "Beta and Alpha" => "Classic Era",
        _ => "",
    };
}
