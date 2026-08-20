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

@Injectable({ providedIn: 'root'})
export class GroceryService extends BaseService {

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
    }

    getGroceryLists(userId: string, recipe: Recipe){
      return this.http.get<Ingredient[]>(this.baseUrl + `/grocery/getgrocerylists?userId=${userId}`, this.httpOptions)
        .pipe(map(groceryLists => {
          return groceryLists;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    createPlan(){
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
  
    createGroceryList(){
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

    getRecipeList(){
        return this.recipeList;
    }

    isInGroceries(recipe: Recipe){
        return this.recipeList.some(r => r.title == recipe.title && r.creator == recipe.creator);
    }

    getIngredientList(){
        return this.ingredientList;
    }

    toggleRecipeToList(recipe: Recipe){
        if(this.recipeList.some(r => r.title == recipe.title && r.creator == recipe.creator)){
            this.removeRecipeFromList(recipe);
        }else{
            this.recipeList.push(recipe);
            this.addIngredientsFromRecipeToList(recipe);
        }
    }

    addIngredientsToList(ingredients: Ingredient[]){
        ingredients.forEach(ingredient => {
            var listIngredient = this.ingredientList.find(i => i.name == ingredient.name);
            if(listIngredient && ingredient.amountType == listIngredient.amountType){
                this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount = ingredient.amount + listIngredient.amount;
            }else{
                this.ingredientList.push(ingredient);
            }
        });
    }

    addIngredientsFromRecipeToList(recipe: Recipe){
        recipe.ingredients.forEach(ingredient => {
            var listIngredient = this.ingredientList.find(i => i.name == ingredient.name);
            if(listIngredient && ingredient.amountType == listIngredient.amountType){
                this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount = ingredient.amount + listIngredient.amount;
            }else{
                this.ingredientList.push(ingredient);
            }
        });
    }

    handleIngredientsOnRecipeRemoval(recipe: Recipe){
        recipe.ingredients.forEach(ingredient => {
            var listIngredient = this.ingredientList.find(i => i.name == ingredient.name);
            if(listIngredient && ingredient.amountType == listIngredient.amountType){
                this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount = listIngredient.amount - ingredient.amount;

                if(this.ingredientList[this.ingredientList.indexOf(listIngredient)].amount == 0){
                    this.removeIngredientFromList(ingredient);
                }
            }
        });
    }

    clearRecipeList(){
        this.recipeList = [];
        this.ingredientList = [];
    }

    removeRecipeFromList(recipe: Recipe){
        var index = this.recipeList.indexOf(recipe, 0);
        if (index > -1) {
            this.recipeList.splice(index, 1);
            recipe.ingredients.forEach(ingredient => {
                this.removeIngredientFromList(ingredient);
            });
        }
    }

    removeIngredientFromList(ingredient: Ingredient){
        var index = this.ingredientList.indexOf(ingredient, 0);
        if (index > -1) {
            this.ingredientList.splice(index, 1);
        }
    }
}