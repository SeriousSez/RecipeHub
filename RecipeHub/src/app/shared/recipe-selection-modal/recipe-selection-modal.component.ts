import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { getTaxonomyValueLabel } from 'src/app/recipe/models/recipe-taxonomy';
import { TranslateService } from '@ngx-translate/core';

@Component({
    selector: 'app-recipe-selection-modal',
    templateUrl: './recipe-selection-modal.component.html',
    styleUrls: ['./recipe-selection-modal.component.css'],
    standalone: false
})
export class RecipeSelectionModalComponent {
    @Input() visible = false;
    @Input() recipes: Recipe[] = [];
    @Input() selectedRecipeId = '';

    @Output() selected = new EventEmitter<Recipe>();
    @Output() closed = new EventEmitter<void>();

    public searchTerm = '';

    constructor(private translateService: TranslateService) { }

    public get filteredRecipes(): Recipe[] {
        const query = this.searchTerm.trim().toLowerCase();
        if (!query) return this.recipes;

        return this.recipes.filter(recipe =>
            [recipe.title, recipe.description, recipe.creator]
                .some(value => value?.toLowerCase().includes(query)));
    }

    public close(): void {
        this.closed.emit();
    }

    public chooseRecipe(recipe: Recipe): void {
        this.selected.emit(recipe);
    }

    public getRecipeBadges(recipe: Recipe): string[] {
        return [...(recipe.categories ?? []), ...(recipe.tags ?? [])]
            .map(value => value.trim())
            .filter(Boolean)
            .map(value => getTaxonomyValueLabel(value, this.translateService))
            .slice(0, 4);
    }

    public getTotalMinutes(recipe: Recipe): number {
        return [
            recipe.preparationMinutes,
            recipe.cookingMinutes,
            recipe.proofingMinutes,
            recipe.chillingMinutes,
            recipe.coolingMinutes,
            recipe.restingMinutes
        ].reduce<number>((total, minutes) => total + (minutes ?? 0), 0);
    }

    public formatTotalTime(recipe: Recipe): string {
        const minutes = this.getTotalMinutes(recipe);
        if (minutes <= 0) return '';
        if (minutes < 60) return `${minutes} min`;

        const hours = Math.floor(minutes / 60);
        const remainingMinutes = minutes % 60;
        return remainingMinutes > 0 ? `${hours} hr ${remainingMinutes} min` : `${hours} hr`;
    }

    public trackRecipe(_: number, recipe: Recipe): string {
        return recipe.id;
    }
}