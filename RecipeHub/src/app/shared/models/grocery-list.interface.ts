import { Ingredient } from "src/app/recipe/models/ingredient.interface";

export interface GroceryList {
    UserId: string;
    Ingredients: Ingredient[];
}