namespace RecipeHub.Domain.Models
{
    public class UserUpdateViewModel
    {
        public string OldUsername { get; set; }
        public string Username { get; set; }
        public string OldEmail { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
    }
}
