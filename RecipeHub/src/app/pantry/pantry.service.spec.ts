import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ConfigService } from '../shared/utils/config.service';
import { PantryItem } from './pantry-item.interface';
import { PantryService } from './pantry.service';

describe('PantryService', () => {
    let http: jasmine.SpyObj<HttpClient>;
    let service: PantryService;

    beforeEach(() => {
        localStorage.clear();
        http = jasmine.createSpyObj<HttpClient>('HttpClient', ['get', 'post']);
        service = new PantryService(http, { getApiURI: () => '/api' } as ConfigService);
    });

    it('adds grocery items locally and updates matching quantities without losing expiry dates', () => {
        localStorage.setItem('recipehub-pantry-items', JSON.stringify([
            { id: 'milk-id', name: 'Milk', amount: 1, amountType: 'Liter', expirationDate: '2026-08-30' }
        ]));

        let result: PantryItem[] = [];
        service.addItems([
            { name: ' milk ', amount: 2, amountType: 'Liter' },
            { name: 'Eggs', amount: 6, amountType: 'Piece' }
        ]).subscribe(items => result = items);

        expect(result.length).toBe(2);
        expect(result.find(item => item.name === 'Milk')).toEqual({ id: 'milk-id', name: 'Milk', amount: 2, amountType: 'Liter', expirationDate: '2026-08-30' });
        expect(localStorage.getItem('recipehub-pantry-ingredients')).toBe('Eggs, Milk');
    });

    it('merges authenticated additions with account pantry items before updating', () => {
        http.get.and.returnValue(of([
            { id: 'flour-id', name: 'Flour', amount: 1, amountType: 'Kilogram', expirationDate: null }
        ]));
        http.post.and.returnValue(of([]));

        service.addItems([{ name: 'Eggs', amount: 6, amountType: 'Piece' }], 'user-id').subscribe();

        const update = http.post.calls.mostRecent();
        expect(update.args[0]).toBe('/api/account/updatepantry');
        const updateBody = update.args[1] as { items: PantryItem[] };
        expect(updateBody.items.map(item => item.name)).toEqual(['Eggs', 'Flour']);
    });

    it('preserves legacy pantry names when adding the first structured item', () => {
        localStorage.setItem('recipehub-pantry-ingredients', 'Spinach, Tomatoes');

        service.addItems([{ name: 'Eggs', amount: 6, amountType: 'Piece' }]).subscribe();

        const storedItems = JSON.parse(localStorage.getItem('recipehub-pantry-items') ?? '[]') as PantryItem[];
        expect(storedItems.map(item => item.name)).toEqual(['Eggs', 'Spinach', 'Tomatoes']);
    });
});