using Microsoft.AspNetCore.Identity;

namespace Sizzle.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string Name { get; set; } = string.Empty;

    public string? VerificationCode { get; set; }

    public ICollection<Recipe> Recipes { get; set; } = [];

    public ICollection<ApplicationUserRecipe> ApplicationUserRecipes { get; set; } = [];

    public ICollection<Comment> Comments { get; set; } = [];

    public ICollection<UserFollower> Followers { get; set; } = [];

    public ICollection<UserFollower> Following { get; set; } = [];
}
