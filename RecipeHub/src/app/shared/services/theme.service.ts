import { Injectable } from '@angular/core';

const STORAGE_KEY = 'recipehub-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {

    init(): void {
        document.documentElement.setAttribute('data-theme', this.getCurrentTheme());
    }

    getCurrentTheme(): string {
        return localStorage.getItem(STORAGE_KEY) ?? 'light';
    }

    isDark(): boolean {
        return this.getCurrentTheme() === 'dark';
    }

    toggleTheme(): string {
        const nextTheme = this.isDark() ? 'light' : 'dark';
        localStorage.setItem(STORAGE_KEY, nextTheme);
        document.documentElement.setAttribute('data-theme', nextTheme);
        return nextTheme;
    }
}
