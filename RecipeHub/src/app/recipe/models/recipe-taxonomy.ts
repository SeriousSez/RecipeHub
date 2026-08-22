export interface RecipeTaxonomyGroup {
    id: string;
    labelKey: string;
    values: string[];
}

export const RECIPE_CATEGORY_GROUPS: RecipeTaxonomyGroup[] = [
    {
        id: 'mealType',
        labelKey: 'recipe.taxonomyGroups.mealType',
        values: ['Breakfast', 'Lunch', 'Dinner', 'Dessert', 'Snack']
    },
    {
        id: 'cuisine',
        labelKey: 'recipe.taxonomyGroups.cuisine',
        values: ['Danish', 'Turkish', 'Italian', 'Asian-inspired', 'Mexican', 'Mediterranean']
    },
    {
        id: 'dietary',
        labelKey: 'recipe.taxonomyGroups.dietary',
        values: ['Vegetarian', 'Vegan', 'Gluten Free', 'Dairy Free']
    }
];

export const RECIPE_TAG_GROUPS: RecipeTaxonomyGroup[] = [
    {
        id: 'practical',
        labelKey: 'recipe.taxonomyGroups.practical',
        values: ['Quick', 'Easy', 'Meal Prep', 'Budget', 'Family Friendly', 'Kid Friendly', 'One Pot']
    },
    {
        id: 'nutrition',
        labelKey: 'recipe.taxonomyGroups.nutrition',
        values: ['Healthy', 'High Protein', 'Low Carb']
    },
    {
        id: 'occasion',
        labelKey: 'recipe.taxonomyGroups.occasion',
        values: ['Christmas', 'Traditional', 'Comfort Food', 'Everyday', 'Party']
    }
];