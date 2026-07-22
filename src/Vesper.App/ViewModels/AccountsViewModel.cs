using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Controls;
using Vesper.Core.Accounts;
using Vesper.Core.Accounts.Microsoft;
using Vesper.Core.Skins;
using Vesper.Core.Storage;

namespace Vesper.App.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly AccountManager _manager;
    private readonly SkinStore _skins;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isAddingLocal;

    [ObservableProperty]
    private string _newLocalUsername = string.Empty;

    [ObservableProperty]
    private Account? _selected;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSigningIn;

    [ObservableProperty]
    private string? _deviceCode;

    [ObservableProperty]
    private string? _deviceVerificationUri;

    [ObservableProperty]
    private Bitmap? _selectedAvatar;

    public AccountsViewModel(AccountManager manager, VesperPaths? paths = null)
    {
        _manager = manager;
        _skins = new SkinStore(paths ?? VesperPaths.Resolve());
        Refresh();
    }

    public bool HasAvatar => SelectedAvatar is not null;

    public static Func<string, Task>? ClipboardWriter { get; set; }

    public bool HasDeviceCode => !string.IsNullOrEmpty(DeviceCode);

    public ObservableCollection<Account> Accounts { get; } = [];

    public bool HasAccounts => Accounts.Count > 0;

    public string SelectedName => Selected?.Username ?? "No account";

    public string SelectedKindLabel => Selected is null
        ? "Add one to play"
        : Selected.Kind == AccountKind.Microsoft ? "Microsoft" : "Local";

    public string SelectedInitial => string.IsNullOrEmpty(Selected?.Username)
        ? "?"
        : Selected.Username[..1].ToUpperInvariant();

    [RelayCommand]
    private void TogglePopup()
    {
        IsOpen = !IsOpen;
        if (!IsOpen)
            CancelAddLocal();
    }

    [RelayCommand]
    private void BeginAddLocal()
    {
        IsAddingLocal = true;
        NewLocalUsername = string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private void CancelAddLocal()
    {
        IsAddingLocal = false;
        NewLocalUsername = string.Empty;
    }

    [RelayCommand]
    private void ConfirmAddLocal()
    {
        var username = NewLocalUsername.Trim();

        if (string.IsNullOrEmpty(username))
        {
            StatusMessage = "Enter a username";
            return;
        }

        var account = _manager.AddLocal(username);
        _manager.Select(account.Id);
        Refresh();
        CancelAddLocal();
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task AddMicrosoftAsync(CancellationToken cancellationToken)
    {
        if (IsSigningIn)
            return;

        IsSigningIn = true;
        StatusMessage = "Contacting Microsoft";

        try
        {
            var auth = new MicrosoftAuth();
            var code = await auth.RequestDeviceCodeAsync(cancellationToken);

            DeviceCode = code.UserCode;
            DeviceVerificationUri = code.VerificationUri;

            await CopyCodeAsync();
            OpenVerificationPage();

            StatusMessage = "Code copied. Paste it in the page that just opened.";

            var result = await auth.PollForTokenAsync(code, cancellationToken);
            var account = _manager.AddOrUpdateMicrosoft(
                result.Username, result.Uuid, result.AccessToken, result.RefreshToken, result.ExpiresAt);

            _manager.Select(account.Id);
            Refresh();
            StatusMessage = "Signed in as " + result.Username;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sign-in cancelled";
        }
        catch (MicrosoftAuthException e)
        {
            StatusMessage = e.Message;
        }
        catch (HttpRequestException)
        {
            StatusMessage = "Could not reach Microsoft. Check your connection.";
        }
        finally
        {
            IsSigningIn = false;
            DeviceCode = null;
            DeviceVerificationUri = null;
        }
    }

    [RelayCommand]
    private async Task CopyCodeAsync()
    {
        if (string.IsNullOrEmpty(DeviceCode) || ClipboardWriter is null)
            return;

        try
        {
            await ClipboardWriter(DeviceCode);
            StatusMessage = "Copied " + DeviceCode + " to your clipboard";
        }
        catch (Exception)
        {
            StatusMessage = "Could not reach the clipboard. Copy the code manually.";
        }
    }

    [RelayCommand]
    private void OpenVerificationPage()
    {
        if (string.IsNullOrEmpty(DeviceVerificationUri))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DeviceVerificationUri,
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            StatusMessage = "Open " + DeviceVerificationUri + " and enter the code.";
        }
    }

    [RelayCommand]
    private void SelectAccount(Account account)
    {
        _manager.Select(account.Id);
        Refresh();
        IsOpen = false;
    }

    [RelayCommand]
    private void RemoveAccount(Account account)
    {
        _manager.Remove(account.Id);
        Refresh();
    }

    private void Refresh()
    {
        Accounts.Clear();
        foreach (var account in _manager.All)
            Accounts.Add(account);

        Selected = _manager.Selected;

        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedKindLabel));
        OnPropertyChanged(nameof(SelectedInitial));
    }

    partial void OnSelectedChanged(Account? value)
    {
        OnPropertyChanged(nameof(SelectedName));
        OnPropertyChanged(nameof(SelectedKindLabel));
        OnPropertyChanged(nameof(SelectedInitial));
        RefreshAvatar(value);
    }

    partial void OnSelectedAvatarChanged(Bitmap? value) => OnPropertyChanged(nameof(HasAvatar));

    partial void OnDeviceCodeChanged(string? value) => OnPropertyChanged(nameof(HasDeviceCode));

    private void RefreshAvatar(Account? account)
    {
        SelectedAvatar?.Dispose();
        SelectedAvatar = null;

        if (account is null)
            return;

        try
        {
            var stored = _skins.ReadSkin(account.Id);
            var pixels = stored is not null
                ? SkinImage.Decode(stored)
                : SkinStore.CreateDefaultSkin(account.SkinModel == SkinModel.Slim);

            if (pixels is not null)
                SelectedAvatar = SkinImage.RenderHead(pixels);
        }
        catch (Exception)
        {
            SelectedAvatar = null;
        }
    }
}
