namespace RecipeHub.Domain.Responses
{
    public class PasswordResetRequestResponse
    {
        public bool Success { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }
        public string Message { get; set; }
    }
}
