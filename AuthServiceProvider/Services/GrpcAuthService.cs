using AuthServiceProvider.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;

namespace AuthServiceProvider.Services;

public class GrpcAuthService : AuthService.AuthServiceBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<GrpcAuthService> _logger;
    
    public GrpcAuthService(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<GrpcAuthService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public override async Task<CreateUserResponse> CreateUser(CreateUserRequest request, ServerCallContext context)
    {
        _logger.LogInformation("gRPC Request received to create user: {Email}", request.Email);

        // 1. Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new CreateUserResponse { Success = false, Message = "User already exists." };
        }


        var username = $"{request.FirstName}{request.LastName}".Replace(" ", "").Trim();

        
        var user = new IdentityUser
        {
            UserName = username,
            Email = request.Email,
            EmailConfirmed = true
        };

        
        string defaultPassword = "DefaultPassword@123";

        var result = await _userManager.CreateAsync(user, defaultPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return new CreateUserResponse { Success = false, Message = errors };
        }

       
        string role = string.IsNullOrEmpty(request.Role) ? "Employee" : request.Role;

        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        return new CreateUserResponse
        {
            Success = true,
            Message = "User created successfully via gRPC.",
            UserId = user.Id
        };
    }
}