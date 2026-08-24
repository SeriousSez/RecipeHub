import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { IngredientService } from 'src/app/recipe/services/ingredient.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { finalize } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { PantryService } from 'src/app/pantry/pantry.service';
import { UserService } from 'src/app/shared/services/user.service';
import { GroceryNearbyStore, GroceryOfferCategory, GroceryOfferGroup, GroceryOfferSearchResponse, GroceryShoppingPreference } from 'src/app/shared/models/grocery-offer-search.interface';

interface GroceryIngredientGroup {
  recipeTitle: string;
  recipeId: string | null;
  ingredients: Ingredient[];
}

interface GroceryIngredientSummary extends Ingredient {
  sourceIngredients: Ingredient[];
}

@Component({
  selector: 'app-grocery',
  templateUrl: './grocery.component.html',
  styleUrls: ['./grocery.component.css'],
  standalone: false
})
export class GroceryComponent implements OnInit {
  @ViewChild('ingredientModal') private ingredientModal: ElementRef;
  @ViewChild('deleteButton') private deleteButton: ElementRef;
  @ViewChild('ingredientEditor') private ingredientEditor?: ElementRef;

  targetUrl: string = '/dashboard/createingredients';

  ingredients: Ingredient[] = [];
  consolidatedIngredients: GroceryIngredientSummary[] = [];
  viewMode: 'all' | 'recipe' = 'all';
  loadingIngredientDetails: Set<string> = new Set<string>();
  loadedIngredientDetails: Set<string> = new Set<string>();

  selectedIngredients: Ingredient[] = [];
  addingToPantry: boolean = false;
  searchingNearbyOffers: boolean = false;
  nearbyOfferResults: GroceryOfferSearchResponse | null = null;
  nearbyOfferErrorKey: string = '';
  dealsCollapsed: boolean = false;
  offerSearchRadiusKm: number = 10;
  readonly offerRadiusOptions: string[] = ['5', '10', '25', '50'];
  offerShoppingPreference: GroceryShoppingPreference = 'balanced';
  readonly offerShoppingPreferences: GroceryShoppingPreference[] = ['balanced', 'budget', 'deals', 'organic', 'premium'];
  ingredientOfferCategories: Record<string, GroceryOfferCategory> = {};
  retryingOfferIngredients: Set<string> = new Set<string>();
  collapsedOfferIngredients: Set<string> = new Set<string>();
  readonly offerCategories: GroceryOfferCategory[] = ['auto', 'produce', 'dairy', 'meat', 'bakery', 'pantry', 'candy', 'chocolate', 'beverages'];
  private lastOfferLocation: { latitude: number; longitude: number } | null = null;
  confirmClearList: boolean = false;
  showAddIngredient: boolean = false;
  ingredientOptions: string[] = [];
  private ingredientNameLabels: Record<string, string> = {};
  draftIngredientName: string = '';
  draftIngredientAmount: number | null = null;
  draftIngredientUnit: string = 'Piece';
  editingIngredient: Ingredient | null = null;
  editIngredientName: string = '';
  editIngredientAmount: number | null = null;
  editIngredientUnit: string = 'Piece';
  readonly ingredientUnits: string[] = ['Piece', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound', 'Clove'];
  openedAccordion: string;
  clickedTableRow: string;

  public sortSetting: string = 'name';
  public ascending: boolean = true;

  constructor(private groceryService: GroceryService, private ingredientService: IngredientService, private datepipe: DatePipe, private router: Router, private translateService: TranslateService, private pantryService: PantryService, private userService: UserService) { }

  ngOnInit() {
    this.getIngredients();
    this.loadIngredientTranslations();
    this.translateService.onLangChange.subscribe(() => this.loadIngredientTranslations());
  }

  getIngredients() {
    this.ingredients = this.groceryService.getIngredientList();
    const summaries = new Map<string, GroceryIngredientSummary>();

    this.ingredients.forEach(ingredient => {
      const key = `${ingredient.name.trim().toLowerCase()}|${ingredient.amountType.trim().toLowerCase()}`;
      const summary = summaries.get(key);
      if (summary) {
        summary.amount += ingredient.amount;
        summary.sourceIngredients.push(ingredient);
      } else {
        summaries.set(key, {
          ...ingredient,
          sourceIngredients: [ingredient]
        });
      }
    });

    this.consolidatedIngredients = Array.from(summaries.values());
  }

  getIngredientDisplayName(ingredient: Ingredient): string {
    const lookupKey = ingredient.name?.trim();
    if (!lookupKey) return ingredient.name;
    return this.ingredientNameLabels[lookupKey.toLowerCase()] ?? ingredient.name;
  }

  getIngredientDisplayNameByName(name: string): string {
    const lookupKey = name?.trim();
    if (!lookupKey) return name;
    return this.ingredientNameLabels[lookupKey.toLowerCase()] ?? name;
  }

  private loadIngredientTranslations() {
    const languageCode = this.translateService.currentLang || 'en';
    const requestedLanguage = { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[languageCode] ?? 'English';

    this.ingredientService.getIngredientsLite(requestedLanguage).subscribe({
      next: ingredients => {
        this.ingredientNameLabels = Object.fromEntries(
          (ingredients ?? []).map(ingredient => [ingredient.name.toLowerCase(), ingredient.displayName ?? ingredient.name])
        );
        this.ingredientOptions = ingredients.map(ingredient => ingredient.name).sort((first, second) => first.localeCompare(second));
      },
      error: () => {
        this.ingredientNameLabels = {};
        this.ingredientOptions = [];
      }
    });
  }

  get ingredientCount() {
    return this.ingredients.length;
  }

  get selectedIngredientCount() {
    return this.selectedIngredients.length;
  }

  get hasIngredients() {
    return this.ingredientCount > 0;
  }

  get offerSearchIngredientCount() {
    return this.getOfferSearchIngredients().length;
  }

  get nearbyOfferGroups(): GroceryOfferGroup[] {
    if (!this.nearbyOfferResults) return [];

    const groups = new Map<string, GroceryOfferGroup>();
    this.nearbyOfferResults.offers.forEach(offer => {
      const key = offer.ingredientName.toLowerCase();
      const group = groups.get(key) ?? { ingredientName: offer.ingredientName, offers: [] };
      group.offers.push(offer);
      groups.set(key, group);
    });

    return Array.from(groups.values());
  }

  get groupedIngredients(): GroceryIngredientGroup[] {
    const groups = new Map<string, GroceryIngredientGroup>();

    this.ingredients.forEach(ingredient => {
      const recipeKey = ingredient.sourceRecipeId || ingredient.sourceRecipeTitle || 'manual';
      const recipeTitle = ingredient.sourceRecipeTitle?.trim() || this.translateService.instant('grocery.manualIngredients');
      const group: GroceryIngredientGroup = groups.get(recipeKey) ?? {
        recipeTitle,
        recipeId: ingredient.sourceRecipeId ?? null,
        ingredients: []
      };

      group.ingredients.push(ingredient);
      groups.set(recipeKey, group);
    });

    return Array.from(groups.values())
      .sort((first, second) => first.recipeTitle.localeCompare(second.recipeTitle));
  }

  get nearbyStores(): GroceryNearbyStore[] {
    return this.nearbyOfferResults?.stores.slice(0, 8) ?? [];
  }

  get sortLabel() {
    switch (this.sortSetting) {
      case 'amount':
        return this.translateService.instant('grocery.amountColumn');
      case 'created':
        return this.translateService.instant('grocery.createdColumn');
      default:
        return this.translateService.instant('grocery.ingredientColumn');
    }
  }

  get groceryListOverlayTitle() {
    return this.translateService.instant('grocery.addToGroceryListTitle');
  }

  addIngredientToList() {
    const name = this.draftIngredientName.trim().replace(/\s+/g, ' ');
    if (!name || this.draftIngredientAmount == null || this.draftIngredientAmount <= 0) return;

    const ingredient: Ingredient = {
      name,
      description: '',
      amount: this.draftIngredientAmount,
      amountType: this.draftIngredientUnit,
      created: new Date().toISOString(),
      image: null
    };

    this.groceryService.addIngredientsToList([ingredient]);
    this.getIngredients();
    this.draftIngredientName = '';
    this.draftIngredientAmount = null;
    this.draftIngredientUnit = 'Piece';
    this.showAddIngredient = false;
  }

  openAddIngredient() {
    this.showAddIngredient = true;
    setTimeout(() => this.ingredientEditor?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' }));
  }

  startEditingIngredient(ingredient: Ingredient) {
    this.editingIngredient = ingredient;
    this.editIngredientName = ingredient.name;
    this.editIngredientAmount = ingredient.amount;
    this.editIngredientUnit = ingredient.amountType;
  }

  isEditingIngredient(ingredient: Ingredient) {
    return this.editingIngredient === ingredient;
  }

  cancelEditingIngredient() {
    this.editingIngredient = null;
  }

  saveIngredientEdit() {
    if (!this.editingIngredient || this.editIngredientAmount == null || this.editIngredientAmount <= 0) return;

    const name = this.editIngredientName.trim().replace(/\s+/g, ' ');
    if (!name) return;

    const originalIngredient = this.editingIngredient;
    const originalIngredientName = originalIngredient.name;
    const updatedIngredient: Ingredient = {
      ...originalIngredient,
      name,
      amount: this.editIngredientAmount,
      amountType: this.editIngredientUnit
    };
    this.groceryService.updateIngredientInList(originalIngredient, updatedIngredient);

    delete this.ingredientOfferCategories[originalIngredientName];
    this.nearbyOfferResults = null;
    this.nearbyOfferErrorKey = '';
    this.editingIngredient = null;
    this.getIngredients();
  }

  removeSelectedFromIngredients() {
    [...this.selectedIngredients].forEach(ingredient => {
      this.groceryService.removeIngredientFromList(ingredient);
    });
    this.selectedIngredients = [];
    this.getIngredients();
  }

  addSelectedToPantry() {
    if (this.selectedIngredients.length === 0 || this.addingToPantry) return;

    const userId = this.userService.isAuthenticated() ? this.userService.getUserId() : undefined;
    this.addingToPantry = true;
    this.pantryService.addItems(this.selectedIngredients.map(ingredient => ({
      name: ingredient.name,
      amount: ingredient.amount,
      amountType: ingredient.amountType
    })), userId || undefined).pipe(finalize(() => this.addingToPantry = false)).subscribe({
      next: () => this.clearSelection()
    });
  }

  findNearbyOffers() {
    if (this.searchingNearbyOffers || !this.hasIngredients) return;

    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      this.nearbyOfferErrorKey = 'grocery.locationUnavailable';
      return;
    }

    this.searchingNearbyOffers = true;
    this.nearbyOfferErrorKey = '';

    navigator.geolocation.getCurrentPosition(position => {
      const ingredients = this.getOfferSearchIngredients();
      this.lastOfferLocation = { latitude: position.coords.latitude, longitude: position.coords.longitude };
      this.groceryService.findNearbyOffers({
        ingredientNames: ingredients.map(ingredient => ingredient.name),
        ingredientCategories: this.getCategoryOverrides(ingredients.map(ingredient => ingredient.name)),
        shoppingPreference: this.offerShoppingPreference,
        countryCode: this.getOfferCountryCode(),
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        radiusKm: this.offerSearchRadiusKm
      }).pipe(finalize(() => this.searchingNearbyOffers = false)).subscribe({
        next: result => {
          this.nearbyOfferResults = result;
          this.dealsCollapsed = false;
          this.collapsedOfferIngredients.clear();
        },
        error: error => this.setNearbyOfferError(error)
      });
    }, error => {
      this.searchingNearbyOffers = false;
      this.nearbyOfferErrorKey = error.code === 1 ? 'grocery.locationPermissionDenied' : 'grocery.locationUnavailable';
    }, {
      enableHighAccuracy: false,
      timeout: 10000,
      maximumAge: 300000
    });
  }

  getOfferCategory(ingredientName: string): GroceryOfferCategory {
    return this.ingredientOfferCategories[ingredientName] ?? 'auto';
  }

  get offerRadiusLabels(): Record<string, string> {
    return this.offerRadiusOptions.reduce((labels, radius) => {
      labels[radius] = `${radius} km`;
      return labels;
    }, {} as Record<string, string>);
  }

  get offerCategoryLabels(): Record<string, string> {
    return this.offerCategories.reduce((labels, category) => {
      labels[category] = this.translateService.instant(`grocery.offerCategories.${category}`);
      return labels;
    }, {} as Record<string, string>);
  }

  get offerShoppingPreferenceLabels(): Record<string, string> {
    return this.offerShoppingPreferences.reduce((labels, preference) => {
      labels[preference] = this.translateService.instant(`grocery.offerShoppingPreferences.${preference}`);
      return labels;
    }, {} as Record<string, string>);
  }

  setOfferSearchRadius(radius: string) {
    this.offerSearchRadiusKm = Number(radius);
  }

  setOfferShoppingPreference(preference: GroceryShoppingPreference) {
    this.offerShoppingPreference = preference;
    if (this.nearbyOfferResults || this.nearbyOfferErrorKey) {
      this.findNearbyOffers();
    }
  }

  toggleDealsCollapsed() {
    this.dealsCollapsed = !this.dealsCollapsed;
  }

  isIngredientDealsCollapsed(ingredientName: string) {
    return this.collapsedOfferIngredients.has(ingredientName.toLowerCase());
  }

  toggleIngredientDealsCollapsed(ingredientName: string) {
    const ingredientKey = ingredientName.toLowerCase();
    if (this.collapsedOfferIngredients.has(ingredientKey)) {
      this.collapsedOfferIngredients.delete(ingredientKey);
    } else {
      this.collapsedOfferIngredients.add(ingredientKey);
    }
  }

  setOfferCategory(ingredientName: string, category: GroceryOfferCategory) {
    this.ingredientOfferCategories[ingredientName] = category;
  }

  isRetryingIngredient(ingredientName: string) {
    return this.retryingOfferIngredients.has(ingredientName);
  }

  retryIngredientOffers(ingredientName: string) {
    if (!this.lastOfferLocation || this.isRetryingIngredient(ingredientName)) return;

    this.retryingOfferIngredients.add(ingredientName);
    this.nearbyOfferErrorKey = '';
    this.groceryService.findNearbyOffers({
      ingredientNames: [ingredientName],
      ingredientCategories: this.getCategoryOverrides([ingredientName]),
      shoppingPreference: this.offerShoppingPreference,
      countryCode: this.getOfferCountryCode(),
      forceRefresh: true,
      latitude: this.lastOfferLocation.latitude,
      longitude: this.lastOfferLocation.longitude,
      radiusKm: this.offerSearchRadiusKm
    }).pipe(finalize(() => this.retryingOfferIngredients.delete(ingredientName))).subscribe({
      next: result => this.mergeIngredientOfferResult(ingredientName, result),
      error: error => this.setNearbyOfferError(error)
    });
  }

  getStoreMapUrl(store: GroceryNearbyStore) {
    return `https://www.google.com/maps/search/?api=1&query=${store.latitude},${store.longitude}`;
  }

  private getOfferSearchIngredients() {
    return this.selectedIngredientCount > 0 ? this.selectedIngredients : this.ingredients;
  }

  private getOfferCountryCode(): 'DK' | 'EE' | 'TR' {
    return { da: 'DK', et: 'EE', tr: 'TR' }[this.translateService.currentLang] as 'DK' | 'EE' | 'TR' ?? 'DK';
  }

  private getCategoryOverrides(ingredientNames: string[]) {
    return ingredientNames.reduce((categories, ingredientName) => {
      categories[ingredientName] = this.getOfferCategory(ingredientName);
      return categories;
    }, {} as Record<string, GroceryOfferCategory>);
  }

  private mergeIngredientOfferResult(ingredientName: string, result: GroceryOfferSearchResponse) {
    this.collapsedOfferIngredients.delete(ingredientName.toLowerCase());
    if (!this.nearbyOfferResults) {
      this.nearbyOfferResults = result;
      return;
    }

    const ingredientKey = ingredientName.toLowerCase();
    this.nearbyOfferResults = {
      ...this.nearbyOfferResults,
      stores: result.stores.length > 0 ? result.stores : this.nearbyOfferResults.stores,
      offers: [
        ...this.nearbyOfferResults.offers.filter(offer => offer.ingredientName.toLowerCase() !== ingredientKey),
        ...result.offers
      ],
      unmatchedIngredients: [
        ...this.nearbyOfferResults.unmatchedIngredients.filter(name => name.toLowerCase() !== ingredientKey),
        ...result.unmatchedIngredients
      ],
      generatedAtUtc: result.generatedAtUtc
    };
  }

  private setNearbyOfferError(error: any) {
    const code = error?.error?.code;
    const isProviderNotConfigured = code === 'grocery_provider_not_configured' || code === 'shelfatlas_not_configured';
    const isRateLimited = error?.status === 429 || code === 'grocery_provider_rate_limited' || code === 'shelfatlas_rate_limited';

    this.nearbyOfferErrorKey = isProviderNotConfigured
      ? 'grocery.offerSearchNotConfigured'
      : isRateLimited
        ? 'grocery.offerSearchRateLimited'
        : 'grocery.offerSearchFailed';
  }

  removeIngredient(ingredient: Ingredient) {
    if (this.editingIngredient === ingredient) this.cancelEditingIngredient();
    const summary = ingredient as Partial<GroceryIngredientSummary>;
    (summary.sourceIngredients ?? [ingredient]).forEach(sourceIngredient => {
      this.groceryService.removeIngredientFromList(sourceIngredient);
    });
    this.selectedIngredients = this.selectedIngredients.filter(item => item !== ingredient);
    this.getIngredients();
  }

  clearGroceryList() {
    this.groceryService.clearRecipeList();
    this.selectedIngredients = [];
    this.confirmClearList = false;
    this.nearbyOfferResults = null;
    this.nearbyOfferErrorKey = '';
    this.getIngredients();
  }

  toggleIngredientSelected(ingredient: Ingredient) {
    var index = this.selectedIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.selectedIngredients.splice(index, 1);
    } else {
      this.selectedIngredients.push(ingredient);
    }
  }

  isIngredientSelected(ingredient: Ingredient) {
    return this.selectedIngredients.indexOf(ingredient, 0) > -1;
  }

  clearSelection() {
    this.selectedIngredients = [];
  }

  removeIngredientFromList(ingredient: Ingredient) {
    var index = this.ingredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.ingredients.splice(index, 1);
    } else {
      this.ingredients.push(ingredient);
    }

    this.getIngredients();
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  sort(sortSetting: string) {
    this.toggleAccordion(this.openedAccordion, this.clickedTableRow);
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    const ingredients = this.viewMode === 'all' ? this.consolidatedIngredients : this.ingredients;

    switch (sortSetting) {
      case 'name':
        ingredients.sort((a, b) => this.ascending == true ? a.name.localeCompare(b.name) : -(a.name.localeCompare(b.name)));
        this.ascending = !this.ascending;
        return;
      case 'amount':
        ingredients.sort((a, b) => this.ascending == true
          ? (a.amount - b.amount) || a.name.localeCompare(b.name)
          : (b.amount - a.amount) || b.name.localeCompare(a.name));
        this.ascending = !this.ascending;
        return;
      case 'created':
        ingredients.sort((a, b) => this.ascending == true ? a.created.localeCompare(b.created) : -a.created.localeCompare(b.created));
        this.ascending = !this.ascending;
        return;
    }
  }

  public closeIngredientModal(ingredient: Ingredient) {
    this.ingredients.push(ingredient);
    this.ingredientModal.nativeElement.click();
  }

  public closeDeleteModal() {
    this.deleteButton.nativeElement.click();
  }

  toggleAccordion(id: string, tableRowId: string) {
    this.toggleTableRowClass(tableRowId);

    var accordion = document.getElementById(id);
    if (accordion == null) return;

    this.handleAccordionStyle(accordion, id, tableRowId);
  }

  handleAccordionStyle(accordion: HTMLElement, id: string, tableRowId: string) {
    if (accordion.style.display == 'table-cell') {
      accordion.style.display = 'none';
      this.openedAccordion = '';
      this.clickedTableRow = '';
    } else {
      this.toggleAccordion(this.openedAccordion, this.clickedTableRow);
      accordion.style.display = 'table-cell';
      this.openedAccordion = id;
      this.clickedTableRow = tableRowId;
    }
  }

  toggleTableRowClass(tableRowId: string) {
    if (tableRowId != '') {
      var tableRow = document.getElementById(tableRowId);
      if (tableRow?.classList.contains("collapsed")) {
        tableRow?.classList.remove("collapsed");
      } else {
        tableRow?.classList.add("collapsed");
      }
    }
  }

  toggleAccordionAndLoad(ingredient: Ingredient, id: string, tableRowId: string) {
    this.loadIngredientDetailsIfNeeded(ingredient);
    this.toggleAccordion(id, tableRowId);
  }

  handleIngredientClick(ingredient: Ingredient, id: string, tableRowId: string) {
    if (this.isMobileViewport()) {
      this.toggleIngredientSelected(ingredient);
      return;
    }

    this.toggleAccordionAndLoad(ingredient, id, tableRowId);
  }

  isIngredientDetailsLoading(ingredient: Ingredient) {
    return this.loadingIngredientDetails.has(this.getIngredientKey(ingredient));
  }

  private loadIngredientDetailsIfNeeded(ingredient: Ingredient) {
    const key = this.getIngredientKey(ingredient);
    if (key == '' || this.loadingIngredientDetails.has(key) || this.loadedIngredientDetails.has(key)) {
      return;
    }

    this.loadingIngredientDetails.add(key);
    this.ingredientService.getIngredientByName(ingredient.name)
      .pipe(finalize(() => this.loadingIngredientDetails.delete(key)))
      .subscribe((result: Ingredient) => {
        if (!result) {
          return;
        }

        ingredient.image = result.image;
        ingredient.description = result.description;
        this.loadedIngredientDetails.add(key);
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  private getIngredientKey(ingredient: Ingredient) {
    return ingredient?.name?.trim().toLowerCase() ?? '';
  }

  private isMobileViewport() {
    return typeof window !== 'undefined' && window.innerWidth <= 600;
  }
}
