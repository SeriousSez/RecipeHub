import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { GroceryRoutingModule } from './grocery.routing';
import { UserService } from '../shared/services/user.service';
import { SharedModule } from '../shared/modules/shared.module';
import { RecipeService } from '../recipe/services/recipe.service';
import { HeaderComponent } from '../header/header.component';
import { GroceryComponent } from './grocery/grocery.component';
import { RecipeModule } from '../recipe/recipe.module';
import { IngredientService } from '../recipe/services/ingredient.service';

@NgModule({
  declarations: [
    GroceryComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,

    GroceryRoutingModule,
    SharedModule,
    RecipeModule
  ],
  providers: [UserService, RecipeService, IngredientService, HeaderComponent],
  bootstrap: []
})
export class GroceryModule { }