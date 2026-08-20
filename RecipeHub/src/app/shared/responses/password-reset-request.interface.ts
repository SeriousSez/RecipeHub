export interface PasswordResetRequestResponse {
    success: boolean;
    email: string;
    token?: string;
    message: string;
}
