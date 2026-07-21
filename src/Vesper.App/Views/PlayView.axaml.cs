using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vesper.App.Views;

public partial class PlayView : UserControl
{
    public PlayView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
