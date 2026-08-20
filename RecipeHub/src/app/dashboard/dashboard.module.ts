import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SharedModule } from '../shared/modules/shared.module';

import { DashboardRoutingModule }  from './dashboard.routing';
import { RootComponent } from './root/root.component';
import { OverviewComponent } from './overview/overview.component';
import { DashboardService } from './services/dashboard.service';

import { AuthGuard } from '../shared/guards/auth.guard';
import { IngredientsComponent } from './ingredients/ingredients.component';

@NgModule({
  imports: [
    DashboardRoutingModule,
    CommonModule, 
    FormsModule,
    SharedModule
  ],
  declarations: [RootComponent, OverviewComponent, IngredientsComponent],
  exports: [ ],
  providers: [AuthGuard, DashboardService]
})

export class DashboardModule { }