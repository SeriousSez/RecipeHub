import { Image } from "./image.interface";

export interface Ingredient {
    id?: string;
    recipeIngredientId?: string;
    groupId?: string;
    groupOrder?: number;
    ingredientOrder?: number;
    name: string;
    displayName?: string;
    description: string;
    language?: string;
    canonicalName?: string;
    category?: string;
    subcategory?: string;
    openFoodFactsId?: string;
    amount: number;
    amountType: string;
    group?: string;
    sourceRecipeId?: string;
    sourceRecipeTitle?: string;
    sourceRecipeCreator?: string;
    created: string;
    image: Image | null;
}