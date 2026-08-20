import { User } from "src/app/account/models/user.interface";
import { Ingredient } from "src/app/recipe/models/ingredient.interface";
import { Recipe } from "src/app/recipe/models/recipe.interface";

export interface Favorites {
    username: User;
    recipes: Recipe[];
    ingredients: Ingredient[];
}