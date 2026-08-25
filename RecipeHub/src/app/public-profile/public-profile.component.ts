import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PublicProfile } from '../account/models/public-profile.interface';
import { UserService } from '../shared/services/user.service';
import { UtilityService } from '../shared/utils/utility.service';
import { Recipe } from '../recipe/models/recipe.interface';

@Component({
    selector: 'app-public-profile',
    templateUrl: './public-profile.component.html',
    styleUrls: ['./public-profile.component.css'],
    standalone: false
})
export class PublicProfileComponent implements OnInit {
    public profile: PublicProfile;
    public error = '';

    constructor(private route: ActivatedRoute, private userService: UserService, public utilityService: UtilityService) { }

    ngOnInit(): void {
        const username = this.route.snapshot.paramMap.get('username') ?? '';
        this.userService.getPublicProfile(username).subscribe({
            next: profile => this.profile = profile,
            error: () => this.error = 'This profile is not available.'
        });
    }

    recipeUrl(recipe: Recipe): string {
        return `/recipe/${this.utilityService.toRecipeKey(recipe.id, recipe.title)}`;
    }
}
