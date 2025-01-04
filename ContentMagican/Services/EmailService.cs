using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace ContentMagican.Services
{
    public class EmailService
    {
        private readonly string _fromEmail;
        private readonly string _password;
        private readonly string _smtpHost;
        private readonly int _port;
        private readonly bool _enableSsl;

        public EmailService(IConfiguration configuration)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _fromEmail = emailSettings["FromEmail"];
            _password = emailSettings["EmailPassword"];
            _smtpHost = emailSettings["SmtpHost"];
            _port = int.Parse(emailSettings["Port"]);
            _enableSsl = bool.Parse(emailSettings["EnableSsl"]);
        }

        public void SendEmail(string toEmail, string subject, string body)
        {
            using (var mailMessage = new MailMessage())
            {
                mailMessage.From = new MailAddress(_fromEmail);
                mailMessage.To.Add(toEmail);
                mailMessage.Subject = subject;
                mailMessage.Body = body;

                using (var smtpClient = new SmtpClient(_smtpHost, _port))
                {
                    smtpClient.Credentials = new NetworkCredential(_fromEmail, _password);
                    smtpClient.EnableSsl = _enableSsl;
                    smtpClient.Send(mailMessage);
                }
            }
        }
    }
}
