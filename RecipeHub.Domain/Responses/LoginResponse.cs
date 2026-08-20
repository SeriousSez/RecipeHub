namespace RecipeHub.Domain.Responses
{
    public class LoginResponse
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string AuthToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
