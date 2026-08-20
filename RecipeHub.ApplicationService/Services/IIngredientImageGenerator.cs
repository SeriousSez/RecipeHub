using RecipeHub.Domain.Models;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public interface IIngredientImageGenerator
    {
        Task<ImageViewModel> GenerateAsync(string ingredientName, string description);
    }
}
