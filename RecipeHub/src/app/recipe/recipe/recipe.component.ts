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
import { IngredientService } from '../services/ingredient.service';
import { RecipeService } from '../services/recipe.service';
import { AngularEditorConfig } from '@kolkov/angular-editor';

@Component({
  selector: 'app-recipe',
  templateUrl: './recipe.component.html',
  styleUrls: ['./recipe.component.css'],
  standalone: false
})
export class RecipeComponent implements OnInit {
  @ViewChildren("select") select: any;
  @ViewChildren("name") name: any;
  @ViewChildren("description") description: any;
  @ViewChildren("amount") amount: any;

  public measurements: string[] = ['Pinch or dash', 'Piece', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound', 'Clove']

  public title: string;
  public creator: string;
  public recipeId: string | null = null;

  public recipe: Recipe;
  public ingredientsToDelete: Ingredient[] = [];
  public ingredients: Ingredient[] = [];
  public currentIngredients: Ingredient[] = [];
  public newIngredients: Ingredient[] = [];
  public newIngredient: Ingredient = { name: "", description: "", amount: 0, amountType: 'Pinch or dash', image: null, created: '' };

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

  constructor(private activatedRoute: ActivatedRoute, private datepipe: DatePipe, private router: Router, public utilityService: UtilityService, private recipeService: RecipeService, private groceryService: GroceryService, private ingredientService: IngredientService, private userService: UserService, private safeService: SafeService, private favoriteService: FavoriteService) {
    this.recipeId = activatedRoute.snapshot.params['id'] || null;
    this.title = this.utilityService.fromSlug(activatedRoute.snapshot.params['title']);
    this.creator = decodeURIComponent(activatedRoute.snapshot.params['creator'] || '');
  }

  ngOnInit(): void {
    this.getRecipe();
    this.subscription = this.userService.authStatus$.subscribe(status => this.status = status);
  }

  ngOnDestroy() {
    // prevent memory leak when component is destroyed
    this.subscription?.unsubscribe();
  }

  //#region favored
  isFavored(recipe: Recipe) {
    var username = this.userService.getUserName();
    if (username.length == 0 || username == '' || username == null) return;

    this.favoriteService.isFavored(username, recipe).subscribe(result => {
      this.favored = result;
    });
  }

  toggleFavorite() {
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
    this.title = recipe.title;
    this.creator = recipe.creator;
    this.recipeId = recipe.id;
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

  addToNewIngredient(event: any) {
    var ingredient = this.ingredients.find(i => i.name == this.select.first.nativeElement.value);
    if (ingredient == null) return;

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

  createRecipeUpgradeModel(recipe: Recipe) {
    var model: RecipeUpdate = {
      oldTitle: this.title,
      title: recipe.title,
      creator: recipe.creator,
      description: recipe.description,
      instructions: recipe.instructions,
      portions: recipe.portions,
      created: recipe.created,
      image: recipe.image,
      ingredients: recipe.ingredients,
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

