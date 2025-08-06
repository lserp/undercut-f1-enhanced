namespace UndercutF1.Console;

public class ToggleCircleOfDoomModeInputHandler(State state) : IInputHandler
{
    public string Description => "Toggle Graphics/Text";

    public ConsoleKey[] Keys => [ConsoleKey.G];

    public string[] DisplayKeys => ["G"];

    public Screen[] ApplicableScreens => [Screen.CircleOfDoom];

    public bool IsEnabled => true;

    public int Sort => 50;

    public Task ExecuteAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default) =>
        // ASCII mode is now the default and only mode - no toggle needed
        // This handler can be used for other Circle of Doom features if needed
        Task.CompletedTask;
}