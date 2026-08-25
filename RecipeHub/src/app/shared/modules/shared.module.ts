// include directives/components commonly used in features modules in this shared modules
// and import me into the feature module
// importing them individually results in: Type xxx is part of the declarations of 2 modules: ... Please consider moving to a higher module...
// https://github.com/angular/angular/issues/10646  

import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { Focus } from '../../directives/focus.directive';
import { SpinnerComponent } from '../../spinner/spinner.component';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AlertComponent } from '../alert/alert.component';
import { RegistrationModal } from '../modals/registration/registration.modal';
import { ingredientModal } from '../modals/ingredient/ingredient.modal';
import { ImageCropperComponent } from 'ngx-image-cropper';
import { CKEditorModule } from 'ckeditor4-angular';
import { ListOverlay } from '../overlays/list-overlay/list.overlay';
import { TranslateModule } from '@ngx-translate/core';
import { ConfirmationComponent } from '../confirmation/confirmation.component';
import { TaxonomySelectComponent } from '../../recipe/taxonomy-select/taxonomy-select.component';
import { FoodPlanModalComponent } from '../../food-plan/food-plan-modal/food-plan-modal.component';
import { DatePickerComponent } from '../date-picker/date-picker.component';
import { RecipeSelectionModalComponent } from '../recipe-selection-modal/recipe-selection-modal.component';


@NgModule({
  declarations: [
    Focus,
    SpinnerComponent,
    AlertComponent,
    RegistrationModal,
    ingredientModal,
    ListOverlay,
    ConfirmationComponent,
    TaxonomySelectComponent,
    FoodPlanModalComponent,
    DatePickerComponent,
    RecipeSelectionModalComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    ImageCropperComponent,
    CKEditorModule,
    TranslateModule
  ],
  exports: [
    Focus,
    SpinnerComponent,
    AlertComponent,
    RegistrationModal,
    ingredientModal,
    ListOverlay,
    ConfirmationComponent,
    TaxonomySelectComponent,
    FoodPlanModalComponent,
    DatePickerComponent,
    RecipeSelectionModalComponent,
    ImageCropperComponent,
    TranslateModule
  ],
  providers: []
})

export class SharedModule { }