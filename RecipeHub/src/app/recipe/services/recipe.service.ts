import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';

import { ConfigService } from '../../shared/utils/config.service';

import { BaseService } from '../../shared/services/base.service';

import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { Recipe } from '../models/recipe.interface';
import { RecipeCreation } from 'src/app/shared/models/recipe.creation.interface';
import { Ingredient } from '../models/ingredient.interface';
import { ingredientModal } from 'src/app/shared/modals/ingredient/ingredient.modal';
import { RecipeUpdate } from '../models/recipe-update.interface';

@Injectable()

export class RecipeService extends BaseService {

  baseUrl: string = '';
  private httpOptions = {
    headers: new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${localStorage.getItem('authToken')}`
    })
  };

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

  getRecipes(): Observable<Recipe[]> {
    return this.http.get<Recipe[]>(this.baseUrl + "/recipe/getall", this.httpOptions)
      .pipe(map(details => {
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