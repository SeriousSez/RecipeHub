using RecipeHub.Domain.Entities.Recipe;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IImageRepository : IBaseRepository<Image>
    {
        Task<Image> GetByUrl(string url);
    }
}