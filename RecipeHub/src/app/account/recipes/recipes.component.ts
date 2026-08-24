import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { DatePipe } from '@angular/common'
import { Router } from '@angular/router';
import { UserService } from 'src/app/shared/services/user.service';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { RecipeService } from 'src/app/recipe/services/recipe.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';

@Component({
  selector: 'app-recipes',
  templateUrl: './recipes.component.html',
  styleUrls: ['./recipes.component.css'],
  standalone: false
})
export class RecipesComponent implements OnInit {
  @ViewChild('recipeModal') private recipeModal: ElementRef;

  public recipes: Recipe[] = [];
  public selectedRecipes: string[] = [];
  public recipeSearch = '';
  public loadingRecipes = true;
  public showDeleteConfirmation = false;
  public deletingRecipes = false;

  public recipeToEdit: Recipe;

  public sortSetting: string = 'created';
  public ascending: boolean = true;
  public sortOptions = ['created', 'title', 'description'];

  constructor(private recipeService: RecipeService, private userService: UserService, private datepipe: DatePipe, private router: Router, private utilityService: UtilityService) { }

  ngOnInit(): void {
    this.getRecipes();
  }

  getRecipes() {
    this.loadingRecipes = true;
    this.recipeService.getRecipesByCreator(this.userService.getUserName())
      .subscribe((recipes: Recipe[]) => {
        this.recipes = recipes;
        this.sort(this.sortSetting);
        this.loadingRecipes = false;
      },
        error => {
          this.loadingRecipes = false;
        });
  }

  get filteredRecipes(): Recipe[] {
    const search = this.recipeSearch.trim().toLocaleLowerCase();
    if (!search) return this.recipes;

    return this.recipes.filter(recipe =>
      [recipe.title, recipe.description, recipe.creator]
        .some(value => value?.toLocaleLowerCase().includes(search))
    );
  }

  requestDeleteRecipes() {
    if (this.selectedRecipes.length > 0) this.showDeleteConfirmation = true;
  }

  cancelDeleteRecipes() {
    this.showDeleteConfirmation = false;
    this.deletingRecipes = false;
  }

  deleteRecipes() {
    if (this.selectedRecipes.length === 0 || this.deletingRecipes) return;
    this.deletingRecipes = true;
    this.recipeService.deleteRecipes(this.selectedRecipes).subscribe((recipes) => {
      this.recipes = recipes;
      this.sort(this.sortSetting);
      this.selectedRecipes = [];
      this.showDeleteConfirmation = false;
      this.deletingRecipes = false;
    },
      error => {
        this.deletingRecipes = false;
        this.showDeleteConfirmation = false;
        //this.notificationService.printErrorMessage(error);
      });
  }

  toggleRecipeSelected(recipe: Recipe) {
    var index = this.selectedRecipes.indexOf(recipe.id, 0);
    if (index > -1) {
      this.selectedRecipes.splice(index, 1);
    } else {
      this.selectedRecipes.push(recipe.id);
    }
  }

  removeRecipeFromList(recipe: Recipe) {
    var index = this.recipes.indexOf(recipe, 0);
    if (index > -1) {
      this.recipes.splice(index, 1);
    } else {
      this.recipes.push(recipe);
    }
  }

  openRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${this.utilityService.toRecipeKey(recipe.id, recipe.title)}`]);
  }

  editRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${this.utilityService.toRecipeKey(recipe.id, recipe.title)}`], { queryParams: { edit: true } });
  }

  changeSort(sortSetting: string) {
    this.sortSetting = sortSetting;
    this.ascending = true;
    this.sort(sortSetting);
  }

  totalMinutes(recipe: Recipe): number {
    const timingValues: Array<number | null | undefined> = [
      recipe.preparationMinutes,
      recipe.cookingMinutes,
      recipe.proofingMinutes,
      recipe.chillingMinutes,
      recipe.coolingMinutes,
      recipe.restingMinutes
    ];

    return timingValues.reduce<number>((total, minutes) => total + (minutes ?? 0), 0);
  }

  formatTotalTime(recipe: Recipe): string {
    const minutes = this.totalMinutes(recipe);
    if (minutes <= 60) return `${minutes} min`;

    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return remainingMinutes > 0 ? `${hours} hr ${remainingMinutes} min` : `${hours} hr`;
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  toRecipeTitle(id: string) {
    return this.recipes.find(r => r.id == id)?.title;
  }

  sort(sortSetting: string) {
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    switch (sortSetting) {
      case 'title':
        this.recipes.sort((a, b) => this.ascending == true ? a.title.localeCompare(b.title) : -a.title.localeCompare(b.title));
        this.ascending = !this.ascending;
        return;
      case 'description':
        this.recipes.sort((a, b) => this.ascending == true ? a.description.localeCompare(b.description) : -a.description.localeCompare(b.description));
        this.ascending = !this.ascending;
        return;
      case 'creator':
        this.recipes.sort((a, b) => this.ascending == true ? a.creator.localeCompare(b.creator) : -a.creator.localeCompare(b.creator));
        this.ascending = !this.ascending;
        return;
      case 'created':
        this.recipes.sort((a, b) => this.ascending == true ? a.created.localeCompare(b.created) : -a.created.localeCompare(b.created));
        this.ascending = !this.ascending;
        return;
    }
  }

  public openRecipeModal(recipe: Recipe) {
    this.recipeToEdit = recipe;
    var modalDoc = document.getElementById('recipeModal');
    if (modalDoc == null) return;
    modalDoc.removeAttribute('aria-hidden');
    modalDoc.style.removeProperty('visibility');
    modalDoc.style.display = 'block';
    // display: block;
    this.recipeModal.nativeElement.click();
  }

  public closeRecipeModal(recipe: any) {
    var index = this.recipes.indexOf(this.recipeToEdit, 0);
    if (index > -1) {
      this.recipes.splice(index, 1);
    } else {
      this.recipes.push(this.recipeToEdit);
    }

    var modalDoc = document.getElementById('recipeModal');
    if (modalDoc == null) return;
    if (modalDoc.nextSibling == null) return;
    modalDoc.parentNode?.removeChild(modalDoc.nextSibling);
    this.recipes.push(recipe);
    modalDoc.setAttribute('aria-hidden', 'true');
    modalDoc.removeAttribute('aria-modal');
    modalDoc.removeAttribute('role');
    modalDoc.style.visibility = 'hidden ';
    modalDoc.style.removeProperty('display');
    modalDoc.classList.remove('show');
    this.recipeModal.nativeElement.click();
  }

}
