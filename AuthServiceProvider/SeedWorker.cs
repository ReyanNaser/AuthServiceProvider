using AuthServiceProvider.Data;
using AuthServiceProvider.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace AuthServiceProvider;

public sealed class SeedWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SeedDataOptions _options;

    public SeedWorker(
        IServiceProvider serviceProvider,
        IOptions<SeedDataOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        await SeedClientsAsync(scope.ServiceProvider, cancellationToken);
        await SeedScopesAsync(scope.ServiceProvider, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private async Task SeedClientsAsync(
        IServiceProvider sp,
        CancellationToken ct)
    {
        var manager = sp.GetRequiredService<IOpenIddictApplicationManager>();

        foreach (var client in _options.Clients)
        {
            var app = await manager.FindByClientIdAsync(client.ClientId, ct);

            if (app is null)
            {
                var descriptor = CreateClientDescriptor(client);
                await manager.CreateAsync(descriptor, ct);
            }
            else
            {
                var descriptor = new OpenIddictApplicationDescriptor();
                await manager.PopulateAsync(descriptor, app, ct);

                EnsurePermissions(descriptor, client);
                await manager.UpdateAsync(app, descriptor, ct);
            }
        }
    }

    private async Task SeedScopesAsync(
     IServiceProvider sp,
     CancellationToken ct)
    {
        var scopeManager = sp.GetRequiredService<IOpenIddictScopeManager>();

        foreach (var scope in _options.Scopes)
        {
            var existing = await scopeManager.FindByNameAsync(scope.Name, ct);

            if (existing is null)
            {
                var descriptor = new OpenIddictScopeDescriptor
                {
                    Name = scope.Name
                };

                foreach (var resource in scope.Resources)
                {
                    descriptor.Resources.Add(resource);
                }

                await scopeManager.CreateAsync(descriptor, ct);
            }
            else
            {
                var descriptor = new OpenIddictScopeDescriptor();
                await scopeManager.PopulateAsync(descriptor, existing, ct);

                foreach (var resource in scope.Resources)
                {
                    if (!descriptor.Resources.Contains(resource))
                    {
                        descriptor.Resources.Add(resource);
                    }
                }

                await scopeManager.UpdateAsync(existing, descriptor, ct);
            }
        }
    }


    private static OpenIddictApplicationDescriptor CreateClientDescriptor(
        SsoClientOptions client)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            DisplayName = client.DisplayName
        };

        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.GrantTypes.Password);
        descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        foreach (var scope in client.Scopes)
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        return descriptor;
    }

    private static void EnsurePermissions(
        OpenIddictApplicationDescriptor descriptor,
        SsoClientOptions client)
    {
        foreach (var scope in client.Scopes)
        {
            var permission = Permissions.Prefixes.Scope + scope;
            if (!descriptor.Permissions.Contains(permission))
                descriptor.Permissions.Add(permission);
        }
    }
}

