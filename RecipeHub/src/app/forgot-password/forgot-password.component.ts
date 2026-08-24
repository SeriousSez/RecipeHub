import { Component, OnDestroy, OnInit } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { ForgotPasswordRequest } from '../shared/models/forgot-password.interface';
import { UserService } from '../shared/services/user.service';

@Component({
    selector: 'app-forgot-password',
    templateUrl: './forgot-password.component.html',
    styleUrls: ['./forgot-password.component.css'],
    standalone: false
})
export class ForgotPasswordComponent implements OnInit, OnDestroy {

    private subscription: Subscription;
    public forgotForm: UntypedFormGroup;
    public isRequesting = false;
    public submitted = false;
    public errors = '';
    public message = '';

    constructor(private formBuilder: UntypedFormBuilder, private userService: UserService) { }

    ngOnInit(): void {
        this.forgotForm = this.formBuilder.group({
            email: ['', [Validators.required, Validators.email]]
        });

        this.subscription = new Subscription();
    }

    ngOnDestroy(): void {
        if (this.subscription) {
            this.subscription.unsubscribe();
        }
    }

    submit({ value, valid }: { value: ForgotPasswordRequest; valid: boolean }) {
        this.submitted = true;
        this.isRequesting = true;
        this.errors = '';
        this.message = '';

        if (!valid) {
            this.isRequesting = false;
            return;
        }

        const resetRequest$ = this.userService.requestPasswordReset(value)
            .subscribe({
                next: (response) => {
                    this.message = response.message ?? 'If an account exists for this email, a password reset link has been sent.';
                    this.isRequesting = false;
                },
                error: (error: any) => {
                    this.isRequesting = false;
                    this.errors = this.extractError(error);
                }
            });

        this.subscription.add(resetRequest$);
    }

    get f(): { [key: string]: AbstractControl } {
        return this.forgotForm.controls;
    }

    private extractError(error: any): string {
        const raw = error?.error ?? error;
        if (typeof raw === 'string') {
            return raw;
        }

        return 'Unable to process request.';
    }
}
