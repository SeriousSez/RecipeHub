import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { Recipe } from '../../models/recipe.interface';
import { Ingredient } from '../../models/ingredient.interface';
import { UtilityService } from 'src/app/shared/utils/utility.service';

@Component({
  selector: 'app-pretty',
  templateUrl: './pretty.component.html',
  styleUrls: ['./pretty.component.css'],
  standalone: true,
  imports: [CommonModule]
})
export class PrettyComponent implements OnInit {
  @Input() recipes: any[] = [];
  @Input() favoredRecipes: any[] = [];

  public recipeList: any[] = [];
  public groceryList: Ingredient[] = [];

  public shownRecipes: any[] = [];
  public selectedRecipes: any[] = [];
  public showFavorites: boolean = false;

  public sortSetting: string = 'created';
  public ascending: boolean = true;

  constructor(private groceryService: GroceryService, public utilityService: UtilityService, private router: Router) {

  }

  ngOnInit(): void {
    this.getGroceryLists();
  }

  getGroceryLists() {
    this.recipeList = this.groceryService.getRecipeList();
    this.groceryList = this.groceryService.getIngredientList();
  }

  addSelectedRecipesToGroceryList() {
    this.selectedRecipes.forEach(recipe => {
      this.groceryService.toggleRecipeToList(recipe);
      this.recipeList = this.groceryService.getRecipeList();
    });
  }

  toggleRecipeSelected(recipe: any) {
    var index = this.selectedRecipes.indexOf(recipe, 0);
    if (index > -1) {
      this.selectedRecipes.splice(index, 1);
    } else {
      this.selectedRecipes.push(recipe);
    }
  }

  toggleDisplay() {
    this.showFavorites = !this.showFavorites;
    if (this.showFavorites) {
      if (this.favoredRecipes == null) this.favoredRecipes = [];

      this.shownRecipes = this.favoredRecipes;
    } else {
      this.shownRecipes = this.recipes;
    }
  }

  openRecipe(recipe: any) {
    this.router.navigate([`recipe/${recipe.Id}/${recipe.Title.toLocaleLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')}`]);
  }

  displayDateOnly(created: string) {
    this.utilityService.displayDateOnly(created);
  }

  sort(sortSetting: string) {
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    switch (sortSetting) {
      case 'title':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.Title.localeCompare(b.Title) : -a.Title.localeCompare(b.Title));
        this.ascending = !this.ascending;
        return;
      case 'creator':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.Creator.localeCompare(b.Creator) : -a.Instructions.localeCompare(b.Creator));
        this.ascending = !this.ascending;
        return;
      case 'created':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.Created.localeCompare(b.Created) : -a.Created.localeCompare(b.Created));
        this.ascending = !this.ascending;
        return;
    }
  }
}
