import { FridgeGrocery } from "./Fridge-grocery";

export interface Fridge {
    id: string;
    name: string;
    amountOfGroceries: number;
    purchased: string;
    retired: string;
    groceries: FridgeGrocery[];
}