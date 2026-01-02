using AuthServiceProvider.DTOs;
using Microsoft.AspNetCore.Identity;

namespace AuthServiceProvider.Features;

public static class RegisterEndpoint
{
    public static void MapRegisterEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", async (UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, 
            RegisterRequest request) =>
        {
            
            var userExists = await userManager.FindByEmailAsync(request.Email);
            if (userExists != null)
                return Results.BadRequest("User already exists!");

            
            var user = new IdentityUser 
            { 
                UserName = request.UserName, 
                Email = request.Email,
                EmailConfirmed = true 
            };

            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            
            if (!await roleManager.RoleExistsAsync(request.Role))
            {
                await roleManager.CreateAsync(new IdentityRole(request.Role));
            }
            
            await userManager.AddToRoleAsync(user, request.Role);

            return Results.Ok(new { Message = "User registered successfully!", UserId = user.Id });
        });
    }
}
