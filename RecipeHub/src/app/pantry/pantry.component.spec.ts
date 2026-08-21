import { PantryComponent } from './pantry.component';

describe('PantryComponent', () => {
    let component: PantryComponent;

    beforeEach(() => {
        localStorage.clear();
        component = new PantryComponent();
        component.ngOnInit();
    });

    it('loads pantry ingredients from local storage', () => {
        localStorage.setItem('recipehub-pantry-ingredients', 'eggs, spinach, tomatoes');

        component = new PantryComponent();
        component.ngOnInit();

        expect(component.pantryIngredients.length).toBe(3);
        expect(component.pantryIngredients[0]).toBe('eggs');
    });

    it('filters pantry ingredients by the current search term', () => {
        component.pantryIngredients = ['eggs', 'spinach', 'tomatoes', 'milk'];
        component.searchTerm = 'spin';

        expect(component.filteredPantryIngredients).toEqual(['spinach']);
    });
});
