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
            var countryCode = ResolveCountryCode(model);
            return _providers.TryGetValue(countryCode, out provider);
        }

        private static string ResolveCountryCode(GroceryOfferSearchViewModel model)
        {
            if (model != null)
            {
                if (model.Latitude >= 54.5 && model.Latitude <= 57.8 && model.Longitude >= 8.0 && model.Longitude <= 15.2)
                    return "DK";
                if (model.Latitude >= 57.5 && model.Latitude <= 59.8 && model.Longitude >= 21.5 && model.Longitude <= 28.3)
                    return "EE";
                if (model.Latitude >= 35.8 && model.Latitude <= 42.2 && model.Longitude >= 25.5 && model.Longitude <= 45.1)
                    return "TR";
            }

            return string.IsNullOrWhiteSpace(model?.CountryCode) ? "DK" : model.CountryCode.Trim();
        }
    }
}