import { Recipe } from '../../recipe/models/recipe.interface';
import { User } from './user.interface';

export interface PublicProfile {
    user: User;
    bio: string;
    isPublic: boolean;
    profileTheme: string;
    featuredRecipes: Recipe[];
}
