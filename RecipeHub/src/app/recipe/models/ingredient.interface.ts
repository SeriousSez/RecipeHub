import { Image } from "./image.interface";

export interface Ingredient {
    name: string;
    displayName?: string;
    description: string;
    language?: string;
    amount: number;
    amountType: string;
    group?: string;
    sourceRecipeId?: string;
    sourceRecipeTitle?: string;
    sourceRecipeCreator?: string;
    created: string;
    image: Image | null;
}