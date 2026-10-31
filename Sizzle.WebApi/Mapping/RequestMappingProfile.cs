using AutoMapper;
using Sizzle.Application.Dtos.Comments;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Dtos.Recipes;
using Sizzle.Application.Dtos.Users;
using Sizzle.WebApi.Requests.Base;
using Sizzle.WebApi.Requests.Comments;
using Sizzle.WebApi.Requests.Other;
using Sizzle.WebApi.Requests.Recipes;
using Sizzle.WebApi.Requests.Users;

namespace Sizzle.WebApi.Mapping;

public class RequestMappingProfile : Profile
{
    public RequestMappingProfile()
    {
        CreateMap<GetRecipesRequestModel, GetRecipesDto>()
            .ForMember(destination => destination.OwnerId, options => options.Ignore())
            .ForMember(destination => destination.ApplicationUserId, options => options.Ignore());
        CreateMap<CreateRecipeRequestModel, CreateRecipeDto>()
            .ForMember(destination => destination.ApplicationUserId,
                options => options.MapFrom((_, _, _, context) => (Guid)context.Items["ApplicationUserId"]));
        CreateMap<UpdateRecipeRequestModel, UpdateRecipeDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom((_, _, _, context) => (Guid)context.Items["RecipeId"]))
            .ForMember(destination => destination.ApplicationUserId,
                options => options.MapFrom((_, _, _, context) => (Guid)context.Items["ApplicationUserId"]));
        CreateMap<RecipeStepRequestModel, RecipeStepInputDto>();
        CreateMap<RecipeIngredientRequestModel, RecipeIngredientInputDto>();
        CreateMap<NewIngredientRequestModel, NewIngredientDto>();
        CreateMap<IFormFile, RecipeMediaUploadDto>()
            .ForMember(destination => destination.OpenReadStream,
                options => options.MapFrom(source => new Func<Stream>(source.OpenReadStream)));

        CreateMap<RegisterUserRequestModel, RegisterUserDto>();
        CreateMap<EmailConfirmationRequestModel, ConfirmEmailDto>();
        CreateMap<LoginRequestModel, LoginUserDto>();
        CreateMap<UpdateUserProfileRequestModel, UpdateUserProfileDto>()
            .ForMember(destination => destination.Icon, options => options.Ignore())
            .ForMember(destination => destination.IconName, options => options.MapFrom(source => source.Icon == null ? null : source.Icon.FileName));
        CreateMap<PaginationRequestModel, GetCommentsByRecipeIdDto>()
            .ForMember(destination => destination.RecipeId, options => options.Ignore());
        CreateMap<CreateCommentRequestModel, CreateCommentDto>()
            .ForMember(destination => destination.RecipeId, options => options.Ignore())
            .ForMember(destination => destination.UserId, options => options.Ignore());
        CreateMap<SearchPaginationRequestModel, GetAllIngredientsDto>()
            .ForMember(destination => destination.LanguageCode, options => options.Ignore());
    }
}
