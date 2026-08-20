import { Component, OnInit } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { UserService } from 'src/app/shared/services/user.service';
import { UserSettingsUpdate } from '../models/user-settings-update.interface';
import { UserSettings } from '../models/user-settings.interface';
import { UserUpdate } from '../models/user-update.interface';
import { User } from '../models/user.interface';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    standalone: false
})
export class ProfileComponent implements OnInit {
  public languages: string[] = ['Danish', 'English', 'Estonian', 'Turkish']

  public username: string;
  public email: string;

  public user: User;
  
  public profileForm: UntypedFormGroup;
  public formHasChanged: boolean = false;

  public errors: string = '';
  public isRequesting: boolean = false;
  
  settings: UserSettings;
  settingsSubscription?: Subscription;

  constructor(private userService: UserService, private formBuilder: UntypedFormBuilder) {
    this.username = this.userService.getUserName();
    this.email = this.userService.getEmail();
    
    this.settingsSubscription = this.userService.settings$.subscribe(settings => this.settings = settings);
  }

  ngOnInit(): void {
    this.getUser();
  }

  getUser(){
    this.userService.get(this.username).subscribe(user => {
      this.user = user;
      this.profileForm = this.formBuilder.group({
        UserName: [user.userName, Validators.required],
        Email: [user.email, [Validators.required, Validators.email]],
        FirstName: [user.firstName, Validators.required],
        LastName: [user.lastName]
      });
    });
  }

  update({ value, valid }: { value: User, valid: boolean }){
    this.isRequesting = true;

    if(valid){
      this.userService.update(this.createUserUpdateModel()).subscribe(result => {
        this.isRequesting = false;
      }, error => {
        this.isRequesting = false;
        this.errors = error;
      });
    }
  }

  updateTheme(){
    if(this.settings.theme === 'Light'){
      this.settings.theme = 'Dark';
    }else{
      this.settings.theme = 'Light' 
    }

    this.updateSettings();
  }

  updateSettings(){
    this.userService.updateSettings(this.createUserSettingsUpdateModel()).subscribe(result => {

    }, error => {
      this.isRequesting = false;
      this.errors = error;
    });
  }

  createUserUpdateModel(){
    var model: UserUpdate = {
      oldUserName: this.username,
      userName: this.profileForm.controls['UserName'].value,
      oldEmail: this.email,
      email: this.profileForm.controls['Email'].value,
      firstName: this.profileForm.controls['FirstName'].value,
      lastName: this.profileForm.controls['LastName'].value,
      role: this.user.role
    };

    return model;
  }

  createUserSettingsUpdateModel(){
    var model: UserSettingsUpdate = {
      userId: this.user.id,
      preferredLanguage: this.settings.preferredLanguage,
      theme: this.settings.theme,
      recipesTheme: this.settings.recipesTheme,
      myRecipesTheme: this.settings.myRecipesTheme,
    };

    return model;
  }

  formCheck({ value, valid }: { value: User, valid: boolean }){
    if(value.userName == this.user.userName && value.firstName == this.user.firstName && value.lastName == this.user.lastName && value.email == this.user.email){
      this.formHasChanged = false;
    }else{
      this.formHasChanged = true;
    }
  }
  
  get f(): { [key: string]: AbstractControl } {
    return this.profileForm.controls;
  }
}
