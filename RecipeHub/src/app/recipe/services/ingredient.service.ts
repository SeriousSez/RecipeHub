import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';

import { ConfigService } from '../../shared/utils/config.service';

import { BaseService } from '../../shared/services/base.service';

import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { IngredientCreation } from 'src/app/shared/models/ingredient.creation.interface';
import { Ingredient } from '../models/ingredient.interface';
import { IngredientPhotoRecognitionResult } from '../models/ingredient-photo-recognition.interface';

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

  getIngredientsLite(language: string = 'English'): Observable<Ingredient[]> {
    return this.http.get<Ingredient[]>(this.baseUrl + `/ingredient/getalllite?language=${encodeURIComponent(language)}`, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  translate(names: string[], language: string, contexts?: Record<string, string>): Observable<Record<string, string>> {
    return this.http.post<Record<string, string>>(this.baseUrl + "/ingredient/translate", { names, language, contexts }, this.httpOptions);
  }

  updateTranslation(ingredientName: string, language: string, translatedName: string): Observable<void> {
    return this.http.post<void>(this.baseUrl + "/ingredient/updatetranslation", { ingredientName, language, translatedName }, this.httpOptions);
  }

  getTranslations(ingredientName: string): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(this.baseUrl + `/ingredient/translations?name=${encodeURIComponent(ingredientName)}`, this.httpOptions);
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

  update(ingredient: Ingredient): Observable<Ingredient> {
    return this.http.post<Ingredient>(this.baseUrl + "/ingredient/update", ingredient, this.httpOptions);
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

  recognizeIngredientsFromPhoto(images: { imageBase64: string, contentType: string }[], language: string): Observable<IngredientPhotoRecognitionResult> {
    return this.http.post<IngredientPhotoRecognitionResult>(this.baseUrl + "/ingredient/recognizephoto", { images, language }, this.httpOptions);
  }
}