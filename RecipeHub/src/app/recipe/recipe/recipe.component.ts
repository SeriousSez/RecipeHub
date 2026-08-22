import { DatePipe } from '@angular/common';
import { Component, OnInit, ViewChildren } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Router } from '@angular/router';
import { ImageCroppedEvent } from 'ngx-image-cropper';
import { Subscription } from 'rxjs';
import { FavoriteRecipe } from 'src/app/shared/models/favorite-recipe.interface';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { UserService } from 'src/app/shared/services/user.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { SafeService } from 'src/app/shared/utils/safe.service';
import { Ingredient } from '../models/ingredient.interface';
import { RecipeUpdate } from '../models/recipe-update.interface';
import { Recipe } from '../models/recipe.interface';
import { RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS } from '../models/recipe-taxonomy';
import { IngredientService } from '../services/ingredient.service';
import { RecipeService } from '../services/recipe.service';
import { AngularEditorConfig } from '@kolkov/angular-editor';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-recipe',
  templateUrl: './recipe.component.html',
  styleUrls: ['./recipe.component.css'],
  standalone: false
})
export class RecipeComponent implements OnInit {
  private readonly pantryStorageKey = 'recipehub-pantry-ingredients';

  @ViewChildren("select") select: any;
  @ViewChildren("name") name: any;
  @ViewChildren("description") description: any;
  @ViewChildren("amount") amount: any;

  public measurements: string[] = ['Pinch or dash', 'Piece', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound', 'Clove']

  public title: string;
  public creator: string;
  public recipeId: string | null = null;

  public recipe: Recipe;
  public basePortions: number | null = null;
  public selectedPortions: number | null = null;
  public ingredientsToDelete: Ingredient[] = [];
  public ingredients: Ingredient[] = [];
  public currentIngredients: Ingredient[] = [];
  public pantryIngredients: string[] = [];
  public newIngredients: Ingredient[] = [];
  public newIngredient: Ingredient = { name: "", description: "", amount: 0, amountType: 'Pinch or dash', image: null, created: '' };
  public ingredientSearch: string = '';
  public categoriesInput: string = '';
  public tagsInput: string = '';
  public readonly categoryGroups = RECIPE_CATEGORY_GROUPS;
  public readonly tagGroups = RECIPE_TAG_GROUPS;
  public activeCategoryGroupId: string = RECIPE_CATEGORY_GROUPS[0].id;
  public activeTagGroupId: string = RECIPE_TAG_GROUPS[0].id;

  public edit: boolean = false;
  public canEdit: boolean = false;
  public showIngredients: boolean = true;
  public favored: boolean = false;
  public inGroceries: boolean = false;

  public errors: string = '';
  public savedOrCanceled: boolean = false;
  public submitted: boolean = false;
  public isRequesting: boolean = false;

  public originalImageUrl: string;
  public imageUrl: string;
  public fileToUpload: File | null;
  public imageChangedEvent: any = '';
  public croppedImage: any = '';
  public showCropOverlay = false;

  status: boolean = false;
  subscription?: Subscription;

  public editorConfig: AngularEditorConfig = {
    editable: true,
    spellcheck: true,
    height: '200px',
    maxHeight: 'auto',
    width: 'auto',
    minWidth: '0',
    translate: 'yes',
    enableToolbar: true,
    showToolbar: true,
    placeholder: 'Enter text here...',
    defaultParagraphSeparator: '',
    defaultFontName: '',
    defaultFontSize: '',
    fonts: [
      { class: 'arial', name: 'Arial' },
      { class: 'times-new-roman', name: 'Times New Roman' },
      { class: 'calibri', name: 'Calibri' },
      { class: 'comic-sans-ms', name: 'Comic Sans MS' }
    ],
    sanitize: true,
    toolbarPosition: 'top'
  };

  constructor(private activatedRoute: ActivatedRoute, private datepipe: DatePipe, private router: Router, public utilityService: UtilityService, private recipeService: RecipeService, private groceryService: GroceryService, private ingredientService: IngredientService, private userService: UserService, private safeService: SafeService, private favoriteService: FavoriteService, private translateService: TranslateService) {
    this.recipeId = activatedRoute.snapshot.params['id'] || null;
    this.title = this.utilityService.fromSlug(activatedRoute.snapshot.params['title']);
    this.creator = decodeURIComponent(activatedRoute.snapshot.params['creator'] || '');
  }

  ngOnInit(): void {
    this.loadPantryIngredients();
    this.status = this.userService.isAuthenticated();
    this.getRecipe();
    this.subscription = this.userService.authStatus$.subscribe(status => this.status = status || this.userService.isAuthenticated());
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
  }

  //#region favored
  isFavored(recipe: Recipe) {
    if (!this.userService.isAuthenticated()) {
      this.favored = false;
      return;
    }

    var username = this.userService.getUserName();
    if (username.length == 0 || username == '' || username == null) return;

    this.favoriteService.isFavored(username, recipe).subscribe(result => {
      this.favored = result;
    });
  }

  toggleFavorite() {
    if (!this.userService.isAuthenticated()) {
      return;
    }

    var model: FavoriteRecipe = {
      username: this.userService.getUserName(),
      recipe: {
        title: this.recipe.title,
        creator: this.recipe.creator
      } as Recipe
    };

    this.favoriteService.favoriteRecipe(model).subscribe(result => {
      this.favored = !this.favored;
    });
  }
  //#endregion

  //#region plan
  isInGroceries(recipe: Recipe) {
    this.inGroceries = this.groceryService.isInGroceries(recipe);
  }

  toggleGroceries() {
    if (!this.userService.isAuthenticated()) {
      return;
    }

    this.groceryService.toggleRecipeToList(this.recipe);
    this.inGroceries = !this.inGroceries;
  }
  //#endregion

  // #region setup
  getRecipe() {
    if (this.recipeId) {
      this.recipeService.getRecipeById(this.recipeId).subscribe((recipe: Recipe) => {
        this.setRecipeState(recipe);
      },
        error => {
          if (error?.status === 404) {
            this.recipeService.getRecipes().subscribe((recipes: Recipe[]) => {
              var matchingRecipe = recipes.find(r => r.id == this.recipeId);
              if (matchingRecipe == null) return;

              this.title = matchingRecipe.title;
              this.creator = matchingRecipe.creator;

              this.recipeService.getRecipe(this.title, this.creator).subscribe((recipe: Recipe) => {
                this.setRecipeState(recipe);
              },
                error => {
                  //this.notificationService.printErrorMessage(error);
                });
            },
              error => {
                //this.notificationService.printErrorMessage(error);
              });
          }
        });

      return;
    }

    this.recipeService.getRecipe(this.title, this.creator).subscribe((recipe: Recipe) => {
      this.setRecipeState(recipe);
    },
      error => {
        //this.notificationService.printErrorMessage(error);
      });
  }

  setRecipeState(recipe: Recipe) {
    this.recipe = recipe;
    this.basePortions = this.parseNumericPortions(recipe.portions);
    this.selectedPortions = this.basePortions;
    this.title = recipe.title;
    this.creator = recipe.creator;
    this.recipeId = recipe.id;
    this.categoriesInput = (recipe.categories ?? []).join(', ');
    this.tagsInput = (recipe.tags ?? []).join(', ');
    this.setCurrentIngredients();
    this.originalImageUrl = recipe.image != null ? recipe.image.url : "../assets/images/food.png";
    this.isFavored(recipe);
    this.isInGroceries(recipe);
    this.getIngredients();

    if (recipe.creator == this.userService.getUserName()) {
      this.canEdit = true;
    }
  }

  getIngredients() {
    this.ingredientService.getIngredients()
      .subscribe((ingredients: Ingredient[]) => {
        this.ingredients = ingredients;
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  setCurrentIngredients() {
    this.currentIngredients = [];

    if (this.recipe.ingredients != null) {
      this.recipe.ingredients.forEach(ingredient => {
        this.currentIngredients.push(this.createIngredientModel(ingredient));
      });
    }
  }

  private loadPantryIngredients(): void {
    if (typeof localStorage === 'undefined') {
      return;
    }

    this.pantryIngredients = (localStorage.getItem(this.pantryStorageKey) ?? '')
      .split(',')
      .map(item => this.normalizeIngredientText(item))
      .filter(Boolean);
  }

  public isPantryIngredient(name: string): boolean {
    const normalizedName = this.normalizeIngredientText(name);
    return normalizedName.length > 0 && this.pantryIngredients.some(item =>
      item === normalizedName || item.includes(normalizedName) || normalizedName.includes(item)
    );
  }

  private normalizeIngredientText(value: string | null | undefined): string {
    const normalized = (value ?? '')
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9\s]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    return normalized.split(' ').map(word => {
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
    }).join(' ');
  }
  //#endregion

  // #region update
  update() {
    this.submitted = true;
    this.isRequesting = true;
    this.errors = '';

    if (this.imageUrl && this.imageUrl !== this.originalImageUrl) {
      if (!this.recipe.image) {
        this.recipe.image = { id: '', url: this.imageUrl, caption: '' };
      } else {
        this.recipe.image.url = this.imageUrl;
      }
    }
    this.recipe.ingredients = this.currentIngredients;

    if (this.ingredientsToDelete.length > 0) {
      this.recipeService.deleteRecipeIngredient(this.ingredientsToDelete).subscribe(result => {
        this.ingredientsToDelete = [];
        this.isRequesting = false;
      }, errors => {
        this.isRequesting = false;
        this.errors = errors.error;
      });
    }

    if (this.newIngredients.length > 0) {
      this.recipeService.addIngredients(this.recipe, this.newIngredients).subscribe(result => {
        this.newIngredients = [];
        this.isRequesting = false;
      }, errors => {
        this.isRequesting = false;
        this.errors = errors.error;
      });
    }

    this.recipeService.update(this.createRecipeUpgradeModel(this.recipe)).subscribe(result => {
      this.router.navigate([
        `recipe/${this.recipe.id}/${this.utilityService.toSlug(this.recipe.title)}`
      ], { replaceUrl: true });

      this.title = this.recipe.title;
      this.creator = this.recipe.creator;
      this.recipeId = this.recipe.id;
      this.edit = false;
      this.isRequesting = false;
    }, errors => {
      this.isRequesting = false;
      this.errors = errors.error;
    });
  }

  displayDateOnly(created: string) {
    var day = this.datepipe.transform(created, 'dd');
    return this.getOrdinalNumber(day) + this.datepipe.transform(created, ' MMMM, yyyy');
  }

  formatPortions(portions: string | null | undefined): string {
    if (!portions || portions.trim() === '') {
      return this.translateService.instant('recipe.servesLabel', { portions: 1 });
    }

    const trimmed = portions.trim();
    const matches = trimmed.match(/^\d+(?:\s*-\s*\d+)?$/);

    if (!matches) {
      return this.translateService.instant('recipe.servesLabel', { portions: trimmed });
    }

    return this.translateService.instant('recipe.servesLabel', { portions: trimmed });
  }

  getTotalRecipeMinutes(): number | null {
    const preparationMinutes = this.recipe?.preparationMinutes ?? 0;
    const cookingMinutes = this.recipe?.cookingMinutes ?? 0;
    const totalMinutes = preparationMinutes + cookingMinutes;
    return totalMinutes > 0 ? totalMinutes : null;
  }

  adjustPortions(change: number): void {
    if (this.selectedPortions == null) {
      return;
    }

    this.selectedPortions = Math.max(1, Math.round((this.selectedPortions + change) * 10) / 10);
  }

  setSelectedPortions(value: number | string | null): void {
    const parsedValue = Number(value);
    if (!Number.isFinite(parsedValue) || parsedValue <= 0) {
      this.selectedPortions = this.basePortions;
      return;
    }

    this.selectedPortions = Math.round(parsedValue * 10) / 10;
  }

  getScaledIngredientAmount(amount: number | null | undefined): string {
    if (amount == null || !Number.isFinite(Number(amount))) {
      return '';
    }

    const scale = this.basePortions && this.selectedPortions
      ? this.selectedPortions / this.basePortions
      : 1;
    const scaledAmount = Number(amount) * scale;

    return new Intl.NumberFormat(undefined, {
      maximumFractionDigits: 2
    }).format(scaledAmount);
  }

  private parseNumericPortions(portions: string | null | undefined): number | null {
    const parsedValue = Number((portions ?? '').trim().replace(',', '.'));
    return Number.isFinite(parsedValue) && parsedValue > 0 ? parsedValue : null;
  }

  getMeasurementAbbreviation(measurement: string) {
    // 'Pinch or dash', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound'
    switch (measurement) {
      case 'Pinch or dash':
        return 'Pinch or dash'
      case 'Piece':
        return 'pcs'
      case 'Milliliter':
        return 'ml'
      case 'Liter':
        return 'l'
      case 'Teaspoon':
        return 'tsp'
      case 'Tablespoon':
        return 'tbs'
      case 'Cup':
        return 'cup'
      case 'Gram':
        return 'gram'
      case 'Kilogram':
        return 'kg'
      case 'Ounce':
        return 'oz'
      case 'Pound':
        return 'lb'
      case 'Clove':
        return 'Cloves'
      default:
        return '';
    }
  }

  getOrdinalNumber(day: string | null) {
    var result = Number(day);
    return result + (result > 0 ? ['th', 'st', 'nd', 'rd'][(result > 3 && result < 21) || result % 10 > 3 ? 0 : result % 10] : '');
  }

  selectExistingIngredient(name: string): void {
    const normalizedName = (name ?? '').trim().toLowerCase();
    const ingredient = this.ingredients.find(item => item.name.toLowerCase() === normalizedName);
    if (!ingredient) return;

    this.newIngredient.name = ingredient.name;
    this.newIngredient.description = ingredient.description;
  }

  addIngredient(event: any) {
    var date = this.datepipe.transform(Date.now(), "yyyy-MM-dd");
    if (date == null) return;

    if (event.Name != null) {
      var ingredient: Ingredient = { name: event.Name, description: event.Description, amount: event.Amount, amountType: event.AmountType, image: null, created: date.toString() }
      this.newIngredients.push(ingredient);
      this.currentIngredients.push(ingredient);
      this.resetIngredientInputs();
    } else {
      var measurement = this.newIngredient.amountType;
      var ingredient: Ingredient = { name: this.name.first.nativeElement.value, description: '', amount: this.amount.first.nativeElement.value, amountType: measurement, image: null, created: date.toString() }
      this.newIngredients.push(ingredient);
      this.currentIngredients.push(ingredient);
      this.resetIngredientInputs();
    }
  }

  resetIngredientInputs() {
    this.name.first.nativeElement.value = '';
    // this.description.first.nativeElement.value = '';
    this.amount.first.nativeElement.value = '';
    this.ingredientSearch = '';
  }

  removeIngredient(ingredient: Ingredient) {
    this.removeIngredientFromCurrent(ingredient);

    var index = this.recipe.ingredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.recipe.ingredients.splice(index, 1);
    }
  }

  removeIngredientFromCurrent(ingredient: Ingredient) {
    var index = this.currentIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      if (this.recipe.ingredients.some(i => i.name == ingredient.name)) {
        this.ingredientsToDelete.push(ingredient);
      }

      this.currentIngredients.splice(index, 1);
    }
  }
  //#endregion

  // #region model creation
  createIngredientModel(ingredient: Ingredient) {
    var model: Ingredient = {
      name: ingredient.name,
      description: ingredient.description,
      image: null,
      amount: ingredient.amount,
      amountType: ingredient.amountType,
      created: ingredient.created
    }

    return model;
  }

  getActiveTaxonomyValues(type: 'category' | 'tag'): string[] {
    const groups = type === 'category' ? this.categoryGroups : this.tagGroups;
    const activeGroupId = type === 'category' ? this.activeCategoryGroupId : this.activeTagGroupId;
    return groups.find(group => group.id === activeGroupId)?.values ?? [];
  }

  setActiveTaxonomyGroup(type: 'category' | 'tag', groupId: string): void {
    if (type === 'category') {
      this.activeCategoryGroupId = groupId;
    } else {
      this.activeTagGroupId = groupId;
    }
  }

  addPresetValue(type: 'category' | 'tag', value: string): void {
    const normalizedValue = (value ?? '').trim();
    if (!normalizedValue) {
      return;
    }

    const target = type === 'category' ? this.categoriesInput : this.tagsInput;
    const existingValues = this.parseCsv(target);

    if (existingValues.some(item => item.toLowerCase() === normalizedValue.toLowerCase())) {
      const updatedValues = existingValues.filter(item => item.toLowerCase() !== normalizedValue.toLowerCase());
      const formatted = updatedValues.map(item => item.charAt(0).toUpperCase() + item.slice(1).toLowerCase()).join(', ');

      if (type === 'category') {
        this.categoriesInput = formatted;
      } else {
        this.tagsInput = formatted;
      }

      return;
    }

    existingValues.push(normalizedValue);
    const formatted = existingValues.map(item => item.charAt(0).toUpperCase() + item.slice(1).toLowerCase()).join(', ');

    if (type === 'category') {
      this.categoriesInput = formatted;
    } else {
      this.tagsInput = formatted;
    }
  }

  public getSelectedValues(rawValue: string | string[] | null | undefined): string[] {
    return this.parseCsv(rawValue);
  }

  public isPresetSelected(type: 'category' | 'tag', value: string): boolean {
    const selectedValues = this.getSelectedValues(type === 'category' ? this.categoriesInput : this.tagsInput).map(item => item.toLowerCase());
    return selectedValues.includes(value.trim().toLowerCase());
  }

  private parseCsv(rawValue: string | string[] | null | undefined): string[] {
    if (!rawValue) {
      return [];
    }

    const items = Array.isArray(rawValue) ? rawValue : String(rawValue).split(',');

    return items
      .map(item => item.trim())
      .filter(item => item.length > 0)
      .map(item => item.charAt(0).toUpperCase() + item.slice(1).toLowerCase());
  }

  createRecipeUpgradeModel(recipe: Recipe) {
    const categories = this.parseCsv(this.categoriesInput);
    const tags = this.parseCsv(this.tagsInput);

    var model: RecipeUpdate = {
      oldTitle: this.title,
      title: recipe.title,
      creator: recipe.creator,
      description: recipe.description,
      instructions: recipe.instructions,
      portions: recipe.portions,
      preparationMinutes: recipe.preparationMinutes,
      cookingMinutes: recipe.cookingMinutes,
      shelfLifeDays: recipe.shelfLifeDays,
      canBeFrozen: recipe.canBeFrozen,
      created: recipe.created,
      image: recipe.image,
      ingredients: recipe.ingredients,
      categories: categories.length > 0 ? categories : (recipe.categories ?? []),
      tags: tags.length > 0 ? tags : (recipe.tags ?? []),
    }

    return model;
  }
  //#endregion

  // #region image
  handleFileInput(event: any) {
    if (event.target.files.length < 1) {
      this.imageUrl = "";
      this.showCropOverlay = false;
      return;
    }

    this.showCropOverlay = true;
    if (!this.recipe.image) {
      this.recipe.image = { id: '', url: '', caption: '' };
    }
    this.imageChangedEvent = event;
    this.fileToUpload = event.target.files.item(0);

    if (this.fileToUpload == null)
      return

    var reader = new FileReader();
    reader.onload = (event: any) => {
      this.imageUrl = event.target.result;
    }

    reader.readAsDataURL(this.fileToUpload);
  }

  removeImage() {
    this.imageUrl = this.originalImageUrl;
    this.savedOrCanceled = false;
    this.showCropOverlay = false;
  }

  cancelImageUpload() {
    this.imageUrl = this.originalImageUrl;
    this.savedOrCanceled = false;
    this.showCropOverlay = false;
  }

  imageCropped(event: ImageCroppedEvent) {
    if (event.base64 == null) return;

    this.imageUrl = event.base64;
    this.savedOrCanceled = true;
  }
  imageLoaded() {
    // show cropper
  }
  cropperReady() {
    // cropper ready
  }
  loadImageFailed() {
    // show message
  }

  setImageCaption(caption: string) {
    if (!this.recipe.image) {
      this.recipe.image = { id: '', url: '', caption: '' };
    }

    this.recipe.image.caption = caption;
  }

  toggleIngredients() {
    this.showIngredients = !this.showIngredients;
  }
  // #endregion
}

