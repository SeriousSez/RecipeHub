import { Image } from "./image.interface";

export interface Ingredient{
    name: string;
    description: string;
    amount: number;
    amountType: string;
    created: string;
    image: Image | null;
}