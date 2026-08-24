import { ImageCreation } from "./image.creation.interface";

export interface IngredientCreation {
    groupId?: string;
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