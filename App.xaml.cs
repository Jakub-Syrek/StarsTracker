using StarsTracker.Views;

namespace StarsTracker;

public partial class App : Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        MainPage = mainPage;
    }
}
