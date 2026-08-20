import { Recipe } from "src/app/recipe/models/recipe.interface";

export interface GroceryPlan {
    UserId: string;
    Recipes: Recipe[];
}