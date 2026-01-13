using AuthServiceProvider.Messages; // Define your DTO namespace
using Microsoft.AspNetCore.Identity;
using NATS.Client.Core;
using NATS.Net;
namespace AuthServiceProvider.Workers;

public class UserCreationWorker : BackgroundService
{
    private readonly INatsConnection _nats;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserCreationWorker> _logger;
    public UserCreationWorker(INatsConnection nats, IServiceProvider serviceProvider, ILogger<UserCreationWorker> logger)
    {
        _nats = nats;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to the subject
        await foreach (var msg in _nats.SubscribeAsync<UserCreatedEvent>("auth.user.create", cancellationToken: stoppingToken))
        {
            if (msg.Data is null) continue;



            _logger.LogInformation($"Received user creation request for {msg.Data.Email}");
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var eventData = msg.Data;
                var userExists = await userManager.FindByEmailAsync(eventData.Email);

                var username = $"{eventData.FirstName}{eventData.LastName}".Replace(" ", "").Trim();
                if (userExists == null)
                {
                    var user = new IdentityUser
                    {
                        UserName = username,
                        Email = eventData.Email,
                        EmailConfirmed = true
                    };
                    // Set a default password or handle generic password generation
                    var result = await userManager.CreateAsync(user, "Default@123");
                    if (result.Succeeded)
                    {
                        if (!await roleManager.RoleExistsAsync(eventData.Role))
                        {
                            await roleManager.CreateAsync(new IdentityRole(eventData.Role));
                        }
                        await userManager.AddToRoleAsync(user, eventData.Role);
                        _logger.LogInformation($"Successfully created user {eventData.Email}");
                    }
                    else
                    {
                        _logger.LogError($"Failed to create user {eventData.Email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user creation message");
            }
        }
    }
}