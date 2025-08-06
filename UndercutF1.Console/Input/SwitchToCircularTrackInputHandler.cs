namespace UndercutF1.Console;

/// <summary>
/// Input handler for switching to the circular track visualization display
/// </summary>
public class SwitchToCircularTrackInputHandler(State state) : IInputHandler
{
    public string Description => "Circular Track";

    public ConsoleKey[] Keys => [ConsoleKey.R];

    public string[] DisplayKeys => ["R"];

    public Screen[] ApplicableScreens =>
    [
        Screen.TimingTower,
        Screen.TimingHistory,
        Screen.RaceControl,
        Screen.DriverTracker,
        Screen.TyreStints,
        Screen.TeamRadio,
        Screen.CircleOfDoom,
        Screen.CircularTrack
    ];

    public bool IsEnabled => true;

    public int Sort => 41; // Place after Circle of Doom (40)

    public Task ExecuteAsync(ConsoleKeyInfo keyInfo, CancellationToken cancellationToken = default)
    {
        state.CurrentScreen = Screen.CircularTrack;
        return Task.CompletedTask;
    }
}