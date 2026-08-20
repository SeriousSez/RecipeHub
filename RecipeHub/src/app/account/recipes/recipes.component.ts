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
  @ViewChild('deleteButton') private deleteButton: ElementRef;

  public recipes: Recipe[] = [];
  public selectedRecipes: string[] = [];

  public recipeToEdit: Recipe;

  public sortSetting: string = 'created';
  public ascending: boolean = true;

  constructor(private recipeService: RecipeService, private userService: UserService, private datepipe: DatePipe, private router: Router, private utilityService: UtilityService) { }

  ngOnInit(): void {
    this.getRecipes();
  }

  getRecipes() {
    this.recipeService.getRecipesByCreator(this.userService.getUserName())
      .subscribe((recipes: Recipe[]) => {
        this.recipes = recipes;
        this.sort(this.sortSetting);
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  deleteRecipes() {
    this.recipeService.deleteRecipes(this.selectedRecipes).subscribe((recipes) => {
      this.selectedRecipes.forEach((id) => {
        var recipe = this.recipes.find(r => r.id == id);
        if (recipe == undefined) return;

        this.removeRecipeFromList(recipe);
      });

      this.selectedRecipes = [];
      this.closeDeleteModal();
    },
      error => {
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
    this.router.navigate([`recipe/${recipe.id}/${this.utilityService.toSlug(recipe.title)}`]);
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

  public closeDeleteModal() {
    this.deleteButton.nativeElement.click();
  }
}
