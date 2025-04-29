namespace UndercutF1.Console.Audio;

public class AudioPlayer(ILogger<AudioPlayer> logger)
{
    private ChildProcess? _process = null;

    public bool Playing => !_process?.Completion.IsCompleted ?? false;

    public bool Errored => _process?.Completion.IsFaulted ?? false;

    public void Play(string filePath)
    {
        if (OperatingSystem.IsMacOS())
        {
            _process = Run("afplay", filePath);
        }
        else if (OperatingSystem.IsLinux())
        {
            _process = Run("mpg123", filePath, "--no-control");
        }
        else if (OperatingSystem.IsWindows())
        {
            _process = Run(
                "ffplay",
                filePath,
                "-nodisp",
                "-autoexit",
                "-hide_banner",
                "-loglevel error"
            );
        }
        else
        {
            _process = null;
            throw new InvalidOperationException(
                "Unable to find a suitable audio playback method for the current operating system"
            );
        }
    }

    public void Stop()
    {
        if (_process is not null && !_process.Completion.IsCompleted)
        {
            logger.LogDebug("Stopping audio playback {PID}", _process.Id);
        }
        else
        {
            logger.LogDebug("No running process to stop");
        }
    }

    private ChildProcess Run(string executable, params string[] args)
    {
        logger.LogDebug(
            "Beginning audio playback of '{FileName}' with executable {Executable}",
            args[0],
            executable
        );
        var process = new ChildProcessBuilder()
            .WithFileName(executable)
            .WithArguments(args)
            .WithThrowOnError(true)
            .Run();

        process.Completion.ContinueWith(LogProcessException, TaskContinuationOptions.OnlyOnFaulted);

        return process;
    }

    private void LogProcessException(Task failed) =>
        logger.LogError(failed.Exception, "Audio playback failed, bad exit code");
}
