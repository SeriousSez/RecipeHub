using System.Threading.Tasks;

namespace RecipeHub.ApplicationService.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(string recipient, string subject, string htmlBody);
    }
}
