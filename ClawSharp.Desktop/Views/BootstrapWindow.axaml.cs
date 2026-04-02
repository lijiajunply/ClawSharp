using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ClawSharp.Desktop.ViewModels;
using ReactiveUI;

namespace ClawSharp.Desktop.Views;

public partial class BootstrapWindow : ReactiveWindow<BootstrapViewModel>
{
    public BootstrapWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif

        this.WhenActivated(d =>
        {
            if (ViewModel != null)
            {
                d(ViewModel.FinishCommand.Subscribe(success =>
                {
                    if (success)
                    {
                        Close(true);
                    }
                }));
            }
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
