import { Subscription } from 'rxjs';
import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router, ActivatedRoute } from '@angular/router';

import { Credentials } from '../shared/models/credentials.interface';
import { UserService } from '../shared/services/user.service';
import { AbstractControl, UntypedFormBuilder, UntypedFormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css'],
  standalone: false
})
export class LoginComponent implements OnInit, OnDestroy {

  private subscription: Subscription;
  public loginForm: UntypedFormGroup;

  brandNew: boolean = false;
  emailConfirmationPending: boolean = false;
  public requiresEmailConfirmation = false;
  public confirmationEmail = '';
  public isResendingConfirmation = false;
  public confirmationResent = false;
  errors: string = "";
  isRequesting: boolean = false;;
  submitted: boolean = false;
  credentials: Credentials = { identity: '', password: '' };

  constructor(private userService: UserService, private router: Router, private activatedRoute: ActivatedRoute, private formBuilder: UntypedFormBuilder) { }

  ngOnInit() {
    this.loginForm = this.formBuilder.group({
      identity: ['', Validators.required],
      password: ['', Validators.required],
      rememberMe: [true]
    });

    // subscribe to router event
    this.subscription = this.activatedRoute.queryParams.subscribe(
      (param: any) => {
        this.brandNew = param['brandNew'];
        this.emailConfirmationPending = param['emailConfirmationPending'];
        this.credentials.identity = param['identity'];
      });
  }

  ngOnDestroy() {
    // prevent memory leak by unsubscribing
    this.subscription.unsubscribe();
  }

  login() {
    const value = this.loginForm.value as Credentials;
    const valid = this.loginForm.valid;

    this.submitted = true;
    this.errors = '';
    this.requiresEmailConfirmation = false;
    this.confirmationResent = false;

    if (valid) {
      this.isRequesting = true;
      this.userService.login(value)
        .subscribe(result => {
          this.router.navigate(['/recipes'], { queryParams: { brandNew: true, email: value.identity } });
          this.isRequesting = false;
        }, errors => {
          this.isRequesting = false;
          if (errors.status === 403 && errors.error?.code === 'email_confirmation_required') {
            this.requiresEmailConfirmation = true;
            this.confirmationEmail = errors.error.email || value.identity;
            this.errors = errors.error.message;
            return;
          }
          this.errors = this.extractError(errors);
        }
        );
    }
  }

  resendConfirmation() {
    if (this.isResendingConfirmation || !this.confirmationEmail) return;

    this.isResendingConfirmation = true;
    this.confirmationResent = false;
    this.userService.resendEmailConfirmation(this.confirmationEmail).subscribe({
      next: () => {
        this.isResendingConfirmation = false;
        this.confirmationResent = true;
      },
      error: (error: any) => {
        this.isResendingConfirmation = false;
        this.errors = this.extractError(error);
      }
    });
  }

  get f(): { [key: string]: AbstractControl } {
    return this.loginForm.controls;
  }

  private extractError(error: any): string {
    const raw = error?.error ?? error;
    if (typeof raw === 'string') return raw;
    if (Array.isArray(raw)) {
      return raw.map((item: any) => item?.description ?? item?.Description ?? 'Unable to log in.').join(' ');
    }

    return raw?.Item2 ?? raw?.item2 ?? raw?.message ?? raw?.Message ?? 'Unable to log in.';
  }
}