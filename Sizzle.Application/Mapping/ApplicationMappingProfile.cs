using AutoMapper;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Dtos.Users;
using Sizzle.Application.Services.Recipes;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Mapping;

public class ApplicationMappingProfile : Profile
{
    public ApplicationMappingProfile()
    {
        CreateMap<CreateRecipeDto, Recipe>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.CreationDate, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUser, options => options.Ignore())
            .ForMember(destination => destination.Category, options => options.Ignore())
            .ForMember(destination => destination.RecipeSteps, options => options.Ignore())
            .ForMember(destination => destination.RecipeIngredients, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUserRecipes, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore());
        CreateMap<UpdateRecipeDto, Recipe>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.IsDraft, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUserId, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUser, options => options.Ignore())
            .ForMember(destination => destination.Category, options => options.Ignore())
            .ForMember(destination => destination.RecipeSteps, options => options.Ignore())
            .ForMember(destination => destination.RecipeIngredients, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUserRecipes, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore());
        CreateMap<RecipeStepInputDto, RecipeStep>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.RecipeId, options => options.Ignore())
            .ForMember(destination => destination.Recipe, options => options.Ignore())
            .ForMember(destination => destination.Position, options => options.Ignore())
            .ForMember(destination => destination.Files, options => options.Ignore());
        CreateMap<RegisterUserDto, ApplicationUser>()
            .ForMember(destination => destination.UserName, options => options.MapFrom(source => source.Email))
            .ForMember(destination => destination.VerificationCode, options => options.Ignore());
        CreateMap<CreateCommentDto, Comment>()
            .ForMember(destination => destination.Content, options => options.MapFrom(source => source.Content.Trim()))
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.User, options => options.Ignore())
            .ForMember(destination => destination.Recipe, options => options.Ignore())
            .ForMember(destination => destination.Parent, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore());
        CreateMap<Comment, CommentDto>()
            .ForMember(destination => destination.UserName, options => options.MapFrom((source, _, _, context) =>
                context.TryGetItems(out var items) && items.TryGetValue("UserName", out var name) && name is string userName
                    ? userName : source.User != null ? source.User.Name : string.Empty))
            .ForMember(destination => destination.UserImageUrl, options => options.MapFrom<CommentImageUrlResolver>());
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(destination => destination.Description, options => options.MapFrom(source => source.Description ?? string.Empty))
            .ForMember(destination => destination.Email, options => options.MapFrom(source => source.Email ?? string.Empty))
            .ForMember(destination => destination.ImageUrl, options => options.MapFrom<UserImageUrlResolver>());
        CreateMap<Recipe, RecipeDto>()
            .ForMember(destination => destination.UserId, options => options.MapFrom(source => source.ApplicationUserId))
            .ForMember(destination => destination.Calories, options => options.MapFrom(source => RecipeCalories.Calculate(source.RecipeIngredients)))
            .ForMember(destination => destination.UserName, options => options.MapFrom(source => source.ApplicationUser == null ? string.Empty : source.ApplicationUser.Name))
            .ForMember(destination => destination.UserImageUrl, options => options.MapFrom<RecipeUserImageUrlResolver>())
            .ForMember(destination => destination.FileUrl, options => options.MapFrom<RecipeFileUrlResolver>());
        CreateMap<Recipe, FeedRecipeDto>()
            .IncludeBase<Recipe, RecipeDto>()
            .ForMember(destination => destination.IsFollowed, options => options.MapFrom((source, _, _, context) =>
                ViewerId(context) is Guid viewerId && source.ApplicationUser?.Followers.Any(follow => follow.FollowerId == viewerId) == true))
            .ForMember(destination => destination.IsLiked, options => options.MapFrom((source, _, _, context) =>
                ViewerId(context).HasValue && source.ApplicationUserRecipes.Any(link => link.IsLiked)))
            .ForMember(destination => destination.IsSaved, options => options.MapFrom((source, _, _, context) =>
                ViewerId(context).HasValue && source.ApplicationUserRecipes.Any(link => link.IsSaved)))
            .ForMember(destination => destination.IsSavedAt, options => options.MapFrom((source, _, _, context) =>
                ViewerId(context).HasValue ? source.ApplicationUserRecipes.FirstOrDefault(link => link.IsSaved)?.IsSavedAt : null));
        CreateMap<Recipe, RecipeDetailsDto>()
            .IncludeBase<Recipe, FeedRecipeDto>()
            .ForMember(destination => destination.Steps, options => options.MapFrom<RecipeDetailsStepsResolver>())
            .ForMember(destination => destination.Ingredients, options => options.MapFrom<RecipeDetailsIngredientsResolver>());
    }

    private static Guid? ViewerId(ResolutionContext context) =>
        context.TryGetItems(out var items) && items.TryGetValue("ViewerId", out var id) ? (Guid?)id : null;
}
