import { Ingredient } from "src/app/recipe/models/ingredient.interface";

export interface FavoriteIngredient {
    username: string;
    recipe: Ingredient;
}