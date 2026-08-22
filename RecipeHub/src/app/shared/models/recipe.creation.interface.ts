import { ImageCreation } from "./image.creation.interface";
import { IngredientCreation } from "./ingredient.creation.interface";

export interface RecipeCreation {
    title: string;
    creator: string;
    description: string;
    language: string;
    instructions: string;
    portions: string;
    preparationMinutes?: number | null;
    cookingMinutes?: number | null;
    chillingMinutes?: number | null;
    coolingMinutes?: number | null;
    restingMinutes?: number | null;
    shelfLifeDays?: number | null;
    canBeFrozen?: boolean | null;
    imageCaption: string;
    imageUrl: string;
    image: ImageCreation;
    ingredients: IngredientCreation[];
    categories?: string[];
    tags?: string[];
}