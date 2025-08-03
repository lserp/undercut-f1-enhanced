using Spectre.Console;

namespace UndercutF1.Console;

public record State
{
    private Screen _screen = Screen.Main;

    public Screen CurrentScreen
    {
        get => _screen;
        set
        {
            AnsiConsole.Clear();
            _screen = value;
        }
    }

    public int CursorOffset { get; set; } = 0;
}
