using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vesper.App.ViewModels;
using Vesper.Core.Accounts;

namespace Vesper.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        AccountsViewModel.ClipboardWriter = text =>
            Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

        ErrorsViewModel.ClipboardWriter = text =>
            Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

        ServersViewModel.ClipboardWriter = text =>
            Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;

        if (DataContext is MainWindowViewModel model)
            await model.InitializeAsync();
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaximize(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnChangeAccountLogo(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Account account } || DataContext is not MainWindowViewModel mainModel)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose Account Logo / Image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images (*.png, *.jpg)")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"],
                },
            ],
        });

        if (files.Count == 0)
            return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            mainModel.Accounts.SetCustomAvatar(account, memory.ToArray());
        }
        catch (Exception ex)
        {
            mainModel.Accounts.StatusMessage = "Could not read logo image: " + ex.Message;
        }
    }
}
