using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vesper.App.Views;

public partial class ModsOverlay : UserControl
{
    public ModsOverlay() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
