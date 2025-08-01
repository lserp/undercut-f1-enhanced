using Microsoft.AspNetCore.Mvc;
using UndercutF1.Data;

namespace UndercutF1.Console.Api;

public enum ControlOperation
{
    PauseClock,
    ResumeClock,
    ToggleClock,
}

public sealed record ControlRequest(ControlOperation operation);

public enum ControlErrorCode
{
    NoRunningSession,
    UnknwonOperation,
};

public sealed record ControlError(ControlErrorCode errorCode)
{
    public string errorMessage =>
        errorCode switch
        {
            ControlErrorCode.NoRunningSession => "No session is currently running",
            ControlErrorCode.UnknwonOperation => "Unknown operation requested",
            _ => "Unknwon error",
        };
}

public static class ControlEndpoints
{
    public static WebApplication MapControlEndpoints(this WebApplication app)
    {
        app.MapPost("/control", ControlApiEndpoint)
            .WithDescription(
                "Issues control commands to the currently running session. These commands allow you to, for example, start or stop the session clock to synchronize your timing screen with another service"
            )
            .WithTags("Control")
            .Produces(StatusCodes.Status200OK)
            .Produces<ControlError>(StatusCodes.Status400BadRequest);

        return app;
    }

    public static IResult ControlApiEndpoint(
        [FromBody] ControlRequest request,
        SessionInfoProcessor sessionInfo,
        IDateTimeProvider dateTimeProvider
    )
    {
        if (sessionInfo.Latest.Name is null)
        {
            return Results.BadRequest(new ControlError(ControlErrorCode.NoRunningSession));
        }

        if (!Enum.IsDefined(request.operation))
        {
            return Results.BadRequest(new ControlError(ControlErrorCode.UnknwonOperation));
        }

        switch (request.operation)
        {
            case ControlOperation.PauseClock when !dateTimeProvider.IsPaused:
                dateTimeProvider.TogglePause();
                break;
            case ControlOperation.ResumeClock when dateTimeProvider.IsPaused:
                dateTimeProvider.TogglePause();
                break;
            case ControlOperation.ToggleClock:
                dateTimeProvider.TogglePause();
                break;
        }

        return Results.Ok();
    }
}
