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
import { PublicProfile } from 'src/app/account/models/public-profile.interface';
import { PublicProfileUpdate } from 'src/app/account/models/public-profile-update.interface';

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
  private _identity = new BehaviorSubject<{ userName: string; email: string }>({ userName: '', email: '' });
  private _settings = new BehaviorSubject<UserSettings>({ preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' });
  // Observable navItem stream
  authStatus$ = this._authStatus.asObservable();
  adminStatus$ = this._adminStatus.asObservable();
  identity$ = this._identity.asObservable();
  settings$ = this._settings.asObservable();

  private settings = { preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' };

  private applySettings(settings: UserSettings): void {
    this.settings = { ...this.settings, ...settings };
    this._settings.next(this.settings);
  }

  constructor(private http: HttpClient, private configService: ConfigService, private jwtHelper: JwtHelperService) {
    super();
    this._authStatus.next(!!this.isAuthenticated());
    this._adminStatus.next(!!this.isAdmin());
    this._identity.next({ userName: this.getUserName(), email: this.getEmail() });
    this._settings.next(this.settings);

    this.baseUrl = configService.getApiURI();

    var userId = localStorage.getItem('userId');
    if (userId != null) {
      this.getSettings(userId).subscribe((settings: UserSettings) => {
        this.applySettings(settings);
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
        this._identity.next({ userName: response.userName, email: response.email });
        console.log(response.authToken);

        this._authStatus.next(this.isAuthenticated());
        this._adminStatus.next(this.isAdmin());

        this.getSettings(response.id).subscribe((settings: UserSettings) => {
          this.applySettings(settings);
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

  confirmEmail(userId: string, token: string): Observable<void> {
    return this.http.get<void>(this.baseUrl + '/auth/confirmemail', {
      params: { userId, token }
    });
  }

  resendEmailConfirmation(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(this.baseUrl + '/auth/resendconfirmation', { email });
  }

  getPublicProfile(username: string): Observable<PublicProfile> {
    return this.http.get<PublicProfile>(this.baseUrl + `/account/public/${encodeURIComponent(username)}`);
  }

  updatePublicProfile(profile: PublicProfileUpdate): Observable<PublicProfile> {
    return this.http.put<PublicProfile>(this.baseUrl + '/account/public-profile', profile, this.httpOptions);
  }

  get(username: string): Observable<User> {
    return this.http.get<User>(this.baseUrl + `/account/get?username=${username}`).pipe(map(response => {
      return response;
    }, (error: any) => console.log(error, "fails")
    ));
  }

  update(user: UserUpdate): Observable<User> {
    return this.http.post<User>(this.baseUrl + "/account/update", user, this.httpOptions)
      .pipe(map(updatedUser => {
        if (updatedUser) {
          localStorage.setItem('userName', updatedUser.userName);
          localStorage.setItem('email', updatedUser.email);
          this._identity.next({ userName: updatedUser.userName, email: updatedUser.email });
        }
        return updatedUser;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  updateSettings(settings: UserSettingsUpdate): Observable<UserSettings> {
    return this.http.post<UserSettings>(this.baseUrl + "/account/updatesettings", settings, this.httpOptions)
      .pipe(map(updatedSettings => {
        this.applySettings(updatedSettings);
        return updatedSettings;
      }, (error: any) => console.log(error, "fails")
      ));
  }

  getSettings(userId: string): Observable<UserSettings> {
    return this.http.get<UserSettings>(this.baseUrl + `/account/getsettings?userId=${userId}`).pipe(map(response => {
      this.applySettings(response);
      return response;
    }, (error: any) => console.log(error, "fails")
    ));
  }

  logout() {
    localStorage.removeItem('authToken');
    localStorage.removeItem('userId');
    localStorage.removeItem('userName');
    localStorage.removeItem('email');
    this._authStatus.next(false);
    this._adminStatus.next(false);
    this._identity.next({ userName: '', email: '' });
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