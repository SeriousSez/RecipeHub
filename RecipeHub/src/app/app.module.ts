import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { AppRoutingModule } from './app-routing.module';
import { HttpClient, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';

import { AppComponent } from './app.component';
import { HomeComponent } from './home/home.component';
import { HeaderComponent } from './header/header.component';
import { ConfigService } from './shared/utils/config.service';
import { AccountModule } from './account/account.module';
import { DashboardModule } from './dashboard/dashboard.module';
import { UserService } from './shared/services/user.service';
import { SharedModule } from './shared/modules/shared.module';
import { LoginComponent } from './login/login.component';
import { RecipeHubComponent } from './svgs/recipe-hub.component';
import { RegistrationComponent } from './registration/registration.component';
import { RecipeModule } from './recipe/recipe.module';
import { AngularEditorModule } from '@kolkov/angular-editor';
import { MobileHeaderComponent } from './header/mobile-header/mobile-header.component';
import { FooterComponent } from './footer/footer.component';
import { MobileFooterComponent } from './footer/mobile-footer/mobile-footer.component';
import { GroceryModule } from './grocery/grocery.module';
import { JwtModule } from '@auth0/angular-jwt';
import { FridgesComponent } from './fridges/fridges.component';
import { ForgotPasswordComponent } from './forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './reset-password/reset-password.component';
import { ConfirmEmailComponent } from './confirm-email/confirm-email.component';
import { FridgeService } from './fridges/fridge.service';
import { PantryComponent } from './pantry/pantry.component';
import { TranslateModule } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';

export function tokenGetter() {
  return localStorage.getItem("token");
}

@NgModule({
  declarations: [
    AppComponent,
    HomeComponent,
    HeaderComponent,
    MobileHeaderComponent,
    RegistrationComponent,
    LoginComponent,
    RecipeHubComponent,
    FooterComponent,
    MobileFooterComponent,
    FridgesComponent,
    ForgotPasswordComponent,
    ResetPasswordComponent,
    ConfirmEmailComponent,
    PantryComponent
  ],
  bootstrap: [AppComponent], imports: [BrowserModule,
    FormsModule,
    ReactiveFormsModule,
    AppRoutingModule,
    JwtModule.forRoot({
      config: {
        tokenGetter: tokenGetter,
        allowedDomains: ["localhost:5001"],
        disallowedRoutes: []
      }
    }),
    AccountModule,
    DashboardModule,
    RecipeModule,
    GroceryModule,
    SharedModule,
    AngularEditorModule,
    TranslateModule.forRoot({
      fallbackLang: 'en'
    })], providers: [HttpClient, ConfigService, UserService, FridgeService, provideHttpClient(withInterceptorsFromDi()), provideTranslateHttpLoader({ prefix: './assets/i18n/', suffix: '.json' })]
})
export class AppModule { }
