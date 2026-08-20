using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities.Recipe;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public class ImageRepository : BaseRepository<Image>, IImageRepository
    {
        protected internal RecipeHubContext _recipeHubContext { get { return _context as RecipeHubContext; } }

        public ImageRepository(RecipeHubContext db) : base(db) { }

        public async Task<Image> GetByUrl(string url)
        {
            return await _context.Images.FirstOrDefaultAsync(i => i.Url == url);
        }
    }
}
