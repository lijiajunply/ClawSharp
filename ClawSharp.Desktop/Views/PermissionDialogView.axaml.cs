using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace ClawSharp.Desktop.Views;

public partial class PermissionDialogView : Window
{
    public string DialogTitle { get; }
    public string Message { get; }

    public PermissionDialogView()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContext = this;
        DialogTitle = "Permission Required";
        Message = "A permission is required.";
    }

    public PermissionDialogView(string title, string message)
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DialogTitle = title;
        Message = message;
        DataContext = this;
        
        // Ensure properties map correctly to Title (for window and binding)
        Title = DialogTitle;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAllowClick(object sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnDenyClick(object sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
