using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vesper.Core.Storage;

namespace Vesper.App.ViewModels;

public partial class ServersViewModel : ObservableObject
{
    private readonly VesperPaths _paths;

    [ObservableProperty]
    private string _statusText = "No servers yet";

    public ServersViewModel(VesperPaths paths) => _paths = paths;

    public ObservableCollection<string> Servers { get; } = [];

    public string ServersDirectory => _paths.ServersDir;

    public bool HasServers => Servers.Count > 0;
}
