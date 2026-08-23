import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';

import { ConfigService } from '../../shared/utils/config.service';

import { BaseService } from '../../shared/services/base.service';

import { Observable, timer } from 'rxjs';
import { map, retry } from 'rxjs/operators';
import { Recipe } from '../models/recipe.interface';
import { RecipeCreation } from 'src/app/shared/models/recipe.creation.interface';
import { Ingredient } from '../models/ingredient.interface';
import { ingredientModal } from 'src/app/shared/modals/ingredient/ingredient.modal';
import { RecipeUpdate } from '../models/recipe-update.interface';
import { RecipePagedQuery, RecipePagedResult } from '../models/recipe-paged.interface';
import { NutritionEstimate, NutritionEstimateIngredient } from '../models/nutrition-estimate.interface';
import { RecipeEngagement } from '../models/recipe-engagement.interface';

@Injectable()

export class RecipeService extends BaseService {

  baseUrl: string = '';
  private get httpOptions() {
    const authToken = localStorage.getItem('authToken');
    return authToken
      ? { headers: new HttpHeaders({ 'Authorization': `Bearer ${authToken}` }) }
      : {};
  }

  constructor(private http: HttpClient, private configService: ConfigService) {
    super();
    this.baseUrl = configService.getApiURI();
  }

  getRecipe(title: string, creator: string): Observable<Recipe> {
    return this.http.get<Recipe>(this.baseUrl + `/recipe/get?title=${encodeURIComponent(title)}&creator=${encodeURIComponent(creator)}`, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getRecipeById(id: string): Observable<Recipe> {
    return this.http.get<Recipe>(this.baseUrl + `/recipe/getbyid/${encodeURIComponent(id)}`, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getRecipeTranslation(id: string, language: string): Observable<Recipe> {
    return this.http.get<Recipe>(this.baseUrl + `/recipe/getbyid/${encodeURIComponent(id)}/translation?language=${encodeURIComponent(language)}`, this.httpOptions);
  }

  getEngagement(recipeId: string): Observable<RecipeEngagement> {
    return this.http.get<RecipeEngagement>(this.baseUrl + `/recipe/engagement/${encodeURIComponent(recipeId)}`, this.httpOptions);
  }

  saveEngagement(recipeId: string, rating: number | null): Observable<RecipeEngagement> {
    return this.http.post<RecipeEngagement>(this.baseUrl + '/recipe/engagement', { recipeId, rating }, this.httpOptions);
  }

  getRecipes(): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(this.baseUrl + "/recipe/getall", this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getRecipesWithIngredients(): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(this.baseUrl + "/recipe/getallwithingredients", this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getRecipesPaged(query: RecipePagedQuery): Observable<RecipePagedResult> {
    const params = new URLSearchParams();
    params.set('page', String(query.page));
    params.set('pageSize', String(query.pageSize));
    if (query.search) params.set('search', query.search);
    if (query.category) params.set('category', query.category);
    if (query.tag) params.set('tag', query.tag);
    if (query.sortBy) params.set('sortBy', query.sortBy);
    if (query.ascending !== undefined) params.set('ascending', String(query.ascending));
    if (query.creator) params.set('creator', query.creator);
    if (query.favoriteIds) params.set('favoriteIds', query.favoriteIds);
    if (query.language) params.set('language', query.language);

    return this.http.get<RecipePagedResult>(this.baseUrl + `/recipe/paged?${params.toString()}`, this.httpOptions)
      .pipe(
        retry({ count: 4, delay: (_, attempt) => timer(1000 * Math.pow(2, attempt - 1)) }),
        map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
        ));
  }

  getRecipesByCreator(creator: string): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(this.baseUrl + "/recipe/getallbycreator?creator=" + creator, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  create(recipe: RecipeCreation): Observable<Recipe> {
    return this.http.post<Recipe>(this.baseUrl + "/recipe/create", recipe, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  estimateNutrition(ingredients: NutritionEstimateIngredient[], portions: number, instructions: string): Observable<NutritionEstimate> {
    return this.http.post<NutritionEstimate>(this.baseUrl + "/recipe/estimate-nutrition", { ingredients, portions, instructions }, this.httpOptions);
  }

  addIngredients(recipe: Recipe, ingredients: Ingredient[]): Observable<Ingredient[]> {
    return this.http.post<Ingredient[]>(this.baseUrl + `/recipe/addingredients?title=${recipe.title}&creator=${recipe.creator}`, ingredients, this.httpOptions)
      .pipe(map(ingredients => {
        return ingredients;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  update(recipe: RecipeUpdate): Observable<RecipeUpdate> {
    return this.http.post<RecipeUpdate>(this.baseUrl + "/recipe/update", recipe, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  deleteRecipes(recipes: string[]): Observable<Recipe[]> {
    return this.http.post<Recipe[]>(this.baseUrl + "/recipe/delete", recipes, this.httpOptions)
      .pipe(map(recipes => {
        return recipes;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  deleteRecipeIngredient(ingredients: Ingredient[]): Observable<Ingredient[]> {
    return this.http.post<Ingredient[]>(this.baseUrl + "/recipe/deleterecipeingredient", ingredients, this.httpOptions)
      .pipe(map(ingredients => {
        return ingredients;
      }, (error: any) => console.log(error, "fails")
      ));
  }
}