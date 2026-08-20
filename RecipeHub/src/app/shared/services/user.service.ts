import { Injectable } from '@angular/core';
import { UserRegistration } from '../models/user.registration.interface';
import { ConfigService } from '../utils/config.service';

import { BaseService } from "./base.service";
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';

//import * as _ from 'lodash';

// Add the RxJS Observable operators we need in this app.
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Login } from '../responses/login.interface';
import { Credentials } from '../models/credentials.interface';
import { User } from 'src/app/account/models/user.interface';
import { UserUpdate } from 'src/app/account/models/user-update.interface';
import { UserSettings } from 'src/app/account/models/user-settings.interface';
import { UserSettingsUpdate } from 'src/app/account/models/user-settings-update.interface';
import { JwtHelperService } from '@auth0/angular-jwt';
import { ForgotPasswordRequest } from '../models/forgot-password.interface';
import { PasswordResetRequestResponse } from '../responses/password-reset-request.interface';
import { ResetPasswordRequest } from '../models/reset-password.interface';

@Injectable()

export class UserService extends BaseService {

  baseUrl: string = '';
  private httpOptions = {
    headers: new HttpHeaders({
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${localStorage.getItem('authToken')}`
    })
  };

  // Observable navItem source
  private _authStatus = new BehaviorSubject<boolean>(false);
  private _adminStatus = new BehaviorSubject<boolean>(false)
  private _settings = new BehaviorSubject<UserSettings>({ preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' });
  // Observable navItem stream
  authStatus$ = this._authStatus.asObservable();
  adminStatus$ = this._adminStatus.asObservable();
  settings$ = this._settings.asObservable();

  private settings = { preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' };

  constructor(private http: HttpClient, private configService: ConfigService, private jwtHelper: JwtHelperService) {
    super();
    this._authStatus.next(!!this.isAuthenticated());
    this._adminStatus.next(!!this.isAdmin());
    this._settings.next(this.settings);

    this.baseUrl = configService.getApiURI();

    var userId = localStorage.getItem('userId');
    if (userId != null) {
      this.getSettings(userId).subscribe((settings: UserSettings) => {
        this._settings.next(settings);
      }, (error: any) => console.log(error));
    }
  }

  public isAuthenticated(): boolean {
    const token = localStorage.getItem("authToken");

    return token != null && !this.jwtHelper.isTokenExpired(token);
  }

  public isAdmin(): boolean {
    const token = localStorage.getItem("authToken");
    if (token == null) return false;

    const decodedToken = this.jwtHelper.decodeToken(token);
    const role = decodedToken['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
    return role === 'Admin';
  }

  register(userRegistration: UserRegistration, target: string) {
    return this.http.post<UserRegistration>(this.baseUrl + target, userRegistration, this.httpOptions)
      .pipe(map(response => {
        return response;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  login(credentials: Credentials): Observable<Login> {
    return this.http.post<Login>(this.baseUrl + '/auth/login', credentials)
      .pipe(map(response => {
        localStorage.setItem('userId', response.id);
        localStorage.setItem('userName', response.userName);
        localStorage.setItem('email', response.email);
        localStorage.setItem('authToken', response.authToken);
        console.log(response.authToken);

        this._authStatus.next(this.isAuthenticated());
        this._adminStatus.next(this.isAdmin());

        this.getSettings(response.id).subscribe((settings: UserSettings) => {
          this._settings.next(settings);
        }, (error: any) => console.log(error));

        return response;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  requestPasswordReset(request: ForgotPasswordRequest): Observable<PasswordResetRequestResponse> {
    return this.http.post<PasswordResetRequestResponse>(this.baseUrl + '/auth/forgotpassword', request)
      .pipe(map(response => response, (error: any) => console.log(error, "fails")));
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(this.baseUrl + '/auth/resetpassword', request)
      .pipe(map(response => response, (error: any) => console.log(error, "fails")));
  }

  get(username: string): Observable<User> {
    return this.http.get<User>(this.baseUrl + `/account/get?username=${username}`).pipe(map(response => {
      return response;
    }, (error: any) => console.log(error, "fails")
    ));
  }

  update(user: UserUpdate): Observable<User> {
    return this.http.post<User>(this.baseUrl + "/account/update", user, this.httpOptions)
      .pipe(map(user => {
        return user;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  updateSettings(settings: UserSettingsUpdate): Observable<UserSettings> {
    return this.http.post<UserSettings>(this.baseUrl + "/account/updatesettings", settings, this.httpOptions)
      .pipe(map(user => {
        return user;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getSettings(userId: string): Observable<UserSettings> {
    return this.http.get<UserSettings>(this.baseUrl + `/account/getsettings?userId=${userId}`).pipe(map(response => {
      this._settings.next(response);
      return response;
    }, (error: any) => console.log(error, "fails")
    ));
  }

  logout() {
    localStorage.removeItem('authToken');
    this._authStatus.next(false);
    this._adminStatus.next(false);
  }

  getUserId() {
    let userId = localStorage.getItem('userId');
    if (userId == null)
      userId = '';

    return userId;
  }

  getUserName() {
    let userName = localStorage.getItem('userName');
    if (userName == null)
      userName = '';

    return userName;
  }

  getEmail() {
    let email = localStorage.getItem('email');
    if (email == null)
      email = '';

    return email;
  }

  getUserLanguage() {
    return this.settings.preferredLanguage;
  }
}