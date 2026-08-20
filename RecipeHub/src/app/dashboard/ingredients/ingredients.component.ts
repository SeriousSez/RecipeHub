import { DatePipe } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { Router } from '@angular/router';
import { Ingredient } from 'src/app/recipe/models/ingredient.interface';
import { IngredientService } from 'src/app/recipe/services/ingredient.service';
import { GroceryService } from 'src/app/shared/services/grocery.service';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-ingredients',
  templateUrl: './ingredients.component.html',
  styleUrls: ['./ingredients.component.css'],
  standalone: false
})
export class IngredientsComponent implements OnInit {
  @ViewChild('ingredientModal') private ingredientModal: ElementRef;
  @ViewChild('deleteButton') private deleteButton: ElementRef;

  targetUrl: string = '/dashboard/createingredients';

  ingredients: Ingredient[];
  busyIngredientNames: Set<string> = new Set<string>();
  loadingIngredientDetails: Set<string> = new Set<string>();
  loadedIngredientDetails: Set<string> = new Set<string>();

  selectedIngredients: Ingredient[] = [];
  openedAccordion: string;
  clickedTableRow: string;

  public sortSetting: string = 'name';
  public ascending: boolean = true;

  constructor(private ingredientService: IngredientService, private groceryService: GroceryService, private datepipe: DatePipe, private router: Router) { }

  ngOnInit() {
    this.getIngredients();
  }

  getIngredients() {
    this.ingredientService.getIngredientsLite()
      .subscribe((ingredients: Ingredient[]) => {
        this.ingredients = ingredients;
        this.sort(this.sortSetting);
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
  }

  deleteIngredients() {
    this.ingredientService.deleteIngredients(this.selectedIngredients).subscribe((ingredients: Ingredient[]) => {
      this.selectedIngredients.forEach((ingredient: Ingredient) => {
        this.removeIngredientFromList(ingredient);
      });

      this.selectedIngredients = [];
      this.closeDeleteModal();
    },
      error => {
        //this.notificationService.printErrorMessage(error);
      });
  }

  toggleIngredientSelected(ingredient: Ingredient) {
    if (this.isIngredientBusy(ingredient)) {
      return;
    }

    var index = this.selectedIngredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.selectedIngredients.splice(index, 1);
    } else {
      this.selectedIngredients.push(ingredient);
    }
  }

  removeIngredientFromList(ingredient: Ingredient) {
    var index = this.ingredients.indexOf(ingredient, 0);
    if (index > -1) {
      this.ingredients.splice(index, 1);
    } else {
      this.ingredients.push(ingredient);
    }
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

  toggleAccordionIfAvailable(ingredient: Ingredient, id: string, tableRowId: string) {
    if (this.isIngredientBusy(ingredient)) {
      return;
    }

    this.loadIngredientDetailsIfNeeded(ingredient);
    this.toggleAccordion(id, tableRowId);
  }

  isIngredientBusy(ingredient: Ingredient) {
    return this.busyIngredientNames.has(this.getIngredientKey(ingredient));
  }

  regenerateIngredientImage(ingredient: Ingredient) {
    const key = this.getIngredientKey(ingredient);
    if (key == '' || this.busyIngredientNames.has(key)) {
      return;
    }

    this.busyIngredientNames.add(key);

    this.ingredientService.regenerateImage(ingredient.name)
      .pipe(finalize(() => this.busyIngredientNames.delete(key)))
      .subscribe((result: any) => {
        if (result?.ingredient) {
          ingredient.image = result.ingredient.image;
          ingredient.description = result.ingredient.description;
          this.loadedIngredientDetails.add(key);
        }
      },
        error => {
          //this.notificationService.printErrorMessage(error);
        });
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
}
