using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LogAnalyzer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _greeting = "Hallo MVVM!";

    [RelayCommand]
    private void UpdateGreeting()
    {
        Greeting = "Aktualisiert: " + System.DateTime.Now.ToString("T");
    }
}
