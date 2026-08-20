import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { DatePipe } from '@angular/common'
import { Router } from '@angular/router';
import { UserService } from 'src/app/shared/services/user.service';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { Recipe } from '../../models/recipe.interface';
import { Ingredient } from '../../models/ingredient.interface';
import { RecipeService } from '../../services/recipe.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';

@Component({
  selector: 'app-pretty',
  templateUrl: './pretty.component.html',
  styleUrls: ['./pretty.component.css'],
  standalone: false
})
export class PrettyComponent implements OnInit {
  @Input() recipes: Recipe[] = [];
  @Input() favoredRecipes: Recipe[] = [];
  @Input() selectedRecipes: Recipe[] = [];
  @Input() pantryIngredients: string[] = [];
  @Input() bestMatchScore: number = 0;
  @Output() selectRecipe = new EventEmitter<Recipe>();

  public recipeList: Recipe[] = [];
  public groceryList: Ingredient[] = [];

  public shownRecipes: Recipe[] = [];
  public showFavorites: boolean = false;

  public sortSetting: string = 'created';
  public ascending: boolean = true;

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, public utilityService: UtilityService, private datepipe: DatePipe, private router: Router) {

  }

  ngOnInit(): void {
    this.shownRecipes = this.recipes;
    console.log('Recipes in pretty component:', this.recipes);
    this.recipes.forEach((recipe, index) => {
      console.log(`Recipe ${index} - Title: ${recipe.title}, Image:`, recipe.image);
    });
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

  toggleRecipeSelected(recipe: Recipe) {
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

  openRecipe(recipe: Recipe) {
    this.router.navigate([`recipe/${recipe.id}/${this.utilityService.toSlug(recipe.title)}`]);
  }

  displayDateOnly(created: string) {
    return this.utilityService.displayDateOnly(created);
  }

  getVisibleBadges(recipe: Recipe): Array<{ value: string, kind: 'category' | 'tag' }> {
    const categories = (recipe.categories ?? []).map(item => item.trim()).filter(Boolean);
    const tags = (recipe.tags ?? []).map(item => item.trim()).filter(Boolean);
    const seen = new Set<string>();
    const badges: Array<{ value: string, kind: 'category' | 'tag' }> = [];

    categories.forEach(category => {
      const normalized = category.toLowerCase();
      if (!seen.has(normalized)) {
        seen.add(normalized);
        badges.push({ value: category, kind: 'category' });
      }
    });

    tags.forEach(tag => {
      const normalized = tag.toLowerCase();
      if (!seen.has(normalized)) {
        seen.add(normalized);
        badges.push({ value: tag, kind: 'tag' });
      }
    });

    return badges;
  }

  getIngredientMatchSummary(recipe: Recipe) {
    if (!this.pantryIngredients || this.pantryIngredients.length === 0) {
      return { matched: 0, missing: 0, isBestMatch: false };
    }

    const pantrySet = this.pantryIngredients.map(value => value.trim().toLowerCase()).filter(Boolean);
    const recipeMatchCount = (recipe.ingredients ?? []).filter(ingredient => {
      const ingredientName = ingredient.name.trim().toLowerCase();
      return pantrySet.some(item =>
        item === ingredientName ||
        item.includes(ingredientName) ||
        ingredientName.includes(item)
      );
    }).length;

    const totalIngredients = (recipe.ingredients ?? []).length;
    const missingCount = Math.max(totalIngredients - recipeMatchCount, 0);

    return {
      matched: recipeMatchCount,
      missing: missingCount,
      isBestMatch: this.bestMatchScore > 0 && recipeMatchCount === this.bestMatchScore
    };
  }

  sort(sortSetting: string) {
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    switch (sortSetting) {
      case 'title':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.title.localeCompare(b.title) : -a.title.localeCompare(b.title));
        this.ascending = !this.ascending;
        return;
      case 'creator':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.creator.localeCompare(b.creator) : -a.instructions.localeCompare(b.creator));
        this.ascending = !this.ascending;
        return;
      case 'created':
        this.shownRecipes.sort((a, b) => this.ascending == true ? a.created.localeCompare(b.created) : -a.created.localeCompare(b.created));
        this.ascending = !this.ascending;
        return;
    }
  }
}
