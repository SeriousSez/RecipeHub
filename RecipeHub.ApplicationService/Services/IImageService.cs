using RecipeHub.Domain.Entities.Recipe;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public interface IImageService
    {
        Task<Image> Create(ImageViewModel model);
        Task<Image> Delete(ImageResponse model);
        Task<IEnumerable<ImageResponse>> GetAll();
    }
}