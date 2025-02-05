using Microsoft.AspNetCore.Identity;

namespace PA_Website.Data
{
    public class SeedRoles
    {
        public static async Task SeedAsync(RoleManager<IdentityRole> roleUser)
        {
            string[] roleNames = { "Admin", "User" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleUser.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleUser.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}
