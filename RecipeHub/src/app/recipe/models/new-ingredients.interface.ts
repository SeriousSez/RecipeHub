import { Ingredient } from "./ingredient.interface";
import { Recipe } from "./recipe.interface";

export interface NewIngredients{
    Recipe: Recipe;
    NewIngredients: Ingredient[];
}