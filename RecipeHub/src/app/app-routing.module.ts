import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { RegistrationComponent } from './registration/registration.component';
import { HomeComponent } from './home/home.component';
import { AuthGuard } from './shared/guards/auth.guard';
import { RecipeComponent } from './recipe/recipe/recipe.component';
import { AdminGuard } from './shared/guards/admin.guard';
import { FridgesComponent } from './fridges/fridges.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'account', loadChildren: () => import('./account/account.module').then(m => m.AccountModule), canActivate: [AuthGuard] },
  { path: 'dashboard', loadChildren: () => import('./dashboard/dashboard.module').then(m => m.DashboardModule), canActivate: [AuthGuard, AdminGuard] },
  { path: 'recipes', loadChildren: () => import('./recipe/recipe.module').then(m => m.RecipeModule) },
  { path: 'grocery', loadChildren: () => import('./grocery/grocery.module').then(m => m.GroceryModule) },
  { path: 'fridges', component: FridgesComponent },
  { path: 'register', component: RegistrationComponent },
  { path: 'login', component: LoginComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: 'recipe/:id/:slug', component: RecipeComponent },
  { path: 'recipe/:title/:creator', component: RecipeComponent },

  { path: '**', component: HomeComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
