using AutoMapper;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Entities.Fridge;
using RecipeHub.Domain.Entities.Grocery;
using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;

namespace RecipeHub.Api
{
    public class AutoMapper : Profile
    {
        public AutoMapper()
        {
            CreateMap<User, RegistrationViewModel>();
            CreateMap<RegistrationViewModel, User>();
            CreateMap<User, UserResponse>();
            CreateMap<UserResponse, User>();

            CreateMap<UserSettings, UserSettingsUpdateViewModel>();
            CreateMap<UserSettingsUpdateViewModel, UserSettings>();
            CreateMap<UserSettings, UserSettingsResponse>();
            CreateMap<UserSettingsResponse, UserSettings>();
            CreateMap<PublicProfile, PublicProfileResponse>();

            CreateMap<Recipe, RecipeViewModel>().ForPath(dest => dest.Creator, opt => opt.MapFrom(x => x.Creator == null ? null : x.Creator.UserName)).ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<RecipeViewModel, Recipe>().ForPath(dest => dest.Creator.UserName, opt => opt.MapFrom(x => x.Creator)).ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<Recipe, RecipeResponse>().ForPath(dest => dest.Creator, opt => opt.MapFrom(x => x.Creator == null ? null : x.Creator.UserName)).ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<RecipeResponse, Recipe>().ForPath(dest => dest.Creator.UserName, opt => opt.MapFrom(x => x.Creator)).ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));

            CreateMap<Image, ImageViewModel>();
            CreateMap<ImageViewModel, Image>();
            CreateMap<Image, ImageResponse>();
            CreateMap<ImageResponse, Image>();

            // Recipe
            CreateMap<Ingredient, IngredientViewModel>().ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<IngredientViewModel, Ingredient>().ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<Ingredient, IngredientResponse>().ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));
            CreateMap<IngredientResponse, Ingredient>().ForMember(dest => dest.Image, opt => opt.MapFrom(x => x.Image));

            CreateMap<RecipeIngredient, IngredientResponse>();
            CreateMap<IngredientResponse, RecipeIngredient>();
            CreateMap<RecipeIngredient, RecipeResponse>();
            CreateMap<RecipeResponse, RecipeIngredient>();

            CreateMap<Favorites, FavoritesResponse>().ForMember(dest => dest.User, opt => opt.MapFrom(x => x.User)).ForMember(dest => dest.Recipes, opt => opt.MapFrom(x => x.Recipes)).ForMember(dest => dest.Ingredients, opt => opt.MapFrom(x => x.Ingredients));
            CreateMap<FavoritesResponse, Favorites>().ForMember(dest => dest.User, opt => opt.MapFrom(x => x.User)).ForMember(dest => dest.Recipes, opt => opt.MapFrom(x => x.Recipes)).ForMember(dest => dest.Ingredients, opt => opt.MapFrom(x => x.Ingredients));

            // Grocery
            CreateMap<GroceryList, GroceryListResponse>().ForPath(dest => dest.UserId, opt => opt.MapFrom(x => x.User.Id)).ForMember(dest => dest.Ingredients, opt => opt.MapFrom(x => x.Ingredients));
            CreateMap<GroceryListResponse, GroceryList>().ForPath(dest => dest.User.Id, opt => opt.MapFrom(x => x.UserId)).ForMember(dest => dest.Ingredients, opt => opt.MapFrom(x => x.Ingredients));

            CreateMap<GroceryIngredient, IngredientResponse>();
            CreateMap<IngredientResponse, GroceryIngredient>();

            CreateMap<Fridge, FridgeResponse>().ForPath(dest => dest.AmountOfGroceries, opt => opt.MapFrom(x => x.Groceries.Count));
            CreateMap<FridgeResponse, Fridge>();
            CreateMap<FridgeModel, Fridge>();

            CreateMap<FridgeGrocery, FridgeGroceryResponse>();
            CreateMap<FridgeGroceryResponse, FridgeGrocery>();
            CreateMap<FridgeGroceryModel, FridgeGrocery>();
        }
    }
}
