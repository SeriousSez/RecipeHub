import { ImageCreation } from "./image.creation.interface";

export interface IngredientCreation {
    groupId?: string;
    groupOrder?: number;
    ingredientOrder?: number;
    name: string;
    description: string;
    language?: string;
    amount: number;
    amountType: string;
    group?: string;
    imageCaption: string;
    image: ImageCreation | null;
    created: string;
}