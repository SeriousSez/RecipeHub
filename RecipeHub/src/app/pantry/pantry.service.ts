import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { ConfigService } from '../shared/utils/config.service';
import { PantryItem } from './pantry-item.interface';

export type PantryItemInput = Pick<PantryItem, 'name' | 'amount' | 'amountType'>;

@Injectable({ providedIn: 'root' })
export class PantryService {
    private readonly baseUrl: string;
    private readonly pantryNamesStorageKey = 'recipehub-pantry-ingredients';
    private readonly pantryItemsStorageKey = 'recipehub-pantry-items';

    constructor(private http: HttpClient, configService: ConfigService) {
        this.baseUrl = configService.getApiURI();
    }

    public getItems(userId: string): Observable<PantryItem[]> {
        return this.http.get<PantryItem[]>(`${this.baseUrl}/account/getpantry?userId=${encodeURIComponent(userId)}`, this.httpOptions);
    }

    public updateItems(userId: string, items: PantryItem[]): Observable<PantryItem[]> {
        return this.http.post<PantryItem[]>(`${this.baseUrl}/account/updatepantry`, { userId, items }, this.httpOptions);
    }

    public addItems(items: PantryItemInput[], userId?: string): Observable<PantryItem[]> {
        if (userId) {
            return this.getItems(userId).pipe(switchMap(accountItems => {
                const pantryItems = this.mergeItems(accountItems.length > 0 ? accountItems : this.getLocalItems(), items);
                this.persistLocalItems(pantryItems);
                return this.updateItems(userId, pantryItems);
            }));
        }

        const pantryItems = this.mergeItems(this.getLocalItems(), items);
        this.persistLocalItems(pantryItems);
        return of(pantryItems);
    }

    public consumeItems(items: PantryItemInput[], userId?: string): Observable<PantryItem[]> {
        if (userId) {
            return this.getItems(userId).pipe(switchMap(accountItems => {
                const pantryItems = this.decrementItems(accountItems.length > 0 ? accountItems : this.getLocalItems(), items);
                this.persistLocalItems(pantryItems);
                return this.updateItems(userId, pantryItems);
            }));
        }

        const pantryItems = this.decrementItems(this.getLocalItems(), items);
        this.persistLocalItems(pantryItems);
        return of(pantryItems);
    }

    private mergeItems(pantryItems: PantryItem[], items: PantryItemInput[]): PantryItem[] {
        items.forEach(item => {
            const name = item.name.trim().replace(/\s+/g, ' ');
            if (!name) return;

            const existing = pantryItems.find(candidate => candidate.name.toLowerCase() === name.toLowerCase());
            if (existing) {
                existing.amount = item.amount;
                existing.amountType = item.amountType;
            } else {
                pantryItems.push({ id: this.createId(), name, amount: item.amount, amountType: item.amountType, expirationDate: null });
            }
        });

        pantryItems.sort((first, second) => first.name.localeCompare(second.name));
        return pantryItems;
    }

    private decrementItems(pantryItems: PantryItem[], items: PantryItemInput[]): PantryItem[] {
        items.forEach(item => {
            const name = item.name.trim().replace(/\s+/g, ' ');
            const amount = item.amount ?? 0;
            if (!name || amount <= 0) return;

            const existing = pantryItems.find(candidate =>
                candidate.name.toLowerCase() === name.toLowerCase() &&
                candidate.amountType.toLowerCase() === item.amountType.toLowerCase());

            if (!existing || existing.amount == null) return;

            existing.amount -= amount;
            if (existing.amount <= 0) {
                pantryItems.splice(pantryItems.indexOf(existing), 1);
            }
        });

        pantryItems.sort((first, second) => first.name.localeCompare(second.name));
        return pantryItems;
    }

    private getLocalItems(): PantryItem[] {
        const storedItems = localStorage.getItem(this.pantryItemsStorageKey);
        if (storedItems) {
            try { return JSON.parse(storedItems) as PantryItem[]; }
            catch { localStorage.removeItem(this.pantryItemsStorageKey); }
        }

        return (localStorage.getItem(this.pantryNamesStorageKey) ?? '')
            .split(',')
            .map(name => name.trim().replace(/\s+/g, ' '))
            .filter(Boolean)
            .map(name => ({ id: this.createId(), name, amount: null, amountType: 'Piece', expirationDate: null }));
    }

    private persistLocalItems(items: PantryItem[]): void {
        localStorage.setItem(this.pantryItemsStorageKey, JSON.stringify(items));
        const names = items.map(item => item.name).join(', ');
        if (names) localStorage.setItem(this.pantryNamesStorageKey, names);
        else localStorage.removeItem(this.pantryNamesStorageKey);
    }

    private createId(): string {
        return typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
    }

    private get httpOptions(): { headers: HttpHeaders } {
        return { headers: new HttpHeaders({ 'Content-Type': 'application/json', 'Authorization': `Bearer ${localStorage.getItem('authToken') ?? ''}` }) };
    }
}