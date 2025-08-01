using UndercutF1.Console.Graphics;
using UndercutF1.Data;

namespace UndercutF1.Console;

public sealed record Options : LiveTimingOptions
{
    /// <summary>
    /// Prefer to use FFmpeg (<c>ffplay</c>) for audio playback (e.g. Team Radio) instead of more native options
    /// such as <c>mpg123</c> or <c>afplay</c>. FFmpeg is always used on Windows.
    /// Defaults to <see langword="false"/> .
    /// </summary>
    public bool PreferFfmpegPlayback { get; set; } = false;

    /// <summary>
    /// If provided, forces the app to output images using the given protocol.
    /// Otherwise, heuristics and queries will be used to determine if graphics are supported, and which protocol to use.
    /// </summary>
    public GraphicsProtocol? ForceGraphicsProtocol { get; set; } = null;

    /// <inheritdoc cref="ExternalPlayerSync.Options" />
    public ExternalPlayerSync.Options? ExternalPlayerSync { get; set; }
}
