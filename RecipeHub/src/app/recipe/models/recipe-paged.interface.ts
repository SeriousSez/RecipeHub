import { Recipe } from "./recipe.interface";

export interface RecipePagedResult {
    items: Recipe[];
    totalCount: number;
    page: number;
    pageSize: number;
    availableCategories: string[];
    availableTags: string[];
}

export interface RecipePagedQuery {
    page: number;
    pageSize: number;
    search?: string;
    category?: string;
    tag?: string;
    sortBy?: string;
    ascending?: boolean;
    creator?: string;
    favoriteIds?: string;
}
