using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System;
using System.Threading.Tasks;

namespace RecipeHub.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class FavoriteController : Controller
    {
        private const string FavoriteCacheVersionKey = "favorites:cache:version";
        private readonly ILogger<FavoriteController> _logger;
        private readonly IFavoriteService _favoriteService;
        private readonly IMemoryCache _memoryCache;

        public FavoriteController(ILogger<FavoriteController> logger, IFavoriteService favoriteService, IMemoryCache memoryCache)
        {
            _logger = logger;
            _favoriteService = favoriteService;
            _memoryCache = memoryCache;
        }

        [HttpGet("get")]
        public async Task<IActionResult> Get(string username)
        {
            var cacheVersion = GetFavoriteCacheVersion();
            var cacheKey = $"favorites:get:{username?.ToLowerInvariant()}:v{cacheVersion}";
            if (_memoryCache.TryGetValue(cacheKey, out FavoritesResponse cachedFavorites))
            {
                return new OkObjectResult(cachedFavorites);
            }

            var favorites = await _favoriteService.Get(username);

            _memoryCache.Set(cacheKey, favorites, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });

            _logger.LogTrace("Favorites fetched! Favorites: {@Favorites}", favorites);
            return new OkObjectResult(favorites);
        }

        [HttpGet("isfavored")]
        public async Task<IActionResult> IsFavored(string username, string title, string creator)
        {
            var favorites = await _favoriteService.IsFavored(username, title, creator);

            _logger.LogTrace("Favorites fetched! Favorites: {@Favorites}", favorites);
            return new OkObjectResult(favorites);
        }

        [HttpPost("recipe")]
        public async Task<IActionResult> Recipe([FromBody] FavoriteRecipeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ingredient = await _favoriteService.Recipe(model);
            if (ingredient == null)
                return BadRequest("Failed to add Recipe to Favorites!");

            BumpFavoriteCacheVersion();

            return new OkResult();
        }

        [HttpPost("ingredient")]
        public async Task<IActionResult> Ingredient([FromBody] FavoriteIngredientViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ingredient = await _favoriteService.Ingredient(model);
            if (ingredient == null)
                return BadRequest("Failed to add Ingredient to Favorites!");

            BumpFavoriteCacheVersion();

            return new OkResult();
        }

        private int GetFavoriteCacheVersion()
        {
            if (_memoryCache.TryGetValue(FavoriteCacheVersionKey, out int version))
            {
                return version;
            }

            _memoryCache.Set(FavoriteCacheVersionKey, 0);
            return 0;
        }

        private void BumpFavoriteCacheVersion()
        {
            var nextVersion = GetFavoriteCacheVersion() + 1;
            _memoryCache.Set(FavoriteCacheVersionKey, nextVersion);
        }
    }
}
