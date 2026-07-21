using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Controls;
using Vesper.Core.Accounts;
using Vesper.Core.Skins;
using Vesper.Core.Storage;

namespace Vesper.App.ViewModels;

public partial class SkinsViewModel : ObservableObject
{
    private readonly AccountManager _accounts;
    private readonly SkinStore _store;
    private readonly Stack<uint[]> _undo = new();

    [ObservableProperty]
    private Account? _selectedAccount;

    [ObservableProperty]
    private uint[]? _pixels;

    [ObservableProperty]
    private bool _slim;

    [ObservableProperty]
    private bool _showOverlay = true;

    [ObservableProperty]
    private bool _paintMode;

    [ObservableProperty]
    private bool _eraseMode;

    [ObservableProperty]
    private string _brushColor = "#B57EDC";

    [ObservableProperty]
    private int _brushSize = 1;

    [ObservableProperty]
    private string _statusText = "Pick an account to edit its skin";

    public SkinsViewModel(VesperPaths paths, AccountManager accounts)
    {
        _accounts = accounts;
        _store = new SkinStore(paths);

        foreach (var swatch in DefaultPalette)
            Palette.Add(swatch);
    }

    public static IReadOnlyList<string> DefaultPalette { get; } =
    [
        "#0A0A0C", "#3A3A47", "#8A8A9A", "#F2EEF6",
        "#B57EDC", "#9A5FC4", "#D14FE8", "#5E3A86",
        "#C89976", "#B08566", "#8A5A3C", "#3A2A22",
        "#E5646B", "#E0B252", "#63D29B", "#4F8BE8",
    ];

    public ObservableCollection<Account> Accounts { get; } = [];

    public ObservableCollection<string> Palette { get; } = [];

    public bool HasSkin => Pixels is not null;

    public bool CanUndo => _undo.Count > 0;

    public string ModelLabel => Slim ? "Slim arms" : "Classic arms";

    public void Load()
    {
        Accounts.Clear();
        foreach (var account in _accounts.All)
            Accounts.Add(account);

        SelectedAccount ??= _accounts.Selected;

        if (SelectedAccount is null)
            StatusText = "Add an account first";
    }

    [RelayCommand]
    private void SelectAccount(Account account) => SelectedAccount = account;

    [RelayCommand]
    private void ToggleModel()
    {
        Slim = !Slim;

        if (SelectedAccount is not null)
        {
            SelectedAccount.SkinModel = Slim ? SkinModel.Slim : SkinModel.Classic;
            _accounts.Save();
        }

        OnPropertyChanged(nameof(ModelLabel));
    }

    [RelayCommand]
    private void TogglePaint()
    {
        PaintMode = !PaintMode;
        StatusText = PaintMode
            ? "Painting. Click the model to colour a pixel."
            : "Drag the model to rotate it.";
    }

    [RelayCommand]
    private void ToggleErase()
    {
        EraseMode = !EraseMode;
        if (EraseMode)
            PaintMode = true;
    }

    [RelayCommand]
    private void PickColor(string color)
    {
        BrushColor = color;
        EraseMode = false;
        PaintMode = true;
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undo.Count == 0)
            return;

        Pixels = _undo.Pop();
        OnPropertyChanged(nameof(CanUndo));
        StatusText = "Undid the last stroke";
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        PushUndo();
        Slim = SelectedAccount?.SkinModel == SkinModel.Slim;
        Pixels = SkinStore.CreateDefaultSkin(Slim);
        StatusText = "Reset to the default Vesper skin";
    }

    [RelayCommand]
    private void Save()
    {
        if (SelectedAccount is null || Pixels is null)
            return;

        try
        {
            _store.WriteSkin(SelectedAccount.Id, SkinImage.Encode(Pixels));
            SelectedAccount.HasCustomSkin = true;
            SelectedAccount.SkinModel = Slim ? SkinModel.Slim : SkinModel.Classic;
            _accounts.Save();
            StatusText = "Saved skin for " + SelectedAccount.Username;
        }
        catch (Exception e)
        {
            StatusText = "Could not save: " + e.Message;
        }
    }

    public void ImportPng(byte[] png)
    {
        var decoded = SkinImage.Decode(png);

        if (decoded is null)
        {
            StatusText = "That is not a valid Minecraft skin. Use a 64x64 or 64x32 PNG.";
            return;
        }

        PushUndo();
        Pixels = decoded;
        StatusText = "Loaded skin. Remember to save.";
    }

    public void Paint(int x, int y)
    {
        if (Pixels is null)
            return;

        var color = EraseMode ? 0u : ParseColor(BrushColor);
        var radius = Math.Max(0, BrushSize - 1);
        var changed = false;

        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var px = x + dx;
                var py = y + dy;

                if (px < 0 || py < 0 || px >= SkinImage.Size || py >= SkinImage.Size)
                    continue;

                var index = py * SkinImage.Size + px;

                if (Pixels[index] == color)
                    continue;

                Pixels[index] = color;
                changed = true;
            }
        }

        if (changed)
            OnPropertyChanged(nameof(Pixels));
    }

    public void BeginStroke() => PushUndo();

    private void PushUndo()
    {
        if (Pixels is null)
            return;

        _undo.Push((uint[])Pixels.Clone());

        while (_undo.Count > 40)
            _undo.Pop();

        OnPropertyChanged(nameof(CanUndo));
    }

    private static uint ParseColor(string value)
    {
        var text = value.TrimStart('#');

        if (text.Length == 6 && uint.TryParse(
                text, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
        {
            return 0xFF000000u | rgb;
        }

        return 0xFFB57EDCu;
    }

    partial void OnSelectedAccountChanged(Account? value)
    {
        _undo.Clear();

        if (value is null)
        {
            Pixels = null;
            return;
        }

        Slim = value.SkinModel == SkinModel.Slim;

        var stored = _store.ReadSkin(value.Id);
        Pixels = stored is not null
            ? SkinImage.Decode(stored) ?? SkinStore.CreateDefaultSkin(Slim)
            : SkinStore.CreateDefaultSkin(Slim);

        StatusText = "Editing skin for " + value.Username;
        OnPropertyChanged(nameof(HasSkin));
        OnPropertyChanged(nameof(CanUndo));
    }

    partial void OnPixelsChanged(uint[]? value) => OnPropertyChanged(nameof(HasSkin));

    partial void OnSlimChanged(bool value) => OnPropertyChanged(nameof(ModelLabel));
}
