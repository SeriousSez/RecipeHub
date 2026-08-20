import { Image } from "./image.interface";
import { Ingredient } from "./ingredient.interface";

export interface RecipeUpdate {
    oldTitle: string;
    title: string;
    creator: string;
    description: string;
    instructions: string;
    portions: string;
    created: string;
    image: Image | null;
    ingredients: Ingredient[];
}