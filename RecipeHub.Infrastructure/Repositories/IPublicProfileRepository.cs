using RecipeHub.Domain.Entities;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public interface IPublicProfileRepository
    {
        Task<PublicProfile> GetByUserId(string userId);
        Task Create(PublicProfile profile);
        Task Update(PublicProfile profile);
    }
}
