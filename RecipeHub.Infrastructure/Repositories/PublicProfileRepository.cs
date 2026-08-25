using Microsoft.EntityFrameworkCore;
using RecipeHub.Domain.Entities;
using System.Threading.Tasks;

namespace RecipeHub.Infrastructure.Repositories
{
    public class PublicProfileRepository : IPublicProfileRepository
    {
        private readonly RecipeHubContext _context;

        public PublicProfileRepository(RecipeHubContext context)
        {
            _context = context;
        }

        public Task<PublicProfile> GetByUserId(string userId)
        {
            return _context.Set<PublicProfile>().FirstOrDefaultAsync(profile => profile.UserId == userId);
        }

        public async Task Create(PublicProfile profile)
        {
            await _context.Set<PublicProfile>().AddAsync(profile);
            await _context.SaveChangesAsync();
        }

        public async Task Update(PublicProfile profile)
        {
            _context.Set<PublicProfile>().Update(profile);
            await _context.SaveChangesAsync();
        }
    }
}
