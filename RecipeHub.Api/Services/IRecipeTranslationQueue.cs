using System;
using System.Collections.Generic;

namespace RecipeHub.Api.Services
{
    public interface IRecipeTranslationQueue
    {
        void Enqueue(IEnumerable<Guid> recipeIds, string preferredLanguage = null);
        void EnqueueRemaining(IEnumerable<Guid> recipeIds, string completedLanguage);
    }
}
