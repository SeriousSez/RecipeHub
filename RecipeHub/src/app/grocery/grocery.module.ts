import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { GroceryRoutingModule } from './grocery.routing';
import { UserService } from '../shared/services/user.service';
import { SharedModule } from '../shared/modules/shared.module';
import { RecipeService } from '../recipe/services/recipe.service';
import { HeaderComponent } from '../header/header.component';
import { GroceryComponent } from './grocery/grocery.component';

@NgModule({
  declarations: [
    GroceryComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    
    GroceryRoutingModule,
    SharedModule
  ],
  providers: [ UserService, RecipeService, HeaderComponent ],
  bootstrap: []
})
export class GroceryModule { }