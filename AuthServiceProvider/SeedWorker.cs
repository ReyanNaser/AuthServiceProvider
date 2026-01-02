using AuthServiceProvider.Data;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServiceProvider;

public class SeedWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public SeedWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        //await context.Database.EnsureCreatedAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var application = await manager.FindByClientIdAsync("attendance-client", cancellationToken);
        if (application is null)
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "attendance-client",
                ClientSecret = "attendance-secret",
                DisplayName = "Attendance Management Client",
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.Password,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.Scopes.Roles,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope + "api",
                    Permissions.Prefixes.Scope + "roles",
                    Permissions.Prefixes.Scope + "offline_access"
                }
            }, cancellationToken);
        }
        else
        {
            var descriptor = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(descriptor, application, cancellationToken);
            
            // Ensure all required permissions are present
            var requiredPermissions = new[]
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Scopes.Roles,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + "api",
                Permissions.Prefixes.Scope + "roles",
                Permissions.Prefixes.Scope + "offline_access"
            };

            foreach (var permission in requiredPermissions)
            {
                if (!descriptor.Permissions.Contains(permission))
                {
                    descriptor.Permissions.Add(permission);
                }
            }

            await manager.UpdateAsync(application, descriptor, cancellationToken);
        }

        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        
        var apiScope = await scopeManager.FindByNameAsync("api", cancellationToken);
        if (apiScope is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "api",
                Resources = { "attendance_api" }
            }, cancellationToken);
        }
        else
        {
            var descriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(descriptor, apiScope, cancellationToken);
            if (!descriptor.Resources.Contains("attendance_api"))
            {
                descriptor.Resources.Add("attendance_api");
                await scopeManager.UpdateAsync(apiScope, descriptor, cancellationToken);
            }
        }

        var rolesScope = await scopeManager.FindByNameAsync("roles", cancellationToken);
        if (rolesScope is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "roles",
                Resources = { "attendance_api" }
            }, cancellationToken);
        }
        else
        {
            var descriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(descriptor, rolesScope, cancellationToken);
            if (!descriptor.Resources.Contains("attendance_api"))
            {
                descriptor.Resources.Add("attendance_api");
                await scopeManager.UpdateAsync(rolesScope, descriptor, cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
