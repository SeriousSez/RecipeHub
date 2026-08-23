import { Component, ElementRef, forwardRef, HostListener, Input, ViewChild } from '@angular/core';
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
    @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;
    @ViewChild('dropdown') private dropdown?: ElementRef<HTMLElement>;

    @Input() groups: RecipeTaxonomyGroup[] = [];
    @Input() options: string[] = [];
    @Input() optionLabels: Record<string, string> = {};
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
    public highlightedIndex = -1;

    private onChange: (value: string) => void = () => undefined;
    private onTouched: () => void = () => undefined;
    private suppressNextFocusOpen = false;

    constructor(private elementRef: ElementRef<HTMLElement>) { }

    public get inputValue(): string {
        const selectedValue = this.selectedValues[0] ?? (!this.multiple && this.emptyLabel ? this.emptyValue : undefined);
        const selectedLabel = !this.multiple && selectedValue === this.emptyValue
            ? this.emptyLabel
            : (!this.multiple ? this.getOptionLabel(selectedValue ?? '') : '');

        if (this.isOpen) {
            return this.searchTerm || selectedLabel;
        }

        return selectedLabel;
    }

    public get filteredGroups(): RecipeTaxonomyGroup[] {
        const query = this.searchTerm.trim().toLowerCase();
        const groups = this.options.length > 0
            ? [{ id: 'options', labelKey: '', values: this.options }]
            : this.groups;

        return groups
            .map(group => ({ ...group, values: group.values.filter(value => !query || this.getOptionLabel(value).toLowerCase().includes(query)) }))
            .filter(group => group.values.length > 0);
    }

    public getOptionLabel(value: string): string {
        return this.optionLabels[value] ?? value;
    }

    public get canAddCustomValue(): boolean {
        const value = this.searchTerm.trim();
        return this.allowCustom && value.length > 0 && !this.selectableValues.some(item => item.toLowerCase() === value.toLowerCase());
    }

    public get filteredValues(): string[] {
        const values = this.filteredGroups.flatMap(group => group.values);
        return !this.multiple && this.emptyLabel ? [this.emptyValue, ...values] : values;
    }

    private get selectableValues(): string[] {
        return [...this.groups.flatMap(group => group.values), ...this.options];
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

    public handleFocus(): void {
        if (this.suppressNextFocusOpen) {
            return;
        }

        this.open();
    }

    public toggleDropdown(): void {
        if (this.disabled) {
            return;
        }

        this.isOpen ? this.close() : this.open();
    }

    public updateSearch(value: string): void {
        this.searchTerm = value;
        this.highlightedIndex = -1;
        if (!this.multiple && this.allowCustom) {
            const customValue = value.trim();
            this.selectedValues = customValue ? [customValue] : [];
            this.onChange(customValue);
        }
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
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            this.open();
            const values = this.filteredValues;
            if (values.length === 0) {
                return;
            }

            const change = event.key === 'ArrowDown' ? 1 : -1;
            this.highlightedIndex = (this.highlightedIndex + change + values.length) % values.length;
            setTimeout(() => this.scrollHighlightedOptionIntoView());
        } else if (event.key === 'Enter') {
            const highlightedValue = this.filteredValues[this.highlightedIndex];
            if (highlightedValue !== undefined) {
                event.preventDefault();
                this.toggleValue(highlightedValue);
            } else if (this.canAddCustomValue) {
                event.preventDefault();
                this.addCustomValue();
            }
        } else if (event.key === 'Escape') {
            event.preventDefault();
            this.close();
        } else if (event.key === 'Tab') {
            this.close();
        }
    }

    public highlightValue(value: string): void {
        this.highlightedIndex = this.filteredValues.indexOf(value);
    }

    public trackGroup(_: number, group: RecipeTaxonomyGroup): string {
        return group.id;
    }

    public trackValue(_: number, value: string): string {
        return value;
    }

    public selectValueWithMouse(value: string, event: MouseEvent): void {
        event.preventDefault();
        event.stopPropagation();
        this.toggleValue(value);
    }

    public addCustomValueWithMouse(event: MouseEvent): void {
        event.preventDefault();
        event.stopPropagation();
        this.addCustomValue();
    }

    public focusInput(): void {
        this.suppressNextFocusOpen = true;
        this.searchInput?.nativeElement.focus();
        this.suppressNextFocusOpen = false;
    }

    private scrollHighlightedOptionIntoView(): void {
        this.dropdown?.nativeElement
            .querySelector<HTMLElement>('.taxonomy-option.is-highlighted')
            ?.scrollIntoView({ block: 'nearest' });
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
            this.highlightedIndex = -1;
            this.onTouched();
        }
    }
}