import { Component, ElementRef, HostBinding, HostListener, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from 'src/app/shared/services/user.service';
import { LanguageService, UiLanguage } from 'src/app/shared/services/language.service';
import { ThemeService } from 'src/app/shared/services/theme.service';

@Component({
  selector: 'app-mobile-header',
  templateUrl: './mobile-header.component.html',
  styleUrls: ['./mobile-header.component.scss'],
  standalone: false
})
export class MobileHeaderComponent implements OnInit {
  @HostBinding('class.navbar-opened') navbarOpened = false;
  @HostBinding('class.second-navbar-opened') secondNavbarOpened = false;

  @HostListener('window:scroll', []) onWindowScroll() {
    var offset = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
    if (offset > 10) {
      this.isFixedNavbar = true;
    } else {
      this.isFixedNavbar = false;
    }
  }

  @HostListener('document:click', ['$event'])
  clickout(event: any) {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.closeNavbar();
    }
  }

  isAuthenticated: boolean = false;
  isAdmin: boolean = false;
  subscription?: Subscription;
  subscription2?: Subscription;

  public isFixedNavbar = false;
  languages: UiLanguage[];
  languageCodes: string[] = [];
  languageLabels: Record<string, string> = {};
  currentLanguage: string;
  isDarkTheme: boolean;

  constructor(private userService: UserService, private router: Router, private elementRef: ElementRef, private languageService: LanguageService, private themeService: ThemeService) { }

  ngOnInit(): void {
    this.subscription = this.userService.authStatus$.subscribe(result => this.isAuthenticated = result);
    this.subscription2 = this.userService.adminStatus$.subscribe(result => this.isAdmin = result);

    this.languages = this.languageService.languages;
    this.languageCodes = this.languages.map(language => language.code);
    this.languageLabels = Object.fromEntries(this.languages.map(language => [language.code, language.label]));
    this.currentLanguage = this.languageService.getCurrentLanguage();
    this.isDarkTheme = this.themeService.isDark();
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

  toggleNavbar() {
    this.navbarOpened = !this.navbarOpened;
  }

  closeNavbar() {
    this.navbarOpened = false;
  }

  logout() {
    this.userService.logout();
    this.toggleNavbar();
    this.router.navigate(['/']);
  }

}
