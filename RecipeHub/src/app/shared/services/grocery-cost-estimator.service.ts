import { Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { map, switchMap } from 'rxjs/operators';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { GroceryCostEstimate, GroceryIngredientOffer, GroceryOfferSearchResponse } from '../models/grocery-offer-search.interface';
import { GroceryService } from './grocery.service';

@Injectable({ providedIn: 'root' })
export class GroceryCostEstimatorService {
    constructor(private groceryService: GroceryService) { }

    public estimateRecipes(recipes: Array<{ recipe: Recipe; servings?: number }>): Observable<GroceryCostEstimate | null> {
        const ingredients = this.getUniqueIngredients(recipes);
        if (ingredients.length === 0) return throwError(() => new Error('No ingredients available.'));

        if (typeof navigator === 'undefined' || !navigator.geolocation) {
            return throwError(() => new Error('Location is unavailable.'));
        }

        return new Observable<GeolocationPosition>(subscriber => {
            navigator.geolocation.getCurrentPosition(
                position => { subscriber.next(position); subscriber.complete(); },
                error => subscriber.error(error),
                { enableHighAccuracy: false, timeout: 10000, maximumAge: 0 }
            );
        }).pipe(
            map(position => ({ position, ingredients })),
            map(({ position, ingredients }) => this.groceryService.findNearbyOffers({
                ingredientNames: ingredients.map(ingredient => ingredient.name),
                countryCode: this.getCountryCode(position.coords.latitude, position.coords.longitude),
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                radiusKm: 10,
                shoppingPreference: 'balanced'
            })),
            switchMap(request => request.pipe(map(response => this.toEstimate(response))))
        );
    }

    private getUniqueIngredients(recipes: Array<{ recipe: Recipe; servings?: number }>): Ingredient[] {
        const ingredients = new Map<string, Ingredient>();
        recipes.forEach(item => (item.recipe.ingredients ?? []).forEach(ingredient => {
            const key = ingredient.name.trim().toLowerCase();
            if (key && !ingredients.has(key)) ingredients.set(key, ingredient);
        }));
        return Array.from(ingredients.values());
    }

    private toEstimate(response: GroceryOfferSearchResponse): GroceryCostEstimate | null {
        const groups = new Map<string, GroceryIngredientOffer[]>();
        response.offers.forEach(offer => {
            const key = offer.ingredientName.toLowerCase();
            groups.set(key, [...(groups.get(key) ?? []), offer]);
        });
        const offers = Array.from(groups.values()).map(items => items.find(item => item.price > 0 && !!item.currency?.trim())).filter((offer): offer is GroceryIngredientOffer => !!offer);
        const currencies = new Set(offers.map(offer => offer.currency.trim().toUpperCase()));
        if (offers.length === 0 || currencies.size !== 1) return null;

        const total = offers.reduce((sum, offer) => sum + offer.price, 0);
        const originalTotal = offers.reduce((sum, offer) => sum + (offer.originalPrice && offer.originalPrice > offer.price ? offer.originalPrice : offer.price), 0);
        return {
            currency: offers[0].currency,
            total,
            originalTotal,
            savings: originalTotal - total,
            coveredCount: offers.length,
            totalCount: groups.size + response.unmatchedIngredients.length,
            usesOldPrices: false
        };
    }

    private getCountryCode(latitude: number, longitude: number): 'DK' | 'EE' | 'TR' | 'NO' | 'SE' {
        if (latitude >= 54.5 && latitude <= 57.8 && longitude >= 8.0 && longitude <= 15.2) return 'DK';
        if (latitude >= 57.5 && latitude <= 59.8 && longitude >= 21.5 && longitude <= 28.3) return 'EE';
        if (latitude >= 35.8 && latitude <= 42.2 && longitude >= 25.5 && longitude <= 45.1) return 'TR';
        if (latitude >= 58.0 && latitude <= 71.5 && longitude >= 4.0 && longitude <= 31.5) return 'NO';
        if (latitude >= 55.0 && latitude <= 69.5 && longitude >= 10.5 && longitude <= 24.5) return 'SE';
        return 'DK';
    }
}