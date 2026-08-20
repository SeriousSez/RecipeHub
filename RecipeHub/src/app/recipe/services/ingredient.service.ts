import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';

import { ConfigService } from '../../shared/utils/config.service';

import { BaseService } from '../../shared/services/base.service';

import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { IngredientCreation } from 'src/app/shared/models/ingredient.creation.interface';
import { Ingredient } from '../models/ingredient.interface';

@Injectable()

export class IngredientService extends BaseService {

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

  getIngredients(): Observable<Ingredient[]> {
    return this.http.get<Ingredient[]>(this.baseUrl + "/ingredient/getall", this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getIngredientsLite(): Observable<Ingredient[]> {
    return this.http.get<Ingredient[]>(this.baseUrl + "/ingredient/getalllite", this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getIngredientByName(name: string): Observable<Ingredient> {
    const encodedName = encodeURIComponent(name ?? '');
    return this.http.get<Ingredient>(this.baseUrl + `/ingredient/getbyname?name=${encodedName}`, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  create(ingredient: IngredientCreation): Observable<Ingredient> {
    return this.http.post<Ingredient>(this.baseUrl + "/ingredient/create", ingredient, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  deleteIngredients(ingredients: Ingredient[]): Observable<Ingredient[]> {
    return this.http.post<Ingredient[]>(this.baseUrl + "/ingredient/delete", ingredients, this.httpOptions)
      .pipe(map(ingredients => {
        return ingredients;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  regenerateImage(name: string): Observable<any> {
    const encodedName = encodeURIComponent(name ?? '');
    return this.http.post<any>(this.baseUrl + `/ingredient/regenerateimage?name=${encodedName}`, {}, this.httpOptions)
      .pipe(map(result => {
        return result;
      }, (error: any) => console.log(error, "fails")
      ));
  }
}