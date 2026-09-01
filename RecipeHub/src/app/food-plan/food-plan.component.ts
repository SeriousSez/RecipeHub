import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { forkJoin, Subscription } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';
import { Recipe } from '../recipe/models/recipe.interface';
import { RecipeService } from '../recipe/services/recipe.service';
import { UserService } from '../shared/services/user.service';
import { UtilityService } from '../shared/utils/utility.service';
import { FoodPlanEntry, FoodPlanEntryRequest, FoodPlanNutritionTotals } from './food-plan-entry.interface';
import { FoodPlanService } from './food-plan.service';
import { LanguageService } from '../shared/services/language.service';
import { getTaxonomyValueLabel } from '../recipe/models/recipe-taxonomy';
import { GroceryService } from '../shared/services/grocery.service';

@Component({
    selector: 'app-food-plan',
    templateUrl: './food-plan.component.html',
    styleUrls: ['./food-plan.component.css'],
    standalone: false
})
export class FoodPlanComponent implements OnInit, OnDestroy {
    public readonly mealSlots = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
    public recipes: Recipe[] = [];
    public entries: FoodPlanEntry[] = [];
    public weekDays: Date[] = [];
    public loading = true;
    public saving = false;
    public addingWeekToGroceries = false;
    public error = '';
    public groceryFeedbackMessage = '';
    public groceryFeedbackType: 'success' | 'danger' = 'success';
    public showRecipeSelectionModal = false;
    public draft: FoodPlanEntryRequest;
    private languageSubscription?: Subscription;
    private weekStart = this.getWeekStart(new Date());

    constructor(private foodPlanService: FoodPlanService, private recipeService: RecipeService, private userService: UserService, private groceryService: GroceryService, private router: Router, private utilityService: UtilityService, private languageService: LanguageService, private translateService: TranslateService) {
        this.draft = this.createDraft();
    }

    public ngOnInit(): void {
        this.setWeekDays();
        this.loadRecipes();
        this.loadEntries();
        this.languageSubscription = this.translateService.onLangChange.subscribe(() => this.loadRecipes());
    }

    public ngOnDestroy(): void {
        this.languageSubscription?.unsubscribe();
    }

    public get weekLabel(): string {
        const end = new Date(this.weekStart);
        end.setDate(end.getDate() + 6);
        return `${this.formatDisplayDate(this.weekStart)} - ${this.formatDisplayDate(end)}`;
    }

    public get plannedCount(): number {
        return this.entries.length;
    }

    public get weekNutritionTotals(): FoodPlanNutritionTotals {
        return this.getNutritionTotals(this.entries);
    }

    public getDayNutritionTotals(day: Date): FoodPlanNutritionTotals {
        return this.getNutritionTotals(this.entriesForDay(day));
    }

    private getNutritionTotals(entries: FoodPlanEntry[]): FoodPlanNutritionTotals {
        return entries.reduce((totals, entry) => {
            const recipe = entry.recipe;
            if (!recipe) return totals;

            if (recipe.calories != null || recipe.proteinGrams != null || recipe.carbohydrateGrams != null || recipe.fatGrams != null) {
                totals.hasData = true;
            }

            totals.calories += recipe.calories ?? 0;
            totals.proteinGrams += recipe.proteinGrams ?? 0;
            totals.carbohydrateGrams += recipe.carbohydrateGrams ?? 0;
            totals.fatGrams += recipe.fatGrams ?? 0;
            return totals;
        }, { calories: 0, proteinGrams: 0, carbohydrateGrams: 0, fatGrams: 0, hasData: false } as FoodPlanNutritionTotals);
    }

    public get mealSlotLabels(): Record<string, string> {
        return Object.fromEntries(this.mealSlots.map(mealSlot => [mealSlot, getTaxonomyValueLabel(mealSlot, this.translateService)]));
    }

    public previousWeek(): void {
        this.weekStart.setDate(this.weekStart.getDate() - 7);
        this.setWeekDays();
        this.loadEntries();
    }

    public nextWeek(): void {
        this.weekStart.setDate(this.weekStart.getDate() + 7);
        this.setWeekDays();
        this.loadEntries();
    }

    public currentWeek(): void {
        this.weekStart = this.getWeekStart(new Date());
        this.setWeekDays();
        this.loadEntries();
    }

    public addEntry(): void {
        if (!this.draft.recipeId || !this.draft.plannedDate || this.saving) return;
        this.saving = true;
        this.error = '';
        this.foodPlanService.create(this.draft).subscribe({
            next: () => {
                this.saving = false;
                this.draft = this.createDraft(this.draft.plannedDate);
                this.loadEntries();
            },
            error: () => {
                this.saving = false;
                this.error = 'Could not save this meal plan entry.';
            }
        });
    }

    public addWeekToGroceries(): void {
        if (this.entries.length === 0 || this.addingWeekToGroceries) return;

        const recipeIds = this.entries.map(entry => entry.recipeId);
        this.addingWeekToGroceries = true;
        this.groceryFeedbackMessage = '';

        forkJoin(recipeIds.map(recipeId => this.recipeService.getRecipeById(recipeId)))
            .pipe(finalize(() => this.addingWeekToGroceries = false))
            .subscribe({
                next: recipes => {
                    recipes.filter(recipe => !!recipe?.ingredients?.length).forEach(recipe => this.groceryService.addRecipeToList(recipe));
                    this.groceryFeedbackType = 'success';
                    this.groceryFeedbackMessage = this.translateService.instant('foodPlan.addedWeekToGroceries', { count: recipes.length });
                },
                error: () => {
                    this.groceryFeedbackType = 'danger';
                    this.groceryFeedbackMessage = this.translateService.instant('foodPlan.addWeekToGroceriesError');
                }
            });
    }

    public get selectedRecipe(): Recipe | undefined {
        return this.recipes.find(recipe => recipe.id === this.draft.recipeId);
    }

    public openRecipeSelectionModal(): void {
        this.showRecipeSelectionModal = true;
    }

    public closeRecipeSelectionModal(): void {
        this.showRecipeSelectionModal = false;
    }

    public selectPlanRecipe(recipe: Recipe): void {
        this.draft.recipeId = recipe.id;
        this.closeRecipeSelectionModal();
    }

    public removeEntry(entry: FoodPlanEntry): void {
        this.foodPlanService.delete(entry.id, this.userService.getUserId()).subscribe({
            next: () => this.entries = this.entries.filter(candidate => candidate.id !== entry.id),
            error: () => this.error = 'Could not remove this meal plan entry.'
        });
    }

    public entriesForDay(day: Date): FoodPlanEntry[] {
        const date = this.toDateInputValue(day);
        return this.entries.filter(entry => this.toDateOnly(entry.occurrenceDate) === date);
    }

    public planForDay(day: Date): void {
        this.draft.plannedDate = this.toDateInputValue(day);
    }

    public isToday(day: Date): boolean {
        return this.toDateInputValue(day) === this.toDateInputValue(new Date());
    }

    public openRecipe(entry: FoodPlanEntry): void {
        if (!entry.recipe) return;
        this.router.navigate([`recipe/${this.utilityService.toRecipeKey(entry.recipe.id, entry.recipe.title)}`]);
    }

    public formatDayName(day: Date): string {
        return day.toLocaleDateString(this.languageService.getCurrentLanguage(), { weekday: 'short' });
    }

    public formatDayNumber(day: Date): string {
        return day.toLocaleDateString(this.languageService.getCurrentLanguage(), { month: 'short', day: 'numeric' });
    }

    public trackEntry(_: number, entry: FoodPlanEntry): string {
        return `${entry.id}-${entry.occurrenceDate}`;
    }

    private loadRecipes(): void {
        this.recipeService.getRecipesPaged({
            page: 1,
            pageSize: 500,
            sortBy: 'title',
            ascending: true,
            language: this.getRecipeLanguage()
        }).subscribe({
            next: result => {
                this.recipes = (result?.items ?? []).sort((first, second) => first.title.localeCompare(second.title));
                this.applyTranslatedRecipesToEntries();
            },
            error: () => {
                this.recipes = [];
            }
        });
    }

    private loadEntries(): void {
        const userId = this.userService.getUserId();
        if (!userId) {
            this.loading = false;
            this.error = 'Log in to plan your meals.';
            return;
        }

        const end = new Date(this.weekStart);
        end.setDate(end.getDate() + 6);
        this.loading = true;
        this.foodPlanService.getEntries(userId, this.toDateInputValue(this.weekStart), this.toDateInputValue(end)).subscribe({
            next: entries => {
                this.entries = entries;
                this.applyTranslatedRecipesToEntries();
                this.loading = false;
            },
            error: () => {
                this.error = 'Could not load your food plan.';
                this.loading = false;
            }
        });
    }

    private setWeekDays(): void {
        this.weekDays = Array.from({ length: 7 }, (_, index) => {
            const day = new Date(this.weekStart);
            day.setDate(day.getDate() + index);
            return day;
        });
    }

    private applyTranslatedRecipesToEntries(): void {
        if (this.recipes.length === 0 || this.entries.length === 0) return;

        this.entries = this.entries.map(entry => ({
            ...entry,
            recipe: this.recipes.find(recipe => recipe.id === entry.recipeId) ?? entry.recipe
        }));
    }

    private getRecipeLanguage(): string {
        return { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[this.languageService.getCurrentLanguage()] ?? 'English';
    }

    private createDraft(plannedDate = this.toDateInputValue(new Date())): FoodPlanEntryRequest {
        return { userId: this.userService.getUserId(), recipeId: '', plannedDate, mealSlot: 'Dinner', notes: '', repeatWeekly: false, repeatUntil: null, position: 0 };
    }

    private getWeekStart(date: Date): Date {
        const start = new Date(date);
        const day = start.getDay();
        const offset = day === 0 ? -6 : 1 - day;
        start.setHours(0, 0, 0, 0);
        start.setDate(start.getDate() + offset);
        return start;
    }

    private toDateInputValue(date: Date): string {
        const year = date.getFullYear();
        const month = `${date.getMonth() + 1}`.padStart(2, '0');
        const day = `${date.getDate()}`.padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    private toDateOnly(value: string): string {
        return value.slice(0, 10);
    }

    private formatDisplayDate(date: Date): string {
        return date.toLocaleDateString(this.languageService.getCurrentLanguage(), { month: 'short', day: 'numeric' });
    }
}