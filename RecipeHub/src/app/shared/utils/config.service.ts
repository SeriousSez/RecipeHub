import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
 
@Injectable()
export class ConfigService {
     
    _apiURI : string;
 
    constructor() {
        this._apiURI = environment.baseUrl;
     }
 
     getApiURI() {
         return this._apiURI;
     }    
}