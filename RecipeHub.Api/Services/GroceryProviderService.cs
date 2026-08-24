using RecipeHub.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeHub.Api.Services
{
    public interface IGroceryProvider
    {
        string CountryCode { get; }
        bool IsConfigured { get; }
        Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model);
    }

    public interface IGroceryOfferService
    {
        bool IsConfigured(GroceryOfferSearchViewModel model);
        Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model);
    }

    public class GroceryProviderService : IGroceryOfferService
    {
        private readonly IReadOnlyDictionary<string, IGroceryProvider> _providers;

        public GroceryProviderService(IEnumerable<IGroceryProvider> providers)
        {
            _providers = providers.ToDictionary(provider => provider.CountryCode, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsConfigured(GroceryOfferSearchViewModel model) =>
            TryGetProvider(model, out var provider) && provider.IsConfigured;

        public Task<GroceryOfferSearchResponse> FindNearbyOffersAsync(GroceryOfferSearchViewModel model)
        {
            if (!TryGetProvider(model, out var provider))
            {
                throw new GroceryOfferProviderException(501);
            }

            return provider.FindNearbyOffersAsync(model);
        }

        private bool TryGetProvider(GroceryOfferSearchViewModel model, out IGroceryProvider provider)
        {
            var countryCode = string.IsNullOrWhiteSpace(model?.CountryCode) ? "DK" : model.CountryCode.Trim();
            return _providers.TryGetValue(countryCode, out provider);
        }
    }
}