import { Recipe } from '../recipe/models/recipe.interface';

export interface FoodPlanEntry {
    id: string;
    userId: string;
    recipeId: string;
    plannedDate: string;
    occurrenceDate: string;
    mealSlot: string;
    servings: number;
    notes: string | null;
    repeatWeekly: boolean;
    repeatUntil: string | null;
    position: number;
    recipe: Recipe | null;
}

export interface FoodPlanEntryRequest {
    id?: string;
    userId: string;
    recipeId: string;
    plannedDate: string;
    mealSlot: string;
    servings: number;
    notes?: string | null;
    repeatWeekly: boolean;
    repeatUntil?: string | null;
    position: number;
}

export interface FoodPlanNutritionTotals {
    calories: number;
    proteinGrams: number;
    carbohydrateGrams: number;
    fatGrams: number;
    hasData: boolean;
}