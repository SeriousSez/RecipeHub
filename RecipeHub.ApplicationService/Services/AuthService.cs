using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using RecipeHub.ApplicationService.Auth;
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

        public AuthService(UserManager<User> userManager, SignInManager<User> signInManager, IJwtFactory jwtFactory, IOptions<JwtIssuerOptions> jwtOptions, IIdentityManager identityManager, IOptions<AppSettings> appSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtFactory = jwtFactory;
            _jwtOptions = jwtOptions.Value;
            _identityManager = identityManager;
            _appSettings = appSettings.Value;

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

            //if (!await _userManager.IsEmailConfirmedAsync(user))
            //    return null;

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
                return null;

            var signingCredentials = _jwtFactory.GetSigningCredentials();
            var claims = await _jwtFactory.GetClaims(user);

            var tokenOptions = _jwtFactory.GenerateTokenOptions(signingCredentials, claims);
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
                Message = "If an account exists for this email, a reset token has been generated.",
                Success = true
            };

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return response;
            }

            response.Token = await _userManager.GeneratePasswordResetTokenAsync(user);

            return response;
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
