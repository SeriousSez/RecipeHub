namespace RecipeHub.Api.Services
{
    public class EmailOptions
    {
        public bool Enabled { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; }
        public string Password { get; set; }
        public string FromAddress { get; set; }
        public string FromName { get; set; } = "RecipeHub";
    }
}
