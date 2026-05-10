namespace StarsTracker;

public partial class App : Application
{
    private readonly StarsTracker.Views.MainPage _mainPage;

    public App(StarsTracker.Views.MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new Window(_mainPage);
}
