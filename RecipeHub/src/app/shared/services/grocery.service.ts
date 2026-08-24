import { Injectable } from '@angular/core';
import { ConfigService } from '../utils/config.service';

import { BaseService } from "./base.service";

//import * as _ from 'lodash';

// Add the RxJS Observable operators we need in this app.
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { GroceryPlan } from '../models/grocery-plan.interface';
import { map } from 'rxjs/operators';
import { UserService } from './user.service';
import { GroceryList } from '../models/grocery-list.interface';
import { GroceryOfferSearchRequest, GroceryOfferSearchResponse } from '../models/grocery-offer-search.interface';

@Injectable({ providedIn: 'root' })
export class GroceryService extends BaseService {
    private readonly recipeListStorageKey = 'recipehub-grocery-recipes';
    private readonly ingredientListStorageKey = 'recipehub-grocery-ingredients';

    baseUrl: string = '';
    private httpOptions = {
        headers: new HttpHeaders({
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${localStorage.getItem('authToken')}`
        })
    };

    private loggedIn = false;

    public recipeList: Recipe[] = [];
    public ingredientList: Ingredient[] = [];

    constructor(private http: HttpClient, private configService: ConfigService, private userService: UserService) {
        super();
        this.loggedIn = !!localStorage.getItem('authToken');
        this.baseUrl = configService.getApiURI();
        this.loadPersistedState();
    }

    private loadPersistedState() {
        if (typeof localStorage === 'undefined') {
            return;
        }

        try {
            const savedRecipes = localStorage.getItem(this.recipeListStorageKey);
            const savedIngredients = localStorage.getItem(this.ingredientListStorageKey);

            this.recipeList = savedRecipes ? JSON.parse(savedRecipes) as Recipe[] : [];
            this.ingredientList = savedIngredients ? JSON.parse(savedIngredients) as Ingredient[] : [];
            this.persistCompactRecipeState();
        } catch {
            this.recipeList = [];
            this.ingredientList = [];
        }
    }

    private persistCompactRecipeState() {
        if (typeof localStorage === 'undefined') {
            return;
        }

        const compactRecipes = this.recipeList.map(recipe => ({
            id: recipe.id,
            title: recipe.title,
            creator: recipe.creator
        }));

        try {
            localStorage.setItem(this.recipeListStorageKey, JSON.stringify(compactRecipes));
        } catch {
            localStorage.removeItem(this.recipeListStorageKey);
        }
    }

    private persistState() {
        if (typeof localStorage === 'undefined') {
            return;
        }

        this.persistCompactRecipeState();

        try {
            localStorage.setItem(this.ingredientListStorageKey, JSON.stringify(this.ingredientList));
        } catch {
            const compactIngredients = this.ingredientList.map(ingredient => ({
                ...ingredient,
                description: '',
                image: null
            }));
            try {
                localStorage.setItem(this.ingredientListStorageKey, JSON.stringify(compactIngredients));
            } catch {
                localStorage.removeItem(this.ingredientListStorageKey);
            }
        }
    }

    getGroceryLists(userId: string, recipe: Recipe) {
        return this.http.get<Ingredient[]>(this.baseUrl + `/grocery/getgrocerylists?userId=${userId}`, this.httpOptions)
            .pipe(map(groceryLists => {
                return groceryLists;
            }, (error: any) => console.log(error, "fails")
            ));
    }

    createPlan() {
        var model: GroceryPlan = {
            UserId: this.userService.getUserId(),
            Recipes: this.recipeList
        }

        return this.http.post<GroceryPlan>(this.baseUrl + "/grocery/createplan", model, this.httpOptions)
            .pipe(map(result => {
                return result;
            }, (error: any) => console.log(error, "fails")
            ));
    }

    createGroceryList() {
        var model: GroceryList = {
            UserId: this.userService.getUserId(),
            Ingredients: this.ingredientList
        }

        return this.http.post<GroceryList>(this.baseUrl + "/grocery/creategrocerylist", model, this.httpOptions)
            .pipe(map(result => {
                return result;
            }, (error: any) => console.log(error, "fails")
            ));
    }

    findNearbyOffers(model: GroceryOfferSearchRequest) {
        return this.http.post<GroceryOfferSearchResponse>(this.baseUrl + "/grocery/nearbyoffers", model, this.httpOptions);
    }

    saveCategoryFeedback(ingredientName: string, category: string, rating: 1 | -1) {
        return this.http.post(this.baseUrl + "/grocery/categoryfeedback", { ingredientName, category, rating }, this.httpOptions);
    }

    getRecipeList() {
        return this.recipeList;
    }

    isInGroceries(recipe: Recipe) {
        return this.recipeList.some(r => r.title == recipe.title && r.creator == recipe.creator);
    }

    getIngredientList() {
        return this.ingredientList;
    }

    private normalizeIngredientName(value: string | null | undefined): string {
        return (value ?? '').trim().toLowerCase().replace(/\s+/g, ' ');
    }

    private getIngredientIdentityKey(ingredient: Partial<Ingredient>): string {
        const recipeKey = ingredient.sourceRecipeId ?? ingredient.sourceRecipeTitle ?? ingredient.sourceRecipeCreator ?? 'manual';
        const nameKey = this.normalizeIngredientName(ingredient.name);
        const unitKey = (ingredient.amountType ?? '').trim().toLowerCase();
        return `${recipeKey}|${nameKey}|${unitKey}`;
    }

    toggleRecipeToList(recipe: Recipe) {
        if (this.recipeList.some(r => r.title == recipe.title && r.creator == recipe.creator)) {
            this.removeRecipeFromList(recipe);
        } else {
            this.recipeList.push(recipe);
            this.addIngredientsFromRecipeToList(recipe);
        }

        this.persistState();
    }

    addIngredientsToList(ingredients: Ingredient[]) {
        ingredients.forEach(ingredient => {
            const targetKey = this.getIngredientIdentityKey(ingredient);
            const listIngredient = this.ingredientList.find(i => this.getIngredientIdentityKey(i) === targetKey);

            if (listIngredient) {
                this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount = ingredient.amount + listIngredient.amount;
            } else {
                this.ingredientList.push(ingredient);
            }
        });

        this.persistState();
    }

    updateIngredientInList(ingredient: Ingredient, updatedIngredient: Ingredient) {
        const index = this.ingredientList.indexOf(ingredient);
        if (index < 0) {
            return ingredient;
        }

        Object.assign(ingredient, updatedIngredient);
        this.persistState();
        return ingredient;
    }

    addIngredientsFromRecipeToList(recipe: Recipe) {
        recipe.ingredients.forEach(ingredient => {
            const recipeIngredient: Ingredient = {
                ...ingredient,
                sourceRecipeId: recipe.id,
                sourceRecipeTitle: recipe.title,
                sourceRecipeCreator: recipe.creator
            };

            const listIngredient = this.ingredientList.find(item =>
                this.normalizeIngredientName(item.name) === this.normalizeIngredientName(ingredient.name) &&
                (item.amountType ?? '').toLowerCase() === (ingredient.amountType ?? '').toLowerCase() &&
                (item.sourceRecipeId ?? item.sourceRecipeTitle ?? item.sourceRecipeCreator ?? 'manual') ===
                (recipe.id ?? recipe.title ?? recipe.creator ?? 'manual'));

            if (listIngredient) {
                this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount = ingredient.amount + listIngredient.amount;
                return;
            }

            this.ingredientList.push(recipeIngredient);
        });

        this.persistState();
    }

    handleIngredientsOnRecipeRemoval(recipe: Recipe) {
        recipe.ingredients.forEach(ingredient => {
            const listIngredient = this.ingredientList.find(i =>
                this.normalizeIngredientName(i.name) === this.normalizeIngredientName(ingredient.name) &&
                (i.amountType ?? '').toLowerCase() === (ingredient.amountType ?? '').toLowerCase() &&
                (i.sourceRecipeId ?? i.sourceRecipeTitle ?? i.sourceRecipeCreator ?? 'manual') ===
                (recipe.id ?? recipe.title ?? recipe.creator ?? 'manual'));

            if (listIngredient) {
                const index = this.ingredientList.indexOf(listIngredient);
                this.ingredientList[index].amount = listIngredient.amount - ingredient.amount;

                if (this.ingredientList[index].amount == 0) {
                    this.ingredientList.splice(index, 1);
                }
            }
        });
    }

    clearRecipeList() {
        this.recipeList = [];
        this.ingredientList = [];
        this.persistState();
    }

    removeRecipeFromList(recipe: Recipe) {
        const index = this.recipeList.findIndex(item => item.title === recipe.title && item.creator === recipe.creator);
        if (index > -1) {
            this.recipeList.splice(index, 1);
            this.ingredientList = this.ingredientList.filter(item => {
                const itemRecipeKey = item.sourceRecipeId ?? item.sourceRecipeTitle ?? item.sourceRecipeCreator ?? 'manual';
                const recipeKey = recipe.id ?? recipe.title ?? recipe.creator ?? 'manual';
                return itemRecipeKey !== recipeKey;
            });
        }

        this.persistState();
    }

    removeIngredientFromList(ingredient: Ingredient) {
        var index = this.ingredientList.indexOf(ingredient, 0);
        if (index > -1) {
            this.ingredientList.splice(index, 1);
        }

        this.persistState();
    }
}