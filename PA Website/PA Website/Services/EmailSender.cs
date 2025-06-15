using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace PA_Website.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly ILogger<EmailSender> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly string _password;
        private readonly bool _enableSsl;

        public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
        {
            _logger = logger;
            _smtpServer = configuration["EmailSettings:SmtpServer"];
            _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
            _fromEmail = configuration["EmailSettings:FromEmail"];
            _fromName = configuration["EmailSettings:FromName"];
            _password = configuration["EmailSettings:Password"];
            _enableSsl = bool.Parse(configuration["EmailSettings:EnableSsl"] ?? "true");

            // Validate required settings
            if (string.IsNullOrEmpty(_smtpServer))
            {
                _logger.LogError("SMTP Server is missing in configuration");
                throw new InvalidOperationException("SMTP Server is missing in configuration");
            }

            if (string.IsNullOrEmpty(_fromEmail))
            {
                _logger.LogError("FromEmail is missing in configuration");
                throw new InvalidOperationException("FromEmail is missing in configuration");
            }

            if (string.IsNullOrEmpty(_password))
            {
                _logger.LogError("Email password is missing in configuration");
                throw new InvalidOperationException("Email password is missing in configuration");
            }

            _logger.LogInformation($"Initializing EmailSender with SMTP: {_smtpServer}:{_smtpPort}, FromEmail: {_fromEmail}, SSL: {_enableSsl}");
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                _logger.LogInformation($"Attempting to send email to {email} with subject: {subject}");

                // Validate input
                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Email address is null or empty");
                    throw new ArgumentException("Email address cannot be null or empty");
                }

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_fromEmail, _password);

                using var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(_fromEmail, _fromName);
                mailMessage.To.Add(email);
                mailMessage.Subject = subject;
                mailMessage.Body = htmlMessage;
                mailMessage.IsBodyHtml = true;

                _logger.LogInformation($"Connecting to SMTP server {_smtpServer}:{_smtpPort}...");

                await client.SendMailAsync(mailMessage);

                _logger.LogInformation($"Successfully sent email to {email}");
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, $"SMTP error sending email to {email}. Status: {smtpEx.StatusCode}, Message: {smtpEx.Message}");
                throw new Exception($"SMTP error: {smtpEx.Message}", smtpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {email}. Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }
    }
}