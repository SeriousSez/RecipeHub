import { Component, OnInit } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { User } from '../models/user.interface';
import { UserUpdate } from '../models/user-update.interface';
import { UserService } from '../../shared/services/user.service';

@Component({
    selector: 'app-settings',
    templateUrl: './settings.component.html',
    styleUrls: ['./settings.component.css'],
    standalone: false
})
export class SettingsComponent implements OnInit {
    public user: User;
    public profileForm: UntypedFormGroup;
    public formHasChanged = false;
    public isUpdatingProfile = false;
    public profileError = '';

    constructor(private userService: UserService, private formBuilder: UntypedFormBuilder) { }

    ngOnInit(): void {
        this.userService.get(this.userService.getUserName()).subscribe(user => {
            this.user = user;
            this.profileForm = this.formBuilder.group({
                UserName: [user.userName, Validators.required],
                Email: [user.email, [Validators.required, Validators.email]],
                FirstName: [user.firstName, Validators.required],
                LastName: [user.lastName]
            });
        });
    }

    profileChanged(): void {
        if (!this.user || !this.profileForm) return;
        this.formHasChanged = this.profileForm.value.UserName !== this.user.userName
            || this.profileForm.value.FirstName !== this.user.firstName
            || this.profileForm.value.LastName !== this.user.lastName;
    }

    updateProfile(): void {
        if (!this.profileForm?.valid || !this.formHasChanged || this.isUpdatingProfile) return;
        const update: UserUpdate = {
            oldUserName: this.userService.getUserName(),
            userName: this.profileForm.value.UserName,
            oldEmail: this.user.email,
            email: this.user.email,
            firstName: this.profileForm.value.FirstName,
            lastName: this.profileForm.value.LastName,
            role: this.user.role
        };
        this.isUpdatingProfile = true;
        this.profileError = '';
        this.userService.update(update).subscribe(updatedUser => {
            this.user = updatedUser;
            this.formHasChanged = false;
            this.isUpdatingProfile = false;
        }, error => {
            this.profileError = error?.error?.message ?? error?.error ?? 'Unable to update your profile.';
            this.isUpdatingProfile = false;
        });
    }

    get f(): { [key: string]: AbstractControl } {
        return this.profileForm?.controls ?? {};
    }

}
