import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GroceryIngredientOffer, GroceryOfferSearchResponse } from 'src/app/shared/models/grocery-offer-search.interface';
import { GroceryComponent } from './grocery.component';

describe('GroceryComponent', () => {
  let component: GroceryComponent;
  let fixture: ComponentFixture<GroceryComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [ GroceryComponent ]
    })
    .compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(GroceryComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

describe('GroceryComponent cost estimate', () => {
  let component: GroceryComponent;

  const createOffer = (overrides: Partial<GroceryIngredientOffer>): GroceryIngredientOffer => ({
    ingredientName: 'milk',
    productName: 'Milk 1L',
    productId: 'product',
    offerId: 'offer',
    chainName: 'Chain',
    storeName: 'Store',
    storeAddress: 'Address',
    storeCity: 'City',
    distanceKm: 1,
    price: 10,
    currency: 'DKK',
    priceKind: 'regular',
    ...overrides
  });

  const createResponse = (offers: GroceryIngredientOffer[], unmatchedIngredients: string[] = []): GroceryOfferSearchResponse => ({
    stores: [],
    offers,
    unmatchedIngredients,
    generatedAtUtc: new Date().toISOString()
  });

  beforeEach(() => {
    component = new GroceryComponent(null as any, null as any, null as any, null as any, null as any, null as any, null as any, null as any);
  });

  it('returns no estimate before a search has run', () => {
    expect(component.costEstimate).toBeNull();
    expect(component.hasMixedOfferCurrencies).toBeFalse();
  });

  it('returns no estimate when the search found no offers', () => {
    component.nearbyOfferResults = createResponse([], ['milk']);

    expect(component.costEstimate).toBeNull();
  });

  it('sums one offer per ingredient in a single currency', () => {
    component.nearbyOfferResults = createResponse([
      createOffer({ ingredientName: 'milk', offerId: 'milk-1', price: 12 }),
      createOffer({ ingredientName: 'milk', offerId: 'milk-2', price: 9 }),
      createOffer({ ingredientName: 'flour', offerId: 'flour-1', price: 20 })
    ]);

    const estimate = component.costEstimate!;

    expect(estimate.currency).toBe('DKK');
    expect(estimate.total).toBe(32);
    expect(estimate.coveredCount).toBe(2);
    expect(estimate.totalCount).toBe(2);
    expect(estimate.usesOldPrices).toBeFalse();
  });

  it('picks the cheapest offer per ingredient for budget shopping', () => {
    component.offerShoppingPreference = 'budget';
    component.nearbyOfferResults = createResponse([
      createOffer({ ingredientName: 'milk', offerId: 'milk-1', price: 12 }),
      createOffer({ ingredientName: 'milk', offerId: 'milk-2', price: 9 })
    ]);

    expect(component.costEstimate!.total).toBe(9);
  });

  it('suppresses the estimate when offers use mixed currencies', () => {
    component.nearbyOfferResults = createResponse([
      createOffer({ ingredientName: 'milk', offerId: 'milk-1', price: 12, currency: 'DKK' }),
      createOffer({ ingredientName: 'flour', offerId: 'flour-1', price: 20, currency: 'NOK' })
    ]);

    expect(component.costEstimate).toBeNull();
    expect(component.hasMixedOfferCurrencies).toBeTrue();
  });

  it('reports partial coverage when ingredients are unmatched', () => {
    component.nearbyOfferResults = createResponse([createOffer({ ingredientName: 'milk', offerId: 'milk-1', price: 12 })], ['flour', 'sugar']);

    const estimate = component.costEstimate!;

    expect(estimate.coveredCount).toBe(1);
    expect(estimate.totalCount).toBe(3);
  });

  it('calculates savings from original prices', () => {
    component.nearbyOfferResults = createResponse([
      createOffer({ ingredientName: 'milk', offerId: 'milk-1', price: 8, originalPrice: 12, priceKind: 'campaign' }),
      createOffer({ ingredientName: 'flour', offerId: 'flour-1', price: 20 })
    ]);

    const estimate = component.costEstimate!;

    expect(estimate.total).toBe(28);
    expect(estimate.originalTotal).toBe(32);
    expect(estimate.savings).toBe(4);
  });

  it('flags estimates that rely on old prices', () => {
    const oldDate = new Date(Date.now() - 200 * 24 * 60 * 60 * 1000).toISOString();
    component.nearbyOfferResults = createResponse([createOffer({ ingredientName: 'milk', offerId: 'milk-1', validFrom: oldDate })]);

    expect(component.costEstimate!.usesOldPrices).toBeTrue();
  });
});
