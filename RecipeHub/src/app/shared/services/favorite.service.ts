import { Injectable } from '@angular/core';
import { ConfigService } from '../utils/config.service';

import { BaseService } from "./base.service";
import { map } from 'rxjs/operators';

//import * as _ from 'lodash';

// Add the RxJS Observable operators we need in this app.
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { FavoriteRecipe } from '../models/favorite-recipe.interface';
import { FavoriteIngredient } from '../models/favorite-ingredient.interface';
import { Favorites } from '../models/favorites.interface';
import { Recipe } from 'src/app/recipe/models/recipe.interface';

@Injectable()

export class FavoriteService extends BaseService {

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

  get(username: string){
    return this.http.get<Favorites>(this.baseUrl + `/favorite/get?username=${username}`, this.httpOptions)
      .pipe(map(favorites => {
        return favorites;
      }, (error: any) => console.log(error, "fails")
    ));
  }

  isFavored(username: string, recipe: Recipe){
    return this.http.get<boolean>(this.baseUrl + `/favorite/isfavored?username=${username}&title=${recipe.title}&creator=${recipe.creator}`, this.httpOptions)
      .pipe(map(favorites => {
        return favorites;
      }, (error: any) => console.log(error, "fails")
    ));
  }

  favoriteRecipe(favoriteRecipe: FavoriteRecipe){
    return this.http.post<FavoriteRecipe>(this.baseUrl + "/favorite/recipe", favoriteRecipe, this.httpOptions)
      .pipe(map(result => {
        return result;
      }, (error: any) => console.log(error, "fails")
    ));
  }

  favoriteIngredient(favoriteIngredient: FavoriteIngredient){
    return this.http.post<FavoriteIngredient>(this.baseUrl + "/favorite/ingredient", favoriteIngredient, this.httpOptions)
      .pipe(map(result => {
        return result;
      }, (error: any) => console.log(error, "fails")
    ));
  }
}