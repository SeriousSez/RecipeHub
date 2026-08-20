using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace RecipeHub.ApplicationService
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            //services.AddScoped<IJwtFactory, JwtFactory>();
            return services;
        }
    }
}
