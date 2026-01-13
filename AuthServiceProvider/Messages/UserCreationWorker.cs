using AuthServiceProvider.Messages;
using Microsoft.AspNetCore.Identity;
using NATS.Net;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
namespace AuthServiceProvider.Workers;

public class UserCreationWorker : BackgroundService
{
    private readonly INatsJSContext _js;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserCreationWorker> _logger;
    public UserCreationWorker(INatsJSContext js, IServiceProvider serviceProvider, ILogger<UserCreationWorker> logger)
    {
        _js = js;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var consumerOpts = new NatsJSConsumeOpts
        {
            MaxMsgs = 1
        };
        var consumerConfig = new ConsumerConfig("AuthUserCreator")
        {
            DurableName = "AuthUserCreator",
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
        };
        var streamName = "EMPLOYEE_EVENTS_V2";
        _logger.LogInformation("Starting JetStream Consumer for stream {Stream}...", streamName);
        try
        {

            var consumer = await _js.CreateOrUpdateConsumerAsync(streamName, consumerConfig, cancellationToken: stoppingToken);
            // Start Consuming
            await foreach (var msg in consumer.ConsumeAsync<UserCreatedEvent>(opts: consumerOpts, cancellationToken: stoppingToken))
            {
                if (msg.Data is null)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }
                _logger.LogInformation("[JetStream] Received user creation for {Email}", msg.Data.Email);
                using var scope = _serviceProvider.CreateScope();
                var success = await ProcessUserCreation(scope, msg.Data);
                if (success)
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    _logger.LogInformation("Message for {Email} processed and acknowledged.", msg.Data.Email);
                }
                else
                {

                    await msg.NakAsync(cancellationToken: stoppingToken);
                    _logger.LogWarning("Message for {Email} failed processing. NAK sent.", msg.Data.Email);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in JetStream consumer loop. Worker stopping.");

        }
    }
    private async Task<bool> ProcessUserCreation(IServiceScope scope, UserCreatedEvent eventData)
    {
        try
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Check if user exists
            var user = await userManager.FindByEmailAsync(eventData.Email);
            if (user != null)
            {
                _logger.LogInformation("User {Email} already exists. Skipping creation.", eventData.Email);
                return true; // Treat as success to Ack the message
            }

            var username = $"{eventData.FirstName}{eventData.LastName}".Replace(" ", "").Trim();
            // 2. Create User
            var newUser = new IdentityUser
            {
                UserName = username, // Use Email as UserName
                Email = eventData.Email,
                EmailConfirmed = true
            };
            // Set a default password
            var result = await userManager.CreateAsync(newUser, "Default@123");
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create user {Email}: {Errors}", eventData.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return false; // Will trigger NAK
            }
            // 3. Assign Role
            if (!string.IsNullOrEmpty(eventData.Role))
            {
                if (!await roleManager.RoleExistsAsync(eventData.Role))
                {
                    await roleManager.CreateAsync(new IdentityRole(eventData.Role));
                }

                var roleResult = await userManager.AddToRoleAsync(newUser, eventData.Role);
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Failed to assign role {Role} to {Email}: {Errors}", eventData.Role, eventData.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));

                    return true;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during user creation logic for {Email}", eventData.Email);
            return false;
        }
    }
}