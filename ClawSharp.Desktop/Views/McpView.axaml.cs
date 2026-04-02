using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClawSharp.Desktop.Views;

public partial class McpView : UserControl
{
    public McpView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
