using AuthServiceProvider.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;

namespace AuthServiceProvider.Services
{
    public class GrpcUpdateRoleService : RoleService.RoleServiceBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<GrpcUpdateRoleService> _logger;

        public GrpcUpdateRoleService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<GrpcUpdateRoleService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public override async Task<PromotionResponse> PromoteToManager(PromotionRequest request, ServerCallContext context)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return new PromotionResponse { Success = false, Message = "User not found." };

            
            if (!await _roleManager.RoleExistsAsync(request.Role))
            {
                await _roleManager.CreateAsync(new IdentityRole(request.Role));
            }
                

            // 2. Add Manager role if they don't have it
            if (!await _userManager.IsInRoleAsync(user, request.Role))
            {
                var result = await _userManager.AddToRoleAsync(user, request.Role);
                if (!result.Succeeded)
                    return new PromotionResponse { Success = false, Message = "Failed to assign role." };
            }

            return new PromotionResponse { Success = true, Message = "Role assigned successfully." };
        }
    }

}
