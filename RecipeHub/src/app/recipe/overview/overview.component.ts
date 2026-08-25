import { Component, HostListener, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common'
import { Recipe } from '../models/recipe.interface';
import { RecipeService } from '../services/recipe.service';
import { ActivatedRoute, Router } from '@angular/router';
import { UserService } from 'src/app/shared/services/user.service';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { Favorites } from 'src/app/shared/models/favorites.interface';
import { forkJoin, Subscription } from 'rxjs';
import { Ingredient } from '../models/ingredient.interface';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { UserSettings } from 'src/app/account/models/user-settings.interface';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { TranslateService } from '@ngx-translate/core';
import { getRecipeNutritionHighlights, RecipeNutritionHighlight, getTaxonomyValueLabel, RecipeTaxonomyGroup, RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS } from '../models/recipe-taxonomy';
import { LanguageService } from 'src/app/shared/services/language.service';

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
    if (this.loadingMore) {
      return;
    }

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
      || !!this.creatorFilter
      || this.selectedCategoryFilters.length > 0
      || this.selectedTagFilters.length > 0
      || this.frozenFilter !== ''
      || this.sortSetting !== 'created'
      || this.ascending !== false
      || this.showFavorites
      || this.showMyRecipes
      || this.showPantryMatches;
  }

  public get activeFilterCount(): number {
    return [
      !!this.searchTerm,
      !!this.creatorFilter,
      this.selectedCategoryFilters.length > 0,
      this.selectedTagFilters.length > 0,
      this.frozenFilter !== '',
      this.showFavorites,
      this.showMyRecipes,
      this.showPantryMatches
    ].filter(Boolean).length;
  }

  clearFilters(): void {
    this.searchTerm = '';
    this.categoryFilter = '';
    this.tagFilter = '';
    this.frozenFilter = '';
    this.sortSetting = 'created';
    this.ascending = false;
    this.showFavorites = false;
    this.showMyRecipes = false;
    this.showPantryMatches = false;
    this.clearCreatorFilter();
    this.applyFiltersAndSort();
  }

  public searchTerm: string = '';
  public creatorFilter: string = '';
  public categoryFilter: string = '';
  public tagFilter: string = '';
  public frozenFilter: '' | 'true' | 'false' = '';
  public pantryIngredients: string = '';
  public availableCategories: string[] = [];
  public availableTags: string[] = [];
  public bestMatchScore: number = 0;

  public get selectedCategoryFilters(): string[] {
    return this.parseTaxonomyFilter(this.categoryFilter);
  }

  public get selectedTagFilters(): string[] {
    return this.parseTaxonomyFilter(this.tagFilter);
  }

  public readonly frozenFilterOptions = ['', 'true', 'false'];

  public get frozenFilterOptionLabels(): Record<string, string> {
    return {
      '': this.translateService.instant('recipe.allFrozenOptions'),
      true: this.translateService.instant('recipe.canBeFrozenOption'),
      false: this.translateService.instant('recipe.cannotBeFrozenOption')
    };
  }

  public sortSetting: string = 'created';

  public get categoryOptionLabels(): Record<string, string> {
    return Object.fromEntries(this.availableCategories.map(category => [category, getTaxonomyValueLabel(category, this.translateService)]));
  }

  public get tagOptionLabels(): Record<string, string> {
    return Object.fromEntries(this.availableTags.map(tag => [tag, getTaxonomyValueLabel(tag, this.translateService)]));
  }

  public get availableCategoryGroups(): RecipeTaxonomyGroup[] {
    return this.buildAvailableTaxonomyGroups(this.availableCategories, RECIPE_CATEGORY_GROUPS);
  }

  public get availableTagGroups(): RecipeTaxonomyGroup[] {
    return this.buildAvailableTaxonomyGroups(this.availableTags, RECIPE_TAG_GROUPS);
  }

  public get sortOptions(): string[] {
    return ['created', 'rating', 'popularity', 'title', 'creator', 'protein', 'carbohydrates', 'fiber'];
  }

  public get nutritionHighlightTags(): string[] {
    const tags = [...this.selectedTagFilters];
    const sortTag = {
      protein: 'High Protein',
      carbohydrates: 'Low Carb',
      fiber: 'High Fiber'
    }[this.sortSetting];

    if (sortTag && !tags.some(tag => this.normalizeText(tag) === this.normalizeText(sortTag))) {
      tags.push(sortTag);
    }

    return tags;
  }

  public get sortOptionLabels(): Record<string, string> {
    return {
      created: this.translateService.instant('recipe.sortCreated'),
      title: this.translateService.instant('recipe.sortTitle'),
      creator: this.translateService.instant('recipe.sortCreator'),
      rating: this.translateService.instant('recipe.sortRating'),
      popularity: this.translateService.instant('recipe.sortPopularity'),
      protein: `${this.translateService.instant('recipe.proteinLabel')} · ${this.translateService.instant('recipe.nutritionPerServing')}`,
      carbohydrates: `${this.translateService.instant('recipe.carbohydratesLabel')} · ${this.translateService.instant('recipe.nutritionPerServing')}`,
      fiber: `${this.translateService.instant('recipe.fiberLabel')} · ${this.translateService.instant('recipe.nutritionPerServing')}`
    };
  }
  public ascending: boolean = false;

  private buildAvailableTaxonomyGroups(availableValues: string[], taxonomyGroups: RecipeTaxonomyGroup[]): RecipeTaxonomyGroup[] {
    const valuesByName = new Map(availableValues.map(value => [value.trim().toLowerCase(), value]));
    const groupedNames = new Set<string>();
    const groups = taxonomyGroups
      .map(group => ({
        ...group,
        values: group.values
          .map(value => {
            const normalizedValue = value.toLowerCase();
            const availableValue = valuesByName.get(normalizedValue);
            if (availableValue) {
              groupedNames.add(normalizedValue);
            }
            return availableValue;
          })
          .filter((value): value is string => !!value)
      }))
      .filter(group => group.values.length > 0);
    const otherValues = availableValues.filter(value => !groupedNames.has(value.trim().toLowerCase()));

    return otherValues.length > 0
      ? [...groups, { id: 'other', labelKey: 'recipe.taxonomyGroups.other', values: otherValues }]
      : groups;
  }

  public loading: boolean = true;
  public refreshing: boolean = false;
  public showRefreshIndicator: boolean = false;
  public loadingMore: boolean = false;
  public matchingLoading: boolean = false;
  public loadError: boolean = false;
  private hasLoadedOnce: boolean = false;
  private activatePantryMatchesWhenReady: boolean = false;
  public groceryFeedbackMessage: string = '';
  public groceryFeedbackType: 'success' | 'danger' = 'success';
  isAuthenticated: boolean = false;
  settings: UserSettings = { preferredLanguage: 'English', theme: 'Light', recipesTheme: 'Pretty', myRecipesTheme: 'Pretty' };
  subscription?: Subscription;
  settingsSubscription?: Subscription;
  languageSubscription?: Subscription;
  private groceryFeedbackTimer?: ReturnType<typeof setTimeout>;
  private refreshIndicatorTimer?: ReturnType<typeof setTimeout>;
  private pageRequestSequence: number = 0;

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, private datepipe: DatePipe, private router: Router, private route: ActivatedRoute, private utilityService: UtilityService, private translateService: TranslateService, private languageService: LanguageService) { }

  ngOnInit(): void {
    this.loadPantryIngredients();
    this.activatePantryMatchesWhenReady = this.route.snapshot.queryParamMap.get('pantry') === 'true';
    this.restoreFilterState();
    this.creatorFilter = this.route.snapshot.queryParamMap.get('creator')?.trim() ?? '';
    this.getRecipes();
    this.getGroceryLists();
    this.subscription = this.userService.authStatus$.subscribe(status => this.isAuthenticated = status);
    this.settingsSubscription = this.userService.settings$.subscribe(settings => this.settings = settings);
    this.languageSubscription = this.translateService.onLangChange.subscribe(() => this.applyFiltersAndSort());

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
      this.categoryFilter = this.normalizeSavedTaxonomyFilter(state.categoryFilter);
      this.tagFilter = this.normalizeSavedTaxonomyFilter(state.tagFilter);
      this.frozenFilter = state.frozenFilter === 'true' || state.frozenFilter === 'false' ? state.frozenFilter : '';
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
      frozenFilter: this.frozenFilter,
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
    this.languageSubscription?.unsubscribe();
    if (this.refreshIndicatorTimer) clearTimeout(this.refreshIndicatorTimer);
  }

  getRecipes() {
    this.matchingRecipes = null;
    this.fetchPage(true);
  }

  private fetchPage(reset: boolean): void {
    const requestSequence = ++this.pageRequestSequence;

    if (reset) {
      this.currentPage = 1;
      if (this.hasLoadedOnce) {
        this.startRefreshing();
      } else {
        this.loading = true;
      }
    } else {
      this.loadingMore = true;
    }
    this.loadError = false;

    const favoriteIdsParam = this.showFavorites
      ? (this.favoredRecipes ?? []).map(recipe => recipe.id).join(',')
      : undefined;
    const creatorParam = this.showMyRecipes ? this.userService.getUserName() : (this.creatorFilter || undefined);
    const effectivePageSize = reset && this.shownRecipes.length > this.pageSize
      ? Math.max(this.pageSize, this.shownRecipes.length)
      : this.pageSize;

    this.recipeService.getRecipesPaged({
      page: this.currentPage,
      pageSize: effectivePageSize,
      search: this.searchTerm || undefined,
      category: this.selectedCategoryFilters.join(',') || undefined,
      tag: this.selectedTagFilters.join(',') || undefined,
      canBeFrozen: this.frozenFilter === '' ? undefined : this.frozenFilter === 'true',
      sortBy: this.sortSetting,
      ascending: this.ascending,
      creator: creatorParam,
      favoriteIds: favoriteIdsParam,
      language: this.getRecipeLanguage()
    }).subscribe({
      next: result => {
        if (requestSequence !== this.pageRequestSequence) return;

        const items = Array.isArray(result?.items) ? result.items : [];
        this.shownRecipes = reset ? items : [...this.shownRecipes, ...items];
        this.totalCount = result?.totalCount ?? this.shownRecipes.length;
        this.availableCategories = result?.availableCategories ?? [];
        this.availableTags = result?.availableTags ?? [];

        this.categoryFilter = this.keepAvailableFilters(this.selectedCategoryFilters, this.availableCategories);
        this.tagFilter = this.keepAvailableFilters(this.selectedTagFilters, this.availableTags);

        this.hasLoadedOnce = true;
        this.stopRefreshing();
        this.loadingMore = false;
        this.persistFilterState();

        if (reset && this.pantryIngredients.trim() && !this.matchingRecipes && !this.matchingLoading) {
          const activateWhenReady = this.activatePantryMatchesWhenReady;
          if (!activateWhenReady) {
            this.loading = false;
          }
          this.loadMatchingRecipes(activateWhenReady);
          this.activatePantryMatchesWhenReady = false;
        } else {
          this.loading = false;
        }
      },
      error: () => {
        if (requestSequence !== this.pageRequestSequence) return;

        if (reset) {
          this.shownRecipes = [];
          this.totalCount = 0;
        } else {
          this.currentPage = Math.max(1, this.currentPage - 1);
        }
        this.loadError = true;
        this.loading = false;
        this.stopRefreshing();
        this.loadingMore = false;
      }
    });
  }

  private getRecipeLanguage(): string {
    return { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[this.languageService.getCurrentLanguage()] ?? 'English';
  }

  private startRefreshing(): void {
    this.refreshing = true;
    if (this.showRefreshIndicator) return;

    if (this.refreshIndicatorTimer) clearTimeout(this.refreshIndicatorTimer);
    this.refreshIndicatorTimer = setTimeout(() => {
      this.showRefreshIndicator = this.refreshing;
      this.refreshIndicatorTimer = undefined;
    }, 180);
  }

  private stopRefreshing(): void {
    this.refreshing = false;
    this.showRefreshIndicator = false;
    if (this.refreshIndicatorTimer) {
      clearTimeout(this.refreshIndicatorTimer);
      this.refreshIndicatorTimer = undefined;
    }
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
      this.clearCreatorFilter();
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
      this.clearCreatorFilter();
    }
    this.applyFiltersAndSort();
  }

  openRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${this.utilityService.toRecipeKey(recipe.id, recipe.title)}`]);
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  getNutritionHighlights(recipe: Recipe): RecipeNutritionHighlight[] {
    return getRecipeNutritionHighlights(recipe, this.nutritionHighlightTags);
  }

  private clearCreatorFilter(): void {
    if (!this.creatorFilter) {
      return;
    }

    this.creatorFilter = '';
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { creator: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  }

  private normalizeText(value: string | null | undefined): string {
    return (value ?? '').trim().toLowerCase();
  }

  private parseTaxonomyFilter(value: string | null | undefined): string[] {
    return (value ?? '')
      .split(',')
      .map(item => item.trim())
      .filter(item => item.length > 0 && this.normalizeText(item) !== 'all');
  }

  private normalizeSavedTaxonomyFilter(value: unknown): string {
    const savedValues = Array.isArray(value) ? value.join(',') : typeof value === 'string' ? value : '';
    return this.parseTaxonomyFilter(savedValues).join(', ');
  }

  private keepAvailableFilters(selected: string[], available: string[]): string {
    const availableValues = new Set(available.map(value => this.normalizeText(value)));
    return selected.filter(value => availableValues.has(this.normalizeText(value))).join(', ');
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
        this.loading = false;

        if (this.showPantryMatches) {
          this.applyFiltersAndSort();
        }
      },
      error: () => {
        this.matchingLoading = false;
        this.showPantryMatches = false;
        this.loading = false;
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

  public getIngredientMatchPercentage(recipe: Recipe): number {
    const ingredientCount = (recipe.ingredients ?? []).length;
    return ingredientCount > 0 ? this.getIngredientMatchScore(recipe) / ingredientCount : 0;
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
    const recipeCategories = new Set((recipe.categories ?? []).map(category => this.normalizeText(category)));
    const recipeTags = new Set((recipe.tags ?? []).map(tag => this.normalizeText(tag)));
    const categoryMatch = this.selectedCategoryFilters.every(category => recipeCategories.has(this.normalizeText(category)));
    const tagMatch = this.selectedTagFilters.every(tag => recipeTags.has(this.normalizeText(tag)));

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
    if (!this.sortOptions.includes(this.sortSetting)) {
      this.sortSetting = 'created';
      this.ascending = false;
    }

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
    } else if (this.creatorFilter) {
      filteredSource = filteredSource.filter(recipe => recipe.creator?.toLowerCase() === this.creatorFilter.toLowerCase());
    }

    const filtered = filteredSource.filter(recipe =>
      this.recipeMatchesFilters(recipe) &&
      this.getIngredientMatchScore(recipe) > 0
    );

    this.shownRecipes = [...filtered].sort((a, b) => {
      const nutritionComparison = this.compareRecipeNutrition(a, b);

      if (nutritionComparison !== 0) {
        return nutritionComparison;
      }

      const coverageComparison = this.getIngredientMatchPercentage(b) - this.getIngredientMatchPercentage(a);

      if (coverageComparison !== 0) {
        return coverageComparison;
      }

      const pantryComparison = this.getIngredientMatchScore(b) - this.getIngredientMatchScore(a);

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
      this.ascending = this.getDefaultSortDirection(sortSetting);
    } else {
      this.ascending = !this.ascending;
    }

    this.applyFiltersAndSort();
  }

  changeSortSetting(sortSetting: string): void {
    if (this.sortSetting === sortSetting) return;

    this.sortSetting = sortSetting;
    this.ascending = this.getDefaultSortDirection(sortSetting);
    this.applyFiltersAndSort();
  }

  private getDefaultSortDirection(sortSetting: string): boolean {
    return !['protein', 'fiber', 'rating', 'popularity'].includes(sortSetting);
  }

  private compareRecipeNutrition(left: Recipe, right: Recipe): number {
    switch (this.sortSetting) {
      case 'protein':
        return this.compareNullableNutrition(left.proteinGrams, right.proteinGrams);
      case 'carbohydrates':
        return this.compareNullableNutrition(left.carbohydrateGrams, right.carbohydrateGrams);
      case 'fiber':
        return this.compareNullableNutrition(left.fiberGrams, right.fiberGrams);
      case 'rating':
        return this.compareNullableNutrition(left.averageRating, right.averageRating);
      case 'popularity':
        return this.ascending
          ? (left.madeCount ?? 0) - (right.madeCount ?? 0)
          : (right.madeCount ?? 0) - (left.madeCount ?? 0);
      default:
        return 0;
    }
  }

  private compareNullableNutrition(left: number | null | undefined, right: number | null | undefined): number {
    if (left == null && right == null) return 0;
    if (left == null) return 1;
    if (right == null) return -1;
    return this.ascending ? left - right : right - left;
  }
}
