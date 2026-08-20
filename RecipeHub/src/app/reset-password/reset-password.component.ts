import { Component, OnDestroy, OnInit } from '@angular/core';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { ResetPasswordRequest } from '../shared/models/reset-password.interface';
import { UserService } from '../shared/services/user.service';

@Component({
    selector: 'app-reset-password',
    templateUrl: './reset-password.component.html',
    styleUrls: ['./reset-password.component.css'],
    standalone: false
})
export class ResetPasswordComponent implements OnInit, OnDestroy {

  private subscription: Subscription;
  public resetForm: UntypedFormGroup;
  public isRequesting = false;
  public submitted = false;
  public errors = '';
  public message = '';

  constructor(private formBuilder: UntypedFormBuilder, private userService: UserService, private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const email = this.route.snapshot.queryParamMap.get('email') ?? '';
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';

    this.resetForm = this.formBuilder.group({
      email: [email, [Validators.required, Validators.email]],
      token: [token, Validators.required],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required]
    }, { validators: this.passwordsMatchValidator });

    this.subscription = new Subscription();
  }

  ngOnDestroy(): void {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  private passwordsMatchValidator = (group: AbstractControl) => {
    const password = group.get('password')?.value;
    const confirmPassword = group.get('confirmPassword')?.value;
    return password === confirmPassword ? null : { mismatch: true };
  };

  submit({ value, valid }: { value: ResetPasswordRequest; valid: boolean }) {
    this.submitted = true;
    this.isRequesting = true;
    this.errors = '';
    this.message = '';

    if (!valid) {
      this.isRequesting = false;
      return;
    }

    const reset$ = this.userService.resetPassword(value).subscribe({
      next: () => {
        this.message = 'Password updated. You can now log in with the new password.';
        this.isRequesting = false;
      },
      error: (error: any) => {
        this.isRequesting = false;
        this.errors = this.extractError(error);
      }
    });

    this.subscription.add(reset$);
  }

  navigateToLogin() {
    this.router.navigate(['/login']);
  }

  private extractError(error: any): string {
    const raw = error?.error ?? error;
    if (Array.isArray(raw)) {
      return raw.map((item: any) => item?.description ?? item?.Description ?? 'Unable to reset password.').join(' ');
    }

    if (typeof raw === 'string') {
      return raw;
    }

    return 'Unable to reset password.';
  }

  get f(): { [key: string]: AbstractControl } {
    return this.resetForm.controls;
  }
}
