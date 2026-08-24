import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { IngredientService } from '../recipe/services/ingredient.service';
import { UserService } from '../shared/services/user.service';
import { PantryItem } from './pantry-item.interface';
import { PantryService } from './pantry.service';
import { TranslateService } from '@ngx-translate/core';

@Component({
    selector: 'app-pantry',
    templateUrl: './pantry.component.html',
    styleUrls: ['./pantry.component.css'],
    standalone: false
})
export class PantryComponent implements OnInit {
    private readonly pantryNamesStorageKey = 'recipehub-pantry-ingredients';
    private readonly pantryItemsStorageKey = 'recipehub-pantry-items';
    public readonly units = ['Piece', 'Gram', 'Kilogram', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Ounce', 'Pound'];
    public pantryItems: PantryItem[] = [];
    public ingredientOptions: string[] = [];
    public searchTerm = '';
    public draftName = '';
    public draftAmount: number | null = null;
    public draftUnit = 'Piece';
    public draftExpirationDate = '';
    public confirmClear = false;
    public syncing = false;
    public syncFailed = false;
    public translatingLocalIngredients = false;
    private ingredientNameLabels: Record<string, string> = {};

    constructor(private pantryService?: PantryService, private ingredientService?: IngredientService, private userService?: UserService, private router?: Router, private translateService?: TranslateService) { }

    public ngOnInit(): void {
        this.loadLocalItems();
        this.loadIngredientOptions();
        this.loadAccountItems();
        this.translateLocalItems();
        this.translateService?.onLangChange.subscribe(() => this.translateLocalItems());
    }

    public get filteredPantryItems(): PantryItem[] {
        const query = this.searchTerm.trim().toLowerCase();
        return this.pantryItems.filter(item => !query || item.name.toLowerCase().includes(query));
    }

    public get isAuthenticated(): boolean { return this.userService?.isAuthenticated() === true; }

    public getItemDisplayName(item: PantryItem): string {
        const lookupKey = item.name?.trim();
        if (!lookupKey) return item.name;
        return this.ingredientNameLabels[lookupKey.toLowerCase()] ?? item.name;
    }

    public addItem(): void {
        const name = this.normalizeName(this.draftName);
        if (!name) return;
        const existing = this.pantryItems.find(item => item.name.toLowerCase() === name.toLowerCase());
        if (existing) {
            existing.amount = this.draftAmount;
            existing.amountType = this.draftUnit;
            existing.expirationDate = this.draftExpirationDate || null;
        } else {
            this.pantryItems = [...this.pantryItems, { id: this.createId(), name, amount: this.draftAmount, amountType: this.draftUnit, expirationDate: this.draftExpirationDate || null }];
        }
        this.sortItems();
        this.resetDraft();
        this.persistItems();
    }

    public updateItem(item: PantryItem): void { item.name = this.normalizeName(item.name); item.expirationDate = item.expirationDate || null; this.persistItems(); }
    public removeItem(item: PantryItem): void { this.pantryItems = this.pantryItems.filter(candidate => candidate.id !== item.id); this.persistItems(); }
    public clearAll(): void { this.pantryItems = []; this.confirmClear = false; this.persistItems(); }
    public findRecipes(): void { this.router?.navigate(['/recipes/overview'], { queryParams: { pantry: 'true' } }); }
    public isExpired(item: PantryItem): boolean { return !!item.expirationDate && item.expirationDate < this.todayDate(); }
    public isExpiringSoon(item: PantryItem): boolean {
        if (!item.expirationDate || this.isExpired(item)) return false;
        return new Date(`${item.expirationDate}T00:00:00`).getTime() <= Date.now() + (3 * 24 * 60 * 60 * 1000);
    }

    private loadIngredientOptions(): void {
        this.ingredientService?.getIngredientsLite().subscribe({ next: items => this.ingredientOptions = items.map(item => item.name).sort((a, b) => a.localeCompare(b)), error: () => this.ingredientOptions = [] });
    }

    private getRequestedLanguage(): string {
        const languageCode = this.translateService?.currentLang || 'en';
        return { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[languageCode] ?? 'English';
    }

    private translateLocalItems(): void {
        const language = this.getRequestedLanguage();
        if (language === 'English' || this.pantryItems.length === 0) {
            this.translatingLocalIngredients = false;
            return;
        }
        this.translatingLocalIngredients = true;
        const names = this.pantryItems.map(item => item.name);
        this.ingredientService?.translate(names, language).subscribe({
            next: translations => {
                Object.entries(translations ?? {}).forEach(([name, displayName]) => {
                    if (displayName) this.ingredientNameLabels[name.toLowerCase()] = displayName;
                });
                this.translatingLocalIngredients = false;
            },
            error: () => { this.translatingLocalIngredients = false; }
        });
    }

    private loadAccountItems(): void {
        if (!this.isAuthenticated) return;
        const userId = this.userService?.getUserId() ?? '';
        if (!userId) return;
        this.syncing = true;
        this.pantryService?.getItems(userId).subscribe({
            next: items => {
                if (items.length === 0 && this.pantryItems.length > 0) this.persistItems();
                else { this.pantryItems = items.map(item => ({ ...item, expirationDate: item.expirationDate?.slice(0, 10) ?? null })); this.sortItems(); this.persistLocalItems(); }
                this.syncing = false;
                this.translateLocalItems();
            },
            error: () => { this.syncFailed = true; this.syncing = false; }
        });
    }

    private loadLocalItems(): void {
        if (typeof localStorage === 'undefined') return;
        const structuredItems = localStorage.getItem(this.pantryItemsStorageKey);
        if (structuredItems) {
            try { this.pantryItems = JSON.parse(structuredItems); this.sortItems(); return; }
            catch { localStorage.removeItem(this.pantryItemsStorageKey); }
        }
        this.pantryItems = (localStorage.getItem(this.pantryNamesStorageKey) ?? '').split(',').map(name => this.normalizeName(name)).filter(Boolean).map(name => ({ id: this.createId(), name, amount: null, amountType: 'Piece', expirationDate: null }));
        this.sortItems();
        this.persistLocalItems();
    }

    private persistItems(): void {
        this.persistLocalItems();
        if (!this.isAuthenticated) return;
        const userId = this.userService?.getUserId() ?? '';
        if (!userId) return;
        this.syncing = true;
        this.syncFailed = false;
        this.pantryService?.updateItems(userId, this.pantryItems).subscribe({ next: () => this.syncing = false, error: () => { this.syncFailed = true; this.syncing = false; } });
    }

    private persistLocalItems(): void {
        if (typeof localStorage === 'undefined') return;
        localStorage.setItem(this.pantryItemsStorageKey, JSON.stringify(this.pantryItems));
        const names = this.pantryItems.map(item => item.name).join(', ');
        if (names) localStorage.setItem(this.pantryNamesStorageKey, names); else localStorage.removeItem(this.pantryNamesStorageKey);
    }

    private resetDraft(): void { this.draftName = ''; this.draftAmount = null; this.draftUnit = 'Piece'; this.draftExpirationDate = ''; }
    private normalizeName(value: string): string { return (value ?? '').trim().replace(/\s+/g, ' '); }
    private sortItems(): void { this.pantryItems.sort((first, second) => first.name.localeCompare(second.name)); }
    private createId(): string { return typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`; }
    private todayDate(): string { return new Date().toISOString().slice(0, 10); }
}
