export interface RecipeTaxonomyGroup {
    id: string;
    labelKey: string;
    values: string[];
}

export interface RecipeNutritionHighlight {
    labelKey: string;
    value: number | null;
    unit: string;
}

export function getRecipeNutritionHighlights(recipe: { proteinGrams?: number | null; carbohydrateGrams?: number | null; fiberGrams?: number | null }, selectedTags: string[]): RecipeNutritionHighlight[] {
    const normalizedTags = new Set(selectedTags.map(tag => tag.trim().toLowerCase()));
    const highlights = [
        { tag: 'high protein', labelKey: 'recipe.proteinLabel', value: recipe.proteinGrams, unit: 'g' },
        { tag: 'low carb', labelKey: 'recipe.carbohydratesLabel', value: recipe.carbohydrateGrams, unit: 'g' },
        { tag: 'high fiber', labelKey: 'recipe.fiberLabel', value: recipe.fiberGrams, unit: 'g' }
    ];

    return highlights
        .filter(highlight => normalizedTags.has(highlight.tag))
        .map(highlight => ({ labelKey: highlight.labelKey, value: highlight.value ?? null, unit: highlight.unit }));
}

export function sortRecipeTaxonomyValues(values: string[], groups: RecipeTaxonomyGroup[]): string[] {
    const ranks = new Map<string, number>();
    groups.forEach((group, groupIndex) => {
        group.values.forEach((value, valueIndex) => {
            ranks.set(value.toLowerCase(), groupIndex * 1000 + valueIndex);
        });
    });

    return values
        .map((value, originalIndex) => ({ value, originalIndex, rank: ranks.get(value.trim().toLowerCase()) ?? Number.MAX_SAFE_INTEGER }))
        .sort((left, right) => left.rank - right.rank || left.originalIndex - right.originalIndex)
        .map(item => item.value);
}

// Maps each canonical (English) taxonomy value to its static i18n key under recipe.taxonomyValues.
const RECIPE_TAXONOMY_VALUE_KEYS: Record<string, string> = {
    'breakfast': 'breakfast', 'brunch': 'brunch', 'lunch': 'lunch', 'dinner': 'dinner', 'dessert': 'dessert', 'snack': 'snack',
    'side dish': 'sideDish', 'appetizer': 'appetizer', 'soup': 'soup', 'salad': 'salad', 'beverage': 'beverage',
    'danish': 'danish', 'turkish': 'turkish', 'italian': 'italian', 'asian-inspired': 'asianInspired', 'mexican': 'mexican',
    'mediterranean': 'mediterranean', 'american': 'american', 'french': 'french', 'greek': 'greek', 'indian': 'indian', 'middle eastern': 'middleEastern',
    'vegetarian': 'vegetarian', 'vegan': 'vegan', 'gluten free': 'glutenFree', 'dairy free': 'dairyFree',
    'winter': 'winter', 'spring': 'spring', 'summer': 'summer', 'autumn': 'autumn',
    'quick': 'quick', 'easy': 'easy', 'meal prep': 'mealPrep', 'make ahead': 'makeAhead', 'budget': 'budget',
    'family friendly': 'familyFriendly', 'kid friendly': 'kidFriendly', 'one pot': 'onePot', 'one pan': 'onePan', 'no cook': 'noCook', 'lunchbox': 'lunchbox', 'no-knead': 'noKnead',
    'healthy': 'healthy', 'high protein': 'highProtein', 'low carb': 'lowCarb', 'high fiber': 'highFiber',
    'christmas': 'christmas', 'easter': 'easter', 'traditional': 'traditional', 'comfort food': 'comfortFood', 'everyday': 'everyday', 'party': 'party',
    'alcohol': 'alcohol', 'caffeine': 'caffeine', 'spicy': 'spicy', 'sweet': 'sweet', 'savory': 'savory', 'sour': 'sour',
    'creamy': 'creamy', 'grilled': 'grilled', 'baked': 'baked', 'boiled': 'boiled', 'simmered': 'simmered', 'sauteed': 'sauteed', 'poached': 'poached', 'braised': 'braised', 'deep-fried': 'deepFried', 'pan-fried': 'panFried', 'roasted': 'roasted', 'steamed': 'steamed', 'slow-cooked': 'slowCooked', 'pressure-cooked': 'pressureCooked', 'marinated': 'marinated', 'fermented': 'fermented', 'raw / no-cook': 'rawNoCook'
};

export interface TranslateLike {
    instant(key: string): string;
}

export function getTaxonomyValueLabel(value: string, translateService: TranslateLike): string {
    const key = RECIPE_TAXONOMY_VALUE_KEYS[value.trim().toLowerCase()];
    if (!key) return value;
    const label = translateService.instant(`recipe.taxonomyValues.${key}`);
    return label && label !== `recipe.taxonomyValues.${key}` ? label : value;
}


export const RECIPE_CATEGORY_GROUPS: RecipeTaxonomyGroup[] = [
    {
        id: 'mealType',
        labelKey: 'recipe.taxonomyGroups.mealType',
        values: ['Breakfast', 'Brunch', 'Lunch', 'Dinner', 'Dessert', 'Snack', 'Side Dish', 'Appetizer', 'Soup', 'Salad', 'Beverage']
    },
    {
        id: 'cuisine',
        labelKey: 'recipe.taxonomyGroups.cuisine',
        values: ['Danish', 'Turkish', 'Italian', 'Asian-inspired', 'Mexican', 'Mediterranean', 'American', 'French', 'Greek', 'Indian', 'Middle Eastern']
    },
    {
        id: 'dietary',
        labelKey: 'recipe.taxonomyGroups.dietary',
        values: ['Vegetarian', 'Vegan', 'Gluten Free', 'Dairy Free']
    },
    {
        id: 'seasonal',
        labelKey: 'recipe.taxonomyGroups.seasonal',
        values: ['Winter', 'Spring', 'Summer', 'Autumn']
    },
];

export const RECIPE_TAG_GROUPS: RecipeTaxonomyGroup[] = [
    {
        id: 'practical',
        labelKey: 'recipe.taxonomyGroups.practical',
        values: ['Quick', 'Easy', 'Meal Prep', 'Make Ahead', 'Budget', 'Family Friendly', 'Kid Friendly', 'Lunchbox', 'One Pot', 'One Pan', 'No Cook', 'No-knead']
    },
    {
        id: 'nutrition',
        labelKey: 'recipe.taxonomyGroups.nutrition',
        values: ['Healthy', 'High Protein', 'Low Carb', 'High Fiber']
    },
    {
        id: 'occasion',
        labelKey: 'recipe.taxonomyGroups.occasion',
        values: ['Christmas', 'Easter', 'Traditional', 'Comfort Food', 'Everyday', 'Party']
    },
    {
        id: 'flavorCharacter',
        labelKey: 'recipe.taxonomyGroups.flavorCharacter',
        values: ['Alcohol', 'Caffeine', 'Spicy', 'Sweet', 'Savory', 'Sour', 'Creamy']
    },
    {
        id: 'cookingMethod',
        labelKey: 'recipe.taxonomyGroups.cookingMethod',
        values: ['Grilled', 'Baked', 'Boiled', 'Simmered', 'Sauteed', 'Poached', 'Braised', 'Deep-fried', 'Pan-fried', 'Roasted', 'Steamed', 'Slow-cooked', 'Pressure-cooked', 'Marinated', 'Fermented', 'Raw / no-cook']
    }
];