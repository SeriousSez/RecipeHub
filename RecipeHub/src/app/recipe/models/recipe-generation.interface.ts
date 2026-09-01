export interface RecipeGenerationRequest {
    prompt?: string;
    pantryItems?: string[];
    language: string;
    portions?: string;
}

export interface GeneratedRecipeIngredient {
    name: string;
    description: string;
    amount: number;
    amountType: string;
    group: string;
    groupOrder: number;
    ingredientOrder: number;
}

export interface GeneratedRecipe {
    title: string;
    description: string;
    instructions: string;
    portions: string;
    preparationMinutes?: number | null;
    cookingMinutes?: number | null;
    proofingMinutes?: number | null;
    chillingMinutes?: number | null;
    coolingMinutes?: number | null;
    restingMinutes?: number | null;
    shelfLifeDays?: number | null;
    canBeFrozen?: boolean | null;
    calories?: number | null;
    proteinGrams?: number | null;
    carbohydrateGrams?: number | null;
    fatGrams?: number | null;
    fiberGrams?: number | null;
    sugarGrams?: number | null;
    sodiumMilligrams?: number | null;
    categories: string[];
    tags: string[];
    ingredients: GeneratedRecipeIngredient[];
    provider?: string;
    errorCode?: string;
}
