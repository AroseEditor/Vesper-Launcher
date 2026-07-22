using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vesper.Core.Servers;

namespace Vesper.App.ViewModels;

public partial class ServerPropertyViewModel : ObservableObject
{
    private readonly Action _changed;

    [ObservableProperty]
    private string _value = string.Empty;

    public ServerPropertyViewModel(ServerPropertySpec spec, string value, Action changed)
    {
        Spec = spec;
        _value = value;
        _changed = changed;
    }

    public ServerPropertySpec Spec { get; }

    public string Key => Spec.Key;

    public string Label => Spec.Label;

    public IReadOnlyList<string> Options => Spec.Options ?? [];

    public int Minimum => Spec.Minimum;

    public int Maximum => Spec.Maximum;

    public bool IsToggle => Spec.Kind == ServerPropertyKind.Toggle;

    public bool IsNumber => Spec.Kind == ServerPropertyKind.Number;

    public bool IsChoice => Spec.Kind == ServerPropertyKind.Choice;

    public bool IsText => Spec.Kind == ServerPropertyKind.Text;

    public string RawLine => $"{Key}={Value}";

    public bool BoolValue
    {
        get => bool.TryParse(Value, out var parsed) && parsed;
        set => Value = value ? "true" : "false";
    }

    public int NumberValue
    {
        get => int.TryParse(Value, out var parsed) ? parsed : Minimum;
        set => Value = Math.Clamp(value, Minimum, Maximum).ToString();
    }

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(RawLine));
        OnPropertyChanged(nameof(BoolValue));
        OnPropertyChanged(nameof(NumberValue));
        _changed();
    }
}

public sealed class ServerPropertyGroupViewModel
{
    public ServerPropertyGroupViewModel(string name) => Name = name;

    public string Name { get; }

    public ObservableCollection<ServerPropertyViewModel> Items { get; } = [];
}
