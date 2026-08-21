import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from '../shared/services/user.service';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css'],
  standalone: false
})
export class HomeComponent implements OnInit {

  status: boolean = false;
  pantryIngredientCount: number = 0;
  subscription?: Subscription;

  constructor(private userService: UserService, private router: Router, private translateService: TranslateService) { }

  ngOnInit(): void {
    this.subscription = this.userService.authStatus$.subscribe(status => {
      this.status = status;
      this.refreshPantryCount();
    });

    this.refreshPantryCount();
  }

  private refreshPantryCount(): void {
    if (typeof localStorage === 'undefined') {
      this.pantryIngredientCount = 0;
      return;
    }

    const pantryValue = localStorage.getItem('recipehub-pantry-ingredients') ?? '';
    this.pantryIngredientCount = pantryValue
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0)
      .length;
  }

  get pantryCardTitle(): string {
    if (this.status) {
      return `${this.pantryIngredientCount}`;
    }

    return this.translateService.instant('home.addPantry');
  }

  get pantryCardSubtitle(): string {
    if (this.status) {
      return this.pantryIngredientCount === 1
        ? this.translateService.instant('home.ingredientReadyToMatch')
        : this.translateService.instant('home.ingredientsReadyToMatch');
    }

    return this.translateService.instant('home.startWithWhatYouHave');
  }

  goToRegister(): void {
    this.router.navigate(['/register']);
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

}
