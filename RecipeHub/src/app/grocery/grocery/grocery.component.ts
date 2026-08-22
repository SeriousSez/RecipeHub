import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { IngredientService } from 'src/app/recipe/services/ingredient.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { finalize } from 'rxjs/operators';
import { TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-grocery',
  templateUrl: './grocery.component.html',
  styleUrls: ['./grocery.component.css'],
  standalone: false
})
export class GroceryComponent implements OnInit {
  @ViewChild('ingredientModal') private ingredientModal: ElementRef;
  @ViewChild('deleteButton') private deleteButton: ElementRef;

  targetUrl: string = '/dashboard/createingredients';

  ingredients: Ingredient[] = [];
  loadingIngredientDetails: Set<string> = new Set<string>();
  loadedIngredientDetails: Set<string> = new Set<string>();

  selectedIngredients: Ingredient[] = [];
  confirmClearList: boolean = false;
  showAddIngredient: boolean = false;
  ingredientOptions: string[] = [];
  draftIngredientName: string = '';
  draftIngredientAmount: number | null = null;
  draftIngredientUnit: string = 'Piece';
  readonly ingredientUnits: string[] = ['Piece', 'Milliliter', 'Liter', 'Teaspoon', 'Tablespoon', 'Cup', 'Gram', 'Kilogram', 'Ounce', 'Pound', 'Clove'];
  openedAccordion: string;
  clickedTableRow: string;

  public sortSetting: string = 'name';
  public ascending: boolean = true;

  constructor(private groceryService: GroceryService, private ingredientService: IngredientService, private datepipe: DatePipe, private router: Router, private translateService: TranslateService) { }

  ngOnInit() {
    this.getIngredients();
    this.ingredientService.getIngredientsLite().subscribe({
      next: ingredients => this.ingredientOptions = ingredients.map(ingredient => ingredient.name).sort((first, second) => first.localeCompare(second)),
      error: () => this.ingredientOptions = []
    });
  }

  getIngredients() {
    this.ingredients = this.groceryService.getIngredientList();
  }

  get ingredientCount() {
    return this.ingredients.length;
  }

  get selectedIngredientCount() {
    return this.selectedIngredients.length;
  }

  get hasIngredients() {
    return this.ingredientCount > 0;
  }

  get sortLabel() {
    switch (this.sortSetting) {
      case 'amount':
        return this.translateService.instant('grocery.amountColumn');
      case 'created':
        return this.translateService.instant('grocery.createdColumn');
      default:
        return this.translateService.instant('grocery.ingredientColumn');
    }
  }

  get groceryListOverlayTitle() {
    return this.translateService.instant('grocery.addToGroceryListTitle');
  }

  save() {
    this.groceryService.createGroceryList().subscribe(result => {

    }, error => {

    });
  }

  addIngredientToList() {
    const name = this.draftIngredientName.trim().replace(/\s+/g, ' ');
    if (!name || this.draftIngredientAmount == null || this.draftIngredientAmount <= 0) return;

    const ingredient: Ingredient = {
      name,
      description: '',
      amount: this.draftIngredientAmount,
      amountType: this.draftIngredientUnit,
      created: new Date().toISOString(),
      image: null
    };

    this.groceryService.addIngredientsToList([ingredient]);
    this.getIngredients();
    this.draftIngredientName = '';
    this.draftIngredientAmount = null;
    this.draftIngredientUnit = 'Piece';
    this.showAddIngredient = false;
  }

  removeSelectedFromIngredients() {
    [...this.selectedIngredients].forEach(ingredient => {
      this.groceryService.removeIngredientFromList(ingredient);
    });
    this.selectedIngredients = [];
    this.getIngredients();
  }

  removeIngredient(ingredient: Ingredient) {
    this.groceryService.removeIngredientFromList(ingredient);
    this.selectedIngredients = this.selectedIngredients.filter(item => item !== ingredient);
    this.getIngredients();
  }

  clearGroceryList() {
    this.groceryService.clearRecipeList();
    this.selectedIngredients = [];
    this.confirmClearList = false;
    this.getIngredients();
  }

  toggleIngredientSelected(ingredient: Ingredient) {
    var index = this.selectedIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.selectedIngredients.splice(index, 1);
    } else {
      this.selectedIngredients.push(ingredient);
    }
  }

  isIngredientSelected(ingredient: Ingredient) {
    return this.selectedIngredients.indexOf(ingredient, 0) > -1;
  }

  clearSelection() {
    this.selectedIngredients = [];
  }

  removeIngredientFromList(ingredient: Ingredient) {
    var index = this.ingredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.ingredients.splice(index, 1);
    } else {
      this.ingredients.push(ingredient);
    }

    this.getIngredients();
  }

  displayDateOnly(created: string) {
    return this.datepipe.transform(created, 'dd-MM-yyyy');
  }

  sort(sortSetting: string) {
    this.toggleAccordion(this.openedAccordion, this.clickedTableRow);
    if (this.sortSetting != sortSetting) this.ascending = true;
    this.sortSetting = sortSetting;

    switch (sortSetting) {
      case 'name':
        this.ingredients.sort((a, b) => this.ascending == true ? a.name.localeCompare(b.name) : -(a.name.localeCompare(b.name)));
        this.ascending = !this.ascending;
        return;
      case 'amount':
        this.ingredients.sort((a, b) => this.ascending == true
          ? (a.amount - b.amount) || a.name.localeCompare(b.name)
          : (b.amount - a.amount) || b.name.localeCompare(a.name));
        this.ascending = !this.ascending;
        return;
      case 'created':
        this.ingredients.sort((a, b) => this.ascending == true ? a.created.localeCompare(b.created) : -a.created.localeCompare(b.created));
        this.ascending = !this.ascending;
        return;
    }
  }

  public closeIngredientModal(ingredient: Ingredient) {
    this.ingredients.push(ingredient);
    this.ingredientModal.nativeElement.click();
  }

  public closeDeleteModal() {
    this.deleteButton.nativeElement.click();
  }

  toggleAccordion(id: string, tableRowId: string) {
    this.toggleTableRowClass(tableRowId);

    var accordion = document.getElementById(id);
    if (accordion == null) return;

    this.handleAccordionStyle(accordion, id, tableRowId);
  }

  handleAccordionStyle(accordion: HTMLElement, id: string, tableRowId: string) {
    if (accordion.style.display == 'table-cell') {
      accordion.style.display = 'none';
      this.openedAccordion = '';
      this.clickedTableRow = '';
    } else {
      this.toggleAccordion(this.openedAccordion, this.clickedTableRow);
      accordion.style.display = 'table-cell';
      this.openedAccordion = id;
      this.clickedTableRow = tableRowId;
    }
  }

  toggleTableRowClass(tableRowId: string) {
    if (tableRowId != '') {
      var tableRow = document.getElementById(tableRowId);
      if (tableRow?.classList.contains("collapsed")) {
        tableRow?.classList.remove("collapsed");
      } else {
        tableRow?.classList.add("collapsed");
      }
    }
  }

  toggleAccordionAndLoad(ingredient: Ingredient, id: string, tableRowId: string) {
    this.loadIngredientDetailsIfNeeded(ingredient);
    this.toggleAccordion(id, tableRowId);
  }

  handleIngredientClick(ingredient: Ingredient, id: string, tableRowId: string) {
    if (this.isMobileViewport()) {
      this.toggleIngredientSelected(ingredient);
      return;
    }

    this.toggleAccordionAndLoad(ingredient, id, tableRowId);
  }

  isIngredientDetailsLoading(ingredient: Ingredient) {
    return this.loadingIngredientDetails.has(this.getIngredientKey(ingredient));
  }

  private loadIngredientDetailsIfNeeded(ingredient: Ingredient) {
    const key = this.getIngredientKey(ingredient);
    if (key == '' || this.loadingIngredientDetails.has(key) || this.loadedIngredientDetails.has(key)) {
      return;
    }

    this.loadingIngredientDetails.add(key);
    this.ingredientService.getIngredientByName(ingredient.name)
      .pipe(finalize(() => this.loadingIngredientDetails.delete(key)))
      .subscribe((result: Ingredient) => {
        if (!result) {
          return;
        }

        ingredient.image = result.image;
        ingredient.description = result.description;
        this.loadedIngredientDetails.add(key);
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  private getIngredientKey(ingredient: Ingredient) {
    return ingredient?.name?.trim().toLowerCase() ?? '';
  }

  private isMobileViewport() {
    return typeof window !== 'undefined' && window.innerWidth <= 600;
  }
}
