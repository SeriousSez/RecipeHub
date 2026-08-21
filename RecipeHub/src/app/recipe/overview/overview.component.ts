import { Component, HostListener, OnInit } from '@angular/core';
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
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-overview',
  templateUrl: './overview.component.html',
  styleUrls: ['./overview.component.css'],
  standalone: false
})
export class OverviewComponent implements OnInit {
  private readonly pantryStorageKey = 'recipehub-pantry-ingredients';
  private readonly filterStorageKey = 'recipehub-overview-filters';

  public recipeList: Recipe[] = [];
  public groceryList: Ingredient[] = [];

  public shownRecipes: Recipe[] = [];
  public matchingRecipes: Recipe[] | null = null;
  public favoredRecipes: Recipe[];
  public selectedRecipeIds: string[] = [];
  public showFavorites: boolean = false;
  public showMyRecipes: boolean = false;
  public showCreateMode: boolean = false;
  public showMobileFilters: boolean = false;
  public isClosingMobileFilters: boolean = false;
  public showPantryMatches: boolean = false;

  public readonly pageSize: number = 9;
  public visibleCount: number = this.pageSize;
  public currentPage: number = 1;
  public totalCount: number = 0;
  private searchDebounceTimer?: ReturnType<typeof setTimeout>;

  public get visibleRecipes(): Recipe[] {
    return this.showPantryMatches ? this.shownRecipes.slice(0, this.visibleCount) : this.shownRecipes;
  }

  public get hasMoreRecipes(): boolean {
    return this.showPantryMatches ? this.visibleCount < this.shownRecipes.length : this.shownRecipes.length < this.totalCount;
  }

  public get displayTotalCount(): number {
    return this.showPantryMatches ? this.shownRecipes.length : this.totalCount;
  }

  loadMore(): void {
    if (this.showPantryMatches) {
      this.visibleCount = Math.min(this.visibleCount + this.pageSize, this.shownRecipes.length);
      return;
    }

    this.currentPage++;
    this.fetchPage(false);
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    if (!this.hasMoreRecipes) {
      return;
    }

    const scrollPosition = window.innerHeight + window.scrollY;
    const documentHeight = document.documentElement.scrollHeight;

    if (scrollPosition >= documentHeight - 400) {
      this.loadMore();
    }
  }

  public get selectedRecipes(): Recipe[] {
    return (this.shownRecipes ?? []).filter(recipe => this.selectedRecipeIds.includes(recipe.id));
  }

  public set selectedRecipes(value: Recipe[]) {
    this.selectedRecipeIds = value.map(recipe => recipe.id);
  }

  public get pantryButtonLabel(): string {
    return this.showPantryMatches ? this.translateService.instant('recipe.showAllRecipesPantry') : this.translateService.instant('recipe.showMatchingRecipes');
  }

  public get hasActiveFilters(): boolean {
    return !!this.searchTerm
      || this.categoryFilter !== 'all'
      || this.tagFilter !== 'all'
      || this.sortSetting !== 'created'
      || this.ascending !== false
      || this.showFavorites
      || this.showMyRecipes
      || this.showPantryMatches;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.categoryFilter = 'all';
    this.tagFilter = 'all';
    this.sortSetting = 'created';
    this.ascending = false;
    this.showFavorites = false;
    this.showMyRecipes = false;
    this.showPantryMatches = false;
    this.applyFiltersAndSort();
  }

  public searchTerm: string = '';
  public categoryFilter: string = 'all';
  public tagFilter: string = 'all';
  public pantryIngredients: string = '';
  public availableCategories: string[] = [];
  public availableTags: string[] = [];
  public bestMatchScore: number = 0;

  public sortSetting: string = 'created';
  public ascending: boolean = false;

  public loading: boolean = true;
  public matchingLoading: boolean = false;
  public loadError: boolean = false;
  public groceryFeedbackMessage: string = '';
  public groceryFeedbackType: 'success' | 'danger' = 'success';
  isAuthenticated: boolean = false;
  settings: UserSettings = { preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' };
  subscription?: Subscription;
  settingsSubscription?: Subscription;
  private groceryFeedbackTimer?: ReturnType<typeof setTimeout>;

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, private datepipe: DatePipe, private router: Router, private utilityService: UtilityService, private translateService: TranslateService) { }

  ngOnInit(): void {
    this.loadPantryIngredients();
    this.restoreFilterState();
    this.getRecipes();
    this.getGroceryLists();
    this.subscription = this.userService.authStatus$.subscribe(status => this.isAuthenticated = status);
    this.settingsSubscription = this.userService.settings$.subscribe(settings => this.settings = settings);

    if (this.isAuthenticated) this.getFavorites();
  }

  private loadPantryIngredients(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    const savedIngredients = localStorage.getItem(this.pantryStorageKey);
    this.pantryIngredients = savedIngredients ?? '';
  }

  private restoreFilterState(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    const saved = localStorage.getItem(this.filterStorageKey);
    if (!saved) {
      return;
    }

    try {
      const state = JSON.parse(saved);
      this.searchTerm = state.searchTerm ?? this.searchTerm;
      this.categoryFilter = state.categoryFilter ?? this.categoryFilter;
      this.tagFilter = state.tagFilter ?? this.tagFilter;
      this.sortSetting = state.sortSetting ?? this.sortSetting;
      this.ascending = state.ascending ?? this.ascending;
    } catch {
      // ignore malformed saved state and fall back to defaults
    }
  }

  private persistFilterState(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    const state = {
      searchTerm: this.searchTerm,
      categoryFilter: this.categoryFilter,
      tagFilter: this.tagFilter,
      sortSetting: this.sortSetting,
      ascending: this.ascending
    };

    localStorage.setItem(this.filterStorageKey, JSON.stringify(state));
  }

  public updatePantryIngredients(value: string): void {
    this.pantryIngredients = value ?? '';

    if (typeof localStorage !== 'undefined') {
      if (this.pantryIngredients.trim().length === 0) {
        localStorage.removeItem(this.pantryStorageKey);
      } else {
        localStorage.setItem(this.pantryStorageKey, this.pantryIngredients);
      }
    }

    this.applyFiltersAndSort();
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
    this.matchingRecipes = null;
    this.fetchPage(true);
  }

  private fetchPage(reset: boolean): void {
    if (reset) {
      this.currentPage = 1;
      this.loading = true;
    }
    this.loadError = false;

    const favoriteIdsParam = this.showFavorites
      ? (this.favoredRecipes ?? []).map(recipe => recipe.id).join(',')
      : undefined;
    const creatorParam = this.showMyRecipes ? this.userService.getUserName() : undefined;

    this.recipeService.getRecipesPaged({
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm || undefined,
      category: this.categoryFilter !== 'all' ? this.categoryFilter : undefined,
      tag: this.tagFilter !== 'all' ? this.tagFilter : undefined,
      sortBy: this.sortSetting,
      ascending: this.ascending,
      creator: creatorParam,
      favoriteIds: favoriteIdsParam
    }).subscribe({
      next: result => {
        const items = Array.isArray(result?.items) ? result.items : [];
        this.shownRecipes = reset ? items : [...this.shownRecipes, ...items];
        this.totalCount = result?.totalCount ?? this.shownRecipes.length;
        this.availableCategories = result?.availableCategories ?? [];
        this.availableTags = result?.availableTags ?? [];

        if (this.categoryFilter !== 'all' && !this.availableCategories.includes(this.categoryFilter)) {
          this.categoryFilter = 'all';
        }

        if (this.tagFilter !== 'all' && !this.availableTags.includes(this.tagFilter)) {
          this.tagFilter = 'all';
        }

        this.loading = false;
        this.persistFilterState();
      },
      error: () => {
        if (reset) {
          this.shownRecipes = [];
          this.totalCount = 0;
        }
        this.loadError = true;
        this.loading = false;
      }
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
      const message = fullRecipes.length === 1
        ? this.translateService.instant('recipe.addedToGroceriesOne')
        : this.translateService.instant('recipe.addedToGroceriesMany', { count: fullRecipes.length });
      this.showGroceryFeedback(message, 'success');
      this.clearSelectedRecipes();
    },
      error => {
        this.showGroceryFeedback(this.translateService.instant('recipe.addToGroceriesError'), 'danger');
      });
  }

  isRecipeSelected(recipe: Recipe): boolean {
    return this.selectedRecipeIds.includes(recipe.id);
  }

  toggleRecipeSelected(recipe: Recipe) {
    const recipeId = recipe.id;
    const index = this.selectedRecipeIds.indexOf(recipeId);

    if (index > -1) {
      this.selectedRecipeIds.splice(index, 1);
    } else {
      this.selectedRecipeIds.push(recipeId);
    }
  }

  clearSelectedRecipes() {
    this.selectedRecipeIds = [];
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
    if (this.showFavorites) {
      this.showMyRecipes = false;
    }
    this.applyFiltersAndSort();
  }

  openMobileFilters() {
    this.isClosingMobileFilters = false;
    this.showMobileFilters = true;
  }

  closeMobileFilters() {
    if (!this.showMobileFilters) {
      return;
    }

    this.isClosingMobileFilters = true;

    setTimeout(() => {
      this.showMobileFilters = false;
      this.isClosingMobileFilters = false;
    }, 180);
  }

  toggleMobileFilters() {
    if (this.showMobileFilters) {
      this.closeMobileFilters();
      return;
    }

    this.openMobileFilters();
  }

  openCreateRecipe() {
    this.showCreateMode = true;
  }

  closeCreateRecipe() {
    this.showCreateMode = false;
  }

  toggleMyRecipes() {
    this.showMyRecipes = !this.showMyRecipes;
    if (this.showMyRecipes) {
      this.showFavorites = false;
    }
    this.applyFiltersAndSort();
  }

  openRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${recipe.id}/${this.utilityService.toSlug(recipe.title)}`]);
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  private normalizeText(value: string | null | undefined): string {
    return (value ?? '').trim().toLowerCase();
  }

  private normalizeIngredientText(value: string | null | undefined): string {
    const normalized = this.normalizeText(value)
      .replace(/[^a-z0-9\s]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    if (!normalized) {
      return '';
    }

    return normalized
      .split(' ')
      .map(word => {
        if (word.length <= 3) {
          return word;
        }

        if (word.endsWith('ies') && word.length > 4) {
          return word.slice(0, -3) + 'y';
        }

        if ((word.endsWith('sses') || word.endsWith('shes') || word.endsWith('ches') || word.endsWith('xes') || word.endsWith('zes')) && word.length > 4) {
          return word.slice(0, -2);
        }

        if (word.endsWith('s') && !word.endsWith('ss')) {
          return word.slice(0, -1);
        }

        return word;
      })
      .join(' ');
  }

  private ingredientNamesMatch(left: string | null | undefined, right: string | null | undefined): boolean {
    const leftName = this.normalizeIngredientText(left);
    const rightName = this.normalizeIngredientText(right);

    if (!leftName || !rightName) {
      return false;
    }

    return leftName === rightName ||
      leftName.includes(rightName) ||
      rightName.includes(leftName) ||
      leftName.replace(/\s+/g, '').includes(rightName.replace(/\s+/g, '')) ||
      rightName.replace(/\s+/g, '').includes(leftName.replace(/\s+/g, ''));
  }

  private parsePantryIngredients(): string[] {
    return this.pantryIngredients
      .split(',')
      .map(item => this.normalizeIngredientText(item))
      .filter(item => item.length > 0);
  }

  public togglePantryMatches(): void {
    if (this.showPantryMatches) {
      this.showPantryMatches = false;
      this.applyFiltersAndSort();
      return;
    }

    if (this.matchingRecipes) {
      this.showPantryMatches = true;
      this.applyFiltersAndSort();
      return;
    }

    this.loadMatchingRecipes(true);
  }

  private loadMatchingRecipes(activateWhenReady: boolean = false): void {
    if (this.matchingRecipes || this.matchingLoading) {
      return;
    }

    this.matchingLoading = true;

    this.recipeService.getRecipesWithIngredients().subscribe({
      next: recipes => {
        this.matchingRecipes = Array.isArray(recipes) ? recipes : [];
        this.matchingLoading = false;
        this.showPantryMatches = this.showPantryMatches || activateWhenReady;

        if (this.showPantryMatches) {
          this.applyFiltersAndSort();
        }
      },
      error: () => {
        this.matchingLoading = false;
        this.showPantryMatches = false;
        this.applyFiltersAndSort();
      }
    });
  }

  public getIngredientMatchScore(recipe: Recipe): number {
    const pantryItems = this.parsePantryIngredients();
    if (pantryItems.length === 0) {
      return 0;
    }

    const recipeNames = (recipe.ingredients ?? []).map(ingredient => this.normalizeIngredientText(ingredient.name));

    return pantryItems.reduce((score, pantryIngredient) => {
      if (!pantryIngredient) {
        return score;
      }

      const hasMatch = recipeNames.some(name => this.ingredientNamesMatch(name, pantryIngredient));

      return hasMatch ? score + 1 : score;
    }, 0);
  }

  public getMissingIngredientCount(recipe: Recipe): number {
    const recipeItems = (recipe.ingredients ?? []).map(item => this.normalizeIngredientText(item.name));
    const pantryItems = this.parsePantryIngredients();

    if (pantryItems.length === 0 || recipeItems.length === 0) {
      return 0;
    }

    return recipeItems.filter(item =>
      !pantryItems.some(pantryItem => this.ingredientNamesMatch(item, pantryItem))
    ).length;
  }

  public getPantryIngredientList(): string[] {
    return this.parsePantryIngredients();
  }

  private recipeMatchesFilters(recipe: Recipe): boolean {
    const term = this.searchTerm.trim().toLowerCase();
    const categoryMatch = this.categoryFilter === 'all' || (recipe.categories ?? []).some(category => this.normalizeText(category) === this.categoryFilter);
    const tagMatch = this.tagFilter === 'all' || (recipe.tags ?? []).some(tag => this.normalizeText(tag) === this.tagFilter);

    if (!categoryMatch || !tagMatch) {
      return false;
    }

    if (!term) {
      return true;
    }

    const searchable = [
      recipe.title,
      recipe.creator,
      recipe.description,
      recipe.instructions,
      ...(recipe.categories ?? []),
      ...(recipe.tags ?? []),
      ...(recipe.ingredients ?? []).map(ingredient => ingredient.name)
    ].filter(Boolean).join(' ').toLowerCase();

    return searchable.includes(term);
  }

  applyFiltersAndSort() {
    if (!this.showPantryMatches) {
      if (this.searchDebounceTimer) {
        clearTimeout(this.searchDebounceTimer);
      }

      this.searchDebounceTimer = setTimeout(() => this.fetchPage(true), 300);
      return;
    }

    const source = this.matchingRecipes ?? [];

    let filteredSource = source;

    if (this.showFavorites) {
      const favoredIds = new Set((this.favoredRecipes ?? []).map(recipe => recipe.id));
      filteredSource = filteredSource.filter(recipe => favoredIds.has(recipe.id));
    }

    if (this.showMyRecipes) {
      const username = this.userService.getUserName();
      filteredSource = filteredSource.filter(recipe => recipe.creator?.toLowerCase() === username?.toLowerCase());
    }

    const filtered = filteredSource.filter(recipe =>
      this.recipeMatchesFilters(recipe) &&
      this.getIngredientMatchScore(recipe) > 0
    );

    this.shownRecipes = [...filtered].sort((a, b) => {
      const pantryItems = this.parsePantryIngredients();
      const pantryComparison = pantryItems.length > 0
        ? this.getIngredientMatchScore(b) - this.getIngredientMatchScore(a)
        : 0;

      if (pantryComparison !== 0) {
        return pantryComparison;
      }

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

    this.bestMatchScore = this.shownRecipes.reduce((max, recipe) => Math.max(max, this.getIngredientMatchScore(recipe)), 0);
    this.visibleCount = Math.min(this.pageSize, this.shownRecipes.length) || this.pageSize;
    this.persistFilterState();
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
}
