import { NgModule } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { SharedModule } from '../shared/modules/shared.module';

import { RecipeRoutingModule } from './recipe.routing';
import { OverviewComponent } from './overview/overview.component';
import { RecipeService } from './services/recipe.service';

import { AuthGuard } from '../shared/guards/auth.guard';
import { CreateComponent } from './create/create.component';
import { RootComponent } from './root/root.component';
import { ImageCropperComponent } from 'ngx-image-cropper';
import { IngredientService } from './services/ingredient.service';
import { RecipeComponent } from './recipe/recipe.component';
import { AngularEditorModule } from '@kolkov/angular-editor';
import { CKEditorModule } from 'ckeditor4-angular';
import { SafeService } from '../shared/utils/safe.service';
import { FavoriteService } from '../shared/services/favorite.service';
import { PrettyComponent } from './overview/pretty/pretty.component';
import { UtilityService } from '../shared/utils/utility.service';

@NgModule({
  imports: [
    RecipeRoutingModule,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    SharedModule,
    ImageCropperComponent,
    AngularEditorModule,
    CKEditorModule
  ],
  declarations: [
    OverviewComponent,
    PrettyComponent,
    CreateComponent,
    RootComponent,
    RecipeComponent
  ],
  exports: [],
  providers: [AuthGuard, RecipeService, IngredientService, DatePipe, SafeService, FavoriteService, UtilityService]
})

export class RecipeModule { } 