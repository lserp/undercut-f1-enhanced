using UndercutF1.Data;

namespace UndercutF1.Console;

public class SwitchToRelativePerformanceTowerInputHandler(State state) : IInputHandler
{
    public bool IsEnabled => true;

    public Screen[] ApplicableScreens => [Screen.Main, Screen.ManageSession, Screen.TimingTower];

    // public ConsoleKey[] Keys => [ConsoleKey.Y];
    public ConsoleKey[] Keys => [];

    public string Description => "Relative Performance Tower";

    public int Sort => 62;

    public async Task ExecuteAsync(
        ConsoleKeyInfo consoleKeyInfo,
        CancellationToken cancellationToken = default
    )
    {
        await Terminal.OutAsync(ControlSequences.ClearScreen(ClearMode.Full), cancellationToken);
        state.CurrentScreen = Screen.RelativePerformanceTower;
        state.CursorOffset = 0;
    }
}
