using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace UndercutF1.Console;

public sealed partial class TerminalInfoProvider
{
    private readonly ILogger<TerminalInfoProvider> _logger;

    private static readonly string[] ITERM_PROTOCOL_SUPPORTED_TERMINALS = ["iterm", "wezterm"];

    [GeneratedRegex(@"\u001B_G(.+)\u001B\\")]
    private static partial Regex TerminalKittyGraphicsResponseRegex();

    [GeneratedRegex(@"\u001B\[\?2026;(\d+)\$y")]
    private static partial Regex TerminalSynchronizedOutputResponseRegex();

    [GeneratedRegex(@"\u001B\[4;(\d+);(\d+)t")]
    private static partial Regex TerminalSizeResponseRegex();

    /// <summary>
    /// Returns <see langword="true" /> if the current terminal supports the iTerm 2 Graphics Protocol.
    /// This is done in a very rudimentary way, and is by no means comprehensive.
    /// </summary>
    /// <returns><see langword="true" /> if the current terminal supports the iTerm 2 Graphics Protocol.</returns>
    public Lazy<bool> IsITerm2ProtocolSupported { get; }

    /// <summary>
    /// Returns <see langword="true" /> if the current terminal supports the Kitty Graphics Protocol.
    /// This is done by sending an escape code to the terminal which supported terminals should respond to.
    /// </summary>
    /// <returns><see langword="true" /> if the current terminal supports the Kitty Graphics Protocol.</returns>
    public Lazy<bool> IsKittyProtocolSupported { get; }

    /// <summary>
    /// Returns <see langword="true" /> if the current terminal support Synchronized Output,
    /// as described in https://gist.github.com/christianparpart/d8a62cc1ab659194337d73e399004036
    /// and https://gitlab.com/gnachman/iterm2/-/wikis/synchronized-updates-spec.
    /// </summary>
    public Lazy<bool> IsSynchronizedOutputSupported { get; }

    /// <summary>
    /// Returns the size of the terminal in pizels. <c>null</c> if terminal size could not be determined.
    /// </summary>
    public Lazy<(int Height, int Width)?> TerminalSize { get; private set; }

    public TerminalInfoProvider(ILogger<TerminalInfoProvider> logger)
    {
        _logger = logger;
        IsITerm2ProtocolSupported = new Lazy<bool>(GetIsITerm2ProtocolSupported);
        IsKittyProtocolSupported = new Lazy<bool>(GetKittyProtocolSupported);
        IsSynchronizedOutputSupported = new Lazy<bool>(GetSynchronizedOutputSupported);
        TerminalSize = new Lazy<(int, int)?>(GetTerminalSize);
        Terminal.Resized += (_new) =>
        {
            TerminalSize = new Lazy<(int, int)?>(GetTerminalSize);
            _ = TerminalSize.Value;
        };
    }

    private bool GetIsITerm2ProtocolSupported()
    {
        var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? string.Empty;
        var supported = ITERM_PROTOCOL_SUPPORTED_TERMINALS.Any(x =>
            termProgram.Contains(x, StringComparison.InvariantCultureIgnoreCase)
        );
        _logger.LogDebug("iTerm2 Graphics Protocol Supported: {Supported}", supported);
        return supported;
    }

    private bool GetKittyProtocolSupported()
    {
        PrepareTerminal();
        var buffer = ArrayPool<byte>.Shared.Rent(32);
        try
        {
            // Query the terminal with a graphic protocol specific escape code
            Terminal.Out("\u001B_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001B\\");
            // Also send a device attributes escape code, so that there is always something to read from stdin
            Terminal.Out("\u001B[c");

            // Expected response: <ESC>_Gi=31;error message or OK<ESC>\
            Terminal.Read(buffer);
            var str = Encoding.ASCII.GetString(buffer);
            var match = TerminalKittyGraphicsResponseRegex().Match(str);

            var supported =
                match.Success
                && match.Groups.Count == 2
                && match
                    .Groups[1]
                    .Captures[0]
                    .Value.Equals("i=31;OK", StringComparison.InvariantCultureIgnoreCase);
            _logger.LogDebug(
                "Kitty Protocol Supported: {Supported}, Response: {Response}",
                supported,
                Util.Sanitize(str)
            );

            if (match.Success && !str.Contains("\u001B[?"))
            {
                DiscardExtraResponse();
            }
            return supported;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to determine if terminal supports Kitty Graphics Protocol");
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            CleanupTerminal();
        }
    }

    private bool GetSynchronizedOutputSupported()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(32);
        try
        {
            // Send a DECRQM query to the terminal to check for support
            Terminal.Out("\u001B[?2026$p");
            // Also send a device attributes escape code, so that there is always something to read from stdin
            Terminal.Out("\u001B[c");

            // Expected response: <ESC> [ ? 2026 ; 1 $ y
            Terminal.Read(buffer);
            var str = Encoding.ASCII.GetString(buffer);
            var match = TerminalSynchronizedOutputResponseRegex().Match(str);

            var supported = false;
            if (
                match.Success
                && match.Groups.Count == 2
                && int.TryParse(match.Groups[1].Captures[0].Value, out var responseValue)
            )
            {
                // See https://gist.github.com/christianparpart/d8a62cc1ab659194337d73e399004036#feature-detection
                supported = responseValue > 0 && responseValue < 4;
            }

            _logger.LogDebug(
                "Synchronized Output Supported: {Supported}, Response: {Response}",
                supported,
                Util.Sanitize(str)
            );

            // DECRQM response with \e[? as well, so ignore first 3 chars then check for \e[? again to see if we've
            // already read the device attribute query
            if (match.Success && !str[3..].Contains("\u001B[?"))
            {
                DiscardExtraResponse();
            }

            return match.Success;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to determine if terminal supports Synchronized Output");
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private (int Height, int Width)? GetTerminalSize()
    {
        PrepareTerminal();
        var buffer = ArrayPool<byte>.Shared.Rent(32);
        try
        {
            // Send a DCS query to the terminal to query terminal size
            Terminal.Out("\u001B[14t");
            // Also send a device attributes escape code, so that there is always something to read from stdin
            Terminal.Out("\u001B[c");

            // Expected response: <ESC> [ ; <height> ; <width> t
            Terminal.Read(buffer);
            var str = Encoding.ASCII.GetString(buffer);
            var match = TerminalSizeResponseRegex().Match(str);

            var height = 0;
            var width = 0;
            _ =
                match.Success
                && match.Groups.Count == 3
                && int.TryParse(match.Groups[1].Captures[0].Value, out height)
                && int.TryParse(match.Groups[2].Captures[0].Value, out width);

            _logger.LogDebug(
                "Terminal Size (px): {Height}, {Width}: {Response}",
                height,
                width,
                Util.Sanitize(str)
            );

            if (!str.Contains("\u001B[?"))
            {
                DiscardExtraResponse();
            }

            return height > 0 && width > 0 ? (height, width) : null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to fetch terminal size in pixels");
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            CleanupTerminal();
        }
    }

    /// <summary>
    /// Prepares the terminal to get it in to a state where it will definitely respond to queries.
    /// Some terminals will not respond to queries if in the middle of a synchronized update.
    /// </summary>
    private void PrepareTerminal()
    {
        if (IsSynchronizedOutputSupported.Value)
        {
            Terminal.Out(TerminalGraphics.EndSynchronizedUpdate());
        }
    }

    private void CleanupTerminal()
    {
        if (IsSynchronizedOutputSupported.Value)
        {
            Terminal.Out(TerminalGraphics.BeginSynchronizedUpdate());
        }
    }

    private void DiscardExtraResponse()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(32);
        try
        {
            // Got a response to the check, so read and throw away the device attributes response as well
            _ = Terminal.Read(buffer);
            _logger.LogDebug("Reading device attr response {Res}", Util.Sanitize(buffer));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
