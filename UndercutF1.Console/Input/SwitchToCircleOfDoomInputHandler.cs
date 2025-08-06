namespace UndercutF1.Console;

public class SwitchToCircleOfDoomInputHandler(State state) : IInputHandler
{
    public string Description => "Circle of Doom";

    public ConsoleKey[] Keys => [ConsoleKey.C];

    public string[] DisplayKeys => ["C"];

    public Screen[] ApplicableScreens =>
    [
        Screen.TimingTower,
        Screen.TimingHistory,
        Screen.RaceControl,
        Screen.DriverTracker,
        Screen.TyreStints,
        Screen.TeamRadio,
        Screen.CircleOfDoom
    ];

    public bool IsEnabled => true;

    public int Sort => 40;

    public Task ExecuteAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        state.CurrentScreen = Screen.CircleOfDoom;
        return Task.CompletedTask;
    }
}