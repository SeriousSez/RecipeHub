export interface RecipeTaxonomyGroup {
    id: string;
    labelKey: string;
    values: string[];
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

export const RECIPE_CATEGORY_GROUPS: RecipeTaxonomyGroup[] = [
    {
        id: 'mealType',
        labelKey: 'recipe.taxonomyGroups.mealType',
        values: ['Breakfast', 'Lunch', 'Dinner', 'Dessert', 'Snack', 'Side Dish', 'Appetizer', 'Soup', 'Salad', 'Beverage']
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
        values: ['Quick', 'Easy', 'Meal Prep', 'Make Ahead', 'Budget', 'Family Friendly', 'Kid Friendly', 'One Pot', 'One Pan', 'No Cook']
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
        id: 'features',
        labelKey: 'recipe.taxonomyGroups.features',
        values: ['Alcohol', 'Caffeine', 'Spicy', 'Sweet', 'Savory', "Creamy", 'Grilled', 'Baked', 'Fried', 'Roasted', 'Steamed']
    }
];