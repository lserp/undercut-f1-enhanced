using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Rendering;
using UndercutF1.Data;

namespace UndercutF1.Console;

public sealed class InfoDisplay(
    TerminalInfoProvider terminalInfo,
    IOptions<Options> options
) : IDisplay
{
    public Screen Screen => Screen.Info;

    public Task<IRenderable> GetContentAsync()
    {
        var content = $"""
            [bold]Configuration[/]
            [bold]Data Directory:[/]        {options.Value.DataDirectory}
            [bold]Log Directory:[/]         {options.Value.LogDirectory}
            [bold]Audible Notifications:[/] {options.Value.Notify}
            [bold]Verbose Mode:[/]          {options.Value.Verbose}
            [bold]Forced Protocol:[/]       {options.Value.ForceGraphicsProtocol?.ToString() ?? "None"}
            [bold]Config Override File:[/]  {File.Exists(
                Options.ConfigFilePath
            )} ({Options.ConfigFilePath})
            See https://github.com/JustAman62/undercut-f1#configuration for information on how to configure these options.

            [bold]Terminal Diagnostics[/]
            [bold]TERM_PROGRAM:[/]        {Environment.GetEnvironmentVariable("TERM_PROGRAM")}
            [bold]Window Size W/H:[/]     {terminalInfo.TerminalSize.Value?.Width}/{terminalInfo.TerminalSize.Value?.Height} ({(terminalInfo.TerminalSize.Value?.Height ?? 0) / Terminal.Size.Height})
            [bold]Kitty Graphics:[/]      {terminalInfo.IsKittyProtocolSupported.Value}
            [bold]iTerm2 Graphics:[/]     {terminalInfo.IsITerm2ProtocolSupported.Value}
            [bold]Sixel Graphics:[/]      {terminalInfo.IsSixelSupported.Value}
            [bold]Synchronized Output:[/] {terminalInfo.IsSynchronizedOutputSupported.Value}
            [bold]Version:[/]             {ThisAssembly.AssemblyInformationalVersion}
            [bold]Runtime Identifier:[/]  {RuntimeInformation.RuntimeIdentifier}
            
            [bold]OS:[/] {RuntimeInformation.OSDescription}
            """;

        return Task.FromResult<IRenderable>(new Panel(new Markup(content)).Expand());
    }
}
