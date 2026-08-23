export interface NutritionEstimateIngredient {
    name: string;
    amount: number;
    amountType: string;
}

export interface NutritionEstimate {
    calories: number;
    proteinGrams: number;
    carbohydrateGrams: number;
    fatGrams: number;
    fiberGrams: number;
    sugarGrams: number;
    sodiumMilligrams: number;
    estimatedIngredientCount: number;
    provider: string;
    errorCode: string;
    coveragePercent: number;
    unmatchedIngredients: string[];
}