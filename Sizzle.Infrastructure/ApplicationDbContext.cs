using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Recipe> Recipes { get; set; }

    public DbSet<ApplicationUserRecipe> ApplicationUserRecipes { get; set; }

    public DbSet<Ingredient> Ingredients { get; set; }

    public DbSet<IngredientTranslation> IngredientTranslations { get; set; }

    public DbSet<Category> Categories { get; set; }

    public DbSet<CategoryTranslation> CategoryTranslations { get; set; }

    public DbSet<RecipeIngredient> RecipeIngredients { get; set; }

    public DbSet<RecipeStep> RecipeSteps { get; set; }

    public DbSet<RecipeStepFile> RecipeStepFiles { get; set; }

    public DbSet<Comment> Comments { get; set; }

    public DbSet<UserFollower> UserFollowers { get; set; }

    public DbSet<DeviceToken> DeviceTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Recipe>()
            .HasOne(r => r.ApplicationUser)
            .WithMany(u => u.Recipes)
            .HasForeignKey(r => r.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Recipe>()
            .HasOne(r => r.Category)
            .WithMany(c => c.Recipes)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CategoryTranslation>()
            .HasKey(t => new { t.CategoryId, t.LanguageCode });

        builder.Entity<CategoryTranslation>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Translations)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUserRecipe>()
            .HasKey(aur => new { aur.ApplicationUserId, aur.RecipeId });

        builder.Entity<ApplicationUserRecipe>()
            .HasOne(aur => aur.ApplicationUser)
            .WithMany(u => u.ApplicationUserRecipes)
            .HasForeignKey(aur => aur.ApplicationUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUserRecipe>()
            .HasOne(aur => aur.Recipe)
            .WithMany(r => r.ApplicationUserRecipes)
            .HasForeignKey(aur => aur.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserFollower>()
            .HasKey(follow => new { follow.FollowerId, follow.FollowingId });

        builder.Entity<UserFollower>()
            .HasOne(follow => follow.Follower)
            .WithMany(user => user.Following)
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserFollower>()
            .HasOne(follow => follow.Following)
            .WithMany(user => user.Followers)
            .HasForeignKey(follow => follow.FollowingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<UserFollower>()
            .HasIndex(follow => follow.FollowingId);

        builder.Entity<DeviceToken>()
            .HasKey(device => device.Token);

        builder.Entity<DeviceToken>()
            .Property(device => device.Token)
            .HasMaxLength(512);

        builder.Entity<DeviceToken>()
            .HasOne(device => device.User)
            .WithMany()
            .HasForeignKey(device => device.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecipeIngredient>()
            .HasKey(ri => new { ri.RecipeId, ri.IngredientId });

        builder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Recipe)
            .WithMany(r => r.RecipeIngredients)
            .HasForeignKey(ri => ri.RecipeId);

        builder.Entity<RecipeIngredient>()
            .HasOne(ri => ri.Ingredient)
            .WithMany(i => i.RecipeIngredients)
            .HasForeignKey(ri => ri.IngredientId);

        builder.Entity<RecipeStep>()
            .HasOne(rs => rs.Recipe)
            .WithMany(r => r.RecipeSteps)
            .HasForeignKey(rs => rs.RecipeId);

        builder.Entity<RecipeStepFile>()
            .HasOne(file => file.RecipeStep)
            .WithMany(step => step.Files)
            .HasForeignKey(file => file.RecipeStepId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasOne(comment => comment.Parent)
            .WithMany(parent => parent.Replies)
            .HasForeignKey(comment => comment.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Comment>()
            .HasOne(comment => comment.User)
            .WithMany(user => user.Comments)
            .HasForeignKey(comment => comment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasOne(comment => comment.Recipe)
            .WithMany(recipe => recipe.Comments)
            .HasForeignKey(comment => comment.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Comment>()
            .HasIndex(comment => new { comment.RecipeId, comment.CreatedAt });

        builder.Entity<Ingredient>()
            .HasOne(i => i.Parent)
            .WithMany(i => i.Children)
            .HasForeignKey(i => i.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<IngredientTranslation>()
            .HasKey(t => new { t.IngredientId, t.LanguageCode });

        builder.Entity<IngredientTranslation>()
            .HasOne(t => t.Ingredient)
            .WithMany(i => i.Translations)
            .HasForeignKey(t => t.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
