using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.Core.Accounts;
using Vesper.Core.Accounts.Microsoft;

namespace Vesper.App.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly AccountManager _manager;

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

    public AccountsViewModel(AccountManager manager)
    {
        _manager = manager;
        Refresh();
    }

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
            StatusMessage = "Enter the code at " + code.VerificationUri;

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
    }
}
