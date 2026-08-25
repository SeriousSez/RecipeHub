import { Component, OnInit } from '@angular/core';
import { Recipe } from '../../recipe/models/recipe.interface';
import { RecipeService } from '../../recipe/services/recipe.service';
import { PublicProfile } from '../models/public-profile.interface';
import { PublicProfileUpdate } from '../models/public-profile-update.interface';
import { UserService } from '../../shared/services/user.service';

@Component({
    selector: 'app-profile',
    templateUrl: './profile.component.html',
    styleUrls: ['./profile.component.css'],
    standalone: false
})
export class ProfileComponent implements OnInit {
    public username = '';
    public publicProfile: PublicProfile;
    public profileRecipes: Recipe[] = [];
    public featuredRecipeIds: string[] = [];
    public isSavingPublicProfile = false;
    public profileSaved = false;
    public error = '';

    constructor(private userService: UserService, private recipeService: RecipeService) {
        this.username = this.userService.getUserName();
    }

    ngOnInit(): void {
        this.userService.getPublicProfile(this.username).subscribe({
            next: profile => {
                this.publicProfile = profile;
                this.featuredRecipeIds = profile.featuredRecipes.map(recipe => recipe.id);
            },
            error: () => this.error = 'Unable to load your public profile.'
        });
        this.recipeService.getRecipesByCreator(this.username).subscribe(recipes => this.profileRecipes = recipes);
    }

    toggleFeaturedRecipe(recipe: Recipe): void {
        if (this.featuredRecipeIds.includes(recipe.id)) {
            this.featuredRecipeIds = this.featuredRecipeIds.filter(id => id !== recipe.id);
        } else if (this.featuredRecipeIds.length < 3) {
            this.featuredRecipeIds = [...this.featuredRecipeIds, recipe.id];
        }
    }

    savePublicProfile(): void {
        if (!this.publicProfile) return;
        this.isSavingPublicProfile = true;
        this.profileSaved = false;
        this.error = '';
        const update: PublicProfileUpdate = {
            userId: this.userService.getUserId(),
            bio: this.publicProfile.bio,
            isPublic: this.publicProfile.isPublic,
            profileTheme: this.publicProfile.profileTheme,
            featuredRecipeIds: this.featuredRecipeIds
        };
        this.userService.updatePublicProfile(update).subscribe({
            next: profile => {
                this.publicProfile = profile;
                this.featuredRecipeIds = profile.featuredRecipes.map(recipe => recipe.id);
                this.isSavingPublicProfile = false;
                this.profileSaved = true;
            },
            error: () => {
                this.isSavingPublicProfile = false;
                this.error = 'Unable to save your public profile.';
            }
        });
    }
}
