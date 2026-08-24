import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
    selector: 'app-confirmation',
    templateUrl: './confirmation.component.html',
    styleUrls: ['./confirmation.component.css'],
    standalone: false
})
export class ConfirmationComponent {
    @Input() visible = false;
    @Input() title = '';
    @Input() message = '';
    @Input() confirmLabel = 'Confirm';
    @Input() loadingLabel = 'Saving...';
    @Input() cancelLabel = 'Cancel';
    @Input() doneLabel = 'Done';
    @Input() successMessage = '';
    @Input() loading = false;
    @Input() completed = false;
    @Input() danger = false;

    @Output() confirmed = new EventEmitter<void>();
    @Output() cancelled = new EventEmitter<void>();

    public confirm(): void {
        if (this.loading || this.completed) return;
        this.confirmed.emit();
    }

    public cancel(): void {
        this.cancelled.emit();
    }
}
