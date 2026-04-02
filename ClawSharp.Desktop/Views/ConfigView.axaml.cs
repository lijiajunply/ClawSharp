using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClawSharp.Desktop.Views;

public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
