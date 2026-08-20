export interface ResetPasswordRequest {
    email: string;
    token: string;
    password: string;
    confirmPassword: string;
}
