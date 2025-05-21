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
        private readonly CodeSettings _codeSettings;
        private readonly ILogger<EmailService> _logger;
        private readonly IAmazonSimpleEmailService _sesClient;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger, IOptions<CodeSettings> codeSettings)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;

            _sesClient = new AmazonSimpleEmailServiceClient(
                _emailSettings.AwsAccessKey,
                _emailSettings.AwsSecretKey,
                RegionEndpoint.GetBySystemName(_emailSettings.AwsRegion));
            _codeSettings = codeSettings.Value;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string username, string code)
        {
            string subject = "Şifre Sıfırlama Kodunuz";

            string body = $@"
                    <!DOCTYPE html>
                    <html lang='tr'>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                        <title>Şifre Sıfırlama Kodu</title>
                        <style>
                            @import url('https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700&display=swap');
        
                            body {{
                                font-family: 'Poppins', Arial, sans-serif;
                                line-height: 1.6;
                                color: #333;
                                background-color: #f9f9f9;
                                margin: 0;
                                padding: 0;
                            }}
        
                            .email-wrapper {{
                                max-width: 600px;
                                margin: 0 auto;
                                background-color: #ffffff;
                                border-radius: 8px;
                                overflow: hidden;
                                box-shadow: 0 4px 10px rgba(0, 0, 0, 0.05);
                            }}
        
                            .email-header {{
                                background: linear-gradient(135deg, #43a047 0%, #2e7d32 100%);
                                padding: 25px 0;
                                text-align: center;
                            }}
        
                            .logo {{
                                margin-bottom: 10px;
                            }}
        
                            .logo img {{
                                max-height: 40px;
                            }}
        
                            .header-title {{
                                color: white;
                                font-size: 22px;
                                font-weight: 600;
                                margin: 0;
                            }}
        
                            .email-body {{
                                padding: 40px 30px;
                                background-color: #ffffff;
                            }}
        
                            .greeting {{
                                font-size: 18px;
                                margin-bottom: 20px;
                                color: #333;
                            }}
        
                            .message {{
                                font-size: 16px;
                                color: #555;
                                margin-bottom: 25px;
                            }}
        
                            .code-container {{
                                text-align: center;
                                margin: 35px 0;
                            }}
        
                            .code-box {{
                                display: inline-block;
                                background-color: #f5f5f5;
                                border: 2px dashed #43a047;
                                color: #333;
                                font-weight: 700;
                                font-size: 32px;
                                letter-spacing: 5px;
                                padding: 15px 35px;
                                border-radius: 8px;
                                box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
                            }}
        
                            .expiry-note {{
                                text-align: center;
                                font-size: 14px;
                                color: #777;
                                margin-top: 15px;
                            }}
        
                            .note {{
                                font-size: 14px;
                                color: #777;
                                background-color: #f8f9fa;
                                border-left: 4px solid #e9ecef;
                                padding: 15px;
                                margin: 25px 0;
                                border-radius: 0 4px 4px 0;
                            }}
        
                            .signature {{
                                margin-top: 30px;
                                font-size: 15px;
                                color: #555;
                            }}
        
                            .team-name {{
                                font-weight: 600;
                                color: #43a047;
                            }}
        
                            .email-footer {{
                                text-align: center;
                                padding: 20px 30px;
                                background-color: #f8f9fa;
                                color: #999;
                                font-size: 13px;
                                border-top: 1px solid #eee;
                            }}
        
                            @media screen and (max-width: 550px) {{
                                .email-body {{
                                    padding: 30px 20px;
                                }}
            
                                .header-title {{
                                    font-size: 20px;
                                }}
            
                                .greeting {{
                                    font-size: 17px;
                                }}
            
                                .message {{
                                    font-size: 15px;
                                }}
            
                                .code-box {{
                                    font-size: 28px;
                                    padding: 12px 25px;
                                }}
                            }}
                        </style>
                    </head>
                    <body>
                        <div class='email-wrapper'>
                            <div class='email-header'>
                                <div class='logo'>
                                    <!-- Logo URL'nizi ekleyin veya sadece metin kullanın -->
                                    <!-- <img src='LOGO_URL' alt='Camply'> -->
                                </div>
                                <h1 class='header-title'>Şifre Sıfırlama Kodunuz</h1>
                            </div>
        
                            <div class='email-body'>
                                <p class='greeting'>Merhaba <strong>{username}</strong>,</p>
            
                                <p class='message'>Hesabınız için bir şifre sıfırlama talebinde bulundunuz. Şifrenizi yenilemek için aşağıdaki 6 haneli kodu kullanın:</p>
            
                                <div class='code-container'>
                                    <div class='code-box'>{code}</div>
                                    <p class='expiry-note'>Bu kod 15 dakika içinde geçerliliğini yitirecektir.</p>
                                </div>
            
                                <div class='note'>
                                    <p>Bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                                    <p style='margin-bottom: 0;'>Hesabınızın güvenliği bizim için önemlidir. Kodu kimseyle paylaşmayın.</p>
                                </div>
            
                                <div class='signature'>
                                    Saygılarımızla,<br>
                                    <span class='team-name'>Camply Ekibi</span>
                                </div>
                            </div>
        
                            <div class='email-footer'>
                                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                                <p style='margin-bottom: 0;'>© 2025 Camply. Tüm hakları saklıdır.</p>
                            </div>
                        </div>
                    </body>
                    </html>
                    ";

            return await SendEmailAsync(email, subject, body);
        }
        public async Task<bool> SendPasswordChangedEmailAsync(string email, string username)
        {
            string subject = "Şifre Değişikliği Bildirimi";

            string body = $@"
    <!DOCTYPE html>
    <html lang='tr'>
    <head>
        <meta charset='UTF-8'>
        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
        <title>Şifre Değişikliği Bildirimi</title>
        <style>
            @import url('https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700&display=swap');
            
            body {{
                font-family: 'Poppins', Arial, sans-serif;
                line-height: 1.6;
                color: #333;
                background-color: #f9f9f9;
                margin: 0;
                padding: 0;
            }}
            
            .email-wrapper {{
                max-width: 600px;
                margin: 0 auto;
                background-color: #ffffff;
                border-radius: 8px;
                overflow: hidden;
                box-shadow: 0 4px 10px rgba(0, 0, 0, 0.05);
            }}
            
            .email-header {{
                background: linear-gradient(135deg, #43a047 0%, #2e7d32 100%);
                padding: 25px 0;
                text-align: center;
            }}
            
            .logo {{
                margin-bottom: 10px;
            }}
            
            .logo img {{
                max-height: 40px;
            }}
            
            .header-title {{
                color: white;
                font-size: 22px;
                font-weight: 600;
                margin: 0;
            }}
            
            .email-body {{
                padding: 40px 30px;
                background-color: #ffffff;
            }}
            
            .greeting {{
                font-size: 18px;
                margin-bottom: 20px;
                color: #333;
            }}
            
            .message {{
                font-size: 16px;
                color: #555;
                margin-bottom: 25px;
            }}
            
            .alert-box {{
                background-color: #e8f5e9;
                border-left: 4px solid #43a047;
                padding: 15px;
                margin: 25px 0;
                border-radius: 0 4px 4px 0;
                color: #2e7d32;
            }}
            
            .alert-box p {{
                margin: 0;
                font-weight: 500;
            }}
            
            .signature {{
                margin-top: 30px;
                font-size: 15px;
                color: #555;
            }}
            
            .team-name {{
                font-weight: 600;
                color: #43a047;
            }}
            
            .email-footer {{
                text-align: center;
                padding: 20px 30px;
                background-color: #f8f9fa;
                color: #999;
                font-size: 13px;
                border-top: 1px solid #eee;
            }}
            
            .warning {{
                background-color: #ffeeed;
                border-left: 4px solid #f44336;
                padding: 15px;
                margin: 25px 0;
                border-radius: 0 4px 4px 0;
                color: #d32f2f;
                font-weight: 500;
            }}
            
            @media screen and (max-width: 550px) {{
                .email-body {{
                    padding: 30px 20px;
                }}
                
                .header-title {{
                    font-size: 20px;
                }}
                
                .greeting {{
                    font-size: 17px;
                }}
                
                .message {{
                    font-size: 15px;
                }}
            }}
        </style>
    </head>
    <body>
        <div class='email-wrapper'>
            <div class='email-header'>
                <div class='logo'>
                    <!-- Logo URL'nizi ekleyin veya sadece metin kullanın -->
                    <!-- <img src='LOGO_URL' alt='Camply'> -->
                </div>
                <h1 class='header-title'>Şifre Değişikliği Bildirimi</h1>
            </div>
            
            <div class='email-body'>
                <p class='greeting'>Merhaba <strong>{username}</strong>,</p>
                
                <p class='message'>Hesabınızın şifresi az önce başarıyla değiştirildi. Güvenlik için bu değişikliği size bildirmek istedik.</p>
                
                <div class='alert-box'>
                    <p>Şifreniz başarıyla güncellendi.</p>
                </div>
                
                <div class='warning'>
                    <p>Eğer bu değişikliği siz yapmadıysanız, lütfen derhal hesabınızı güvence altına almak için bizimle iletişime geçin.</p>
                </div>
                
                <div class='signature'>
                    Saygılarımızla,<br>
                    <span class='team-name'>Camply Ekibi</span>
                </div>
            </div>
            
            <div class='email-footer'>
                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                <p style='margin-bottom: 0;'>© 2025 Camply. Tüm hakları saklıdır.</p>
            </div>
        </div>
    </body>
    </html>
    ";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailVerificationAsync(string email, string username, string verificationCode)
        {

            string subject = "E-posta Doğrulama Kodunuz";

            string body = $@"
                <!DOCTYPE html>
                <html lang='tr'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>E-posta Doğrulama Kodu</title>
                    <style>
                        @import url('https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700&display=swap');
        
                        body {{
                            font-family: 'Poppins', Arial, sans-serif;
                            line-height: 1.6;
                            color: #333;
                            background-color: #f9f9f9;
                            margin: 0;
                            padding: 0;
                        }}
        
                        .email-wrapper {{
                            max-width: 600px;
                            margin: 0 auto;
                            background-color: #ffffff;
                            border-radius: 8px;
                            overflow: hidden;
                            box-shadow: 0 4px 10px rgba(0, 0, 0, 0.05);
                        }}
        
                        .email-header {{
                            background: linear-gradient(135deg, #43a047 0%, #2e7d32 100%);
                            padding: 25px 0;
                            text-align: center;
                        }}
        
                        .logo {{
                            margin-bottom: 10px;
                        }}
        
                        .logo img {{
                            max-height: 40px;
                        }}
        
                        .header-title {{
                            color: white;
                            font-size: 22px;
                            font-weight: 600;
                            margin: 0;
                        }}
        
                        .email-body {{
                            padding: 40px 30px;
                            background-color: #ffffff;
                        }}
        
                        .greeting {{
                            font-size: 18px;
                            margin-bottom: 20px;
                            color: #333;
                        }}
        
                        .message {{
                            font-size: 16px;
                            color: #555;
                            margin-bottom: 25px;
                        }}
        
                        .code-container {{
                            text-align: center;
                            margin: 35px 0;
                        }}
        
                        .code-box {{
                            display: inline-block;
                            background-color: #f5f5f5;
                            border: 2px dashed #43a047;
                            color: #333;
                            font-weight: 700;
                            font-size: 32px;
                            letter-spacing: 5px;
                            padding: 15px 35px;
                            border-radius: 8px;
                            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
                        }}
        
                        .expiry-note {{
                            text-align: center;
                            font-size: 14px;
                            color: #777;
                            margin-top: 15px;
                        }}
        
                        .signature {{
                            margin-top: 30px;
                            font-size: 15px;
                            color: #555;
                        }}
        
                        .team-name {{
                            font-weight: 600;
                            color: #43a047;
                        }}
        
                        .email-footer {{
                            text-align: center;
                            padding: 20px 30px;
                            background-color: #f8f9fa;
                            color: #999;
                            font-size: 13px;
                            border-top: 1px solid #eee;
                        }}
        
                        @media screen and (max-width: 550px) {{
                            .email-body {{
                                padding: 30px 20px;
                            }}
            
                            .header-title {{
                                font-size: 20px;
                            }}
            
                            .greeting {{
                                font-size: 17px;
                            }}
            
                            .message {{
                                font-size: 15px;
                            }}
            
                            .code-box {{
                                font-size: 28px;
                                padding: 12px 25px;
                            }}
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-wrapper'>
                        <div class='email-header'>
                            <div class='logo'>
                                <!-- Logo URL'nizi ekleyin veya sadece metin kullanın -->
                                <!-- <img src='LOGO_URL' alt='Camply'> -->
                            </div>
                            <h1 class='header-title'>E-posta Doğrulama</h1>
                        </div>
        
                        <div class='email-body'>
                            <p class='greeting'>Merhaba <strong>{username}</strong>,</p>
            
                            <p class='message'>Camply'e hoş geldiniz! Hesabınızı etkinleştirmek için aşağıdaki doğrulama kodunu kullanın:</p>
            
                            <div class='code-container'>
                                <div class='code-box'>{verificationCode}</div>
                                <p class='expiry-note'>Bu kod {_codeSettings.CodeExpirationMinutes} dakika içinde geçerliliğini yitirecektir.</p>
                            </div>
            
                            <div class='signature'>
                                Saygılarımızla,<br>
                                <span class='team-name'>Camply Ekibi</span>
                            </div>
                        </div>
        
                        <div class='email-footer'>
                            <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                            <p style='margin-bottom: 0;'>© 2025 Camply. Tüm hakları saklıdır.</p>
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