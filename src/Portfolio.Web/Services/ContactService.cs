using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    /// <summary>Address that receives notifications of new contact messages.</summary>
    public string? NotifyTo { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(NotifyTo);
}

/// <summary>
/// Persists contact messages and, when SMTP is configured, sends an e-mail notification.
/// </summary>
public class ContactService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<SmtpOptions> smtpOptions,
    ILogger<ContactService> logger)
{
    public async Task SubmitAsync(ContactMessage message)
    {
        message.Id = 0;
        message.IsRead = false;
        message.CreatedAt = DateTime.UtcNow;
        message.Name = message.Name.Trim();
        message.Email = message.Email.Trim();

        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.ContactMessages.Add(message);
            await db.SaveChangesAsync();
        }

        await TrySendNotificationAsync(message);
    }

    public async Task<List<ContactMessage>> GetMessagesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ContactMessages.AsNoTracking().OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task SetReadAsync(int id, bool isRead)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ContactMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, isRead));
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ContactMessages.Where(m => m.Id == id).ExecuteDeleteAsync();
    }

    private async Task TrySendNotificationAsync(ContactMessage message)
    {
        var smtp = smtpOptions.Value;
        if (!smtp.IsConfigured) return;

        try
        {
            using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
            if (!string.IsNullOrWhiteSpace(smtp.User))
                client.Credentials = new NetworkCredential(smtp.User, smtp.Password);

            using var mail = new MailMessage(smtp.From ?? smtp.User ?? smtp.NotifyTo!, smtp.NotifyTo!)
            {
                Subject = $"[Portfólio] {message.Subject ?? "Nova mensagem"} — {message.Name}",
                Body = $"Nome: {message.Name}\nE-mail: {message.Email}\n\n{message.Message}",
            };
            mail.ReplyToList.Add(new MailAddress(message.Email, message.Name));

            await client.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            // The message is already saved; e-mail failure must not break the form.
            logger.LogError(ex, "Falha ao enviar notificação de contato por e-mail.");
        }
    }
}
