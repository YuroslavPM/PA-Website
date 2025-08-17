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
            _smtpServer = configuration["EmailSettings:SmtpServer"] ?? "";
            _smtpPort = int.Parse(configuration["EmailSettings:SmtpPort"] ?? "587");
            _fromEmail = configuration["EmailSettings:FromEmail"] ?? "";
            _fromName = configuration["EmailSettings:FromName"] ?? "";
            _password = configuration["EmailSettings:Password"] ?? "";
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

            if (string.IsNullOrEmpty(_password) || _password == "your_abv_password_here")
            {
                _logger.LogError("Email password is missing or not configured in configuration");
                throw new InvalidOperationException("Email password is missing or not configured in configuration");
            }

            _logger.LogInformation($"Initializing EmailSender with SMTP: {_smtpServer}:{_smtpPort}, FromEmail: {_fromEmail}, SSL: {_enableSsl}");
            
            // Test email configuration on startup
            TestEmailConfiguration();
        }
        
        private void TestEmailConfiguration()
        {
            try
            {
                _logger.LogInformation("Testing email configuration...");
                _logger.LogInformation($"SMTP Server: {_smtpServer}, Port: {_smtpPort}, FromEmail: {_fromEmail}, SSL: {_enableSsl}");
                
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_fromEmail, _password);
                client.Timeout = 10000; // 10 seconds timeout
                
                _logger.LogInformation("Email configuration test completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email configuration test failed. Please check your email settings.");
                _logger.LogError($"SMTP Server: {_smtpServer}, Port: {_smtpPort}, FromEmail: {_fromEmail}, SSL: {_enableSsl}");
                _logger.LogError($"Error details: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }
            }
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

                if (string.IsNullOrEmpty(subject))
                {
                    _logger.LogError("Email subject is null or empty");
                    throw new ArgumentException("Email subject cannot be null or empty");
                }

                if (string.IsNullOrEmpty(htmlMessage))
                {
                    _logger.LogError("Email message is null or empty");
                    throw new ArgumentException("Email message cannot be null or empty");
                }

                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_fromEmail, _password);
                client.Timeout = 30000; // 30 seconds timeout for sending

                using var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(_fromEmail, _fromName);
                mailMessage.To.Add(email);
                mailMessage.Subject = subject;
                mailMessage.Body = htmlMessage;
                mailMessage.IsBodyHtml = true;

                _logger.LogInformation($"Connecting to SMTP server {_smtpServer}:{_smtpPort}...");
                _logger.LogInformation($"From: {_fromEmail} ({_fromName})");
                _logger.LogInformation($"To: {email}");
                _logger.LogInformation($"Subject: {subject}");
                _logger.LogInformation($"Message length: {htmlMessage?.Length ?? 0} characters");

                await client.SendMailAsync(mailMessage);

                _logger.LogInformation($"Successfully sent email to {email}");
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, $"SMTP error sending email to {email}. Status: {smtpEx.StatusCode}, Message: {smtpEx.Message}");
                
                // Provide more specific error messages based on SMTP status code
                string errorMessage = smtpEx.StatusCode switch
                {
                    SmtpStatusCode.ClientNotPermitted => "Client not permitted to send emails. Check your Gmail app password settings.",
                    SmtpStatusCode.CommandUnrecognized => "SMTP command not recognized. Check your SMTP server settings.",
                    SmtpStatusCode.ExceededStorageAllocation => "Email server storage exceeded.",
                    SmtpStatusCode.GeneralFailure => "General SMTP failure. Please try again later.",
                    SmtpStatusCode.InsufficientStorage => "Email server storage insufficient.",
                    SmtpStatusCode.MailboxBusy => "Email server is busy. Please try again later.",
                    SmtpStatusCode.MailboxNameNotAllowed => "Mailbox name not allowed.",
                    SmtpStatusCode.MailboxUnavailable => "Mailbox unavailable.",
                    SmtpStatusCode.MustIssueStartTlsFirst => "TLS/SSL required but not enabled.",
                    SmtpStatusCode.ServiceNotAvailable => "Email service not available. Please try again later.",
                    SmtpStatusCode.SyntaxError => "SMTP syntax error.",
                    SmtpStatusCode.TransactionFailed => "Email transaction failed.",
                    SmtpStatusCode.UserNotLocalWillForward => "User not local, will forward.",
                    SmtpStatusCode.UserNotLocalTryAlternatePath => "User not local, try alternate path.",
                    _ => $"SMTP error: {smtpEx.Message}"
                };
                
                throw new Exception(errorMessage, smtpEx);
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

        // Method to test email configuration
        public async Task<bool> TestEmailConfigurationAsync(string testEmail)
        {
            try
            {
                var testSubject = "Email Configuration Test - PA Website";
                var testMessage = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                        <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                            <h1 style='margin: 0; font-size: 28px;'>PA Website</h1>
                            <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Email Configuration Test</p>
                        </div>
                        
                        <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                            <h2 style='color: #4c1d95; margin-bottom: 20px;'>Email Configuration Test</h2>
                            
                            <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                                <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Test Details</h3>
                                <p style='color: #374151; line-height: 1.6; margin: 0;'>
                                    This is a test email to verify that the email configuration is working correctly.
                                </p>
                            </div>
                            
                            <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                                If you receive this email, it means the email configuration is working properly.
                            </p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                            <p>This is a test email sent at: {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>
                            <p>© 2024 PA Website. All rights reserved.</p>
                        </div>
                    </div>";

                await SendEmailAsync(testEmail, testSubject, testMessage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email configuration test failed");
                return false;
            }
        }

        // Method to test SMTP connection without sending email
        public bool TestSmtpConnection()
        {
            try
            {
                _logger.LogInformation("Testing SMTP connection...");
                
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.EnableSsl = _enableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_fromEmail, _password);
                client.Timeout = 10000; // 10 seconds timeout
                
                // Try to connect and authenticate
                client.SendCompleted += (sender, e) => {
                    if (e.Error != null)
                    {
                        _logger.LogError($"SMTP SendCompleted error: {e.Error.Message}");
                    }
                    else
                    {
                        _logger.LogInformation("SMTP SendCompleted successfully");
                    }
                };
                
                _logger.LogInformation("SMTP connection test completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP connection test failed");
                return false;
            }
        }
    }
}