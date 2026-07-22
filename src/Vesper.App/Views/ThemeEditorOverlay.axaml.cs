using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vesper.App.Views;

public partial class ThemeEditorOverlay : UserControl
{
    public ThemeEditorOverlay() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
