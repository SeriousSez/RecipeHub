using AutoMapper;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Entities.Grocery;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure.Interfaces;
using RecipeHub.Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public class GroceryService : IGroceryService
    {
        private readonly ILogger<GroceryService> _logger;
        private readonly IGroceryRepository _groceryRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public GroceryService(ILogger<GroceryService> logger, IGroceryRepository groceryRepository, IUserRepository userRepository, IMapper mapper)
        {
            _logger = logger;
            _groceryRepository = groceryRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<GroceryListResponse> GetGroceryList(string userId)
        {
            var groceryList = await _groceryRepository.GetByUserId(userId);

            return _mapper.Map<GroceryListResponse>(groceryList);
        }

        public async Task Create(Guid userId)
        {
            var user = await _userRepository.GetByUserId(userId);
            await _groceryRepository.Create(new GroceryList { User = user, Ingredients = new List<GroceryIngredient>() });

            _logger.LogTrace("GroceryList created!");
        }
    }
}
