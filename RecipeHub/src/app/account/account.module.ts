import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';

import { AccountRoutingModule } from './account.routing';
import { UserService } from '../shared/services/user.service';
import { SharedModule } from '../shared/modules/shared.module';
import { ProfileComponent } from './profile/profile.component';
import { RecipesComponent } from './recipes/recipes.component';
import { RecipeService } from '../recipe/services/recipe.service';
import { HeaderComponent } from '../header/header.component';

@NgModule({
  declarations: [
    ProfileComponent,
    RecipesComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    
    AccountRoutingModule,
    SharedModule
  ],
  providers: [ UserService, RecipeService, HeaderComponent ],
  bootstrap: []
})
export class AccountModule { }