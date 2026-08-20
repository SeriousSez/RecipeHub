import { Component, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common'
import { Recipe } from '../models/recipe.interface';
import { RecipeService } from '../services/recipe.service';
import { Router } from '@angular/router';
import { UserService } from 'src/app/shared/services/user.service';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { Favorites } from 'src/app/shared/models/favorites.interface';
import { forkJoin, Subscription } from 'rxjs';
import { Ingredient } from '../models/ingredient.interface';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { UserSettings } from 'src/app/account/models/user-settings.interface';
import { UtilityService } from 'src/app/shared/utils/utility.service';

@Component({
  selector: 'app-overview',
  templateUrl: './overview.component.html',
  styleUrls: ['./overview.component.css'],
  standalone: false
})
export class OverviewComponent implements OnInit {
  public recipeList: Recipe[] = [];
  public groceryList: Ingredient[] = [];

  public shownRecipes: Recipe[] = [];
  public recipes: Recipe[];
  public favoredRecipes: Recipe[];
  public selectedRecipes: Recipe[] = [];
  public showFavorites: boolean = false;
  public searchTerm: string = '';

  public sortSetting: string = 'created';
  public ascending: boolean = true;

  public loading: boolean = true;
  public loadError: boolean = false;
  public groceryFeedbackMessage: string = '';
  public groceryFeedbackType: 'success' | 'danger' = 'success';
  isAuthenticated: boolean = false;
  settings: UserSettings = { preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' };
  subscription?: Subscription;
  settingsSubscription?: Subscription;
  private groceryFeedbackTimer?: ReturnType<typeof setTimeout>;

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, private datepipe: DatePipe, private router: Router, private utilityService: UtilityService) { }

  ngOnInit(): void {
    this.getRecipes();
    this.getGroceryLists();
    this.subscription = this.userService.authStatus$.subscribe(status => this.isAuthenticated = status);
    this.settingsSubscription = this.userService.settings$.subscribe(settings => this.settings = settings);

    if (this.isAuthenticated) this.getFavorites();
  }

  getGroceryLists() {
    this.recipeList = this.groceryService.getRecipeList();
    this.groceryList = this.groceryService.getIngredientList();
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
    this.settingsSubscription?.unsubscribe();
  }

  getRecipes() {
    this.loading = true;
    this.loadError = false;

    this.recipeService.getRecipes().subscribe((recipes: Recipe[]) => {
      const normalizedRecipes = Array.isArray(recipes) ? recipes : [];
      this.recipes = normalizedRecipes;
      this.applyFiltersAndSort();
      this.loading = false;
    },
      error => {
        this.recipes = [];
        this.shownRecipes = [];
        this.loadError = true;
        this.loading = false;
      });
  }

  getFavorites() {
    var username = this.userService.getUserName();
    if (username.length == 0 || username == '' || username == null) return;

    this.favoriteService.get(username).subscribe((favorites: Favorites) => {
      this.favoredRecipes = favorites.recipes;
    },
      error => {
        //this.notificationService.printErrorMessage(error);
      });
  }

  addSelectedRecipesToGroceryList() {
    if (this.selectedRecipes.length === 0) return;

    const selectedRecipeRequests = this.selectedRecipes.map(recipe => this.recipeService.getRecipe(recipe.title, recipe.creator));

    forkJoin(selectedRecipeRequests).subscribe((fullRecipes: Recipe[]) => {
      fullRecipes.forEach(recipe => this.groceryService.toggleRecipeToList(recipe));
      this.recipeList = this.groceryService.getRecipeList();
      this.showGroceryFeedback(`Added ${fullRecipes.length} recipe${fullRecipes.length === 1 ? '' : 's'} to groceries.`, 'success');
      this.clearSelectedRecipes();
    },
      error => {
        this.showGroceryFeedback('Unable to add recipes to groceries right now.', 'danger');
      });
  }

  toggleRecipeSelected(recipe: Recipe) {
    var index = this.selectedRecipes.indexOf(recipe, 0);
    if (index > -1) {
      this.selectedRecipes.splice(index, 1);
    } else {
      this.selectedRecipes.push(recipe);
    }
  }

  clearSelectedRecipes() {
    this.selectedRecipes = [];
  }

  private showGroceryFeedback(message: string, type: 'success' | 'danger') {
    this.groceryFeedbackMessage = message;
    this.groceryFeedbackType = type;

    if (this.groceryFeedbackTimer) {
      clearTimeout(this.groceryFeedbackTimer);
    }

    this.groceryFeedbackTimer = setTimeout(() => {
      this.groceryFeedbackMessage = '';
    }, 3500);
  }

  toggleDisplay() {
    this.showFavorites = !this.showFavorites;
    this.applyFiltersAndSort();
  }

  openRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${recipe.id}/${this.utilityService.toSlug(recipe.title)}`]);
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  applyFiltersAndSort() {
    const source = this.showFavorites ? (this.favoredRecipes ?? []) : (this.recipes ?? []);
    const term = this.searchTerm.trim().toLowerCase();

    const filtered = source.filter(recipe => {
      if (!term) return true;

      const searchable = [
        recipe.title,
        recipe.creator,
        recipe.description,
        recipe.instructions
      ].filter(Boolean).join(' ').toLowerCase();

      return searchable.includes(term);
    });

    this.shownRecipes = [...filtered].sort((a, b) => {
      let comparison = 0;

      switch (this.sortSetting) {
        case 'title':
          comparison = a.title.localeCompare(b.title);
          break;
        case 'creator':
          comparison = a.creator.localeCompare(b.creator);
          break;
        case 'instructions':
          comparison = (a.instructions ?? '').localeCompare(b.instructions ?? '');
          break;
        case 'created':
        default:
          comparison = new Date(a.created).getTime() - new Date(b.created).getTime();
          break;
      }

      return this.ascending ? comparison : -comparison;
    });
  }

  sort(sortSetting: string) {
    if (this.sortSetting !== sortSetting) {
      this.sortSetting = sortSetting;
      this.ascending = true;
    } else {
      this.ascending = !this.ascending;
    }

    this.applyFiltersAndSort();
  }

  toggleSortDirection() {
    this.ascending = !this.ascending;
    this.applyFiltersAndSort();
  }
}
