import { Component, ElementRef, forwardRef, HostBinding, HostListener, Input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { LanguageService } from '../services/language.service';

interface DatePickerDay {
    date: Date;
    value: string;
    label: string;
    inMonth: boolean;
    isToday: boolean;
    isSelected: boolean;
}

@Component({
    selector: 'app-date-picker',
    templateUrl: './date-picker.component.html',
    styleUrls: ['./date-picker.component.css'],
    providers: [{
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => DatePickerComponent),
        multi: true
    }],
    standalone: false
})
export class DatePickerComponent implements ControlValueAccessor {
    @Input() placeholder = 'dd/mm/yyyy';
    @Input() ariaLabel = 'Date';
    @Input() disabled = false;

    @HostBinding('class.date-picker-host-open')
    public get hostOpen(): boolean {
        return this.isOpen;
    }

    public isOpen = false;
    public value = '';
    public viewDate = new Date();

    private onChange: (value: string) => void = () => undefined;
    private onTouched: () => void = () => undefined;

    constructor(private elementRef: ElementRef<HTMLElement>, private languageService: LanguageService) { }

    public get weekdayLabels(): string[] {
        const monday = new Date(2026, 0, 5);
        return Array.from({ length: 7 }, (_, index) => {
            const date = new Date(monday);
            date.setDate(monday.getDate() + index);
            return date.toLocaleDateString(this.languageService.getCurrentLanguage(), { weekday: 'short' });
        });
    }

    public get displayValue(): string {
        const date = this.parseDate(this.value);
        if (!date) return '';
        return date.toLocaleDateString(this.languageService.getCurrentLanguage(), { day: '2-digit', month: '2-digit', year: 'numeric' });
    }

    public get monthLabel(): string {
        return this.viewDate.toLocaleDateString(this.languageService.getCurrentLanguage(), { month: 'long', year: 'numeric' });
    }

    public get calendarDays(): DatePickerDay[] {
        const firstOfMonth = new Date(this.viewDate.getFullYear(), this.viewDate.getMonth(), 1);
        const mondayOffset = (firstOfMonth.getDay() + 6) % 7;
        const start = new Date(firstOfMonth);
        start.setDate(firstOfMonth.getDate() - mondayOffset);

        return Array.from({ length: 42 }, (_, index) => {
            const date = new Date(start);
            date.setDate(start.getDate() + index);
            const value = this.toDateValue(date);
            return {
                date,
                value,
                label: String(date.getDate()),
                inMonth: date.getMonth() === this.viewDate.getMonth(),
                isToday: value === this.toDateValue(new Date()),
                isSelected: value === this.value
            };
        });
    }

    public writeValue(value: string | null | undefined): void {
        this.value = value ?? '';
        this.viewDate = this.parseDate(this.value) ?? new Date();
    }

    public registerOnChange(callback: (value: string) => void): void {
        this.onChange = callback;
    }

    public registerOnTouched(callback: () => void): void {
        this.onTouched = callback;
    }

    public setDisabledState(disabled: boolean): void {
        this.disabled = disabled;
    }

    public toggle(): void {
        if (this.disabled) return;
        this.isOpen = !this.isOpen;
    }

    public open(): void {
        if (this.disabled) return;
        this.isOpen = true;
    }

    public close(): void {
        this.isOpen = false;
        this.onTouched();
    }

    public previousMonth(): void {
        this.viewDate = new Date(this.viewDate.getFullYear(), this.viewDate.getMonth() - 1, 1);
    }

    public nextMonth(): void {
        this.viewDate = new Date(this.viewDate.getFullYear(), this.viewDate.getMonth() + 1, 1);
    }

    public selectDate(value: string): void {
        this.value = value;
        this.viewDate = this.parseDate(value) ?? this.viewDate;
        this.onChange(value);
        this.close();
    }

    public clear(event: MouseEvent): void {
        event.stopPropagation();
        this.value = '';
        this.onChange('');
        this.close();
    }

    public selectToday(event: MouseEvent): void {
        event.stopPropagation();
        this.selectDate(this.toDateValue(new Date()));
    }

    public trackDay(_: number, day: DatePickerDay): string {
        return day.value;
    }

    @HostListener('document:click', ['$event'])
    public handleDocumentClick(event: MouseEvent): void {
        if (!this.elementRef.nativeElement.contains(event.target as Node)) {
            this.close();
        }
    }

    private parseDate(value: string): Date | null {
        const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
        if (!match) return null;
        return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
    }

    private toDateValue(date: Date): string {
        const year = date.getFullYear();
        const month = `${date.getMonth() + 1}`.padStart(2, '0');
        const day = `${date.getDate()}`.padStart(2, '0');
        return `${year}-${month}-${day}`;
    }
}