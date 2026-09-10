import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from '../shared/services/user.service';
import { TranslateService } from '@ngx-translate/core';
import { RecipeService } from '../recipe/services/recipe.service';
import { Recipe } from '../recipe/models/recipe.interface';
import { UtilityService } from '../shared/utils/utility.service';
import { LanguageService } from '../shared/services/language.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css'],
  standalone: false
})
export class HomeComponent implements OnInit {

  status: boolean = false;
  pantryIngredientCount: number = 0;
  recommendedRecipes: Recipe[] = [];
  recommendationsLoading = true;
  subscription?: Subscription;
  private recommendationsRequestId = 0;

  constructor(private userService: UserService, private router: Router, private translateService: TranslateService, private recipeService: RecipeService, public utilityService: UtilityService, private languageService: LanguageService) { }

  ngOnInit(): void {
    this.subscription = this.userService.authStatus$.subscribe(status => {
      this.status = status;
      this.refreshPantryCount();
      this.loadRecommendations();
    });

    this.refreshPantryCount();
  }

  private loadRecommendations(): void {
    const requestId = ++this.recommendationsRequestId;
    const personalized = this.status;
    this.recommendationsLoading = true;

    if (personalized) {
      this.recipeService.getRecommendations(3, this.languageService.getCurrentRecipeLanguage()).subscribe({
        next: recipes => {
          if (requestId !== this.recommendationsRequestId) return;
          this.recommendedRecipes = (recipes ?? []).slice(0, 3);
          this.recommendationsLoading = false;
        },
        error: () => {
          if (requestId === this.recommendationsRequestId) {
            this.recommendedRecipes = [];
            this.recommendationsLoading = false;
          }
        }
      });
      return;
    }

    this.recipeService.getRecipesPaged({
      page: 1,
      pageSize: 3,
      sortBy: 'popularity',
      ascending: false,
      language: this.languageService.getCurrentRecipeLanguage()
    }).subscribe({
      next: result => {
        if (requestId !== this.recommendationsRequestId) return;

        const candidates = (result?.items ?? []).slice(0, 18);
        this.recommendedRecipes = candidates.slice(0, 3);
        this.recommendationsLoading = false;
      },
      error: () => {
        if (requestId === this.recommendationsRequestId) {
          this.recommendedRecipes = [];
          this.recommendationsLoading = false;
        }
      }
    });
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
