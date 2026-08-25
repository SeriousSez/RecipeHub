using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeHub.ApplicationService.Services;

namespace RecipeHub.Api.Services
{
    public class RecipeTranslationQueue : BackgroundService, IRecipeTranslationQueue
    {
        private static readonly string[] SupportedLanguages = { "Danish", "English", "Estonian", "Turkish" };
        private readonly Channel<TranslationJob> _jobs = Channel.CreateBounded<TranslationJob>(new BoundedChannelOptions(250)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        private readonly ConcurrentDictionary<string, byte> _queued = new ConcurrentDictionary<string, byte>();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RecipeTranslationQueue> _logger;

        public RecipeTranslationQueue(IServiceScopeFactory scopeFactory, ILogger<RecipeTranslationQueue> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Enqueue(IEnumerable<Guid> recipeIds, string preferredLanguage = null)
        {
            var languages = SupportedLanguages.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(preferredLanguage))
            {
                languages = new[] { preferredLanguage }.Concat(SupportedLanguages)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var recipeId in recipeIds?.Distinct() ?? Enumerable.Empty<Guid>())
            {
                foreach (var language in languages)
                {
                    var key = $"{recipeId}:{language}";
                    if (_queued.TryAdd(key, 0))
                    {
                        if (!_jobs.Writer.TryWrite(new TranslationJob(recipeId, language, key)))
                            _queued.TryRemove(key, out _);
                    }
                }
            }
        }

        public void EnqueueRemaining(IEnumerable<Guid> recipeIds, string completedLanguage)
        {
            var remainingLanguages = SupportedLanguages
                .Where(language => !language.Equals(completedLanguage, StringComparison.OrdinalIgnoreCase));

            foreach (var recipeId in recipeIds?.Distinct() ?? Enumerable.Empty<Guid>())
            {
                foreach (var language in remainingLanguages)
                {
                    var key = $"{recipeId}:{language}";
                    if (_queued.TryAdd(key, 0) && !_jobs.Writer.TryWrite(new TranslationJob(recipeId, language, key)))
                        _queued.TryRemove(key, out _);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var recipeService = scope.ServiceProvider.GetRequiredService<IRecipeService>();
                    var translationService = scope.ServiceProvider.GetRequiredService<IRecipeTranslationService>();
                    var recipe = await recipeService.Get(job.RecipeId);
                    if (recipe != null && !string.Equals(recipe.Language, job.Language, StringComparison.OrdinalIgnoreCase))
                    {
                        await translationService.TranslateAsync(recipe, job.Language);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Background translation failed for recipe {RecipeId} and language {Language}", job.RecipeId, job.Language);
                }
                finally
                {
                    _queued.TryRemove(job.Key, out _);
                }
            }
        }

        private sealed record TranslationJob(Guid RecipeId, string Language, string Key);
    }
}
