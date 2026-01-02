using System.Security.Claims;
using AuthServiceProvider.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore; // Needed for GetOpenIddictServerRequest
using Microsoft.AspNetCore;

namespace AuthServiceProvider.Features;

public static class LoginEndpoint
{
    public static void MapLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", async (
            HttpContext context, 
            UserManager<IdentityUser> userManager, 
            SignInManager<IdentityUser> signInManager) =>
        {
            var request = context.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

            if (request.IsPasswordGrantType())
            {
                var user = await userManager.FindByNameAsync(request.Username);
                if (user is null)
                {
                    return Results.Problem("Invalid username or password", statusCode: 400);
                }

                // Check password
                var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
                if (!result.Succeeded)
                {
                    return Results.Problem("Invalid username or password", statusCode: 400);
                }

                // Create principal
                var principal = await signInManager.CreateUserPrincipalAsync(user);

                // OpenIddict requires the "sub" claim to be set explicitly if not found
                if (!principal.HasClaim(c => c.Type == OpenIddictConstants.Claims.Subject))
                {
                    var id = await userManager.GetUserIdAsync(user);
                    var identity = (ClaimsIdentity)principal.Identity!;
                    identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, id));
                }

                // Set scopes
                var scopes = request.GetScopes();
                principal.SetScopes(scopes);
                principal.SetResources("attendance_api");

                // Set destinations
                foreach (var claim in principal.Claims)
                {
                    claim.SetDestinations(GetDestinations(claim));
                }

                return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsRefreshTokenGrantType())
            {
                // Authenticate the refresh token
                var info = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var user = await userManager.GetUserAsync(info.Principal);
                if (user is null)
                {
                    return Results.Problem("Invalid refresh token", statusCode: 400);
                }

                var principal = await signInManager.CreateUserPrincipalAsync(user);

                foreach (var claim in principal.Claims)
                {
                    claim.SetDestinations(GetDestinations(claim));
                }

                return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            return Results.BadRequest(new OpenIddictResponse
            {
                Error = OpenIddictConstants.Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        });
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Access token destination
        yield return OpenIddictConstants.Destinations.AccessToken;

        // Identity token destination for specific claims
        if (claim.Type == OpenIddictConstants.Claims.Name ||
            claim.Type == OpenIddictConstants.Claims.Email ||
            claim.Type == OpenIddictConstants.Claims.Role)
        {
            yield return OpenIddictConstants.Destinations.IdentityToken;
        }
    }
}
