export interface GroceryOfferSearchRequest {
    ingredientNames: string[];
    ingredientCategories?: Record<string, GroceryOfferCategory>;
    shoppingPreference?: GroceryShoppingPreference;
    countryCode: 'DK' | 'EE' | 'TR';
    forceRefresh?: boolean;
    latitude: number;
    longitude: number;
    radiusKm: number;
}

export type GroceryOfferCategory = 'auto' | 'produce' | 'dairy' | 'meat' | 'bakery' | 'pantry' | 'candy' | 'chocolate' | 'beverages';
export type GroceryShoppingPreference = 'balanced' | 'budget' | 'deals' | 'organic' | 'premium';

export interface GroceryOfferSearchResponse {
    stores: GroceryNearbyStore[];
    offers: GroceryIngredientOffer[];
    unmatchedIngredients: string[];
    generatedAtUtc: string;
}

export interface GroceryNearbyStore {
    id: string;
    name: string;
    chainName: string;
    address: string;
    city: string;
    postalCode?: string;
    latitude: number;
    longitude: number;
    distanceKm: number;
}

export interface GroceryIngredientOffer {
    ingredientName: string;
    productName: string;
    productId: string;
    offerId: string;
    productUrl?: string;
    chainName: string;
    storeName: string;
    storeAddress: string;
    storeCity: string;
    storePostalCode?: string;
    distanceKm: number;
    price: number;
    originalPrice?: number;
    discountPercentage?: number;
    currency: string;
    priceKind: 'campaign' | 'regular';
    validFrom?: string;
    validTo?: string;
    imageUrl?: string;
}

export interface GroceryOfferGroup {
    ingredientName: string;
    offers: GroceryIngredientOffer[];
}