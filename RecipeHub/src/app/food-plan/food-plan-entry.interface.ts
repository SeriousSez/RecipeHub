import { Recipe } from '../recipe/models/recipe.interface';

export interface FoodPlanEntry {
    id: string;
    userId: string;
    recipeId: string;
    plannedDate: string;
    occurrenceDate: string;
    mealSlot: string;
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
    notes?: string | null;
    repeatWeekly: boolean;
    repeatUntil?: string | null;
    position: number;
}