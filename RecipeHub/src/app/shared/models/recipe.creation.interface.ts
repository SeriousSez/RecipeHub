import { ImageCreation } from "./image.creation.interface";
import { IngredientCreation } from "./ingredient.creation.interface";

export interface RecipeCreation {
    title: string;
    creator: string;
    description: string;
    language: string;
    instructions: string;
    portions: string;
    imageCaption: string;
    imageUrl: string;
    image: ImageCreation;
    ingredients: IngredientCreation[];
}