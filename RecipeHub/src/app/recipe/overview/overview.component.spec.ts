import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OverviewComponent } from './overview.component';

describe('OverviewComponent', () => {
  let component: OverviewComponent;
  let fixture: ComponentFixture<OverviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [OverviewComponent]
    })
      .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(OverviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('filters recipes by category, tag, and search text', () => {
    component.recipeList = [
      {
        id: '1',
        title: 'Veggie Omelette',
        creator: 'Sam',
        description: 'A quick breakfast bowl',
        instructions: 'Whisk eggs and vegetables.',
        portions: '2',
        created: '2024-01-01T00:00:00Z',
        image: null,
        ingredients: [
          { name: 'Eggs', description: '', amount: 2, amountType: 'Piece', created: '', image: null },
          { name: 'Spinach', description: '', amount: 1, amountType: 'Cup', created: '', image: null }
        ],
        categories: ['breakfast', 'healthy'],
        tags: ['quick', 'protein']
      },
      {
        id: '2',
        title: 'Garden Salad',
        creator: 'Alex',
        description: 'A fresh lunch salad',
        instructions: 'Chop and toss the vegetables.',
        portions: '1',
        created: '2024-01-02T00:00:00Z',
        image: null,
        ingredients: [
          { name: 'Lettuce', description: '', amount: 1, amountType: 'Cup', created: '', image: null },
          { name: 'Tomatoes', description: '', amount: 2, amountType: 'Piece', created: '', image: null }
        ],
        categories: ['salad', 'lunch'],
        tags: ['healthy']
      },
      {
        id: '3',
        title: 'Chicken Pasta',
        creator: 'Jamie',
        description: 'Comforting dinner recipe',
        instructions: 'Cook pasta and sauce together.',
        portions: '4',
        created: '2024-01-03T00:00:00Z',
        image: null,
        ingredients: [
          { name: 'Chicken', description: '', amount: 200, amountType: 'Gram', created: '', image: null },
          { name: 'Pasta', description: '', amount: 250, amountType: 'Gram', created: '', image: null }
        ],
        categories: ['dinner'],
        tags: ['comfort']
      }
    ];

    component.categoryFilter = 'breakfast';
    component.tagFilter = 'quick';
    component.searchTerm = 'egg';
    component.applyFiltersAndSort();

    expect(component.shownRecipes.length).toBe(1);
    expect(component.shownRecipes[0].title).toBe('Veggie Omelette');
  });

  it('filters recipes by ingredients the user already has', () => {
    component.recipeList = [
      {
        id: '1',
        title: 'Veggie Omelette',
        creator: 'Sam',
        description: 'A quick breakfast bowl',
        instructions: 'Whisk eggs and vegetables.',
        portions: '2',
        created: '2024-01-01T00:00:00Z',
        image: null,
        ingredients: [
          { name: 'Eggs', description: '', amount: 2, amountType: 'Piece', created: '', image: null },
          { name: 'Spinach', description: '', amount: 1, amountType: 'Cup', created: '', image: null }
        ],
        categories: ['breakfast'],
        tags: ['quick']
      },
      {
        id: '2',
        title: 'Chicken Pasta',
        creator: 'Jamie',
        description: 'Comforting dinner recipe',
        instructions: 'Cook pasta and sauce together.',
        portions: '4',
        created: '2024-01-03T00:00:00Z',
        image: null,
        ingredients: [
          { name: 'Chicken', description: '', amount: 200, amountType: 'Gram', created: '', image: null },
          { name: 'Pasta', description: '', amount: 250, amountType: 'Gram', created: '', image: null }
        ],
        categories: ['dinner'],
        tags: ['comfort']
      }
    ];

    component.pantryIngredients = 'eggs, cheese';
    component.showPantryMatches = true;
    component.matchingRecipes = component.recipeList;
    component.applyFiltersAndSort();

    expect(component.shownRecipes.length).toBe(1);
    expect(component.shownRecipes[0].title).toBe('Veggie Omelette');
    expect(component.getIngredientMatchScore(component.recipeList[0])).toBe(1);
  });

  it('shows only pantry matches when enabled and restores all recipes when disabled', () => {
    component.pantryIngredients = 'eggs, spinach';
    component.recipeList = [
      {
        id: '1',
        title: 'Egg Omelette',
        creator: 'Sam',
        description: '',
        instructions: '',
        portions: '1',
        created: '2024-01-01T00:00:00Z',
        image: null,
        ingredients: [{ name: 'Eggs', description: '', amount: 1, amountType: 'Piece', created: '', image: null }],
        categories: [],
        tags: []
      },
      {
        id: '2',
        title: 'Tomato Soup',
        creator: 'Alex',
        description: '',
        instructions: '',
        portions: '1',
        created: '2024-01-02T00:00:00Z',
        image: null,
        ingredients: [{ name: 'Tomatoes', description: '', amount: 1, amountType: 'Piece', created: '', image: null }],
        categories: [],
        tags: []
      }
    ];

    component.togglePantryMatches();

    expect(component.showPantryMatches).toBeTrue();
    expect(component.shownRecipes.length).toBe(1);
    expect(component.shownRecipes[0].title).toBe('Egg Omelette');
    expect(component.pantryButtonLabel).toBe('Show all recipes');

    component.togglePantryMatches();

    expect(component.showPantryMatches).toBeFalse();
    expect(component.shownRecipes.length).toBe(2);
    expect(component.pantryButtonLabel).toBe('Show matching recipes');
  });
});
