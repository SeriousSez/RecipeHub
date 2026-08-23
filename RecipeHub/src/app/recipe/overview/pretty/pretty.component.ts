import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { DatePipe } from '@angular/common'
import { Router } from '@angular/router';
import { UserService } from 'src/app/shared/services/user.service';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { RecipeService } from 'src/app/recipe/services/recipe.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { TranslateService } from '@ngx-translate/core';
import { RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS, sortRecipeTaxonomyValues } from '../../models/recipe-taxonomy';

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

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, public utilityService: UtilityService, private datepipe: DatePipe, private router: Router, private translateService: TranslateService) {

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

  isRecipeSelected(recipe: Recipe): boolean {
    return this.selectedRecipes.some(selectedRecipe => selectedRecipe.id === recipe.id);
  }

  addSelectedRecipesToGroceryList() {
    this.selectedRecipes.forEach(recipe => {
      this.groceryService.toggleRecipeToList(recipe);
      this.recipeList = this.groceryService.getRecipeList();
    });
  }

  toggleRecipeSelected(recipe: Recipe) {
    const index = this.selectedRecipes.findIndex(selectedRecipe => selectedRecipe.id === recipe.id);

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

  getVisibleBadges(recipe: Recipe): Array<{ value: string, cssClass: string, label: string }> {
    const categories = sortRecipeTaxonomyValues((recipe.categories ?? []).map(item => item.trim()).filter(Boolean), RECIPE_CATEGORY_GROUPS);
    const tags = sortRecipeTaxonomyValues((recipe.tags ?? []).map(item => item.trim()).filter(Boolean), RECIPE_TAG_GROUPS);
    const seen = new Set<string>();
    const badges: Array<{ value: string, cssClass: string, label: string }> = [];

    categories.forEach(category => {
      const normalized = category.toLowerCase();
      if (!seen.has(normalized)) {
        seen.add(normalized);
        const group = RECIPE_CATEGORY_GROUPS.find(item =>
          item.values.some(value => value.toLowerCase() === normalized)
        );
        const groupLabel = this.translateService.instant(group?.labelKey ?? 'recipe.taxonomyGroups.custom');
        const label = this.translateService.instant('recipe.categoryBadgeTooltip', { group: groupLabel });
        badges.push({ value: category, cssClass: `recipe-category recipe-category-${group?.id ?? 'other'}`, label });
      }
    });

    tags.forEach(tag => {
      const normalized = tag.toLowerCase();
      if (!seen.has(normalized)) {
        seen.add(normalized);
        const group = RECIPE_TAG_GROUPS.find(item =>
          item.values.some(value => value.toLowerCase() === normalized)
        );
        const groupLabel = this.translateService.instant(group?.labelKey ?? 'recipe.taxonomyGroups.custom');
        const label = this.translateService.instant('recipe.tagBadgeTooltip', { group: groupLabel });
        badges.push({ value: tag, cssClass: `recipe-tag recipe-tag-${group?.id ?? 'other'}`, label });
      }
    });

    return badges;
  }

  getIngredientMatchSummary(recipe: Recipe) {
    if (!this.pantryIngredients || this.pantryIngredients.length === 0) {
      return { matched: 0, missing: 0, isBestMatch: false, label: this.translateService.instant('recipe.noPantryMatch') };
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

    const pct = totalIngredients > 0 ? Math.round((recipeMatchCount / totalIngredients) * 100) : 0;
    const label = pct >= 80 ? this.translateService.instant('recipe.excellentMatch')
      : pct >= 60 ? this.translateService.instant('recipe.greatMatch')
        : pct >= 40 ? this.translateService.instant('recipe.goodMatch')
          : pct > 0 ? this.translateService.instant('recipe.someOverlap')
            : this.translateService.instant('recipe.noPantryMatch');

    return {
      matched: recipeMatchCount,
      missing: missingCount,
      isBestMatch: this.bestMatchScore > 0 && recipeMatchCount === this.bestMatchScore,
      label
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
