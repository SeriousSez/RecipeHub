import { PantryComponent } from './pantry.component';

describe('PantryComponent', () => {
    let component: PantryComponent;

    beforeEach(() => {
        localStorage.clear();
        component = new PantryComponent();
        component.ngOnInit();
    });

    it('migrates pantry ingredient names from local storage', () => {
        localStorage.removeItem('recipehub-pantry-items');
        localStorage.setItem('recipehub-pantry-ingredients', 'eggs, spinach, tomatoes');

        component = new PantryComponent();
        component.ngOnInit();

        expect(component.pantryItems.length).toBe(3);
        expect(component.pantryItems[0].name).toBe('eggs');
    });

    it('filters pantry ingredients by the current search term', () => {
        component.pantryItems = [
            { id: '1', name: 'eggs', amount: null, amountType: 'Piece', expirationDate: null },
            { id: '2', name: 'spinach', amount: null, amountType: 'Gram', expirationDate: null }
        ];
        component.searchTerm = 'spin';

        expect(component.filteredPantryItems.map(item => item.name)).toEqual(['spinach']);
    });

    it('adds structured pantry items and keeps recipe matching names in sync', () => {
        component.draftName = 'Milk';
        component.draftAmount = 2;
        component.draftUnit = 'Liter';
        component.addItem();

        expect(component.pantryItems[0].amount).toBe(2);
        expect(localStorage.getItem('recipehub-pantry-ingredients')).toBe('Milk');
    });
});
