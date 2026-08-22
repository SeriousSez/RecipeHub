import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ImageCroppedEvent } from 'ngx-image-cropper';
import { AngularEditorConfig } from '@kolkov/angular-editor';
import { IngredientCreation } from 'src/app/shared/models/ingredient.creation.interface';
import { RecipeCreation } from 'src/app/shared/models/recipe.creation.interface';
import { UserService } from 'src/app/shared/services/user.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { Ingredient } from '../models/ingredient.interface';
import { RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS } from '../models/recipe-taxonomy';
import { IngredientService } from '../services/ingredient.service';
import { RecipeService } from '../services/recipe.service';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-create',
  templateUrl: './create.component.html',
  styleUrls: ['./create.component.css'],
  standalone: false
})
export class CreateComponent implements OnInit {
  public measurements: string[] = ['Pinch or dash', 'Piece', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound', 'Clove']
  public languages: string[] = ['Danish', 'English', 'Estonian', 'Turkish']

  @ViewChild("select", { static: true }) select: ElementRef;
  //#region preview
  public fakeInstructions: string = "<p><em><strong>Spice</strong></em></p><p><tt>An aromatic or pungent vegetable substance used to flavour food, e.g. cloves, pepper, or cumin.</tt></p><p><img alt='Get to Know Your SPICEs - Zuken US' src='https://www.zuken.com/us/wp-content/uploads/sites/12/2020/06/BL0236-spices-1280x620-1.jpg' style='height:100%; width:100%' /></p><p><q><cite><small>He ordered his regular breakfast. Two eggs sunnyside up, hash browns, and two strips of bacon. He continued to look at the menu wondering if this would be the day he added something new. This was also part of the routine. A few seconds of hesitation to see if something else would be added to the order before demuring and saying that would be all. It was the same exact meal that he had ordered every day for the past two years.</small></cite></q></p>";
  public fakeDescription: string = "A spice is a seed, fruit, root, bark, or other plant substance primarily used for flavoring or coloring food. Spices are distinguished from herbs, which are the leaves, flowers, or stems of plants used for flavoring or as a garnish. Spices are sometimes used in medicine, religious rituals, cosmetics or perfume production.";
  public recipePreview: boolean = true;
  //#endregion

  public recipeForm: UntypedFormGroup;
  public ingredientForm: UntypedFormGroup;

  public errors: string = '';
  public isRequesting: boolean = false;
  public submitted: boolean = false;

  public defaultIngredient: IngredientCreation = { name: "", description: "", amount: 0, amountType: 'Pinch or dash', imageCaption: "", image: null, created: '' };
  public newIngredient: IngredientCreation;
  public selectedIngredient: Ingredient | null = null;
  public ingredientSearch: string = '';
  public newIngredients: IngredientCreation[] = [];
  public ingredients: Ingredient[];
  public categoriesInput: string = '';
  public tagsInput: string = '';
  public readonly categoryGroups = RECIPE_CATEGORY_GROUPS;
  public readonly tagGroups = RECIPE_TAG_GROUPS;
  public activeCategoryGroupId: string = RECIPE_CATEGORY_GROUPS[0].id;
  public activeTagGroupId: string = RECIPE_TAG_GROUPS[0].id;

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

  constructor(public utilityService: UtilityService, private recipeService: RecipeService, private ingredientService: IngredientService, public userService: UserService, private router: Router, private formBuilder: UntypedFormBuilder, private translateService: TranslateService) { }

  public get badExampleTitle(): string {
    return this.translateService.instant('recipe.badExampleTitle');
  }

  public get previewImageAlt(): string {
    return this.translateService.instant('recipe.previewImageAlt');
  }

  public get ingredientOptions(): string[] {
    return this.ingredients?.map(ingredient => ingredient.name) ?? [];
  }

  ngOnInit(): void {
    this.getIngredients();

    this.newIngredient = { ...this.defaultIngredient };
    this.imageUrl = this.defaultImageUrl;
    this.recipeForm = this.formBuilder.group({
      title: ['', Validators.required],
      description: ['', Validators.required],
      language: [this.userService.getUserLanguage(), Validators.required],
      instructions: ['', Validators.required],
      portions: ['', Validators.required],
      preparationMinutes: [null, Validators.min(0)],
      cookingMinutes: [null, Validators.min(0)],
      shelfLifeDays: [null, Validators.min(0)],
      canBeFrozen: [false],
      imageCaption: [''],
      categories: [''],
      tags: [''],
      ingredients: []
    });

    this.ingredientForm = this.formBuilder.group({
      name: ['', Validators.required],
      description: ['']
    });

  }

  getIngredients() {
    this.ingredientService.getIngredientsLite()
      .subscribe((ingredients: Ingredient[]) => {
        this.ingredients = ingredients;
        this.ingredients.sort((a, b) => a.name.localeCompare(b.name));
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  create({ value, valid }: { value: RecipeCreation, valid: boolean }) {
    this.submitted = true;
    this.isRequesting = true;
    this.errors = '';

    value.creator = this.userService.getUserName();
    value.imageUrl = this.imageUrl;
    value.image = { url: this.imageUrl, caption: value.imageCaption }
    value.ingredients = this.newIngredients;
    value.categories = this.parseCsv(value.categories ?? this.categoriesInput);
    value.tags = this.parseCsv(value.tags ?? this.tagsInput);

    if (valid) {
      this.recipeService.create(value)
        .subscribe(result => {
          this.router.navigate([`/recipe/${result.id}/${this.utilityService.toSlug(result.title)}`]);
        }, errors => {
          this.isRequesting = false;
          this.errors = errors.error;
        });
    }
  }

  public getSelectedValues(rawValue: string | string[] | null | undefined): string[] {
    return this.parseCsv(rawValue);
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

  addToNewIngredient(event: Ingredient | null) {
    if (!event) {
      return;
    }

    this.newIngredient.name = event.name;
    this.newIngredient.description = event.description;
  }

  selectExistingIngredient(name: string): void {
    const normalizedName = (name ?? '').trim().toLowerCase();
    const ingredient = this.ingredients?.find(item => item.name.toLowerCase() === normalizedName) ?? null;
    this.selectedIngredient = ingredient;
    this.addToNewIngredient(ingredient);
  }

  addIngredient() {
    const ingredient: IngredientCreation = {
      name: this.newIngredient.name,
      description: this.newIngredient.description,
      language: this.recipeForm.controls['language'].value,
      amount: this.newIngredient.amount,
      amountType: this.newIngredient.amountType,
      imageCaption: '',
      image: null,
      created: ''
    };

    this.newIngredients.push(ingredient);
    this.resetNewIngredient();
    this.selectedIngredient = null;
    this.ingredientSearch = '';
  }

  resetNewIngredient() {
    this.newIngredient = { ...this.defaultIngredient };
  }

  removeIngredient(ingredient: IngredientCreation) {
    var index = this.newIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.newIngredients.splice(index, 1);
    } else {
      this.newIngredients.push(ingredient);
    }
  }

  handleFileInput(event: any) {
    if (event.target.files.length < 1) {
      this.imageUrl = "";
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
    this.savedOrCanceled = false;
  }

  cancelImageUpload() {
    this.imageUrl = this.defaultImageUrl;
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

  get f(): { [key: string]: AbstractControl } {
    return this.recipeForm.controls;
  }

  get formValues() {
    return this.recipeForm.value;
  }

  toRecipePreview() {
    this.recipePreview = true;
  }

  toCardPreview() {
    this.recipePreview = false;
  }
}
