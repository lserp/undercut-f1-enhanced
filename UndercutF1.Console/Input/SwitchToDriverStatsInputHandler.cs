namespace UndercutF1.Console;

public class SwitchToDriverStatsInputHandler(State state) : IInputHandler
{
    public bool IsEnabled => true;

    public Screen[] ApplicableScreens => [Screen.Main, Screen.ManageSession, Screen.TimingTower, Screen.RelativePerformanceTower];

    public ConsoleKey[] Keys => [ConsoleKey.B];

    public string Description => "Driver Stats";

    public int Sort => 63;

    public async Task ExecuteAsync(
        ConsoleKeyInfo consoleKeyInfo,
        CancellationToken cancellationToken = default
    )
    {
        await Terminal.OutAsync(ControlSequences.ClearScreen(ClearMode.Full), cancellationToken);
        state.CurrentScreen = Screen.DriverStats;
        state.CursorOffset = 0;
    }
}
