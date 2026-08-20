import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { map } from 'rxjs/operators';
import { ConfigService } from '../shared/utils/config.service';
import { Fridge } from './models/fridge';
import { Observable } from 'rxjs';
import { FridgeGrocery } from './models/Fridge-grocery';

@Injectable({ providedIn: 'root'})
export class FridgeService {
    baseUrl: string = '';
    private httpOptions = {
      headers: new HttpHeaders({
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${localStorage.getItem('authToken')}`
      })
    };

    public recipeList: Fridge[] = [];
  
    constructor(private http: HttpClient, private configService: ConfigService) {
      this.baseUrl = configService.getApiURI();
    }
  
    get(userId: string): Observable<Fridge[]> {  
      return this.http.get<Fridge[]>(this.baseUrl + `/fridge/get?userId=${userId}`, this.httpOptions)
        .pipe(map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    add(fridge: Fridge) {  
      return this.http.post(this.baseUrl + "/fridge/add", fridge, this.httpOptions)
        .pipe(map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    retire(id: string) {  
      return this.http.post(this.baseUrl + `/fridge/retire?fridgeId=${id}`, null, this.httpOptions)
        .pipe(map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    unRetire(id: string) {  
      return this.http.post(this.baseUrl + `/fridge/unretire?fridgeId=${id}`, null, this.httpOptions)
        .pipe(map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    getGroceries(id: string): Observable<FridgeGrocery[]> {
        return this.http.get<FridgeGrocery[]>(this.baseUrl + `/fridge/getgroceries?fridgeId=${id}`, this.httpOptions)
        .pipe(map(details => {
            return details;
        }, (error: any) => console.log(error, "fails")
        ));
    }
  
    addGrocery(grocery: FridgeGrocery) {  
      return this.http.post(this.baseUrl + "/fridge/addgrocery", grocery, this.httpOptions)
        .pipe(map(details => {
          return details;
        }, (error: any) => console.log(error, "fails")
      ));
    }
  
    removeGrocery(id: string) {  
      return this.http.post(this.baseUrl + `/fridge/removegrocery?id=${id}`, null, this.httpOptions)
        .pipe(map(users => {
          return users;
        }, (error: any) => console.log(error, "fails")
      ));
    }
}