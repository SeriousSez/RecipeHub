import { ImageCreation } from "./image.creation.interface";

export interface IngredientCreation {
    name: string;
    description: string;
    language?: string;
    amount: number;
    amountType: string;
    imageCaption: string;
    image: ImageCreation | null;
    created: string;
}