import { Injectable } from '@angular/core';
import { GeneratedRecipe } from '../models/recipe-generation.interface';

@Injectable({ providedIn: 'root' })
export class RecipeDraftService {
    private draft: GeneratedRecipe | null = null;

    public setDraft(draft: GeneratedRecipe): void {
        this.draft = draft;
    }

    public peekDraft(): GeneratedRecipe | null {
        return this.draft;
    }

    public consumeDraft(): GeneratedRecipe | null {
        const draft = this.draft;
        this.draft = null;
        return draft;
    }

    public clearDraft(): void {
        this.draft = null;
    }
}
