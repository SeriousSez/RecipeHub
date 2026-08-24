import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ImageCroppedEvent, ImageCropperComponent, LoadedImage } from 'ngx-image-cropper';
import { AngularEditorConfig } from '@kolkov/angular-editor';
import { IngredientCreation } from 'src/app/shared/models/ingredient.creation.interface';
import { RecipeCreation } from 'src/app/shared/models/recipe.creation.interface';
import { UserService } from 'src/app/shared/services/user.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { Ingredient } from '../models/ingredient.interface';
import { RecipeTaxonomyGroup, RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS, sortRecipeTaxonomyValues, getTaxonomyValueLabel } from '../models/recipe-taxonomy';
import { IngredientService } from '../services/ingredient.service';
import { RecipeService } from '../services/recipe.service';
import { TaxonomySelectComponent } from '../taxonomy-select/taxonomy-select.component';
import { TranslateService } from '@ngx-translate/core';
import { finalize } from 'rxjs/operators';
import { NutritionEstimate } from '../models/nutrition-estimate.interface';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-create',
  templateUrl: './create.component.html',
  styleUrls: ['./create.component.css'],
  standalone: false
})
export class CreateComponent implements OnInit, OnDestroy {
  imageAspectRatio = 4 / 3;
  imageCropperWidth = 'min(100%, 853px, 93.33vh)';
  public measurements: string[] = ['Gram', 'Milliliter', 'Piece', 'Teaspoon', 'Tablespoon', 'Cup', 'Kilogram', 'Liter', 'Pinch or dash', 'Clove', 'To taste', 'Ounce', 'Pound']
  public readonly measurementGroups: RecipeTaxonomyGroup[] = [
    { id: 'common', labelKey: 'recipe.measurementGroups.common', values: ['Piece', 'Teaspoon', 'Tablespoon', 'Cup'] },
    { id: 'metric', labelKey: 'recipe.measurementGroups.metric', values: ['Gram', 'Kilogram', 'Milliliter', 'Liter'] },
    { id: 'imperial', labelKey: 'recipe.measurementGroups.imperial', values: ['Ounce', 'Pound'] },
    { id: 'other', labelKey: 'recipe.measurementGroups.other', values: ['Pinch or dash', 'Clove', 'To taste'] }
  ];
  public get measurementLabels(): Record<string, string> {
    return this.measurements.reduce((labels, measurement) => {
      labels[measurement] = this.translateService.instant(`pantry.units.${measurement}`);
      return labels;
    }, {} as Record<string, string>);
  }
  public languages: string[] = ['Danish', 'English', 'Estonian', 'Turkish']

  @ViewChild("select", { static: true }) select: ElementRef;
  @ViewChild('imageCropper') imageCropper?: ImageCropperComponent;
  @ViewChild('ingredientNameSelect') ingredientNameSelect?: TaxonomySelectComponent;
  //#region preview
  public fakeInstructions: string = "<p><em><strong>Spice</strong></em></p><p><tt>An aromatic or pungent vegetable substance used to flavour food, e.g. cloves, pepper, or cumin.</tt></p><p><img alt='Get to Know Your SPICEs - Zuken US' src='https://www.zuken.com/us/wp-content/uploads/sites/12/2020/06/BL0236-spices-1280x620-1.jpg' style='height:100%; width:100%' /></p><p><q><cite><small>He ordered his regular breakfast. Two eggs sunnyside up, hash browns, and two strips of bacon. He continued to look at the menu wondering if this would be the day he added something new. This was also part of the routine. A few seconds of hesitation to see if something else would be added to the order before demuring and saying that would be all. It was the same exact meal that he had ordered every day for the past two years.</small></cite></q></p>";
  public fakeDescription: string = "A spice is a seed, fruit, root, bark, or other plant substance primarily used for flavoring or coloring food. Spices are distinguished from herbs, which are the leaves, flowers, or stems of plants used for flavoring or as a garnish. Spices are sometimes used in medicine, religious rituals, cosmetics or perfume production.";
  public recipePreview: boolean = true;
  public editorView: 'edit' | 'preview' = 'edit';
  //#endregion

  public recipeForm: UntypedFormGroup;
  public ingredientForm: UntypedFormGroup;

  public errors: string = '';
  public isRequesting: boolean = false;
  public submitted: boolean = false;
  public estimatingNutrition: boolean = false;
  public nutritionEstimateMessageKey: string = '';
  public nutritionUnmatchedCount: number = 0;
  public nutritionEstimateProvider: string = '';

  public defaultIngredient: IngredientCreation = { name: "", description: "", amount: 0, amountType: 'Pinch or dash', group: '', imageCaption: "", image: null, created: '' };
  public newIngredient: IngredientCreation;
  public newIngredients: IngredientCreation[] = [];
  public movingIngredientGroup: string | null = null;
  public movingIngredient: IngredientCreation | null = null;
  public draggedIngredientGroup: string | null = null;
  public draggedIngredient: IngredientCreation | null = null;
  public ingredientGroupNames: string[] = [];
  public newIngredientGroupName: string = '';
  public activeIngredientGroup: string | null = '';
  public editingIngredientGroup: string | null = null;
  public ingredientGroupRenameValue: string = '';
  public ingredients: Ingredient[];
  public categoriesInput: string = '';
  public tagsInput: string = '';
  public readonly categoryGroups = RECIPE_CATEGORY_GROUPS;
  public readonly tagGroups = RECIPE_TAG_GROUPS;
  public activeCategoryGroupId: string = RECIPE_CATEGORY_GROUPS[0].id;
  public activeTagGroupId: string = RECIPE_TAG_GROUPS[0].id;

  get ingredientEditorGroups(): Array<{ name: string; ingredients: IngredientCreation[] }> {
    const groups = new Map<string, IngredientCreation[]>([['', []]]);
    this.ingredientGroupNames.forEach(groupName => groups.set(groupName, []));

    for (const ingredient of this.newIngredients) {
      const groupName = ingredient.group?.trim() ?? '';
      const groupIngredients = groups.get(groupName) ?? [];
      groupIngredients.push(ingredient);
      groups.set(groupName, groupIngredients);
    }

    return Array.from(groups, ([name, ingredients]) => ({ name, ingredients }));
  }

  public defaultImageUrl: string = "../../assets/images/food.png";
  public imageUrl: string;
  public fileToUpload: File | null;
  public savedOrCanceled: boolean = false;
  public imageChangedEvent: any = '';
  public croppedImage: any = '';
  public showCropOverlay = false;

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

  constructor(public utilityService: UtilityService, private recipeService: RecipeService, private ingredientService: IngredientService, public userService: UserService, private router: Router, private formBuilder: UntypedFormBuilder, private translateService: TranslateService, private datepipe: DatePipe) { }

  public get badExampleTitle(): string {
    return this.translateService.instant('recipe.badExampleTitle');
  }

  public get previewImageAlt(): string {
    return this.translateService.instant('recipe.previewImageAlt');
  }

  public get ingredientOptions(): string[] {
    return this.ingredients?.map(ingredient => ingredient.name) ?? [];
  }

  public get ingredientOptionLabels(): Record<string, string> {
    return Object.fromEntries((this.ingredients ?? []).map(ingredient => [ingredient.name, ingredient.displayName ?? ingredient.name]));
  }

  private languageSubscription?: Subscription;
  private ingredientRequestId = 0;

  ngOnInit(): void {
    this.getIngredients(this.translateService.currentLang || 'en');
    this.languageSubscription = this.translateService.onLangChange.subscribe(event => this.getIngredients(event.lang));

    this.newIngredient = { ...this.defaultIngredient };
    this.imageUrl = this.defaultImageUrl;
    this.recipeForm = this.formBuilder.group({
      title: ['', Validators.required],
      description: ['', Validators.required],
      language: [this.getUiLanguage(), Validators.required],
      instructions: ['', Validators.required],
      portions: ['', Validators.required],
      preparationMinutes: [null, Validators.min(0)],
      cookingMinutes: [null, Validators.min(0)],
      proofingMinutes: [null, Validators.min(0)],
      chillingMinutes: [null, Validators.min(0)],
      coolingMinutes: [null, Validators.min(0)],
      restingMinutes: [null, Validators.min(0)],
      shelfLifeDays: [null, Validators.min(0)],
      canBeFrozen: [false],
      calories: [null, Validators.min(0)],
      proteinGrams: [null, Validators.min(0)],
      carbohydrateGrams: [null, Validators.min(0)],
      fatGrams: [null, Validators.min(0)],
      fiberGrams: [null, Validators.min(0)],
      sugarGrams: [null, Validators.min(0)],
      sodiumMilligrams: [null, Validators.min(0)],
      imageCaption: [''],
      image: [null, Validators.required],
      categories: [''],
      tags: [''],
      ingredients: []
    });

    this.ingredientForm = this.formBuilder.group({
      name: ['', Validators.required],
      description: ['']
    });

  }

  ngOnDestroy(): void {
    this.languageSubscription?.unsubscribe();
  }

  getIngredients(languageCode: string = this.translateService.currentLang || 'en') {
    const requestId = ++this.ingredientRequestId;
    const requestedLanguage = this.mapUiLanguage(languageCode);
    this.ingredientService.getIngredientsLite(requestedLanguage)
      .subscribe((ingredients: Ingredient[]) => {
        if (requestId !== this.ingredientRequestId) return;
        this.ingredients = ingredients;
        const collator = new Intl.Collator(languageCode, { sensitivity: 'base', numeric: true });
        this.ingredients.sort((first, second) => collator.compare(
          first.displayName ?? first.name,
          second.displayName ?? second.name
        ));
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  create({ value, valid }: { value: RecipeCreation, valid: boolean }) {
    this.submitted = true;
    this.errors = '';

    if (!valid) {
      this.editorView = 'edit';
      this.recipeForm.markAllAsTouched();
      return;
    }

    this.isRequesting = true;

    value.creator = this.userService.getUserName();
    value.imageUrl = this.imageUrl;
    value.image = { url: this.imageUrl, caption: value.imageCaption }
    const groupOrders = new Map<string, number>();
    const ingredientOrders = new Map<string, number>();
    const orderedIngredients = this.ingredientEditorGroups.flatMap(group => group.ingredients);
    value.ingredients = orderedIngredients.map(ingredient => {
      const groupName = ingredient.group?.trim() ?? '';
      if (!groupOrders.has(groupName)) groupOrders.set(groupName, groupOrders.size);
      const ingredientOrder = ingredientOrders.get(groupName) ?? 0;
      ingredientOrders.set(groupName, ingredientOrder + 1);
      return { ...ingredient, groupOrder: groupOrders.get(groupName), ingredientOrder };
    });
    value.categories = this.parseCsv(value.categories ?? this.categoriesInput);
    value.tags = this.parseCsv(value.tags ?? this.tagsInput);

    this.recipeService.create(value)
      .subscribe(result => {
        this.router.navigate([`/recipe/${this.utilityService.toRecipeKey(result.id, result.title)}`]);
      }, errors => {
        this.isRequesting = false;
        this.errors = errors.error;
      });
  }

  public estimateNutrition(): void {
    if (this.estimatingNutrition || this.newIngredients.length === 0) return;

    const portions = Number(String(this.recipeForm.get('portions')?.value ?? '').replace(',', '.')) || 1;
    const instructions = String(this.recipeForm.get('instructions')?.value ?? '');
    this.estimatingNutrition = true;
    this.nutritionEstimateMessageKey = '';
    this.recipeService.estimateNutrition(this.newIngredients, portions, instructions)
      .pipe(finalize(() => this.estimatingNutrition = false))
      .subscribe({
        next: estimate => this.applyNutritionEstimate(estimate),
        error: () => this.nutritionEstimateMessageKey = 'recipe.nutritionEstimateFailed'
      });
  }

  private applyNutritionEstimate(estimate: NutritionEstimate): void {
    this.nutritionUnmatchedCount = estimate.unmatchedIngredients?.length ?? 0;
    this.nutritionEstimateProvider = estimate.provider ?? '';
    if (estimate.estimatedIngredientCount === 0) {
      this.nutritionEstimateMessageKey = estimate.errorCode === 'insufficient_quota'
        ? 'recipe.nutritionEstimateQuotaExceeded'
        : estimate.errorCode
          ? 'recipe.nutritionEstimateRateLimited'
          : 'recipe.nutritionEstimateNoResults';
      return;
    }

    this.recipeForm.patchValue({
      calories: estimate.calories,
      proteinGrams: estimate.proteinGrams,
      carbohydrateGrams: estimate.carbohydrateGrams,
      fatGrams: estimate.fatGrams,
      fiberGrams: estimate.fiberGrams,
      sugarGrams: estimate.sugarGrams,
      sodiumMilligrams: estimate.sodiumMilligrams
    });
    this.nutritionEstimateMessageKey = this.nutritionUnmatchedCount > 0
      ? 'recipe.nutritionEstimatePartial'
      : 'recipe.nutritionEstimateComplete';
  }

  public getSelectedValues(rawValue: string | string[] | null | undefined): string[] {
    return this.parseCsv(rawValue);
  }

  public getPreviewCategoryBadges(): Array<{ value: string; cssClass: string; label: string; displayValue: string }> {
    return sortRecipeTaxonomyValues(this.getSelectedValues(this.recipeForm.get('categories')?.value), RECIPE_CATEGORY_GROUPS)
      .map(category => this.getPreviewBadge(category, 'category'));
  }

  public getPreviewTagBadges(): Array<{ value: string; cssClass: string; label: string; displayValue: string }> {
    return sortRecipeTaxonomyValues(this.getSelectedValues(this.recipeForm.get('tags')?.value), RECIPE_TAG_GROUPS)
      .map(tag => this.getPreviewBadge(tag, 'tag'));
  }

  public getPreviewBadges(): Array<{ value: string; cssClass: string; label: string; displayValue: string }> {
    const seen = new Set<string>();
    return [...this.getPreviewCategoryBadges(), ...this.getPreviewTagBadges()].filter(badge => {
      const normalizedValue = badge.value.toLowerCase();
      if (seen.has(normalizedValue)) return false;
      seen.add(normalizedValue);
      return true;
    });
  }

  public get previewIngredientGroups(): Array<{ name: string; ingredients: IngredientCreation[] }> {
    const groups = new Map<string, IngredientCreation[]>();
    this.newIngredients.forEach(ingredient => {
      const groupName = ingredient.group?.trim() ?? '';
      const ingredients = groups.get(groupName) ?? [];
      ingredients.push(ingredient);
      groups.set(groupName, ingredients);
    });
    return Array.from(groups, ([name, ingredients]) => ({ name, ingredients }));
  }

  public getPreviewTotalMinutes(): number | null {
    const fields = ['preparationMinutes', 'cookingMinutes', 'proofingMinutes', 'chillingMinutes', 'coolingMinutes', 'restingMinutes'];
    const totalMinutes = fields.reduce((total, field) => total + (Number(this.recipeForm.get(field)?.value) || 0), 0);
    return totalMinutes > 0 ? totalMinutes : null;
  }

  public formatPreviewDuration(totalMinutes: number): string {
    if (totalMinutes < 60) return `${totalMinutes} ${this.translateService.instant('recipe.minutesShort')}`;

    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return minutes === 0
      ? `${hours} ${this.translateService.instant('recipe.hoursShort')}`
      : this.translateService.instant('recipe.hoursMinutesDuration', { hours, minutes });
  }

  public formatPreviewPortions(): string {
    const portions = String(this.recipeForm.get('portions')?.value ?? '').trim() || '1';
    return this.translateService.instant('recipe.servesLabel', { portions });
  }

  public formatPreviewIngredientAmount(amount: number | null | undefined): string {
    if (amount == null || !Number.isFinite(Number(amount))) return '';

    const numericAmount = Number(amount);
    const wholeAmount = Math.floor(numericAmount);
    if (Math.abs(numericAmount - wholeAmount - .5) < .001) return wholeAmount > 0 ? `${wholeAmount} 1/2` : '1/2';
    return new Intl.NumberFormat(undefined, { maximumFractionDigits: 2 }).format(numericAmount);
  }

  public getPreviewMeasurementAbbreviation(measurement: string): string {
    const abbreviations: Record<string, string> = {
      'To taste': 'to taste',
      'Pinch or dash': 'Pinch or dash',
      Piece: 'pcs',
      Milliliter: 'ml',
      Liter: 'l',
      Teaspoon: 'tsp',
      Tablespoon: 'tbs',
      Cup: 'cup',
      Gram: 'gram',
      Kilogram: 'kg',
      Ounce: 'oz',
      Pound: 'lb',
      Clove: 'Cloves'
    };
    return abbreviations[measurement] ?? measurement;
  }

  public getPreviewDate(): string | null {
    const day = this.datepipe.transform(new Date(), 'dd');
    const dayNumber = Number(day);
    const suffix = dayNumber > 0 ? ['th', 'st', 'nd', 'rd'][(dayNumber > 3 && dayNumber < 21) || dayNumber % 10 > 3 ? 0 : dayNumber % 10] : '';
    return `${dayNumber}${suffix}${this.datepipe.transform(new Date(), ' MMMM, yyyy')}`;
  }

  private getPreviewBadge(value: string, type: 'category' | 'tag'): { value: string; cssClass: string; label: string; displayValue: string } {
    const groups = type === 'category' ? RECIPE_CATEGORY_GROUPS : RECIPE_TAG_GROUPS;
    const normalizedValue = value.toLowerCase();
    const group = groups.find(item => item.values.some(groupValue => groupValue.toLowerCase() === normalizedValue));
    const groupLabel = this.translateService.instant(group?.labelKey ?? 'recipe.taxonomyGroups.custom');
    const label = this.translateService.instant(type === 'category' ? 'recipe.categoryBadgeTooltip' : 'recipe.tagBadgeTooltip', { value, group: groupLabel });
    return { value, cssClass: `recipe-${type} recipe-${type}-${group?.id ?? 'other'}`, label, displayValue: getTaxonomyValueLabel(value, this.translateService) };
  }

  public getTaxonomyValueDisplayLabel(value: string): string {
    return getTaxonomyValueLabel(value, this.translateService);
  }

  public isPresetSelected(type: 'category' | 'tag', value: string): boolean {
    const fieldName = type === 'category' ? 'categories' : 'tags';
    const selectedValues = this.getSelectedValues(this.recipeForm.get(fieldName)?.value).map(item => item.toLowerCase());
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
    const fieldName = type === 'category' ? 'categories' : 'tags';
    const existingValues = this.parseCsv(this.recipeForm.get(fieldName)?.value ?? '');
    const normalizedValue = value.trim();
    const updatedValues = existingValues.some(item => item.toLowerCase() === normalizedValue.toLowerCase())
      ? existingValues.filter(item => item.toLowerCase() !== normalizedValue.toLowerCase())
      : [...existingValues, normalizedValue];

    this.recipeForm.get(fieldName)?.setValue(updatedValues.join(', '));
  }

  selectIngredientName(name: string): void {
    const normalizedName = (name ?? '').trim().toLowerCase();
    const ingredient = this.ingredients?.find(item => item.name.toLowerCase() === normalizedName) ?? null;
    this.newIngredient.name = name;
    this.newIngredient.description = ingredient?.description ?? '';
  }

  updateIngredientName(ingredientToUpdate: IngredientCreation, name: string): void {
    const normalizedName = (name ?? '').trim().toLowerCase();
    const ingredient = this.ingredients?.find(item => item.name.toLowerCase() === normalizedName) ?? null;
    ingredientToUpdate.name = name;
    ingredientToUpdate.description = ingredient?.description ?? '';
    ingredientToUpdate.language = ingredient ? 'English' : this.getUiLanguage();
  }

  selectMeasurement(measurement: string): void {
    this.newIngredient.amountType = measurement;
    if (measurement === 'To taste') {
      this.newIngredient.amount = 0;
    }
  }

  addIngredient() {
    const originalName = this.newIngredient.name.trim();
    const existingIngredient = this.ingredients?.find(item => item.name.toLowerCase() === originalName.toLowerCase());
    const ingredient: IngredientCreation = {
      name: existingIngredient?.name ?? originalName,
      description: this.newIngredient.description,
      language: existingIngredient ? 'English' : this.getUiLanguage(),
      amount: this.newIngredient.amountType === 'To taste' ? 0 : this.newIngredient.amount,
      amountType: this.newIngredient.amountType,
      group: this.activeIngredientGroup || undefined,
      imageCaption: '',
      image: null,
      created: ''
    };

    this.newIngredients.push(ingredient);
    this.resetNewIngredient();
    setTimeout(() => this.ingredientNameSelect?.focusInput());
  }

  private getUiLanguage(): string {
    return this.mapUiLanguage(this.translateService.currentLang);
  }

  private mapUiLanguage(languageCode: string): string {
    return { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[languageCode] ?? 'English';
  }

  resetNewIngredient() {
    this.newIngredient = { ...this.defaultIngredient, group: this.activeIngredientGroup || undefined };
  }

  addIngredientGroup(): void {
    const groupName = this.newIngredientGroupName.trim();
    if (!groupName) return;

    const existingGroup = this.ingredientGroupNames.find(group => group.toLowerCase() === groupName.toLowerCase());
    if (!existingGroup) {
      this.ingredientGroupNames.push(groupName);
    }

    this.selectIngredientGroup(existingGroup ?? groupName);
    this.newIngredientGroupName = '';
  }

  selectIngredientGroup(groupName: string): void {
    this.activeIngredientGroup = groupName;
    this.newIngredient.group = groupName;
  }

  closeIngredientComposer(): void {
    this.activeIngredientGroup = null;
    this.newIngredient.group = undefined;
  }

  startIngredientGroupRename(groupName: string): void {
    this.editingIngredientGroup = groupName;
    this.ingredientGroupRenameValue = groupName;
  }

  saveIngredientGroupRename(groupName: string): void {
    const requestedName = this.ingredientGroupRenameValue.trim();
    if (!requestedName) return;

    const existingGroup = this.ingredientGroupNames.find(group => group !== groupName && group.toLowerCase() === requestedName.toLowerCase());
    const targetName = existingGroup ?? requestedName;
    this.newIngredients.forEach(ingredient => {
      if (ingredient.group === groupName) ingredient.group = targetName;
    });
    this.ingredientGroupNames = this.ingredientGroupNames
      .map(group => group === groupName ? targetName : group)
      .filter((group, index, groups) => groups.findIndex(item => item.toLowerCase() === group.toLowerCase()) === index);
    if (this.activeIngredientGroup === groupName) this.selectIngredientGroup(targetName);
    this.cancelIngredientGroupRename();
  }

  cancelIngredientGroupRename(): void {
    this.editingIngredientGroup = null;
    this.ingredientGroupRenameValue = '';
  }

  removeIngredientGroup(groupName: string): void {
    this.newIngredients.forEach(ingredient => {
      if (ingredient.group === groupName) ingredient.group = undefined;
    });
    this.ingredientGroupNames = this.ingredientGroupNames.filter(group => group !== groupName);
    if (this.activeIngredientGroup === groupName) this.selectIngredientGroup('');
    if (this.editingIngredientGroup === groupName) this.cancelIngredientGroupRename();
  }

  removeIngredient(ingredient: IngredientCreation) {
    var index = this.newIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.newIngredients.splice(index, 1);
    } else {
      this.newIngredients.push(ingredient);
    }
  }

  moveIngredientGroup(groupName: string, direction: -1 | 1): void {
    const index = this.ingredientGroupNames.indexOf(groupName);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= this.ingredientGroupNames.length) return;

    const reordered = this.ingredientGroupNames.slice();
    const [group] = reordered.splice(index, 1);
    reordered.splice(targetIndex, 0, group);
    this.ingredientGroupNames = reordered;
    this.movingIngredientGroup = groupName;
    setTimeout(() => this.movingIngredientGroup = null, 240);
  }

  moveIngredient(ingredient: IngredientCreation, direction: -1 | 1): void {
    const groupName = ingredient.group?.trim() ?? '';
    const groupIngredients = this.newIngredients.filter(item => (item.group?.trim() ?? '') === groupName);
    const index = groupIngredients.indexOf(ingredient);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= groupIngredients.length) return;

    const target = groupIngredients[targetIndex];
    const firstIndex = this.newIngredients.indexOf(ingredient);
    const secondIndex = this.newIngredients.indexOf(target);
    [this.newIngredients[firstIndex], this.newIngredients[secondIndex]] = [this.newIngredients[secondIndex], this.newIngredients[firstIndex]];
    this.movingIngredient = ingredient;
    setTimeout(() => this.movingIngredient = null, 240);
  }

  dragIngredientGroup(groupName: string): void {
    this.draggedIngredientGroup = groupName;
  }

  dropIngredientGroup(targetGroupName: string): void {
    if (this.draggedIngredient) {
      this.dropIngredientIntoGroup(targetGroupName);
      return;
    }

    const sourceGroupName = this.draggedIngredientGroup;
    this.draggedIngredientGroup = null;
    if (!sourceGroupName || !targetGroupName || sourceGroupName === targetGroupName) return;

    const sourceIndex = this.ingredientGroupNames.indexOf(sourceGroupName);
    const targetIndex = this.ingredientGroupNames.indexOf(targetGroupName);
    if (sourceIndex < 0 || targetIndex < 0) return;

    const reordered = this.ingredientGroupNames.slice();
    const [group] = reordered.splice(sourceIndex, 1);
    reordered.splice(targetIndex, 0, group);
    this.ingredientGroupNames = reordered;
    this.movingIngredientGroup = sourceGroupName;
    setTimeout(() => this.movingIngredientGroup = null, 240);
  }

  dragRecipeIngredient(ingredient: IngredientCreation): void {
    this.draggedIngredient = ingredient;
  }

  dropRecipeIngredient(target: IngredientCreation): void {
    const source = this.draggedIngredient;
    this.draggedIngredient = null;
    if (!source || source === target) return;

    const sourceIndex = this.newIngredients.indexOf(source);
    const targetIndex = this.newIngredients.indexOf(target);
    if (sourceIndex < 0 || targetIndex < 0) return;

    source.group = target.group;
    this.newIngredients.splice(sourceIndex, 1);
    this.newIngredients.splice(sourceIndex < targetIndex ? targetIndex - 1 : targetIndex, 0, source);
    this.movingIngredient = source;
    setTimeout(() => this.movingIngredient = null, 240);
  }

  private dropIngredientIntoGroup(targetGroupName: string): void {
    const source = this.draggedIngredient;
    this.draggedIngredient = null;
    if (!source) return;

    source.group = targetGroupName;
    const sourceIndex = this.newIngredients.indexOf(source);
    if (sourceIndex < 0) return;
    this.newIngredients.splice(sourceIndex, 1);
    let insertIndex = this.newIngredients.length;
    for (let index = this.newIngredients.length - 1; index >= 0; index--) {
      if (this.newIngredients[index].group?.trim() === targetGroupName) {
        insertIndex = index + 1;
        break;
      }
    }
    this.newIngredients.splice(insertIndex, 0, source);
    this.movingIngredient = source;
    setTimeout(() => this.movingIngredient = null, 240);
  }

  handleFileInput(event: any) {
    this.recipeForm.get('image')?.setValue(null);
    this.recipeForm.get('image')?.markAsTouched();

    if (event.target.files.length < 1) {
      this.imageUrl = this.defaultImageUrl;
      this.showCropOverlay = false;
      return;
    }

    this.showCropOverlay = true;
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
    this.imageUrl = this.defaultImageUrl;
    this.recipeForm.get('image')?.setValue(null);
    this.savedOrCanceled = false;
  }

  cancelImageUpload() {
    this.imageUrl = this.defaultImageUrl;
    this.recipeForm.get('image')?.setValue(null);
    this.savedOrCanceled = false;
    this.showCropOverlay = false;
  }

  imageCropped(event: ImageCroppedEvent) {
    if (event.base64 == null) return;

    this.imageUrl = event.base64;
    this.recipeForm.get('image')?.setValue(event.base64);
    this.savedOrCanceled = true;
  }
  imageLoaded(event: LoadedImage) {
    const { width, height } = event.transformed.size;
    this.imageAspectRatio = width > 0 && height > 0 ? width / height : 4 / 3;
    this.imageCropperWidth = `min(100%, ${this.imageAspectRatio * 640}px, ${this.imageAspectRatio * 70}vh)`;
    setTimeout(() => {
      this.imageCropper?.resetCropperPosition();
      const croppedImage = this.imageCropper?.crop('base64');
      if (croppedImage) this.imageCropped(croppedImage);
    });
  }
  cropperReady() {
    // cropper ready
  }
  loadImageFailed() {
    this.imageUrl = this.defaultImageUrl;
    this.recipeForm.get('image')?.setValue(null);
    this.recipeForm.get('image')?.markAsTouched();
    this.showCropOverlay = false;
  }

  get f(): { [key: string]: AbstractControl } {
    return this.recipeForm.controls;
  }

  get formValues() {
    return this.recipeForm.value;
  }

  setEditorView(view: 'edit' | 'preview'): void {
    this.editorView = view;
  }

  toRecipePreview() {
    this.recipePreview = true;
  }

  toCardPreview() {
    this.recipePreview = false;
  }
}
