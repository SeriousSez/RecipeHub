import { Image } from "./image.interface";

export interface Ingredient {
    name: string;
    description: string;
    language?: string;
    amount: number;
    amountType: string;
    group?: string;
    created: string;
    image: Image | null;
}