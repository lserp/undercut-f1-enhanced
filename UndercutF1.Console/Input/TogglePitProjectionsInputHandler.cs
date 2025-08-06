namespace UndercutF1.Console;

public class TogglePitProjectionsInputHandler : IInputHandler
{
    public string Description => "Toggle Pit Projections";

    public ConsoleKey[] Keys => [ConsoleKey.P];

    public string[] DisplayKeys => ["P"];

    public Screen[] ApplicableScreens => [Screen.CircleOfDoom];

    public bool IsEnabled => true;

    public int Sort => 51;

    public Task ExecuteAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        UndercutF1.Console.Display.CircleOfDoomDisplay.TogglePitProjections();
        return Task.CompletedTask;
    }
}