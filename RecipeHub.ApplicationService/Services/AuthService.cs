using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RecipeHub.ApplicationService.Auth;
using RecipeHub.ApplicationService.Interfaces;
using RecipeHub.Domain.Entities;
using RecipeHub.Domain.Helpers;
using RecipeHub.Domain.Models;
using RecipeHub.Domain.Responses;
using RecipeHub.Infrastructure.Managers;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace RecipeHub.ApplicationService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IJwtFactory _jwtFactory;
        private readonly JsonSerializerSettings _serializerSettings;
        private readonly JwtIssuerOptions _jwtOptions;
        private readonly IIdentityManager _identityManager;
        private readonly AppSettings _appSettings;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public AuthService(UserManager<User> userManager, SignInManager<User> signInManager, IJwtFactory jwtFactory, IOptions<JwtIssuerOptions> jwtOptions, IIdentityManager identityManager, IOptions<AppSettings> appSettings, IEmailSender emailSender, IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtFactory = jwtFactory;
            _jwtOptions = jwtOptions.Value;
            _identityManager = identityManager;
            _appSettings = appSettings.Value;
            _emailSender = emailSender;
            _configuration = configuration;

            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
        }

        public async Task<LoginResponse> Login(CredentialsViewModel request)
        {
            var user = await _userManager.FindByNameAsync(request.Identity);
            if (user == null)
                user = await _userManager.FindByEmailAsync(request.Identity);

            if (user == null)
                return null;

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                return new LoginResponse
                {
                    Email = user.Email,
                    EmailConfirmed = false
                };
            }

            //if (!await _userManager.IsEmailConfirmedAsync(user))
            //    return null;

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                return null;

            var signingCredentials = _jwtFactory.GetSigningCredentials();
            var claims = await _jwtFactory.GetClaims(user);

            var tokenOptions = _jwtFactory.GenerateTokenOptions(signingCredentials, claims, request.RememberMe);
            var token = new JwtSecurityTokenHandler().WriteToken(tokenOptions);

            //var message = new Message(user.Email, "User Logged In", "You have logged in!");
            //var emailResult = await _emailSender.SendEmailAsync(message);
            //if (emailResult.Failure)
            //    return new ErrorResult<LoginResponse>(emailResult.AsErrorResult().Message);

            var response = new LoginResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AuthToken = token,
                ExpiresIn = (int)_jwtOptions.ValidFor.TotalSeconds
            };

            return response;
        }

        public async Task<IdentityResult> SendEmailConfirmation(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var frontendUrl = (_configuration["PasswordReset:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
            var confirmationUrl = $"{frontendUrl}/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";

            try
            {
                await _emailSender.SendAsync(
                    user.Email,
                    "Confirm your RecipeHub email",
                    BuildEmailConfirmationEmail(confirmationUrl));
            }
            catch (Exception exception)
            {
                return IdentityResult.Failed(new IdentityError { Description = exception.Message });
            }

            return IdentityResult.Success;
        }

        public async Task<IdentityResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });

            return await _userManager.ConfirmEmailAsync(user, token);
        }

        public async Task<ClaimsIdentity> GetClaimsIdentity(CredentialsViewModel credentials)
        {
            if (!string.IsNullOrEmpty(credentials.Identity) && !string.IsNullOrEmpty(credentials.Password))
            {
                // get the user to verifty
                var userToVerify = await _userManager.FindByNameAsync(credentials.Identity);

                if (userToVerify != null)
                {
                    return await CheckCredentials(userToVerify, credentials);
                }
                else
                {
                    userToVerify = await _userManager.FindByEmailAsync(credentials.Identity);
                    if (userToVerify != null)
                    {
                        return await CheckCredentials(userToVerify, credentials);
                    }
                }
            }

            // Credentials are invalid, or account doesn't exist
            return await Task.FromResult<ClaimsIdentity>(null);
        }

        public async Task<ClaimsIdentity> CheckCredentials(User userToVerify, CredentialsViewModel credentials)
        {
            // check the credentials  
            if (await _userManager.CheckPasswordAsync(userToVerify, credentials.Password) == false)
                return null;

            var role = await _identityManager.GetUserRole(userToVerify);

            return new ClaimsIdentity(new GenericIdentity(userToVerify.UserName, "Token"), new[]
            {
                new Claim("username", userToVerify.UserName),
                new Claim("displayname", userToVerify.UserName),
                new Claim(ClaimTypes.Role, role)
            });
        }

        public async Task<PasswordResetRequestResponse> GeneratePasswordResetToken(ForgotPasswordViewModel request)
        {
            var response = new PasswordResetRequestResponse
            {
                Email = request.Email,
                Message = "If an account exists for this email, a password reset link has been sent.",
                Success = true
            };

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return response;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var frontendUrl = (_configuration["PasswordReset:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
            var resetUrl = $"{frontendUrl}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";

            try
            {
                await _emailSender.SendAsync(
                    user.Email,
                    "Reset your RecipeHub password",
                    BuildPasswordResetEmail(resetUrl));
            }
            catch
            {
                return new PasswordResetRequestResponse
                {
                    Email = request.Email,
                    Success = false,
                    Message = "We could not send the password reset email. Please try again later."
                };
            }

            return response;
        }

        private static string BuildPasswordResetEmail(string resetUrl)
        {
            return $"""
<!doctype html>
        <html lang="en">
<body style="margin:0;background:#f4f7fb;color:#172033;font-family:Arial,Helvetica,sans-serif;">
    <div style="padding:32px 16px;">
        <div style="width:100%;max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e1e8f0;border-radius:16px;overflow:hidden;box-shadow:0 14px 32px rgba(15,23,42,.08);">
            <div style="padding:24px 32px;background:#1f2c37;color:#ffffff;">
                <div style="font-size:22px;font-weight:800;letter-spacing:-.04em;">RecipeHub</div>
                <div style="margin-top:6px;color:#b9c8d5;font-size:13px;">Simple food, thoughtfully organized.</div>
            </div>
            <div style="padding:34px 32px 30px;">
                <div style="display:inline-block;padding:7px 11px;border-radius:999px;background:#eff6ff;color:#1d4ed8;font-size:12px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;">Account security</div>
                <h1 style="margin:18px 0 12px;color:#0f172a;font-size:28px;line-height:1.15;letter-spacing:-.04em;">Reset your password</h1>
                <p style="margin:0;color:#475569;font-size:15px;line-height:1.7;">We received a request to reset the password for your RecipeHub account. Use the button below to choose a new one.</p>
                <div style="padding:26px 0 22px;text-align:center;">
                    <a href="{resetUrl}" style="display:inline-block;padding:13px 22px;border-radius:999px;background:#2563eb;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;">Reset my password</a>
                </div>
                <p style="margin:0;color:#64748b;font-size:13px;line-height:1.65;">This link expires in two hours. For your security, it can only be used once.</p>
                <div style="height:1px;margin:24px 0;background:#e5eaf0;"></div>
                <p style="margin:0;color:#64748b;font-size:13px;line-height:1.65;">If the button does not work, copy and paste this link into your browser:</p>
                <p style="margin:8px 0 0;word-break:break-all;color:#2563eb;font-size:12px;line-height:1.6;">{resetUrl}</p>
                <p style="margin:24px 0 0;color:#64748b;font-size:13px;line-height:1.65;">Didn't request a password reset? You can safely ignore this email. Your password will not change.</p>
            </div>
            <div style="padding:18px 32px;background:#f8fafc;color:#94a3b8;font-size:12px;line-height:1.5;">This is an automated message from RecipeHub. Please do not reply to this email.</div>
        </div>
    </div>
</body>
</html>
""";
        }

        private static string BuildEmailConfirmationEmail(string confirmationUrl)
        {
            return $"""
<!doctype html>
<html lang="en">
<body style="margin:0;background:#f4f7fb;color:#172033;font-family:Arial,Helvetica,sans-serif;">
    <div style="padding:32px 16px;">
        <div style="width:100%;max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #e1e8f0;border-radius:16px;overflow:hidden;box-shadow:0 14px 32px rgba(15,23,42,.08);">
            <div style="padding:24px 32px;background:#1f2c37;color:#ffffff;">
                <div style="font-size:22px;font-weight:800;letter-spacing:-.04em;">RecipeHub</div>
                <div style="margin-top:6px;color:#b9c8d5;font-size:13px;">Simple food, thoughtfully organized.</div>
            </div>
            <div style="padding:34px 32px 30px;">
                <div style="display:inline-block;padding:7px 11px;border-radius:999px;background:#ecfdf5;color:#047857;font-size:12px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;">Welcome to RecipeHub</div>
                <h1 style="margin:18px 0 12px;color:#0f172a;font-size:28px;line-height:1.15;letter-spacing:-.04em;">Confirm your email</h1>
                <p style="margin:0;color:#475569;font-size:15px;line-height:1.7;">Thanks for creating a RecipeHub account. Confirm your email address to finish setting up your account and start organizing your recipes.</p>
                <div style="padding:26px 0 22px;text-align:center;">
                    <a href="{confirmationUrl}" style="display:inline-block;padding:13px 22px;border-radius:999px;background:#2563eb;color:#ffffff;font-size:15px;font-weight:700;text-decoration:none;">Confirm my email</a>
                </div>
                <p style="margin:0;color:#64748b;font-size:13px;line-height:1.65;">This link is for your account only. If you did not create a RecipeHub account, you can safely ignore this email.</p>
                <div style="height:1px;margin:24px 0;background:#e5eaf0;"></div>
                <p style="margin:0;color:#64748b;font-size:13px;line-height:1.65;">Button not working? Copy and paste this link into your browser:</p>
                <p style="margin:8px 0 0;word-break:break-all;color:#2563eb;font-size:12px;line-height:1.6;">{confirmationUrl}</p>
            </div>
            <div style="padding:18px 32px;background:#f8fafc;color:#94a3b8;font-size:12px;line-height:1.5;">This is an automated message from RecipeHub. Please do not reply to this email.</div>
        </div>
    </div>
</body>
</html>
""";
        }

        public async Task<IdentityResult> ResetPassword(ResetPasswordViewModel request)
        {
            if (request.Password != request.ConfirmPassword)
            {
                return IdentityResult.Failed(new IdentityError { Description = "Passwords do not match." });
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return IdentityResult.Failed(new IdentityError { Description = "User not found." });
            }

            return await _userManager.ResetPasswordAsync(user, request.Token, request.Password);
        }
    }
}
