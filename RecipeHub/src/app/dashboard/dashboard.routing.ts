import { NgModule } from '@angular/core';
import { RouterModule, Routes }        from '@angular/router';

import { RootComponent }    from './root/root.component';
import { OverviewComponent }    from './overview/overview.component'; 

import { AuthGuard } from '../shared/guards/auth.guard';
import { IngredientsComponent } from './ingredients/ingredients.component';

const dashboardRoutes: Routes = ([
  {
    path: '', component: RootComponent, canActivate: [AuthGuard],

    children: [      
      { path: '', component: OverviewComponent },
      { path: 'users',  component: OverviewComponent },
      { path: 'ingredients',  component: IngredientsComponent },

      { path: '**', component: OverviewComponent }
    ]
  }
]);

@NgModule({
  imports: [RouterModule.forChild(dashboardRoutes)],
  exports: [RouterModule]
})
export class DashboardRoutingModule { }
