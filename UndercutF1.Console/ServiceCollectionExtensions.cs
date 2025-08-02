namespace UndercutF1.Console;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInputHandlers(this IServiceCollection services)
    {
        var inputHandlerTypes = typeof(IInputHandler)
            .Assembly.GetTypes()
            .Where(x => x != typeof(IInputHandler) && x.IsAssignableTo(typeof(IInputHandler)));

        foreach (var type in inputHandlerTypes)
        {
            _ = services.AddSingleton(typeof(IInputHandler), type);
        }

        return services;
    }

    public static IServiceCollection AddDisplays(this IServiceCollection services)
    {
        var displayTypes = typeof(IDisplay)
            .Assembly.GetTypes()
            .Where(x => x != typeof(IDisplay) && x.IsAssignableTo(typeof(IDisplay)));

        foreach (var type in displayTypes)
        {
            _ = services.AddSingleton(type);
            _ = services.AddSingleton(typeof(IDisplay), sp => sp.GetRequiredService(type));
        }

        services.AddSingleton<CommonDisplayComponents>();
        services.AddSingleton<LogDisplayOptions>();
        services.AddSingleton<StartSimulatedSessionOptions>();

        return services;
    }
}
