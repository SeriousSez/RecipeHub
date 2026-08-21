import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from '../shared/services/user.service';

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

  constructor(private userService: UserService, private router: Router) { }

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

    return 'Add pantry';
  }

  get pantryCardSubtitle(): string {
    if (this.status) {
      return this.pantryIngredientCount === 1 ? 'ingredient ready to match' : 'ingredients ready to match';
    }

    return 'start with what you have';
  }

  goToRegister(): void {
    this.router.navigate(['/register']);
  }

  goToLogin(): void {
    this.router.navigate(['/login']);
  }

}
