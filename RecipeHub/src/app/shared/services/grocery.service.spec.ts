import { GroceryService } from './grocery.service';

describe('GroceryService', () => {
    beforeEach(() => {
        localStorage.clear();
    });

    it('keeps recipe context when adding the same ingredient from multiple recipes', () => {
        const service = new GroceryService(
            {} as any,
            { getApiURI: () => 'http://localhost' } as any,
            { getUserId: () => 'user-1', isAuthenticated: () => false } as any
        );

        const recipeOne = {
            id: 'recipe-1',
            title: 'Spicy noodles',
            creator: 'alice',
            ingredients: [
                {
                    name: 'noodles',
                    description: '',
                    amount: 1,
                    amountType: 'Package',
                    created: '2024-01-01T00:00:00.000Z',
                    image: null
                }
            ],
            description: '',
            instructions: '',
            portions: '2',
            created: '2024-01-01T00:00:00.000Z',
            image: null
        } as any;

        const recipeTwo = {
            id: 'recipe-2',
            title: 'Chicken noodle soup',
            creator: 'alice',
            ingredients: [
                {
                    name: 'noodles',
                    description: '',
                    amount: 1,
                    amountType: 'Package',
                    created: '2024-01-02T00:00:00.000Z',
                    image: null
                }
            ],
            description: '',
            instructions: '',
            portions: '2',
            created: '2024-01-02T00:00:00.000Z',
            image: null
        } as any;

        service.addIngredientsFromRecipeToList(recipeOne);
        service.addIngredientsFromRecipeToList(recipeTwo);

        const noodles = service.getIngredientList().filter(item => item.name === 'noodles');

        expect(noodles.length).toBe(2);
        expect(noodles.every(item => item.sourceRecipeTitle)).toBeTrue();
        expect(noodles.map(item => item.sourceRecipeTitle).sort()).toEqual(['Chicken noodle soup', 'Spicy noodles']);
    });

    it('does not collapse different recipe entries by raw ingredient name alone', () => {
        const service = new GroceryService(
            {} as any,
            { getApiURI: () => 'http://localhost' } as any,
            { getUserId: () => 'user-1', isAuthenticated: () => false } as any
        );

        const chickenRecipe = {
            id: 'recipe-1',
            title: 'Chicken noodle soup',
            creator: 'alice',
            ingredients: [
                { name: 'noodles', description: '', amount: 1, amountType: 'Package', created: '2024-01-01T00:00:00.000Z', image: null },
                { name: 'chicken breast', description: '', amount: 1, amountType: 'Piece', created: '2024-01-01T00:00:00.000Z', image: null }
            ],
            description: '', instructions: '', portions: '2', created: '2024-01-01T00:00:00.000Z', image: null
        } as any;

        const ramenRecipe = {
            id: 'recipe-2',
            title: 'Cup noodles',
            creator: 'alice',
            ingredients: [
                { name: 'noodles', description: '', amount: 1, amountType: 'Package', created: '2024-01-02T00:00:00.000Z', image: null },
                { name: 'garlic', description: '', amount: 2, amountType: 'Clove', created: '2024-01-02T00:00:00.000Z', image: null }
            ],
            description: '', instructions: '', portions: '1', created: '2024-01-02T00:00:00.000Z', image: null
        } as any;

        service.addIngredientsFromRecipeToList(chickenRecipe);
        service.addIngredientsFromRecipeToList(ramenRecipe);

        const recipeNames = service.getIngredientList()
            .filter(item => item.name === 'noodles')
            .map(item => item.sourceRecipeTitle)
            .sort();

        expect(recipeNames).toEqual(['Chicken noodle soup', 'Cup noodles']);
        expect(service.getIngredientList().length).toBe(4);
    });

    it('removes a persisted recipe using its identity rather than object reference', () => {
        const service = new GroceryService(
            {} as any,
            { getApiURI: () => 'http://localhost' } as any,
            { getUserId: () => 'user-1', isAuthenticated: () => false } as any
        );
        const recipe = {
            id: 'recipe-1',
            title: 'Chicken noodle soup',
            creator: 'alice',
            ingredients: [{ name: 'noodles', description: '', amount: 1, amountType: 'Package', created: '2024-01-01T00:00:00.000Z', image: null }]
        } as any;

        service.addIngredientsFromRecipeToList(recipe);
        service.recipeList.push(recipe);
        service.removeRecipeFromList({ ...recipe, ingredients: [...recipe.ingredients] } as any);

        expect(service.getRecipeList()).toEqual([]);
        expect(service.getIngredientList()).toEqual([]);
    });
});
