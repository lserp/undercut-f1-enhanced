using System.Text;

namespace UndercutF1.Console;

/// <summary>
/// Provide control sequences for displaying images in terminals.
/// See the following for documentation on the protocols:
/// <list type="bullet">
/// <item>iTerm2: https://iterm2.com/documentation-images.html</item>
/// <item>Kitty: https://sw.kovidgoyal.net/kitty/graphics-protocol/</item>
/// </list>
/// </summary>
public static class TerminalGraphics
{
    private const string ESCAPE_CSI = "\u001B["; // Begins an Control Sequence Introducer
    private const string ESCAPE_OSC = "\u001B]"; // Begins an Operating System Command
    private const string ESCAPE_APC = "\u001B_G"; // Begins an Application Programming Command
    private const string ESCAPE_ST = "\u001B\\"; // String Terminator

    private static readonly string EncodedFileName = Convert.ToBase64String(
        Encoding.ASCII.GetBytes("drivertracker.png")
    );

    /// <summary>
    /// Given a base64 encoded string of the PNG file,
    /// returns the control sequence for displaying that image in the terminal.
    /// The provided image is resized by the terminal to fit within the provided
    /// <paramref name="height"/> and <paramref name="width"/>.
    /// </summary>
    /// <param name="height">Height of the image in cells</param>
    /// <param name="width">Width of the image in cells</param>
    /// <param name="base64EncodedImage">Base 64 encoded PNG image data</param>
    /// <returns>The control sequence as a string.</returns>
    public static string ITerm2GraphicsSequence(int height, int width, string base64EncodedImage)
    {
        var args = new string[]
        {
            $"name={EncodedFileName}",
            $"width={width}",
            $"height={height}",
            "preserveAspectRatio=0",
            "inline=1",
        };

        return $"{ESCAPE_OSC}1337;File={string.Join(';', args)}:{base64EncodedImage}{ESCAPE_ST}";
    }

    /// <summary>
    /// Given a base64 encoded string of the PNG file, returns the control sequence for
    /// displaying that image in the terminal, using the Kitty Graphics Protocol.
    /// </summary>
    /// <param name="height">Height of the image in cells</param>
    /// <param name="width">Width of the image in cells</param>
    /// <param name="base64EncodedImage">Base 64 encoded PNG image data</param>
    /// <returns>The control sequence as a string.</returns>
    public static string[] KittyGraphicsSequence(int height, int width, string base64EncodedImage)
    {
        var args = new string[]
        {
            "a=T", // Immediate mode
            "q=1", // Suppress success responses back from the terminal
            $"r={height}", // Num rows
            $"c={width}", // Num rows
            $"f=100" // We'll be sending PNG encoded base64 data
        };

        return base64EncodedImage
            .Chunk(4000)
            .Select(
                (base64Chunk, index) =>
                    (base64Chunk.Length, index) switch
                    {
                        // Start a multi chunk
                        (4000, 0) =>
                            $"{ESCAPE_APC}{string.Join(',', args)},m=1;{new string(base64Chunk)}{ESCAPE_ST}",
                        // Only a single chunk
                        (_, 0) =>
                            $"{ESCAPE_APC}{string.Join(',', args)};{new string(base64Chunk)}{ESCAPE_ST}",
                        // Middle chunks
                        (4000, _) => $"{ESCAPE_APC}m=1;{new string(base64Chunk)}{ESCAPE_ST}",
                        // Final chunk
                        (_, _) => $"{ESCAPE_APC}m=0;{new string(base64Chunk)}{ESCAPE_ST}",
                    }
            )
            .ToArray();
    }

    /// <summary>
    /// Returns a control sequence that instructs the terminal to delete previously displayed images.
    /// </summary>
    /// <returns>The control sequence as a string.</returns>
    public static string KittyGraphicsSequenceDelete()
    {
        var args = new string[]
        {
            "a=d", // Immediate mode
            "d=A", // Delete images intersecting with the current cursor (i.e. refresh)
        };
        return $"{ESCAPE_APC}{string.Join(',', args)};{ESCAPE_ST}";
    }

    /// <summary>
    /// Begins a Synchronized Update using the Synchronized Output feature.
    /// </summary>
    /// <returns>The control sequence as a string.</returns>
    public static string BeginSynchronizedUpdate() => $"{ESCAPE_CSI}?2026h";

    /// <summary>
    /// Ends a previously started Synchronized Update using the Synchronized Output feature.
    /// </summary>
    /// <returns>The control sequence as a string.</returns>
    public static string EndSynchronizedUpdate() => $"{ESCAPE_CSI}?2026l";
}
