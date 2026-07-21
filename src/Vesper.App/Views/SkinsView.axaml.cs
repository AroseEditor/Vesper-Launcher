using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Vesper.App.Controls;
using Vesper.App.ViewModels;

namespace Vesper.App.Views;

public partial class SkinsView : UserControl
{
    private SkinViewport? _viewport;

    public SkinsView()
    {
        InitializeComponent();

        _viewport = this.FindControl<SkinViewport>("Viewport");

        if (_viewport is null)
            return;

        _viewport.Painted += OnPainted;
        _viewport.PointerPressed += OnViewportPressed;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private SkinsViewModel? Model => DataContext as SkinsViewModel;

    private void OnViewportPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Model is { PaintMode: true })
            Model.BeginStroke();
    }

    private void OnPainted(object? sender, SkinPaintEventArgs e)
    {
        Model?.Paint(e.X, e.Y);
        _viewport?.Invalidate();
    }

    private async void OnUpload(object? sender, RoutedEventArgs e)
    {
        if (Model is null)
            return;

        var top = TopLevel.GetTopLevel(this);

        if (top is null)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a Minecraft skin",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Minecraft skin")
                {
                    Patterns = ["*.png"],
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
            Model.ImportPng(memory.ToArray());
            _viewport?.Invalidate();
        }
        catch (Exception ex)
        {
            Model.StatusText = "Could not read that file: " + ex.Message;
        }
    }
}
