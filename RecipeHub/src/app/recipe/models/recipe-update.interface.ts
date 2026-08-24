import { Image } from "./image.interface";
import { Ingredient } from "./ingredient.interface";

export interface RecipeUpdate {
    oldTitle: string;
    title: string;
    creator: string;
    description: string;
    instructions: string;
    language: string;
    portions: string;
    preparationMinutes?: number | null;
    cookingMinutes?: number | null;
    chillingMinutes?: number | null;
    coolingMinutes?: number | null;
    restingMinutes?: number | null;
    shelfLifeDays?: number | null;
    canBeFrozen?: boolean | null;
    calories?: number | null;
    proteinGrams?: number | null;
    carbohydrateGrams?: number | null;
    fatGrams?: number | null;
    fiberGrams?: number | null;
    sugarGrams?: number | null;
    sodiumMilligrams?: number | null;
    created: string;
    image: Image | null;
    ingredients: Ingredient[];
    categories?: string[];
    tags?: string[];
}