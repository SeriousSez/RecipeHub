import { Recipe } from "src/app/recipe/models/recipe.interface";

export interface FavoriteRecipe {
    username: string;
    recipe: Recipe;
}