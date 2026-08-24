using Microsoft.AspNetCore.Identity;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> Login(CredentialsViewModel request);
        Task<ClaimsIdentity> GetClaimsIdentity(CredentialsViewModel credentials);
        Task<ClaimsIdentity> CheckCredentials(User userToVerify, CredentialsViewModel credentials);
        Task<PasswordResetRequestResponse> GeneratePasswordResetToken(ForgotPasswordViewModel request);
        Task<IdentityResult> ResetPassword(ResetPasswordViewModel request);
        Task<IdentityResult> SendEmailConfirmation(string email);
        Task<IdentityResult> ConfirmEmail(string userId, string token);
    }
}
