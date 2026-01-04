using Microsoft.AspNetCore.Identity.UI.Services;
using PA_Website.Models;

namespace PA_Website.Services
{
    public class EmailService : IEmailService
    {
        private readonly IEmailSender _emailSender;
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public EmailService(IEmailSender emailSender, ILogger<EmailService> logger, 
            IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _emailSender = emailSender;
            _logger = logger;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task SendReservationConfirmationAsync(User user, UserService reservation, Service service)
        {
            try
            {
                var subject = "Нова резервация - Душевна Мозайка";
                var htmlMessage = CreateReservationEmailTemplate(user, reservation, service);
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reservation confirmation email to {Email}", user.Email);
            }
        }

        public async Task SendReservationCancellationAsync(User user, UserService reservation, Service service)
        {
            try
            {
                var subject = "Резервация отменена - Душевна Мозайка";
                var htmlMessage = CreateCancellationEmailTemplate(user, reservation, service);
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation email to {Email}", user.Email);
            }
        }

        public async Task SendReservationRescheduleAsync(User user, UserService oldReservation, UserService newReservation, Service service)
        {
            try
            {
                var subject = "Резервация пренасрочена - Душевна Мозайка";
                var htmlMessage = CreateRescheduleEmailTemplate(user, oldReservation, newReservation, service);
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
                _logger.LogInformation("Successfully sent reschedule email to {Email} for reservation {ReservationId}", user.Email, newReservation.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reschedule email to {Email} for reservation {ReservationId}", user.Email, newReservation.Id);
            }
        }

        public async Task SendStatusUpdateAsync(User user, UserService reservation, Service service, string oldStatus, string newStatus)
        {
            try
            {
                if (newStatus == "Confirmed" && oldStatus != "Confirmed")
                {
                    var subject = "Потвърждение на плащането - Резервацията е активирана";
                    var htmlMessage = CreateConfirmationEmailTemplate(user, reservation, service);
                    await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
                }
                else if (newStatus == "Completed" && oldStatus != "Completed" && 
                         service.CategoryOfService.ToLower() == "астрология")
                {
                    var subject = "Вашата астрологична услуга е завършена";
                    var htmlMessage = CreateAstroCardCompletionTemplate(user, reservation, service);
                    await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status update email to {Email}", user.Email);
            }
        }

        public async Task SendAstroCardCompletionAsync(User user, UserService reservation, Service service)
        {
            try
            {
                var subject = "Вашата астрологична услуга е завършена";
                var htmlMessage = CreateAstroCardCompletionTemplate(user, reservation, service);
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send astro card completion email to {Email}", user.Email);
            }
        }

        public async Task SendAdminNotificationAsync(User user, UserService reservation, Service service, string action)
        {
            try
            {
                var adminEmail = _configuration["EmailSettings:AdminEmail"] ?? "dushevna_mozaika@abv.bg";
                var subject = GetAdminNotificationSubject(action);
                var htmlMessage = CreateAdminNotificationTemplate(user, reservation, service, action);
                await _emailSender.SendEmailAsync(adminEmail, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send admin notification email");
            }
        }

        public async Task SendBulkEmailAsync(List<EmailRecipient> recipients, string subject, string message, string emailType)
        {
            foreach (var recipient in recipients)
            {
                try
                {
                    if (string.IsNullOrEmpty(recipient.Email))
                    {
                        _logger.LogWarning("Recipient {Name} has no email address", $"{recipient.FName} {recipient.LName}");
                        continue;
                    }

                    string emailHtml = recipient.IsUser 
                        ? CreateBulkEmailTemplateForUser(recipient, message, emailType)
                        : CreateBulkEmailTemplateForSubscriber(recipient.Email, message, emailType);

                    await _emailSender.SendEmailAsync(recipient.Email, subject, emailHtml);
                    _logger.LogInformation("Successfully sent bulk email to {Email}", recipient.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send bulk email to {Email}", recipient.Email);
                }
            }
        }

        public async Task SendCustomEmailAsync(User user, UserService reservation, Service service, string subject, string message, string emailType)
        {
            try
            {
                string htmlMessage;
                
                switch (emailType?.ToLower())
                {
                    case "confirmation":
                        htmlMessage = CreateConfirmationEmailTemplate(user, reservation, service);
                        break;
                    case "reminder":
                        htmlMessage = CreateReminderEmailTemplate(user, reservation, service);
                        break;
                    case "custom":
                        htmlMessage = CreateCustomEmailTemplate(user, reservation, service, message, emailType);
                        break;
                    default:
                        htmlMessage = CreateCustomEmailTemplate(user, reservation, service, message, emailType);
                        break;
                }
                
                await _emailSender.SendEmailAsync(user.Email, subject, htmlMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send custom email to {Email}", user.Email);
                throw; // Re-throw to let the controller handle it
            }
        }

        #region Email Templates

        private string CreateReservationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Нова резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е създадена успешно. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Чакаща</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Ще получите потвърждение от администратор в най-кратки срокове.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateCancellationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #ef4444, #dc2626); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Резервация отменена</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е отменена успешно. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на отменената резервация</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #ef4444; font-weight: bold;'>Отменена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Ако имате въпроси или искате да направите нова резервация, не се колебайте да се свържете с нас.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Нова резервация
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateRescheduleEmailTemplate(User user, UserService oldReservation, UserService newReservation, Service service)
        {
            var oldDateTime = GetFormattedDateTime(oldReservation, service);
            var newDateTime = GetFormattedDateTime(newReservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Резервация пренасрочена</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е пренасрочена успешно. Ето промените:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на промяната</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <div style='background: #fee2e2; padding: 15px; border-radius: 8px; margin: 10px 0; border-left: 4px solid #ef4444;'>
                                <p style='margin: 5px 0; color: #991b1b;'><strong>❌ Стара резервация:</strong> {oldDateTime}</p>
                            </div>
                            <div style='background: #dcfce7; padding: 15px; border-radius: 8px; margin: 10px 0; border-left: 4px solid #10b981;'>
                                <p style='margin: 5px 0; color: #166534;'><strong>✅ Нова резервация:</strong> {newDateTime}</p>
                            </div>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Чакаща</span></p>
                        </div>
                        
                        <div style='background: #fef3c7; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                            <p style='margin: 0; color: #92400e; font-weight: 600;'>
                                <i class='fas fa-info-circle' style='margin-right: 8px;'></i>
                                Ще получите потвърждение от администратор в най-кратки срокове.
                            </p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2025 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateConfirmationEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #10b981, #059669); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Потвърждение на резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Вашата резервация е потвърдена от администратор. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #10b981; font-weight: bold;'>Потвърдена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Очакваме ви в посочения час. Ако имате въпроси, не се колебайте да се свържете с нас.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateReminderEmailTemplate(User user, UserService reservation, Service service)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #f59e0b, #d97706); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Напомняне за резервация</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Това е напомняне за вашата предстояща резервация. Ето детайлите:
                        </p>
                        
                        <div style='background: #fef3c7; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                            <h3 style='color: #92400e; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: #f59e0b; font-weight: bold;'>Потвърдена</span></p>
                        </div>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Моля, не забравяйте за вашата резервация. Очакваме ви в посочения час.
                        </p>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateAstroCardCompletionTemplate(User user, UserService reservation, Service service)
        {
            var baseUrl = GetBaseUrl();
            var userServiceUrl = $"{baseUrl}/UserServices/Details/{reservation.Id}";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #3b82f6, #1d4ed8); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Астрологична услуга завършена</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Радваме се да Ви информираме, че вашата астрологична услуга е успешно завършена!
                        </p>
                        
                        <div style='background: #dbeafe; padding: 15px; border-left: 5px solid #3b82f6; margin: 15px 0;'>
                            <h4 style='color: #1e40af; margin-top: 0;'>Статус на услугата: <strong>ЗАВЪРШЕНА</strong></h4>
                            <p>Вашата астрологична карта е готова и качена в системата.</p>
                        </div>
                        
                        <p><strong>Детайли за услугата:</strong></p>
                        <ul>
                            <li>Услуга: {service.NameService}</li>
                            <li>Категория: {service.CategoryOfService}</li>
                            <li>Дата за астрологичен анализ: {reservation.AstrologicalDate:dd.MM.yyyy HH:mm}</li>
                            <li>Място на раждане: {reservation.AstrologicalPlaceOfBirth}</li>
                            <li>Цена: {service.Price:F2} €</li>
                        </ul>
                        
                        <p><strong>Какво следва:</strong></p>
                        <ul>
                            <li>Вашата астрологична карта е качена и достъпна</li>
                            <li>Можете да я изтеглите от вашия профил</li>
                            <li>Ще получите подробен анализ на вашата астрологична карта</li>
                        </ul>
                        
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{userServiceUrl}' style='background-color: #7c3aed; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
                                <i class='fas fa-download' style='margin-right: 8px;'></i>
                                Преглед на резервацията
                            </a>
                        </div>
                        
                        <p style='font-size: 14px; color: #6b7280;'>
                            <strong>Забележка:</strong> Бутонът по-горе ще Ви отведе директно към страницата с детайлите на вашата резервация, 
                            където можете да изтеглите астрологичната карта.
                        </p>
                        
                        <p>Благодарим Ви, че избрахте нашите услуги!</p>
                        <p>С уважение,<br>Екипът на Душевна Мозайка</p>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично. Моля, не отговаряйте на него.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateAdminNotificationTemplate(User user, UserService reservation, Service service, string action)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var actionText = GetActionText(action);
            var color = GetActionColor(action);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Административно известие</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Известие за администратор</h2>
                        
                        <p style='color: #374151; line-height: 1.6; margin-bottom: 20px;'>
                            Има {actionText} от потребител. Ето детайлите:
                        </p>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Детайли на резервацията</h3>
                            <p style='margin: 5px 0;'><strong>Потребител:</strong> {user.FName} {user.LName}</p>
                            <p style='margin: 5px 0;'><strong>Имейл:</strong> {user.Email}</p>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Категория:</strong> {service.CategoryOfService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                            <p style='margin: 5px 0;'><strong>Цена:</strong> {service.Price:F2} €</p>
                            <p style='margin: 5px 0;'><strong>Статус:</strong> <span style='color: {color}; font-weight: bold;'>{reservation.Status}</span></p>
                            <p style='margin: 5px 0;'><strong>ID на резервация:</strong> {reservation.Id}</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/IndexAdmin' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен автоматично.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateBulkEmailTemplateForUser(EmailRecipient recipient, string message, string emailType)
        {
            var headerColor = GetEmailTypeColor(emailType);
            var headerText = GetEmailTypeText(emailType);
            var baseUrl = GetBaseUrl();
            var servicesUrl = $"{baseUrl}/Services";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: {headerColor}; color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>{headerText}</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {recipient.FName}!</h2>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <div style='color: #374151; line-height: 1.6; white-space: pre-wrap;'>{message}</div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{servicesUrl}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Разгледай услуги
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateBulkEmailTemplateForSubscriber(string email, string message, string emailType)
        {
            var headerColor = GetEmailTypeColor(emailType);
            var headerText = GetEmailTypeText(emailType);
            var baseUrl = GetBaseUrl();
            var servicesUrl = $"{baseUrl}/Services";

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: {headerColor}; color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>{headerText}</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте!</h2>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <div style='color: #374151; line-height: 1.6; white-space: pre-wrap;'>{message}</div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{servicesUrl}' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Разгледай услуги
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        private string CreateCustomEmailTemplate(User user, UserService reservation, Service service, string customMessage, string emailType)
        {
            var dateTime = GetFormattedDateTime(reservation, service);
            var baseUrl = GetBaseUrl();

            return $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc;'>
                    <div style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 30px; border-radius: 15px; text-align: center;'>
                        <h1 style='margin: 0; font-size: 28px;'>Душевна Мозайка</h1>
                        <p style='margin: 10px 0 0 0; font-size: 16px; opacity: 0.9;'>Съобщение от администратор</p>
                    </div>
                    
                    <div style='background: white; padding: 30px; border-radius: 15px; margin-top: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>
                        <h2 style='color: #4c1d95; margin-bottom: 20px;'>Здравейте, {user.FName}!</h2>
                        
                        <div style='background: #f3f4f6; padding: 20px; border-radius: 10px; margin: 20px 0;'>
                            <h3 style='color: #4c1d95; margin: 0 0 15px 0;'>Вашата резервация</h3>
                            <p style='margin: 5px 0;'><strong>Услуга:</strong> {service.NameService}</p>
                            <p style='margin: 5px 0;'><strong>Дата и час:</strong> {dateTime}</p>
                        </div>
                        
                        <div style='background: #fef3c7; padding: 20px; border-radius: 10px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                            <h3 style='color: #92400e; margin: 0 0 15px 0;'>Съобщение от администратор</h3>
                            <p style='color: #374151; line-height: 1.6; margin: 0;'>{customMessage}</p>
                        </div>
                        
                        <div style='text-align: center; margin-top: 30px;'>
                            <a href='{baseUrl}/UserServices/Dashboard' 
                               style='background: linear-gradient(135deg, #4c1d95, #7c3aed); color: white; padding: 12px 30px; text-decoration: none; border-radius: 8px; display: inline-block;'>
                                Преглед на резервациите
                            </a>
                        </div>
                    </div>
                    
                    <div style='text-align: center; margin-top: 20px; color: #6b7280; font-size: 14px;'>
                        <p>Този имейл е изпратен от администратор.</p>
                        <p>© 2024 Душевна Мозайка. Всички права запазени.</p>
                    </div>
                </div>";
        }

        #endregion

        #region Helper Methods

        private string GetFormattedDateTime(UserService reservation, Service service)
        {
            if (service.CategoryOfService.ToLower() == "астрология")
            {
                return reservation.AstrologicalDate?.ToString("dd.MM.yyyy") ?? "Дата не е посочена";
            }
            
            var dateStr = reservation.ReservationDate.ToString("dd.MM.yyyy");
            if (reservation.ReservationTime.HasValue)
            {
                return $"{dateStr} в {reservation.ReservationTime.Value:hh\\:mm}";
            }
            
            return dateStr;
        }

        private string GetBaseUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null) return "https://localhost:5001";
            
            return $"{request.Scheme}://{request.Host}";
        }

        private string GetAdminNotificationSubject(string action)
        {
            return action switch
            {
                "created" => "Нова резервация - Известие",
                "rescheduled" => "Пренасрочена резервация - Известие",
                "cancelled" => "Отменена резервация - Известие",
                _ => "Промяна в резервация - Известие"
            };
        }

        private string GetActionText(string action)
        {
            return action switch
            {
                "created" => "нова резервация",
                "rescheduled" => "пренасрочена резервация",
                "cancelled" => "отменена резервация",
                _ => "промяна в резервация"
            };
        }

        private string GetActionColor(string action)
        {
            return action switch
            {
                "created" => "#10b981",
                "rescheduled" => "#f59e0b",
                "cancelled" => "#ef4444",
                _ => "#6b7280"
            };
        }

        private string GetEmailTypeColor(string emailType)
        {
            return emailType switch
            {
                "announcement" => "linear-gradient(135deg, #4c1d95, #7c3aed)",
                "newsletter" => "linear-gradient(135deg, #10b981, #059669)",
                "promotion" => "linear-gradient(135deg, #f59e0b, #d97706)",
                _ => "linear-gradient(135deg, #4c1d95, #7c3aed)"
            };
        }

        private string GetEmailTypeText(string emailType)
        {
            return emailType switch
            {
                "announcement" => "Важно съобщение",
                "newsletter" => "Новини и обновления",
                "promotion" => "Специална оферта",
                _ => "Съобщение от Душевна Мозайка"
            };
        }

        #endregion
    }
} 