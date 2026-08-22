import { Component, ElementRef, forwardRef, HostListener, Input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { RecipeTaxonomyGroup } from '../models/recipe-taxonomy';

@Component({
    selector: 'app-taxonomy-select',
    templateUrl: './taxonomy-select.component.html',
    styleUrls: ['./taxonomy-select.component.css'],
    providers: [{
        provide: NG_VALUE_ACCESSOR,
        useExisting: forwardRef(() => TaxonomySelectComponent),
        multi: true
    }],
    standalone: false
})
export class TaxonomySelectComponent implements ControlValueAccessor {
    @Input() groups: RecipeTaxonomyGroup[] = [];
    @Input() options: string[] = [];
    @Input() placeholder = '';
    @Input() ariaLabel = '';
    @Input() multiple = true;
    @Input() allowCustom = true;
    @Input() emptyValue = '';
    @Input() emptyLabel = '';

    public isOpen = false;
    public searchTerm = '';
    public selectedValues: string[] = [];
    public disabled = false;

    private onChange: (value: string) => void = () => undefined;
    private onTouched: () => void = () => undefined;

    constructor(private elementRef: ElementRef<HTMLElement>) { }

    public get inputValue(): string {
        if (this.isOpen) {
            return this.searchTerm;
        }

        const selectedValue = this.selectedValues[0];
        return !this.multiple && selectedValue === this.emptyValue ? this.emptyLabel : (!this.multiple ? selectedValue ?? '' : '');
    }

    public get filteredGroups(): RecipeTaxonomyGroup[] {
        const query = this.searchTerm.trim().toLowerCase();
        const groups = this.options.length > 0
            ? [{ id: 'options', labelKey: '', values: this.options }]
            : this.groups;

        return groups
            .map(group => ({ ...group, values: group.values.filter(value => !query || value.toLowerCase().includes(query)) }))
            .filter(group => group.values.length > 0);
    }

    public get canAddCustomValue(): boolean {
        const value = this.searchTerm.trim();
        return this.allowCustom && value.length > 0 && !this.allValues.some(item => item.toLowerCase() === value.toLowerCase());
    }

    private get allValues(): string[] {
        return [...this.groups.flatMap(group => group.values), ...this.options, ...this.selectedValues];
    }

    public writeValue(value: string | string[] | null | undefined): void {
        if (!value) {
            this.selectedValues = [];
            return;
        }

        const values = Array.isArray(value) ? value : value.split(',');
        this.selectedValues = values.map(item => item.trim()).filter(item => item.length > 0);
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

    public open(): void {
        if (!this.disabled) {
            this.isOpen = true;
        }
    }

    public toggleDropdown(): void {
        if (this.disabled) {
            return;
        }

        this.isOpen ? this.close() : this.open();
    }

    public updateSearch(value: string): void {
        this.searchTerm = value;
        this.open();
    }

    public toggleValue(value: string): void {
        const normalizedValue = value.trim();
        if (!this.multiple) {
            this.selectedValues = [normalizedValue];
            this.onChange(normalizedValue);
            this.close();
            return;
        }

        const existingIndex = this.selectedValues.findIndex(item => item.toLowerCase() === normalizedValue.toLowerCase());
        this.selectedValues = existingIndex >= 0
            ? this.selectedValues.filter((_, index) => index !== existingIndex)
            : [...this.selectedValues, normalizedValue];
        this.onChange(this.selectedValues.join(', '));
    }

    public addCustomValue(): void {
        if (this.canAddCustomValue) {
            this.toggleValue(this.searchTerm);
            this.searchTerm = '';
        }
    }

    public handleKeydown(event: KeyboardEvent): void {
        if (event.key === 'Enter' && this.canAddCustomValue) {
            event.preventDefault();
            this.addCustomValue();
        } else if (event.key === 'Escape') {
            this.close();
        }
    }

    public isSelected(value: string): boolean {
        return this.selectedValues.some(item => item.toLowerCase() === value.toLowerCase());
    }

    public removeValue(value: string, event: MouseEvent): void {
        event.stopPropagation();
        this.toggleValue(value);
    }

    @HostListener('document:click', ['$event'])
    public closeWhenClickingOutside(event: MouseEvent): void {
        if (!this.elementRef.nativeElement.contains(event.target as Node)) {
            this.close();
        }
    }

    private close(): void {
        if (this.isOpen) {
            this.isOpen = false;
            this.searchTerm = '';
            this.onTouched();
        }
    }
}