import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { UserService } from '../shared/services/user.service';

@Component({
    selector: 'app-confirm-email',
    templateUrl: './confirm-email.component.html',
    styleUrls: ['./confirm-email.component.css'],
    standalone: false
})
export class ConfirmEmailComponent implements OnInit, OnDestroy {
    public isConfirming = true;
    public confirmed = false;
    public error = '';
    private subscription = new Subscription();

    constructor(private route: ActivatedRoute, private userService: UserService) { }

    ngOnInit(): void {
        const userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
        const token = this.route.snapshot.queryParamMap.get('token') ?? '';

        if (!userId || !token) {
            this.isConfirming = false;
            this.error = 'This confirmation link is incomplete.';
            return;
        }

        this.subscription.add(this.userService.confirmEmail(userId, token).subscribe({
            next: () => {
                this.confirmed = true;
                this.isConfirming = false;
            },
            error: (response: any) => {
                this.isConfirming = false;
                this.error = this.extractError(response);
            }
        }));
    }

    ngOnDestroy(): void {
        this.subscription.unsubscribe();
    }

    private extractError(response: any): string {
        const raw = response?.error ?? response;
        if (Array.isArray(raw)) {
            return raw.map((item: any) => item?.description ?? item?.Description ?? 'Unable to confirm email.').join(' ');
        }

        return typeof raw === 'string' ? raw : 'Unable to confirm email.';
    }
}
