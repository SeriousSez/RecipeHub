export interface PantryItem {
    id: string;
    name: string;
    amount: number | null;
    amountType: string;
    expirationDate: string | null;
}