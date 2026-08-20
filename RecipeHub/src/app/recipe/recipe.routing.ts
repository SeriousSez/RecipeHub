import { NgModule } from '@angular/core';
import { RouterModule, Routes }        from '@angular/router';

import { AuthGuard } from '../shared/guards/auth.guard';
import { CreateComponent } from './create/create.component';
import { OverviewComponent } from './overview/overview.component';
import { RootComponent } from './root/root.component';

const recipeRoutes: Routes = ([
  {
    path: '', component: RootComponent,
    
    children: [
      { path: '', component: OverviewComponent },
      { path: 'recipes', component: OverviewComponent },
      { path: 'create', component: CreateComponent, canActivate: [AuthGuard] },

      { path: '**', component: OverviewComponent }
    ]
  }
]);

@NgModule({
  imports: [RouterModule.forChild(recipeRoutes)],
  exports: [RouterModule]
})
export class RecipeRoutingModule { }
