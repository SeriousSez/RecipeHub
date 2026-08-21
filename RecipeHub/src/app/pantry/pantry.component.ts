import { Component, OnInit } from '@angular/core';

@Component({
    selector: 'app-pantry',
    templateUrl: './pantry.component.html',
    styleUrls: ['./pantry.component.css'],
    standalone: false
})
export class PantryComponent implements OnInit {
    private readonly pantryStorageKey = 'recipehub-pantry-ingredients';

    public pantryIngredients: string[] = [];
    public searchTerm: string = '';
    public pantryInput: string = '';

    ngOnInit(): void {
        this.loadPantryIngredients();
    }

    get filteredPantryIngredients(): string[] {
        const term = this.searchTerm.trim().toLowerCase();

        if (!term) {
            return this.pantryIngredients;
        }

        return this.pantryIngredients.filter(ingredient =>
            ingredient.toLowerCase().includes(term)
        );
    }

    public loadPantryIngredients(): void {
        if (typeof localStorage === 'undefined') {
            return;
        }

        const savedIngredients = localStorage.getItem(this.pantryStorageKey) ?? '';
        this.pantryIngredients = this.parsePantryIngredients(savedIngredients);
        this.pantryInput = this.pantryIngredients.join(', ');
    }

    public updatePantryInput(value: string): void {
        this.pantryInput = value ?? '';
        this.pantryIngredients = this.parsePantryIngredients(this.pantryInput);
        this.persistPantryIngredients();
    }

    public addIngredient(value: string): void {
        const ingredient = (value ?? '').trim();
        if (!ingredient) {
            return;
        }

        const normalized = this.normalizeIngredient(ingredient);
        if (!normalized) {
            return;
        }

        if (!this.pantryIngredients.some(item => item.toLowerCase() === normalized.toLowerCase())) {
            this.pantryIngredients.push(normalized);
            this.pantryInput = this.pantryIngredients.join(', ');
            this.persistPantryIngredients();
        }
    }

    public removeIngredient(ingredient: string): void {
        this.pantryIngredients = this.pantryIngredients.filter(item => item.toLowerCase() !== ingredient.toLowerCase());
        this.pantryInput = this.pantryIngredients.join(', ');
        this.persistPantryIngredients();
    }

    private parsePantryIngredients(value: string): string[] {
        return (value ?? '')
            .split(',')
            .map(item => this.normalizeIngredient(item))
            .filter(item => item.length > 0);
    }

    private normalizeIngredient(value: string): string {
        return (value ?? '')
            .trim()
            .replace(/\s+/g, ' ')
            .replace(/\s*,\s*/g, ',');
    }

    private persistPantryIngredients(): void {
        if (typeof localStorage === 'undefined') {
            return;
        }

        if (this.pantryIngredients.length === 0) {
            localStorage.removeItem(this.pantryStorageKey);
            return;
        }

        localStorage.setItem(this.pantryStorageKey, this.pantryIngredients.join(', '));
    }
}
