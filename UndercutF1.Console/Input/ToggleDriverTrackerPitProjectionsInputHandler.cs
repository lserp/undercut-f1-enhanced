namespace UndercutF1.Console;

public class ToggleDriverTrackerPitProjectionsInputHandler(State state) : IInputHandler
{
    public string Description => "Toggle Pit Projections";

    public ConsoleKey[] Keys => [ConsoleKey.T];

    public string[] DisplayKeys => ["T"];

    public Screen[] ApplicableScreens => [Screen.DriverTracker];

    public bool IsEnabled => true;

    public int Sort => 50;

    public Task ExecuteAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        // Toggle pit stop projections on the driver tracker
        DriverTrackerDisplay.TogglePitProjections();
        return Task.CompletedTask;
    }
}