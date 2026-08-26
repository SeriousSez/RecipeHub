import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { DatePipe } from '@angular/common'
import { UserService } from 'src/app/shared/services/user.service';
import { FavoriteService } from 'src/app/shared/services/favorite.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { RecipeService } from 'src/app/recipe/services/recipe.service';
import { UtilityService } from 'src/app/shared/utils/utility.service';
import { TranslateService } from '@ngx-translate/core';
import { getRecipeNutritionHighlights, RecipeNutritionHighlight, RECIPE_CATEGORY_GROUPS, RECIPE_TAG_GROUPS, sortRecipeTaxonomyValues, getTaxonomyValueLabel } from '../../models/recipe-taxonomy';

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
  @Input() activeTagFilters: string[] = [];
  @Input() activeSortSetting: string = 'created';
  @Input() showEngagement: boolean = false;
  @Output() selectRecipe = new EventEmitter<Recipe>();

  public recipeList: Recipe[] = [];
  public groceryList: Ingredient[] = [];

  public shownRecipes: Recipe[] = [];
  public showFavorites: boolean = false;

  public sortSetting: string = 'created';
  public ascending: boolean = true;

  constructor(private recipeService: RecipeService, private userService: UserService, private favoriteService: FavoriteService, private groceryService: GroceryService, public utilityService: UtilityService, private datepipe: DatePipe, private translateService: TranslateService) {

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

  displayDateOnly(created: string) {
    return this.utilityService.displayDateOnly(created);
  }

  getVisibleBadges(recipe: Recipe): Array<{ value: string, cssClass: string, label: string, displayValue: string }> {
    const categories = sortRecipeTaxonomyValues((recipe.categories ?? []).map(item => item.trim()).filter(Boolean), RECIPE_CATEGORY_GROUPS);
    const tags = sortRecipeTaxonomyValues((recipe.tags ?? []).map(item => item.trim()).filter(Boolean), RECIPE_TAG_GROUPS);
    const seen = new Set<string>();
    const badges: Array<{ value: string, cssClass: string, label: string, displayValue: string }> = [];

    categories.forEach(category => {
      const normalized = category.toLowerCase();
      if (!seen.has(normalized)) {
        seen.add(normalized);
        const group = RECIPE_CATEGORY_GROUPS.find(item =>
          item.values.some(value => value.toLowerCase() === normalized)
        );
        const groupLabel = this.translateService.instant(group?.labelKey ?? 'recipe.taxonomyGroups.custom');
        const label = this.translateService.instant('recipe.categoryBadgeTooltip', { group: groupLabel });
        badges.push({ value: category, cssClass: `recipe-category recipe-category-${group?.id ?? 'other'}`, label, displayValue: getTaxonomyValueLabel(category, this.translateService) });
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
        badges.push({ value: tag, cssClass: `recipe-tag recipe-tag-${group?.id ?? 'other'}`, label, displayValue: getTaxonomyValueLabel(tag, this.translateService) });
      }
    });

    return badges;
  }

  getNutritionHighlights(recipe: Recipe): RecipeNutritionHighlight[] {
    return getRecipeNutritionHighlights(recipe, this.activeTagFilters);
  }

  getSortHighlights(recipe: Recipe): Array<{ value: string, label: string }> {
    if (this.activeSortSetting === 'creator') {
      return recipe.creator ? [{ value: recipe.creator, label: this.translateService.instant('recipe.sortCreator') }] : [];
    }

    if (this.activeSortSetting === 'time') {
      const timeHighlights = [
        { value: recipe.preparationMinutes, labelKey: 'recipe.preparationTimeLabel' },
        { value: recipe.cookingMinutes, labelKey: 'recipe.cookingTimeLabel' },
        { value: recipe.proofingMinutes, labelKey: 'recipe.proofingTimeLabel' },
        { value: recipe.chillingMinutes, labelKey: 'recipe.chillingTimeLabel' },
        { value: recipe.coolingMinutes, labelKey: 'recipe.coolingTimeLabel' },
        { value: recipe.restingMinutes, labelKey: 'recipe.restingTimeLabel' }
      ]
        .filter(item => item.value != null && item.value > 0)
        .map(item => ({ value: this.formatDuration(item.value ?? 0), label: this.translateService.instant(item.labelKey) }));

      const totalTime = this.getTotalRecipeMinutes(recipe);
      return totalTime > 0
        ? [{ value: this.formatDuration(totalTime), label: this.translateService.instant('recipe.sortTime') }, ...timeHighlights]
        : [];
    }

    return [];
  }

  private getTotalRecipeMinutes(recipe: Recipe): number {
    return [
      recipe.preparationMinutes,
      recipe.cookingMinutes,
      recipe.proofingMinutes,
      recipe.chillingMinutes,
      recipe.coolingMinutes,
      recipe.restingMinutes
    ].reduce<number>((total, value) => total + (value ?? 0), 0);
  }

  private formatDuration(minutes: number): string {
    if (minutes <= 0) return '';
    if (minutes < 60) return `${minutes} min`;

    const hours = Math.floor(minutes / 60);
    const remainingMinutes = minutes % 60;
    return remainingMinutes > 0 ? `${hours} hr ${remainingMinutes} min` : `${hours} hr`;
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
