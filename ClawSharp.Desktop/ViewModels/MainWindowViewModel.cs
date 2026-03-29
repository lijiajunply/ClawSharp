using System.Collections.Generic;

namespace ClawSharp.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia!";
    
    public IReadOnlyList<string> ThreadSpaces { get; } = ["init"];
}
