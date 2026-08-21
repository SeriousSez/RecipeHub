import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

import { UserService } from '../shared/services/user.service';
import { LanguageService, UiLanguage } from '../shared/services/language.service';
import { ThemeService } from '../shared/services/theme.service';

@Component({
  selector: 'app-header',
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.scss'],
  standalone: false
})
export class HeaderComponent implements OnInit, OnDestroy {

  username: string = '';
  email: string = '';
  isAuthenticated: boolean = false;
  isAdmin: boolean = false;
  subscription?: Subscription;
  subscription2?: Subscription;

  languages: UiLanguage[];
  currentLanguage: string;
  isDarkTheme: boolean;

  constructor(private userService: UserService, private router: Router, private languageService: LanguageService, private themeService: ThemeService) { }

  logout() {
    this.userService.logout();
    this.router.navigate(['/']);
  }

  ngOnInit() {
    this.username = this.userService.getUserName();
    this.email = this.userService.getEmail();
    this.subscription = this.userService.authStatus$.subscribe(result => this.isAuthenticated = result);
    this.subscription2 = this.userService.adminStatus$.subscribe(result => this.isAdmin = result);

    this.languages = this.languageService.languages;
    this.currentLanguage = this.languageService.getCurrentLanguage();
    this.isDarkTheme = this.themeService.isDark();
  }

  get currentLanguageLabel(): string {
    return this.languages?.find(language => language.code === this.currentLanguage)?.label ?? '';
  }

  changeLanguage(code: string): void {
    this.currentLanguage = code;
    this.languageService.setLanguage(code);
  }

  toggleTheme(): void {
    this.isDarkTheme = this.themeService.toggleTheme() === 'dark';
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
  }
}