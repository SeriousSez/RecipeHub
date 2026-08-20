import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';

import { ConfigService } from '../../shared/utils/config.service';

import {BaseService} from '../../shared/services/base.service';

import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { User } from 'src/app/account/models/user.interface';

@Injectable()

export class DashboardService extends BaseService {

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

  getUsers(): Observable<User[]> {  
    return this.http.get<User[]>(this.baseUrl + "/dashboard/getusers", this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
    ));
  }

  getRoles(): Observable<string[]> {
  return this.http.get<string[]>(this.baseUrl + "/dashboard/getRoles", this.httpOptions)
    .pipe(map(details => {
      return details;
    }, (error: any) => console.log(error, "fails")
  ));
  }

  addRole(user: User) {  
    return this.http.post<User>(this.baseUrl + "/dashboard/addRole", user, this.httpOptions)
      .pipe(map(details => {
        return details;
      }, (error: any) => console.log(error, "fails")
    ));
  }

  deleteUsers(users: User[]): Observable<User[]> {  
    return this.http.post<User[]>(this.baseUrl + "/dashboard/deleteUsers", users, this.httpOptions)
      .pipe(map(users => {
        return users;
      }, (error: any) => console.log(error, "fails")
    ));
  }
}