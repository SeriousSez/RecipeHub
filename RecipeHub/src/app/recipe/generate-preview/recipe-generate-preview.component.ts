import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateService } from '@ngx-translate/core';
import { GeneratedRecipe } from '../models/recipe-generation.interface';
import { RecipeDraftService } from '../services/recipe-draft.service';

@Component({
    selector: 'app-recipe-generate-preview',
    templateUrl: './recipe-generate-preview.component.html',
    styleUrls: ['../recipe/recipe.component.css', './recipe-generate-preview.component.css'],
    standalone: false
})
export class RecipeGeneratePreviewComponent implements OnInit {
    public draft: GeneratedRecipe | null = null;

    constructor(private recipeDraftService: RecipeDraftService, private router: Router, private translateService: TranslateService) { }

    ngOnInit(): void {
        this.draft = this.recipeDraftService.peekDraft();
        if (!this.draft) {
            this.router.navigate(['/recipes']);
        }
    }

    public get ingredientGroups(): Array<{ name: string; ingredients: GeneratedRecipe['ingredients'] }> {
        const groups = new Map<string, GeneratedRecipe['ingredients']>();
        for (const ingredient of this.draft?.ingredients ?? []) {
            const groupName = ingredient.group?.trim() ?? '';
            const groupIngredients = groups.get(groupName) ?? [];
            groupIngredients.push(ingredient);
            groups.set(groupName, groupIngredients);
        }
        return Array.from(groups, ([name, ingredients]) => ({ name, ingredients }));
    }

    public get totalMinutes(): number | null {
        if (!this.draft) return null;
        const minutes = [this.draft.preparationMinutes, this.draft.cookingMinutes, this.draft.proofingMinutes, this.draft.chillingMinutes, this.draft.coolingMinutes, this.draft.restingMinutes]
            .filter((value): value is number => value != null);
        return minutes.length > 0 ? minutes.reduce((sum, value) => sum + value, 0) : null;
    }

    public get hasNutrition(): boolean {
        const draft = this.draft;
        return !!draft && [draft.calories, draft.proteinGrams, draft.carbohydrateGrams, draft.fatGrams, draft.fiberGrams, draft.sugarGrams, draft.sodiumMilligrams].some(value => value != null);
    }

    public formatDuration(totalMinutes: number | null): string {
        if (totalMinutes == null) return '';
        const hours = Math.floor(totalMinutes / 60);
        const minutes = totalMinutes % 60;
        if (hours === 0) return `${minutes} ${this.translateService.instant('recipe.minutesShort')}`;
        if (minutes === 0) return `${hours} ${this.translateService.instant('recipe.hoursShort')}`;
        return `${hours} ${this.translateService.instant('recipe.hoursShort')} ${minutes} ${this.translateService.instant('recipe.minutesShort')}`;
    }

    public getScaledIngredientAmount(amount: number): string {
        return Number.isInteger(amount) ? `${amount}` : amount.toFixed(2).replace(/\.?0+$/, '');
    }

    public useThisRecipe(): void {
        this.router.navigate(['/recipes/create']);
    }

    public discardDraft(): void {
        this.recipeDraftService.clearDraft();
        this.router.navigate(['/recipes']);
    }
}
