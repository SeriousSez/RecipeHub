import { Component, OnDestroy, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { IngredientService } from '../recipe/services/ingredient.service';
import { UserService } from '../shared/services/user.service';
import { PantryItem } from './pantry-item.interface';
import { PantryService } from './pantry.service';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { RecognizedPantryItem } from '../recipe/models/ingredient-photo-recognition.interface';
import { RecipeTaxonomyGroup } from '../recipe/models/recipe-taxonomy';
import { RecipeService } from '../recipe/services/recipe.service';
import { RecipeDraftService } from '../recipe/services/recipe-draft.service';

interface PendingPantryPhoto {
    dataUrl: string;
    base64: string;
    contentType: string;
}

@Component({
    selector: 'app-pantry',
    templateUrl: './pantry.component.html',
    styleUrls: ['./pantry.component.css'],
    standalone: false
})
export class PantryComponent implements OnInit, OnDestroy {
    private readonly pantryNamesStorageKey = 'recipehub-pantry-ingredients';
    private readonly pantryItemsStorageKey = 'recipehub-pantry-items';
    public readonly units = ['Piece', 'Gram', 'Kilogram', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Ounce', 'Pound'];
    public readonly unitGroups: RecipeTaxonomyGroup[] = [
        { id: 'common', labelKey: 'recipe.measurementGroups.common', values: ['Piece', 'Teaspoon', 'Tablespoon', 'Cup'] },
        { id: 'metric', labelKey: 'recipe.measurementGroups.metric', values: ['Gram', 'Kilogram', 'Milliliter', 'Liter'] },
        { id: 'imperial', labelKey: 'recipe.measurementGroups.imperial', values: ['Ounce', 'Pound'] }
    ];
    public pantryItems: PantryItem[] = [];
    public ingredientOptions: string[] = [];
    public ingredientOptionLabels: Record<string, string> = {};
    public get unitLabels(): Record<string, string> {
        return this.units.reduce((labels, unit) => {
            labels[unit] = this.translateService?.instant(`pantry.units.${unit}`) ?? unit;
            return labels;
        }, {} as Record<string, string>);
    }
    public searchTerm = '';
    public draftName = '';
    public draftAmount: number | null = null;
    public draftUnit = 'Piece';
    public draftExpirationDate = '';
    public confirmClear = false;
    public syncing = false;
    public syncFailed = false;
    public translatingLocalIngredients = false;
    public sortColumn: 'name' | 'amount' | 'unit' | 'expirationDate' = 'name';
    public sortAscending = true;
    public showPhotoCapture = false;
    public capturingPhoto = false;
    public photoErrorKey = '';
    public pendingPhotos: PendingPantryPhoto[] = [];
    public recognizedItems: RecognizedPantryItem[] = [];
    public selectedRecognizedNames = new Set<string>();
    public showGenerateRecipe = false;
    public generatingRecipe = false;
    public generateRecipeErrorKey = '';
    public generateRecipePrompt = '';
    private ingredientNameLabels: Record<string, string> = {};
    private languageSubscription?: Subscription;
    private ingredientRequestId = 0;

    constructor(private pantryService?: PantryService, private ingredientService?: IngredientService, private userService?: UserService, private router?: Router, private translateService?: TranslateService, private recipeService?: RecipeService, private recipeDraftService?: RecipeDraftService) { }

    public ngOnInit(): void {
        this.loadLocalItems();
        this.loadIngredientOptions();
        this.loadAccountItems();
        this.languageSubscription = this.translateService?.onLangChange.subscribe(event => this.loadIngredientOptions(event.lang));
    }

    public ngOnDestroy(): void {
        this.languageSubscription?.unsubscribe();
    }

    public get filteredPantryItems(): PantryItem[] {
        const query = this.searchTerm.trim().toLowerCase();
        return this.pantryItems
            .filter(item => !query || item.name.toLowerCase().includes(query))
            .slice()
            .sort((first, second) => this.compareItems(first, second));
    }

    public sortBy(column: 'name' | 'amount' | 'unit' | 'expirationDate'): void {
        if (this.sortColumn === column) this.sortAscending = !this.sortAscending;
        else {
            this.sortColumn = column;
            this.sortAscending = true;
        }
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

    public openPhotoCapture(): void {
        this.showPhotoCapture = true;
        this.photoErrorKey = '';
        this.recognizedItems = [];
        this.selectedRecognizedNames.clear();
        this.pendingPhotos = [];
    }

    public closePhotoCapture(): void {
        this.showPhotoCapture = false;
        this.capturingPhoto = false;
        this.photoErrorKey = '';
        this.recognizedItems = [];
        this.selectedRecognizedNames.clear();
        this.pendingPhotos = [];
    }

    public openGenerateRecipe(): void {
        this.showGenerateRecipe = true;
        this.generateRecipeErrorKey = '';
        this.generateRecipePrompt = '';
    }

    public closeGenerateRecipe(): void {
        this.showGenerateRecipe = false;
        this.generatingRecipe = false;
        this.generateRecipeErrorKey = '';
    }

    public generateRecipeFromPantry(): void {
        if (this.generatingRecipe) return;

        this.generatingRecipe = true;
        this.generateRecipeErrorKey = '';
        const pantryItems = this.pantryItems.map(item => item.name);
        const prompt = this.generateRecipePrompt.trim() || undefined;

        this.recipeService?.generateRecipe({ pantryItems, prompt, language: this.getRequestedLanguage() }).subscribe({
            next: draft => {
                this.generatingRecipe = false;
                if (draft?.errorCode) {
                    this.generateRecipeErrorKey = draft.errorCode === 'not_configured' ? 'pantry.generateRecipeNotConfigured' : 'pantry.generateRecipeFailed';
                    return;
                }
                this.recipeDraftService?.setDraft(draft);
                this.closeGenerateRecipe();
                this.router?.navigate(['/recipe/generate/preview']);
            },
            error: () => {
                this.generatingRecipe = false;
                this.generateRecipeErrorKey = 'pantry.generateRecipeFailed';
            }
        });
    }

    public onPhotoSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        const files = Array.from(input?.files ?? []);
        if (files.length === 0 || this.capturingPhoto) return;

        this.photoErrorKey = '';
        this.recognizedItems = [];
        this.selectedRecognizedNames.clear();

        Promise.all(files.map(file => this.readPhotoAsDataUrl(file))).then(photos => {
            this.pendingPhotos = [...this.pendingPhotos, ...photos.filter((photo): photo is PendingPantryPhoto => !!photo)];
            if (input) input.value = '';
        });
    }

    public removePendingPhoto(index: number): void {
        this.pendingPhotos = this.pendingPhotos.filter((_, photoIndex) => photoIndex !== index);
    }

    public analyzePendingPhotos(): void {
        if (this.pendingPhotos.length === 0 || this.capturingPhoto) return;

        this.capturingPhoto = true;
        this.photoErrorKey = '';
        const images = this.pendingPhotos.map(photo => ({ imageBase64: photo.base64, contentType: photo.contentType }));

        this.ingredientService?.recognizeIngredientsFromPhoto(images, this.getRequestedLanguage()).subscribe({
            next: result => {
                this.capturingPhoto = false;
                this.recognizedItems = (result?.items ?? [])
                    .filter(item => !!item?.name)
                    .map(item => ({ ...item, name: this.capitalizeName(item.name) }));
                this.recognizedItems.forEach(item => this.selectedRecognizedNames.add(item.name));
                if (this.recognizedItems.length === 0) this.photoErrorKey = 'pantry.noIngredientsRecognized';
            },
            error: error => {
                this.capturingPhoto = false;
                const errorCode = error?.error?.errorCode;
                this.photoErrorKey = errorCode === 'not_configured' ? 'pantry.photoRecognitionNotConfigured' : 'pantry.photoRecognitionFailed';
            }
        });
    }

    private readPhotoAsDataUrl(file: File): Promise<PendingPantryPhoto | null> {
        return new Promise(resolve => {
            const reader = new FileReader();
            reader.onload = () => {
                const rawDataUrl = typeof reader.result === 'string' ? reader.result : '';
                if (!rawDataUrl) {
                    resolve(null);
                    return;
                }

                const image = new Image();
                image.onload = () => {
                    // Re-encode into JPEG so unsupported source formats (e.g. AVIF/HEIC captures) still reach the recognition API.
                    const canvas = document.createElement('canvas');
                    canvas.width = image.naturalWidth || image.width;
                    canvas.height = image.naturalHeight || image.height;
                    const context = canvas.getContext('2d');
                    if (!context) {
                        resolve(null);
                        return;
                    }

                    context.drawImage(image, 0, 0);
                    const jpegDataUrl = canvas.toDataURL('image/jpeg', 0.85);
                    const [, base64] = jpegDataUrl.split(',');
                    resolve(base64 ? { dataUrl: jpegDataUrl, base64, contentType: 'image/jpeg' } : null);
                };
                image.onerror = () => resolve(null);
                image.src = rawDataUrl;
            };
            reader.onerror = () => resolve(null);
            reader.readAsDataURL(file);
        });
    }

    public toggleRecognizedIngredient(name: string): void {
        if (this.selectedRecognizedNames.has(name)) this.selectedRecognizedNames.delete(name);
        else this.selectedRecognizedNames.add(name);
    }

    public isRecognizedIngredientSelected(name: string): boolean {
        return this.selectedRecognizedNames.has(name);
    }

    public addRecognizedIngredientsToPantry(): void {
        const items = this.recognizedItems.filter(item => this.selectedRecognizedNames.has(item.name));
        if (items.length === 0) return;

        items.forEach(item => {
            const normalized = this.normalizeName(item.name);
            if (!normalized) return;

            const existing = this.pantryItems.find(candidate => candidate.name.toLowerCase() === normalized.toLowerCase());
            if (existing) {
                existing.amount = item.amount ?? existing.amount;
                existing.amountType = item.amountType ?? existing.amountType;
                existing.expirationDate = item.expirationDate ?? existing.expirationDate;
            } else {
                this.pantryItems = [...this.pantryItems, { id: this.createId(), name: normalized, amount: item.amount ?? null, amountType: item.amountType ?? 'Piece', expirationDate: item.expirationDate ?? null }];
            }
        });

        this.sortItems();
        this.persistItems();
        this.closePhotoCapture();
    }

    private loadIngredientOptions(languageCode: string = this.translateService?.currentLang || 'en'): void {
        const requestId = ++this.ingredientRequestId;
        const requestedLanguage = this.mapUiLanguage(languageCode);
        this.ingredientService?.getIngredientsLite(requestedLanguage).subscribe({
            next: items => {
                if (requestId !== this.ingredientRequestId) return;
                const collator = new Intl.Collator(languageCode, { sensitivity: 'base', numeric: true });
                const sortedItems = items.slice().sort((first, second) => collator.compare(first.displayName ?? first.name, second.displayName ?? second.name));
                this.ingredientOptions = sortedItems.map(item => item.name);
                this.ingredientOptionLabels = Object.fromEntries(sortedItems.map(item => [item.name, item.displayName ?? item.name]));
                this.ingredientNameLabels = { ...this.ingredientOptionLabels };
                this.translateLocalItems();
            },
            error: () => {
                this.ingredientOptions = [];
                this.ingredientOptionLabels = {};
                this.ingredientNameLabels = {};
                this.translateLocalItems();
            }
        });
    }

    private getRequestedLanguage(): string {
        const languageCode = this.translateService?.currentLang || 'en';
        return this.mapUiLanguage(languageCode);
    }

    private mapUiLanguage(languageCode: string): string {
        return { da: 'Danish', et: 'Estonian', tr: 'Turkish' }[languageCode] ?? 'English';
    }

    private translateLocalItems(): void {
        const language = this.getRequestedLanguage();
        if (language === 'English' || this.pantryItems.length === 0) {
            this.ingredientNameLabels = { ...this.ingredientOptionLabels };
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
    private capitalizeName(value: string): string {
        const normalized = this.normalizeName(value);
        return normalized ? normalized.charAt(0).toUpperCase() + normalized.slice(1) : normalized;
    }
    private sortItems(): void { this.pantryItems.sort((first, second) => first.name.localeCompare(second.name)); }
    private compareItems(first: PantryItem, second: PantryItem): number {
        let comparison = 0;
        if (this.sortColumn === 'amount') comparison = (first.amount ?? -Infinity) - (second.amount ?? -Infinity);
        else if (this.sortColumn === 'unit') comparison = first.amountType.localeCompare(second.amountType);
        else if (this.sortColumn === 'expirationDate') comparison = (first.expirationDate || '9999-12-31').localeCompare(second.expirationDate || '9999-12-31');
        else comparison = first.name.localeCompare(second.name);

        return (comparison || first.name.localeCompare(second.name)) * (this.sortAscending ? 1 : -1);
    }
    private createId(): string { return typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`; }
    private todayDate(): string { return new Date().toISOString().slice(0, 10); }
}
