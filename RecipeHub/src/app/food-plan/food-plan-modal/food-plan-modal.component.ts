import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { forkJoin } from 'rxjs';
import { Recipe } from 'src/app/recipe/models/recipe.interface';
import { UserService } from 'src/app/shared/services/user.service';
import { TranslateService } from '@ngx-translate/core';
import { FoodPlanEntryRequest } from '../food-plan-entry.interface';
import { FoodPlanService } from '../food-plan.service';
import { getTaxonomyValueLabel } from 'src/app/recipe/models/recipe-taxonomy';
import { LanguageService } from 'src/app/shared/services/language.service';

@Component({
    selector: 'app-food-plan-modal',
    templateUrl: './food-plan-modal.component.html',
    styleUrls: ['./food-plan-modal.component.css'],
    standalone: false
})
export class FoodPlanModalComponent implements OnChanges {
    @Input() visible = false;
    @Input() recipes: Recipe[] = [];
    @Output() closed = new EventEmitter<void>();
    @Output() saved = new EventEmitter<void>();

    public saving = false;
    public feedbackMessage = '';
    public feedbackType: 'success' | 'danger' = 'success';
    public selectedDates: string[] = [];
    public mealSlot = 'Dinner';
    public repeatWeekly = false;
    public repeatUntil = '';
    public notes = '';
    public readonly mealSlots = ['Breakfast', 'Lunch', 'Dinner', 'Snack'];
    public weekDays: Date[] = [];
    public plannedRecipes: Recipe[] = [];
    private weekStart = this.getWeekStart(new Date());

    constructor(private foodPlanService: FoodPlanService, private userService: UserService, private translateService: TranslateService, private languageService: LanguageService) { }

    public get mealSlotLabels(): Record<string, string> {
        return Object.fromEntries(this.mealSlots.map(mealSlot => [mealSlot, getTaxonomyValueLabel(mealSlot, this.translateService)]));
    }

    public ngOnChanges(changes: SimpleChanges): void {
        if (changes.visible?.currentValue === true && changes.visible.previousValue !== true) {
            this.reset();
        }
    }

    public close(): void {
        if (this.saving) return;
        this.closed.emit();
    }

    public toggleDate(date: string): void {
        const index = this.selectedDates.indexOf(date);
        this.selectedDates = index >= 0
            ? this.selectedDates.filter(item => item !== date)
            : [...this.selectedDates, date].sort();
    }

    public isDateSelected(date: string): boolean {
        return this.selectedDates.includes(date);
    }

    public get weekLabel(): string {
        const end = new Date(this.weekStart);
        end.setDate(end.getDate() + 6);
        return `${this.displayRangeDate(this.weekStart)} - ${this.displayRangeDate(end)}`;
    }

    public previousWeek(): void {
        this.changeWeek(-7);
    }

    public nextWeek(): void {
        this.changeWeek(7);
    }

    public currentWeek(): void {
        this.weekStart = this.getWeekStart(new Date());
        this.setWeekDays(true);
    }

    public save(): void {
        if (this.plannedRecipes.length === 0 || this.selectedDates.length === 0 || this.saving) return;

        const userId = this.userService.getUserId();
        const requests = this.plannedRecipes.flatMap((recipe, recipeIndex) => this.selectedDates.map((plannedDate, dateIndex) => {
            const entry: FoodPlanEntryRequest = {
                userId,
                recipeId: recipe.id,
                plannedDate,
                mealSlot: this.mealSlot,
                notes: this.notes?.trim() || null,
                repeatWeekly: this.repeatWeekly,
                repeatUntil: this.repeatWeekly && this.repeatUntil ? this.repeatUntil : null,
                position: (dateIndex * this.plannedRecipes.length) + recipeIndex
            };

            return this.foodPlanService.create(entry);
        }));

        this.saving = true;
        this.feedbackMessage = '';
        forkJoin(requests).subscribe({
            next: () => {
                this.saving = false;
                this.feedbackType = 'success';
                this.feedbackMessage = this.translateService.instant('recipe.addedToFoodPlan', { count: requests.length });
                this.saved.emit();
            },
            error: () => {
                this.saving = false;
                this.feedbackType = 'danger';
                this.feedbackMessage = this.translateService.instant('recipe.addToFoodPlanError');
            }
        });
    }

    public displayDay(day: Date): string {
        return day.toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' });
    }

    public displayDayName(day: Date): string {
        return day.toLocaleDateString(this.languageService.getCurrentLanguage(), { weekday: 'short' });
    }

    public displayDayDate(day: Date): string {
        return day.toLocaleDateString(this.languageService.getCurrentLanguage(), { month: 'short', day: 'numeric' });
    }

    public toPlanDate(day: Date): string {
        return this.toDateInputValue(day);
    }

    private reset(): void {
        this.plannedRecipes = [...this.recipes];
        this.weekStart = this.getWeekStart(new Date());
        this.setWeekDays(true);
        this.mealSlot = 'Dinner';
        this.repeatWeekly = false;
        this.repeatUntil = '';
        this.notes = '';
        this.feedbackMessage = '';
        this.feedbackType = 'success';
        this.saving = false;
    }

    private changeWeek(dayOffset: number): void {
        this.weekStart.setDate(this.weekStart.getDate() + dayOffset);
        this.setWeekDays(true);
        this.feedbackMessage = '';
    }

    private setWeekDays(selectDefaultDate: boolean): void {
        this.weekDays = Array.from({ length: 7 }, (_, index) => {
            const date = new Date(this.weekStart);
            date.setDate(date.getDate() + index);
            return date;
        });

        if (!selectDefaultDate) return;

        const today = this.toDateInputValue(new Date());
        this.selectedDates = this.weekDays.some(day => this.toDateInputValue(day) === today)
            ? [today]
            : [this.toDateInputValue(this.weekDays[0])];
    }

    private getWeekStart(date: Date): Date {
        const start = new Date(date);
        const day = start.getDay();
        const offset = day === 0 ? -6 : 1 - day;
        start.setHours(0, 0, 0, 0);
        start.setDate(start.getDate() + offset);
        return start;
    }

    private displayRangeDate(date: Date): string {
        return date.toLocaleDateString(this.languageService.getCurrentLanguage(), { month: 'short', day: 'numeric' });
    }

    private toDateInputValue(date: Date): string {
        const year = date.getFullYear();
        const month = `${date.getMonth() + 1}`.padStart(2, '0');
        const day = `${date.getDate()}`.padStart(2, '0');
        return `${year}-${month}-${day}`;
    }
}