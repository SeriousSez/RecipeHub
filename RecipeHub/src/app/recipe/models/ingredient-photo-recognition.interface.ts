export interface RecognizedPantryItem {
    name: string;
    amount: number | null;
    amountType: string | null;
    expirationDate: string | null;
}

export interface IngredientPhotoRecognitionResult {
    items: RecognizedPantryItem[];
    provider?: string;
    errorCode?: string;
}
