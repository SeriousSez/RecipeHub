import { Image } from "./image.interface";
import { Ingredient } from "./ingredient.interface";

export interface RecipeUpdate {
    oldTitle: string;
    title: string;
    creator: string;
    description: string;
    instructions: string;
    portions: string;
    preparationMinutes?: number | null;
    cookingMinutes?: number | null;
    chillingMinutes?: number | null;
    coolingMinutes?: number | null;
    restingMinutes?: number | null;
    shelfLifeDays?: number | null;
    canBeFrozen?: boolean | null;
    created: string;
    image: Image | null;
    ingredients: Ingredient[];
    categories?: string[];
    tags?: string[];
}