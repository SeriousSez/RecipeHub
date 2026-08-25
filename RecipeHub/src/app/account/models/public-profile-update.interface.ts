export interface PublicProfileUpdate {
    userId: string;
    bio: string;
    isPublic: boolean;
    profileTheme: string;
    featuredRecipeIds: string[];
}
