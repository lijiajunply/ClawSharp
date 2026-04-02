using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ClawSharp.Desktop.Views;

public partial class AgentListView : UserControl
{
    public AgentListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
