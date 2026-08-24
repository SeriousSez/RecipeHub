using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using RecipeHub.ApplicationService.Auth;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.ApplicationService.Services;
using RecipeHub.Domain.Entities;
using RecipeHub.Infrastructure;
using RecipeHub.Infrastructure.Interfaces;
using RecipeHub.Infrastructure.Managers;
using RecipeHub.Infrastructure.Repositories;
using RecipeHub.Infrastructure.Repositories.Fridge;
using RecipeHub.Infrastructure.Repositories.Grocery;
using System;
using System.Data.Common;
using System.Text;
using RecipeHub.Api.Services;
using RecipeHub.Api.Converters;
using System.Threading.RateLimiting;

namespace RecipeHub
{
    public class Startup
    {
        private const string CorsPolicyName = "AllowOrigin";
        private const string SecretKey = "iNivDmHLpUA223sqsfhqGbMRdRj1PVkH";
        private readonly SymmetricSecurityKey _signinKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(SecretKey));

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            var groceryOfferRateLimit = Math.Max(1, Configuration.GetValue("GroceryOffers:RateLimitPerMinute", 20));
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddPolicy("GroceryOffers", context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = groceryOfferRateLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
                options.AddPolicy("RecipeTranslations", context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, Configuration.GetValue("RecipeTranslation:RateLimitPerMinute", 30)),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRecipeRepository, RecipeRepository>();
            services.AddScoped<IIngredientRepository, IngredientRepository>();
            services.AddScoped<IRecipeIngredientRepository, RecipeIngredientRepository>();
            services.AddScoped<IImageRepository, ImageRepository>();
            services.AddScoped<IFavoriteRepository, FavoriteRepository>();
            services.AddScoped<IGroceryRepository, GroceryRepository>();
            services.AddScoped<IFridgeRepository, FridgeRepository>();
            services.AddScoped<IFridgeGroceryRepository, FridgeGroceryRepository>();

            services.AddScoped<IIdentityManager, IdentityManager>();

            services.AddScoped<IJwtFactory, JwtFactory>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IRecipeService, RecipeService>();
            services.AddScoped<IIngredientService, IngredientService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddHttpClient<OpenAiIngredientImageGenerator>();
            services.AddHttpClient<LocalStableDiffusionIngredientImageGenerator>();
            services.AddHttpClient<WikipediaIngredientImageGenerator>();
            services.AddHttpClient<IRecipeNutritionEstimator, RecipeNutritionEstimator>();
            services.AddHttpClient<IRecipeTranslationService, OpenAiRecipeTranslationService>();
            services.AddHttpClient("Madpris", client =>
            {
                client.BaseAddress = new Uri(Configuration["Madpris:BaseUrl"] ?? "https://madpris.gratis.dk/");
                client.Timeout = TimeSpan.FromSeconds(60);
            });
            services.AddHttpClient("ShelfAtlas", client =>
            {
                client.BaseAddress = new Uri(Configuration["ShelfAtlas:BaseUrl"] ?? "https://api.shelfatlas.com/api/v1/public/catalog/");
                client.Timeout = TimeSpan.FromSeconds(20);
            });
            services.AddHttpClient("CamGoz", client =>
            {
                client.BaseAddress = new Uri(Configuration["CamGoz:BaseUrl"] ?? "https://camgoz.jojapi.net/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("MarketFiyati", client =>
            {
                client.BaseAddress = new Uri(Configuration["MarketFiyati:BaseUrl"] ?? "https://api.marketfiyati.org.tr/api/v2/");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddHttpClient("OpenPrices", client =>
            {
                client.BaseAddress = new Uri(Configuration["OpenPrices:BaseUrl"] ?? "https://prices.openfoodfacts.org/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Configuration["OpenPrices:UserAgent"] ?? "RecipeHub/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddScoped<IGroceryProvider, MadprisGroceryOfferService>();
            services.AddScoped<IGroceryProvider, CamGozGroceryProvider>();
            services.AddScoped<IGroceryProvider, OpenPricesGroceryProvider>();
            services.AddScoped<IGroceryOfferService, GroceryProviderService>();
            services.AddScoped<IIngredientImageGenerator, IngredientImageGenerator>();
            services.AddScoped<IFavoriteService, FavoriteService>();
            services.AddScoped<IGroceryService, GroceryService>();
            services.AddScoped<IFridgeService, FridgeService>();

            SetupDatabase(services);

            // Configure CORS - MORE PERMISSIVE for development
            services.AddCors(c =>
            {
                c.AddPolicy(CorsPolicyName, options => options
                    .WithOrigins(
                        "http://localhost:4200",
                        "https://localhost:4200",
                        "http://127.0.0.1:4200",
                        "https://127.0.0.1:4200",
                        "https://www.recipes.sezginsahin.dk",
                        "https://recipes.sezginsahin.dk"
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("Authorization")
                    .AllowCredentials());
            });

            // Fixed: Use "JWTSettings" to match appsettings.json (all caps)
            var jwtSettings = Configuration.GetSection("JWTSettings");

            // Add null checks to prevent crashes
            var validIssuer = jwtSettings.GetSection("validIssuer").Value ?? "RecipeHub";
            var validAudience = jwtSettings.GetSection("validAudience").Value ?? "http://localhost:44366/";
            var securityKey = jwtSettings.GetSection("securityKey").Value ?? SecretKey;

            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = validIssuer,
                    ValidAudience = validAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey))
                };

                // Allow authorization header in CORS
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                        logger.LogInformation($"JWT Message Received: {context.Request.Headers["Authorization"]}");
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                };
            });

            services.AddIdentity<User, IdentityRole>().AddEntityFrameworkStores<RecipeHubContext>().AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(opt => opt.TokenLifespan = TimeSpan.FromHours(2));

            services.Configure<IdentityOptions>(options =>
            {
                // Default Password settings.
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 0;

                options.User.RequireUniqueEmail = true;

                //options.Lockout.AllowedForNewUsers = true;
                //options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
                //options.Lockout.MaxFailedAccessAttempts = 3;
            });

            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new ImageViewModelJsonConverter());
            });
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "RecipeHub.API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\"",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            var connectionString = Configuration.GetConnectionString("MySql");
            var connectionBuilder = new DbConnectionStringBuilder();
            var server = "<unknown>";
            var database = "<unknown>";

            try
            {
                connectionBuilder.ConnectionString = connectionString ?? string.Empty;
                server = connectionBuilder.TryGetValue("Server", out var serverValue) ? serverValue?.ToString() : server;
                database = connectionBuilder.TryGetValue("Database", out var databaseValue) ? databaseValue?.ToString() : database;
            }
            catch
            {
                // Keep startup logging safe even when the configured connection string is malformed.
            }

            logger.LogInformation("RecipeHub runtime environment: {EnvironmentName}; database target: Server={Server}; Database={Database}", env.EnvironmentName, server, database);

            // Add exception handling first to catch errors
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            // Use forwarded headers before other middleware
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.All
            });

            // Log all requests for debugging - INCLUDING OPTIONS
            app.Use(async (context, next) =>
            {
                logger.LogInformation($"Request: {context.Request.Method} {context.Request.Path} from {context.Request.Headers["Origin"]}");
                logger.LogInformation($"Headers: {string.Join(", ", context.Request.Headers.Keys)}");

                await next();
                logger.LogInformation($"Response: {context.Response.StatusCode}");
            });

            // Completely disable HTTPS redirection for now
            // app.UseHttpsRedirection();

            // Swagger configuration
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RecipeHub v1"));
            }

            app.UseRouting();
            app.UseRateLimiter();
            app.UseCors(CorsPolicyName);
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapSwagger();
            });

            // Run startup data seeding only in development
            if (env.IsDevelopment())
            {
                try
                {
                    app.SeedDatabase();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error seeding database");
                }
            }
        }

        private void SetupDatabase(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("MySql");

            services.AddDbContext<RecipeHubContext>(options =>
            {
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    options.UseInMemoryDatabase(databaseName: "RecipeHub");
                    return;
                }

                try
                {
                    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 11)));
                }
                catch
                {
                    options.UseInMemoryDatabase(databaseName: "RecipeHub");
                }
            });

            services.AddAutoMapper(cfg =>
            {
                // Disable method mapping to avoid scanning extension methods like MaxFloat that break generic constraints
                cfg.ShouldMapMethod = _ => false;
                cfg.AddProfile<Api.AutoMapper>();
            }, typeof(Startup));
        }

        private void SetupSecurity(IServiceCollection services)
        {
            services.AddIdentity<User, IdentityRole>().AddEntityFrameworkStores<RecipeHubContext>().AddDefaultTokenProviders();
            services.Configure<DataProtectionTokenProviderOptions>(opt => opt.TokenLifespan = TimeSpan.FromHours(2));

            //Enable CORS
            services.AddCors(c =>
            {
                c.AddPolicy("AllowOrigin", options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
                c.AddPolicy("CorsPolicy", options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
                c.AddPolicy("*", options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            var jwtSettings = Configuration.GetSection("JWTSettings");
            services.AddAuthentication(opt =>
            {
                opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.GetSection("validIssuer").Value,
                    ValidAudience = jwtSettings.GetSection("validAudience").Value,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.GetSection("securityKey").Value))
                };
            });
        }
    }
}