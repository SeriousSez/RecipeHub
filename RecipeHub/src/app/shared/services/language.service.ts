import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export interface UiLanguage {
    code: string;
    label: string;
}

const STORAGE_KEY = 'recipehub-ui-language';

@Injectable({ providedIn: 'root' })
export class LanguageService {

    public readonly languages: UiLanguage[] = [
        { code: 'da', label: 'Dansk' },
        { code: 'en', label: 'English' },
        { code: 'et', label: 'Eesti' },
        { code: 'tr', label: 'Türkçe' }
    ];

    constructor(private translateService: TranslateService) {
        this.translateService.addLangs(this.languages.map(language => language.code));
        this.translateService.setFallbackLang('en');
    }

    init(): void {
        this.translateService.use(this.getCurrentLanguage());
    }

    getCurrentLanguage(): string {
        const storedLanguage = localStorage.getItem(STORAGE_KEY);
        if (storedLanguage) return storedLanguage;

        const browserLanguage = (this.translateService.getBrowserLang() ?? '').toLowerCase();
        return this.languages.some(language => language.code === browserLanguage) ? browserLanguage : 'en';
    }

    setLanguage(code: string): void {
        localStorage.setItem(STORAGE_KEY, code);
        this.translateService.use(code);
    }
}
