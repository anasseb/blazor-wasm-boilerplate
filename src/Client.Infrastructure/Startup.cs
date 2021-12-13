﻿using System.Globalization;
using System.Reflection;
using FSH.BlazorWebAssembly.Client.Infrastructure.Authentication;
using FSH.BlazorWebAssembly.Client.Infrastructure.Managers;
using FSH.BlazorWebAssembly.Client.Infrastructure.Managers.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FSH.BlazorWebAssembly.Client.Infrastructure;

public static class Startup
{
    private const string ClientName = "FullStackHero.API";

    public static IServiceCollection AddClientServices(this IServiceCollection services, IConfiguration config) =>
        services
            .AddDistributedMemoryCache() // why do we need a distributed memorycache in a client application?
            .AddLocalization(options => options.ResourcesPath = "Resources")
            .AddBlazoredLocalStorage()
            .AddMudServices(configuration =>
                {
                    configuration.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
                    configuration.SnackbarConfiguration.HideTransitionDuration = 100;
                    configuration.SnackbarConfiguration.ShowTransitionDuration = 100;
                    configuration.SnackbarConfiguration.VisibleStateDuration = 3000;
                    configuration.SnackbarConfiguration.ShowCloseIcon = false;
                })
            .AddScoped<IClientPreferenceManager, ClientPreferenceManager>()
            .AutoRegisterInterfaces<IManager>()
            .AutoRegisterInterfaces<IApiService>()

            .AddAuthentication(config)
            .AddAuthorizationCore(RegisterPermissionClaims)

            .AddHttpClient(ClientName, client =>
                {
                    client.DefaultRequestHeaders.AcceptLanguage.Clear();
                    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName);
                    client.BaseAddress = new Uri(config[ConfigConstants.ApiBaseUrl]);
                })
                .AddAuthenticationHandler(config)
                .Services
            .AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName));

    private static void RegisterPermissionClaims(AuthorizationOptions options)
    {
        foreach (var prop in typeof(PermissionConstants)
            .GetNestedTypes()
            .SelectMany(c => c.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)))
        {
            if (prop.GetValue(null)?.ToString() is string permission)
            {
                options.AddPolicy(permission, policy => policy.RequireClaim(ClaimConstants.Permission, permission));
            }
        }
    }

    public static IServiceCollection AutoRegisterInterfaces<T>(this IServiceCollection services)
    {
        var @interface = typeof(T);

        var types = @interface
            .Assembly
            .GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Select(t => new
                {
                    Service = t.GetInterface($"I{t.Name}"),
                    Implementation = t
                })
            .Where(t => t.Service != null);

        foreach (var type in types)
        {
            if (@interface.IsAssignableFrom(type.Service))
            {
                services.AddTransient(type.Service, type.Implementation);
            }
        }

        return services;
    }
}