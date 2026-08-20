import { Image } from "../../recipe/models/image.interface";

export interface FridgeGrocery {
    id: string;
    name: string;
    description: string;
    amount: number;
    amountType: string;
    purchaseDate: string;
    expirationDate: string;
    image: Image | null;
    fridgeId: string;
}