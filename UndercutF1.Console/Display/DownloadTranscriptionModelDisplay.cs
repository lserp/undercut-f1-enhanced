using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console;

public class DownloadTranscriptionModelDisplay(ITranscriptionProvider transcriptionProvider)
    : IDisplay
{
    public Screen Screen => Screen.DownloadTranscriptionModel;

    public Task<IRenderable> GetContentAsync()
    {
        var text = $"""
            Transcriptions require a Whisper ML model to be downloaded.
            We can download this model for you, and will store it at {transcriptionProvider.ModelPath}
            This path is set as a child of the DataDirectory configuration option.
            The model is around [bold]550MB[/].

            Please press [bold]⏎ Enter[/] if you're happy for this model to be downloaded, or press [bold]Esc[/] to abort.

            Once downloaded, you'll be sent back to the Team Radio list so you can try to transcribe again.
            """;

        return Task.FromResult<IRenderable>(new Panel(new Markup(text)).Expand());
    }
}
