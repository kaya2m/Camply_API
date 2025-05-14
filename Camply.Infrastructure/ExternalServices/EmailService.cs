using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Collections.Generic;

namespace Camply.Infrastructure.ExternalServices
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IAmazonSimpleEmailService _sesClient;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;

            _sesClient = new AmazonSimpleEmailServiceClient(
                _emailSettings.AwsAccessKey,
                _emailSettings.AwsSecretKey,
                RegionEndpoint.GetBySystemName(_emailSettings.AwsRegion));
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string username, string token, string resetLink)
        {
            string subject = "Şifre Sıfırlama - Camply";

            string body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
                    .content {{ padding: 20px; border: 1px solid #ddd; }}
                    .button {{ display: inline-block; background-color: #4CAF50; color: white; padding: 10px 20px; 
                             text-decoration: none; border-radius: 5px; margin-top: 20px; }}
                    .footer {{ margin-top: 20px; font-size: 12px; color: #777; text-align: center; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>Camply - Şifre Sıfırlama</h2>
                    </div>
                    <div class='content'>
                        <p>Merhaba <strong>{username}</strong>,</p>
                        <p>Hesabınız için bir şifre sıfırlama talebinde bulundunuz.</p>
                        <p>Şifrenizi sıfırlamak için aşağıdaki butona tıklayın:</p>
                        <a href='{resetLink}' class='button'>Şifremi Sıfırla</a>
                        <p>Eğer bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                        <p>Bu link 1 saat süreyle geçerlidir.</p>
                        <p>Saygılarımızla,<br>Camply Ekibi</p>
                    </div>
                    <div class='footer'>
                        <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                    </div>
                </div>
            </body>
            </html>
            ";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordChangedEmailAsync(string email, string username)
        {
            string subject = "Şifre Değişikliği Bildirimi - Camply";

            string body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
                    .content {{ padding: 20px; border: 1px solid #ddd; }}
                    .footer {{ margin-top: 20px; font-size: 12px; color: #777; text-align: center; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>Camply - Şifre Değişikliği</h2>
                    </div>
                    <div class='content'>
                        <p>Merhaba <strong>{username}</strong>,</p>
                        <p>Hesabınızın şifresi başarıyla değiştirildi.</p>
                        <p>Eğer bu değişikliği siz yapmadıysanız, lütfen derhal bizimle iletişime geçin.</p>
                        <p>Saygılarımızla,<br>Camply Ekibi</p>
                    </div>
                    <div class='footer'>
                        <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                    </div>
                </div>
            </body>
            </html>
            ";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailVerificationAsync(string email, string username, string token, string verificationLink)
        {
            string subject = "E-posta Doğrulama - Camply";

            string body = $@"
            <html>
            <head>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background-color: #4CAF50; color: white; padding: 10px; text-align: center; }}
                    .content {{ padding: 20px; border: 1px solid #ddd; }}
                    .button {{ display: inline-block; background-color: #4CAF50; color: white; padding: 10px 20px; 
                             text-decoration: none; border-radius: 5px; margin-top: 20px; }}
                    .footer {{ margin-top: 20px; font-size: 12px; color: #777; text-align: center; }}
                </style>    
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>Camply - E-posta Doğrulama</h2>
                    </div>
                    <div class='content'>
                        <p>Merhaba <strong>{username}</strong>,</p>
                        <p>Camply hesabınızı oluşturduğunuz için teşekkür ederiz.</p>
                        <p>E-posta adresinizi doğrulamak için aşağıdaki butona tıklayın:</p>
                        <a href='{verificationLink}' class='button'>E-postamı Doğrula</a>
                        <p>Saygılarımızla,<br>Camply Ekibi</p>
                    </div>
                    <div class='footer'>
                        <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                    </div>
                </div>
            </body>
            </html>
            ";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var sendRequest = new SendEmailRequest
                {
                    Source = $"{_emailSettings.SenderName} <{_emailSettings.SenderEmail}>",
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { to }
                    },
                    Message = new Message
                    {
                        Subject = new Content(subject),
                        Body = new Body
                        {
                            Html = new Content
                            {
                                Charset = "UTF-8",
                                Data = body
                            }
                        }
                    }
                };

                var response = await _sesClient.SendEmailAsync(sendRequest);
                _logger.LogInformation($"E-posta başarıyla gönderildi: {to}, Konu: {subject}, MessageId: {response.MessageId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"E-posta gönderilirken hata oluştu: {to}, Konu: {subject}");
                return false;
            }
        }
    }
}