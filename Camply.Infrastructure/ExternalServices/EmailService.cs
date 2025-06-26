using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;

namespace Camply.Infrastructure.ExternalServices
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly CodeSettings _codeSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger, IOptions<CodeSettings> codeSettings)
        {
            _emailSettings = emailSettings.Value;
            _logger = logger;
            _codeSettings = codeSettings.Value;
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string username, string code)
        {
            string subject = "Şifre Sıfırlama Kodunuz";
            string body = GetPasswordResetEmailBody(username, code);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordChangedEmailAsync(string email, string username)
        {
            string subject = "Şifre Değişikliği Bildirimi";
            string body = GetPasswordChangedEmailBody(username);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailVerificationAsync(string email, string username, string verificationCode)
        {
            string subject = "E-posta Doğrulama Kodunuz";
            string body = GetEmailVerificationBody(username, verificationCode);
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var client = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort);
                client.EnableSsl = _emailSettings.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);

                using var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName);
                mailMessage.To.Add(to);
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation($"E-posta başarıyla gönderildi: {to}, Konu: {subject}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"E-posta gönderilirken hata oluştu: {to}, Konu: {subject}");
                return false;
            }
        }

        private string GetPasswordResetEmailBody(string username, string code)
        {
            return $@"
                <!DOCTYPE html>
                <html lang='tr'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Şifre Sıfırlama Kodu</title>
                    <style>
                        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap');
        
                        * {{
                            margin: 0;
                            padding: 0;
                            box-sizing: border-box;
                        }}
        
                        body {{
                            font-family: 'Inter', system-ui, -apple-system, sans-serif;
                            line-height: 1.6;
                            color: #1f2937;
                            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            margin: 0;
                            padding: 20px 0;
                        }}
        
                        .email-container {{
                            max-width: 680px;
                            margin: 0 auto;
                            background: rgba(255, 255, 255, 0.95);
                            backdrop-filter: blur(20px);
                            border-radius: 24px;
                            overflow: hidden;
                            box-shadow: 0 25px 50px rgba(0, 0, 0, 0.15);
                            border: 1px solid rgba(255, 255, 255, 0.2);
                        }}
        
                        .email-header {{
                            background: linear-gradient(135deg, #1e293b 0%, #334155 50%, #475569 100%);
                            padding: 48px 40px;
                            text-align: center;
                            position: relative;
                            overflow: hidden;
                        }}
        
                        .email-header::before {{
                            content: '';
                            position: absolute;
                            top: 0;
                            left: 0;
                            right: 0;
                            bottom: 0;
                            background: url('data:image/svg+xml;utf8,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><defs><pattern id=""grain"" width=""100"" height=""100"" patternUnits=""userSpaceOnUse""><circle cx=""25"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""25"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/></pattern></defs><rect width=""100"" height=""100"" fill=""url(%23grain)""/></svg>');
                            opacity: 0.5;
                        }}
        
                        .logo-container {{
                            position: relative;
                            z-index: 2;
                            margin-bottom: 24px;
                        }}
        
                        .logo {{
                            height: 48px;
                            width: auto;
                            filter: brightness(1.1);
                        }}
        
                        .header-content {{
                            position: relative;
                            z-index: 2;
                        }}
        
                        .header-title {{
                            color: #ffffff;
                            font-size: 28px;
                            font-weight: 700;
                            margin: 16px 0 8px;
                            letter-spacing: -0.5px;
                        }}
        
                        .header-subtitle {{
                            color: #cbd5e1;
                            font-size: 16px;
                            font-weight: 400;
                            opacity: 0.9;
                        }}
        
                        .email-body {{
                            padding: 56px 48px 48px;
                            background: #ffffff;
                            position: relative;
                        }}
        
                        .greeting {{
                            font-size: 20px;
                            font-weight: 600;
                            margin-bottom: 24px;
                            color: #1f2937;
                        }}
        
                        .message {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 40px;
                            line-height: 1.7;
                        }}
        
                        .code-section {{
                            text-align: center;
                            margin: 48px 0;
                            position: relative;
                        }}
        
                        .code-label {{
                            font-size: 14px;
                            font-weight: 600;
                            color: #6b7280;
                            text-transform: uppercase;
                            letter-spacing: 1px;
                            margin-bottom: 16px;
                        }}
        
                        .code-wrapper {{
                            background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
                            border: 2px solid #e2e8f0;
                            border-radius: 16px;
                            padding: 32px;
                            margin: 24px auto;
                            max-width: 320px;
                            position: relative;
                            box-shadow: 0 8px 25px rgba(0, 0, 0, 0.08);
                        }}
        
                        .code-box {{
                            font-family: 'SF Mono', 'Monaco', 'Menlo', monospace;
                            font-size: 36px;
                            font-weight: 800;
                            color: #1e293b;
                            letter-spacing: 8px;
                            margin: 0;
                            text-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
                        }}
        
                        .expiry-note {{
                            font-size: 14px;
                            color: #94a3b8;
                            margin-top: 20px;
                            font-weight: 500;
                        }}
        
                        .security-notice {{
                            background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
                            border: 1px solid #f59e0b;
                            border-radius: 12px;
                            padding: 24px;
                            margin: 40px 0;
                        }}
        
                        .security-notice h4 {{
                            color: #92400e;
                            font-size: 16px;
                            font-weight: 600;
                            margin-bottom: 8px;
                            display: flex;
                            align-items: center;
                            gap: 8px;
                        }}
        
                        .security-notice p {{
                            color: #a16207;
                            font-size: 14px;
                            margin: 0;
                            line-height: 1.6;
                        }}
        
                        .social-section {{
                            text-align: center;
                            margin-top: 48px;
                            padding-top: 32px;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .social-text {{
                            font-size: 14px;
                            color: #6b7280;
                            margin-bottom: 16px;
                        }}
        
                        .signature {{
                            margin-top: 40px;
                            text-align: center;
                        }}
        
                        .signature-text {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 8px;
                        }}
        
                        .team-name {{
                            font-size: 18px;
                            font-weight: 700;
                            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            -webkit-background-clip: text;
                            -webkit-text-fill-color: transparent;
                            background-clip: text;
                        }}
        
                        .email-footer {{
                            background: #f8fafc;
                            padding: 32px 48px;
                            text-align: center;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .footer-content {{
                            font-size: 13px;
                            color: #9ca3af;
                            line-height: 1.6;
                        }}
        
                        .footer-content p {{
                            margin: 4px 0;
                        }}
        
                        @media screen and (max-width: 640px) {{
                            body {{
                                padding: 10px 0;
                            }}
        
                            .email-container {{
                                margin: 0 16px;
                                border-radius: 16px;
                            }}
        
                            .email-header {{
                                padding: 32px 24px;
                            }}
        
                            .email-body {{
                                padding: 40px 24px 32px;
                            }}
        
                            .email-footer {{
                                padding: 24px;
                            }}
        
                            .header-title {{
                                font-size: 24px;
                            }}
        
                            .greeting {{
                                font-size: 18px;
                            }}
        
                            .code-box {{
                                font-size: 28px;
                                letter-spacing: 4px;
                            }}
        
                            .code-wrapper {{
                                padding: 24px;
                                margin: 16px auto;
                            }}
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-container'>
                        <div class='email-header'>
                            <div class='logo-container'>
                                <img src='https://camplymedia.blob.core.windows.net/system-images/camply-logo-for-ligth.svg' alt='Camply Logo' class='logo'>
                            </div>
                            <div class='header-content'>
                                <h1 class='header-title'>Şifre Sıfırlama</h1>
                                <p class='header-subtitle'>Hesabınızı güvende tutuyoruz</p>
                            </div>
                        </div>
        
                        <div class='email-body'>
                            <h2 class='greeting'>Merhaba {username}! 👋</h2>
            
                            <p class='message'>
                                Hesabınız için bir şifre sıfırlama talebinde bulundunuz. Güvenliğiniz bizim önceliğimiz! 
                                Yeni şifrenizi oluşturmak için aşağıdaki doğrulama kodunu kullanın.
                            </p>
            
                            <div class='code-section'>
                                <div class='code-label'>Doğrulama Kodu</div>
                                <div class='code-wrapper'>
                                    <div class='code-box'>{code}</div>
                                </div>
                                <p class='expiry-note'>⏰ Bu kod 15 dakika içinde geçerliliğini yitirecektir</p>
                            </div>
            
                            <div class='security-notice'>
                                <h4>🔒 Güvenlik Hatırlatması</h4>
                                <p>Bu işlemi siz yapmadıysanız, bu e-postayı görmezden gelebilirsiniz. Kodunuzu kimseyle paylaşmayın ve şüpheli aktivite fark ederseniz hemen bizimle iletişime geçin.</p>
                            </div>
            
                            <div class='social-section'>
                                <p class='social-text'>Camply topluluğuna katıldığınız için teşekkürler! 🎯</p>
                            </div>
            
                            <div class='signature'>
                                <p class='signature-text'>Saygılarımızla,</p>
                                <p class='team-name'>Camply Ekibi</p>
                            </div>
                        </div>
        
                        <div class='email-footer'>
                            <div class='footer-content'>
                                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                                <p>© 2025 Camply. Tüm hakları saklıdır.</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetPasswordChangedEmailBody(string username)
        {
            return $@"
                <!DOCTYPE html>
                <html lang='tr'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Şifre Değişikliği Bildirimi</title>
                    <style>
                        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap');
        
                        * {{
                            margin: 0;
                            padding: 0;
                            box-sizing: border-box;
                        }}
        
                        body {{
                            font-family: 'Inter', system-ui, -apple-system, sans-serif;
                            line-height: 1.6;
                            color: #1f2937;
                            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
                            margin: 0;
                            padding: 20px 0;
                        }}
        
                        .email-container {{
                            max-width: 680px;
                            margin: 0 auto;
                            background: rgba(255, 255, 255, 0.95);
                            backdrop-filter: blur(20px);
                            border-radius: 24px;
                            overflow: hidden;
                            box-shadow: 0 25px 50px rgba(0, 0, 0, 0.15);
                            border: 1px solid rgba(255, 255, 255, 0.2);
                        }}
        
                        .email-header {{
                            background: linear-gradient(135deg, #065f46 0%, #047857 50%, #059669 100%);
                            padding: 48px 40px;
                            text-align: center;
                            position: relative;
                            overflow: hidden;
                        }}
        
                        .email-header::before {{
                            content: '';
                            position: absolute;
                            top: 0;
                            left: 0;
                            right: 0;
                            bottom: 0;
                            background: url('data:image/svg+xml;utf8,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><defs><pattern id=""grain"" width=""100"" height=""100"" patternUnits=""userSpaceOnUse""><circle cx=""25"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""25"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/></pattern></defs><rect width=""100"" height=""100"" fill=""url(%23grain)""/></svg>');
                            opacity: 0.5;
                        }}
        
                        .logo-container {{
                            position: relative;
                            z-index: 2;
                            margin-bottom: 24px;
                        }}
        
                        .logo {{
                            height: 48px;
                            width: auto;
                            filter: brightness(1.1);
                        }}
        
                        .header-content {{
                            position: relative;
                            z-index: 2;
                        }}
        
                        .header-title {{
                            color: #ffffff;
                            font-size: 28px;
                            font-weight: 700;
                            margin: 16px 0 8px;
                            letter-spacing: -0.5px;
                        }}
        
                        .header-subtitle {{
                            color: #a7f3d0;
                            font-size: 16px;
                            font-weight: 400;
                            opacity: 0.9;
                        }}
        
                        .email-body {{
                            padding: 56px 48px 48px;
                            background: #ffffff;
                            position: relative;
                        }}
        
                        .greeting {{
                            font-size: 20px;
                            font-weight: 600;
                            margin-bottom: 24px;
                            color: #1f2937;
                        }}
        
                        .message {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 40px;
                            line-height: 1.7;
                        }}
        
                        .success-section {{
                            text-align: center;
                            margin: 48px 0;
                            position: relative;
                        }}
        
                        .success-icon {{
                            width: 80px;
                            height: 80px;
                            margin: 0 auto 24px;
                            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
                            border-radius: 50%;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            box-shadow: 0 8px 25px rgba(16, 185, 129, 0.25);
                        }}
        
                        .checkmark {{
                            width: 32px;
                            height: 32px;
                            stroke: white;
                            stroke-width: 3;
                            fill: none;
                        }}
        
                        .success-message {{
                            background: linear-gradient(135deg, #ecfdf5 0%, #d1fae5 100%);
                            border: 1px solid #10b981;
                            border-radius: 12px;
                            padding: 24px;
                            margin: 24px 0;
                        }}
        
                        .success-message h4 {{
                            color: #047857;
                            font-size: 18px;
                            font-weight: 600;
                            margin-bottom: 8px;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            gap: 8px;
                        }}
        
                        .success-message p {{
                            color: #065f46;
                            font-size: 16px;
                            margin: 0;
                            line-height: 1.6;
                        }}
        
                        .warning-notice {{
                            background: linear-gradient(135deg, #fef2f2 0%, #fecaca 100%);
                            border: 1px solid #ef4444;
                            border-radius: 12px;
                            padding: 24px;
                            margin: 40px 0;
                        }}
        
                        .warning-notice h4 {{
                            color: #dc2626;
                            font-size: 16px;
                            font-weight: 600;
                            margin-bottom: 8px;
                            display: flex;
                            align-items: center;
                            gap: 8px;
                        }}
        
                        .warning-notice p {{
                            color: #b91c1c;
                            font-size: 14px;
                            margin: 0;
                            line-height: 1.6;
                        }}
        
                        .social-section {{
                            text-align: center;
                            margin-top: 48px;
                            padding-top: 32px;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .social-text {{
                            font-size: 14px;
                            color: #6b7280;
                            margin-bottom: 16px;
                        }}
        
                        .signature {{
                            margin-top: 40px;
                            text-align: center;
                        }}
        
                        .signature-text {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 8px;
                        }}
        
                        .team-name {{
                            font-size: 18px;
                            font-weight: 700;
                            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
                            -webkit-background-clip: text;
                            -webkit-text-fill-color: transparent;
                            background-clip: text;
                        }}
        
                        .email-footer {{
                            background: #f8fafc;
                            padding: 32px 48px;
                            text-align: center;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .footer-content {{
                            font-size: 13px;
                            color: #9ca3af;
                            line-height: 1.6;
                        }}
        
                        .footer-content p {{
                            margin: 4px 0;
                        }}
        
                        @media screen and (max-width: 640px) {{
                            body {{
                                padding: 10px 0;
                            }}
        
                            .email-container {{
                                margin: 0 16px;
                                border-radius: 16px;
                            }}
        
                            .email-header {{
                                padding: 32px 24px;
                            }}
        
                            .email-body {{
                                padding: 40px 24px 32px;
                            }}
        
                            .email-footer {{
                                padding: 24px;
                            }}
        
                            .header-title {{
                                font-size: 24px;
                            }}
        
                            .greeting {{
                                font-size: 18px;
                            }}
        
                            .success-icon {{
                                width: 64px;
                                height: 64px;
                            }}
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-container'>
                        <div class='email-header'>
                            <div class='logo-container'>
                                <img src='https://camplymedia.blob.core.windows.net/system-images/camply-logo-for-ligth.svg' alt='Camply Logo' class='logo'>
                            </div>
                            <div class='header-content'>
                                <h1 class='header-title'>Şifre Değiştirildi</h1>
                                <p class='header-subtitle'>Hesabınız güvende</p>
                            </div>
                        </div>
        
                        <div class='email-body'>
                            <h2 class='greeting'>Merhaba {username}! 👋</h2>
            
                            <p class='message'>
                                Hesabınızın şifresi az önce başarıyla değiştirildi. Güvenliğiniz bizim önceliğimiz olduğu için 
                                bu önemli değişikliği size bildirmek istedik.
                            </p>
            
                            <div class='success-section'>
                                <div class='success-icon'>
                                    <svg class='checkmark' viewBox='0 0 24 24'>
                                        <path d='M9 12l2 2 4-4' stroke='currentColor' stroke-width='3' fill='none' stroke-linecap='round' stroke-linejoin='round'/>
                                    </svg>
                                </div>
                                
                                <div class='success-message'>
                                    <h4>✅ Şifre Başarıyla Güncellendi</h4>
                                    <p>Yeni şifreniz aktif edildi ve hesabınız güvende.</p>
                                </div>
                            </div>
            
                            <div class='warning-notice'>
                                <h4>⚠️ Önemli Güvenlik Bildirimi</h4>
                                <p>Eğer bu değişikliği siz yapmadıysanız, lütfen derhal hesabınızı güvence altına almak için bizimle iletişime geçin. Şüpheli aktivite bildirimi için destek@camply.com adresine yazabilirsiniz.</p>
                            </div>
            
                            <div class='social-section'>
                                <p class='social-text'>Camply ile güvenli sosyal medya deneyiminiz devam ediyor! 🔒</p>
                            </div>
            
                            <div class='signature'>
                                <p class='signature-text'>Saygılarımızla,</p>
                                <p class='team-name'>Camply Güvenlik Ekibi</p>
                            </div>
                        </div>
        
                        <div class='email-footer'>
                            <div class='footer-content'>
                                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                                <p>© 2025 Camply. Tüm hakları saklıdır.</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";
        }

        private string GetEmailVerificationBody(string username, string verificationCode)
        {
            return $@"
                <!DOCTYPE html>
                <html lang='tr'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>E-posta Doğrulama Kodu</title>
                    <style>
                        @import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700;800&display=swap');
        
                        * {{
                            margin: 0;
                            padding: 0;
                            box-sizing: border-box;
                        }}
        
                        body {{
                            font-family: 'Inter', system-ui, -apple-system, sans-serif;
                            line-height: 1.6;
                            color: #1f2937;
                            background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #a855f7 100%);
                            margin: 0;
                            padding: 20px 0;
                        }}
        
                        .email-container {{
                            max-width: 680px;
                            margin: 0 auto;
                            background: rgba(255, 255, 255, 0.95);
                            backdrop-filter: blur(20px);
                            border-radius: 24px;
                            overflow: hidden;
                            box-shadow: 0 25px 50px rgba(0, 0, 0, 0.15);
                            border: 1px solid rgba(255, 255, 255, 0.2);
                        }}
        
                        .email-header {{
                            background: linear-gradient(135deg, #4338ca 0%, #6366f1 50%, #8b5cf6 100%);
                            padding: 48px 40px;
                            text-align: center;
                            position: relative;
                            overflow: hidden;
                        }}
        
                        .email-header::before {{
                            content: '';
                            position: absolute;
                            top: 0;
                            left: 0;
                            right: 0;
                            bottom: 0;
                            background: url('data:image/svg+xml;utf8,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><defs><pattern id=""grain"" width=""100"" height=""100"" patternUnits=""userSpaceOnUse""><circle cx=""25"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""25"" cy=""75"" r=""1"" fill=""rgba(255,255,255,0.03)""/><circle cx=""75"" cy=""25"" r=""1"" fill=""rgba(255,255,255,0.03)""/></pattern></defs><rect width=""100"" height=""100"" fill=""url(%23grain)""/></svg>');
                            opacity: 0.5;
                        }}
        
                        .logo-container {{
                            position: relative;
                            z-index: 2;
                            margin-bottom: 24px;
                        }}
        
                        .logo {{
                            height: 48px;
                            width: auto;
                            filter: brightness(1.1);
                        }}
        
                        .header-content {{
                            position: relative;
                            z-index: 2;
                        }}
        
                        .header-title {{
                            color: #ffffff;
                            font-size: 28px;
                            font-weight: 700;
                            margin: 16px 0 8px;
                            letter-spacing: -0.5px;
                        }}
        
                        .header-subtitle {{
                            color: #c7d2fe;
                            font-size: 16px;
                            font-weight: 400;
                            opacity: 0.9;
                        }}
        
                        .email-body {{
                            padding: 56px 48px 48px;
                            background: #ffffff;
                            position: relative;
                        }}
        
                        .welcome-section {{
                            text-align: center;
                            margin-bottom: 40px;
                        }}
        
                        .welcome-icon {{
                            width: 80px;
                            height: 80px;
                            margin: 0 auto 24px;
                            background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
                            border-radius: 50%;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            box-shadow: 0 8px 25px rgba(99, 102, 241, 0.25);
                        }}
        
                        .wave-hand {{
                            font-size: 32px;
                        }}
        
                        .greeting {{
                            font-size: 24px;
                            font-weight: 700;
                            margin-bottom: 16px;
                            color: #1f2937;
                            background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
                            -webkit-background-clip: text;
                            -webkit-text-fill-color: transparent;
                            background-clip: text;
                        }}
        
                        .welcome-message {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 32px;
                            line-height: 1.7;
                        }}
        
                        .code-section {{
                            text-align: center;
                            margin: 48px 0;
                            position: relative;
                        }}
        
                        .code-label {{
                            font-size: 14px;
                            font-weight: 600;
                            color: #6b7280;
                            text-transform: uppercase;
                            letter-spacing: 1px;
                            margin-bottom: 16px;
                        }}
        
                        .code-wrapper {{
                            background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
                            border: 2px solid #e2e8f0;
                            border-radius: 16px;
                            padding: 32px;
                            margin: 24px auto;
                            max-width: 320px;
                            position: relative;
                            box-shadow: 0 8px 25px rgba(0, 0, 0, 0.08);
                        }}
        
                        .code-box {{
                            font-family: 'SF Mono', 'Monaco', 'Menlo', monospace;
                            font-size: 36px;
                            font-weight: 800;
                            color: #1e293b;
                            letter-spacing: 8px;
                            margin: 0;
                            text-shadow: 0 1px 2px rgba(0, 0, 0, 0.1);
                        }}
        
                        .expiry-note {{
                            font-size: 14px;
                            color: #94a3b8;
                            margin-top: 20px;
                            font-weight: 500;
                        }}
        
                        .features-section {{
                            background: linear-gradient(135deg, #faf5ff 0%, #f3e8ff 100%);
                            border: 1px solid #c4b5fd;
                            border-radius: 16px;
                            padding: 32px;
                            margin: 40px 0;
                        }}
        
                        .features-title {{
                            font-size: 18px;
                            font-weight: 600;
                            color: #6d28d9;
                            margin-bottom: 16px;
                            text-align: center;
                        }}
        
                        .features-list {{
                            display: grid;
                            gap: 12px;
                            color: #5b21b6;
                            font-size: 14px;
                        }}
        
                        .feature-item {{
                            display: flex;
                            align-items: center;
                            gap: 8px;
                        }}
        
                        .social-section {{
                            text-align: center;
                            margin-top: 48px;
                            padding-top: 32px;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .social-text {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 16px;
                        }}
        
                        .signature {{
                            margin-top: 40px;
                            text-align: center;
                        }}
        
                        .signature-text {{
                            font-size: 16px;
                            color: #6b7280;
                            margin-bottom: 8px;
                        }}
        
                        .team-name {{
                            font-size: 18px;
                            font-weight: 700;
                            background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
                            -webkit-background-clip: text;
                            -webkit-text-fill-color: transparent;
                            background-clip: text;
                        }}
        
                        .email-footer {{
                            background: #f8fafc;
                            padding: 32px 48px;
                            text-align: center;
                            border-top: 1px solid #e5e7eb;
                        }}
        
                        .footer-content {{
                            font-size: 13px;
                            color: #9ca3af;
                            line-height: 1.6;
                        }}
        
                        .footer-content p {{
                            margin: 4px 0;
                        }}
        
                        @media screen and (max-width: 640px) {{
                            body {{
                                padding: 10px 0;
                            }}
        
                            .email-container {{
                                margin: 0 16px;
                                border-radius: 16px;
                            }}
        
                            .email-header {{
                                padding: 32px 24px;
                            }}
        
                            .email-body {{
                                padding: 40px 24px 32px;
                            }}
        
                            .email-footer {{
                                padding: 24px;
                            }}
        
                            .header-title {{
                                font-size: 24px;
                            }}
        
                            .greeting {{
                                font-size: 20px;
                            }}
        
                            .code-box {{
                                font-size: 28px;
                                letter-spacing: 4px;
                            }}
        
                            .code-wrapper {{
                                padding: 24px;
                                margin: 16px auto;
                            }}
        
                            .welcome-icon {{
                                width: 64px;
                                height: 64px;
                            }}
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-container'>
                        <div class='email-header'>
                            <div class='logo-container'>
                                <img src='https://camplymedia.blob.core.windows.net/system-images/camply-logo-for-ligth.svg' alt='Camply Logo' class='logo'>
                            </div>
                            <div class='header-content'>
                                <h1 class='header-title'>Hoş Geldiniz!</h1>
                                <p class='header-subtitle'>Hesabınızı doğrulayın ve keşfe başlayın</p>
                            </div>
                        </div>
        
                        <div class='email-body'>
                            <div class='welcome-section'>
                                <h2 class='greeting'>Camply'e Hoş Geldin {username}!</h2>
                                <p class='welcome-message'>
                                    Yeni sosyal medya deneyiminiz başlıyor! Hesabınızı aktif etmek ve 
                                    Camply topluluğuna katılmak için aşağıdaki doğrulama kodunu kullanın.
                                </p>
                            </div>
            
                            <div class='code-section'>
                                <div class='code-label'>E-posta Doğrulama Kodu</div>
                                <div class='code-wrapper'>
                                    <div class='code-box'>{verificationCode}</div>
                                </div>
                                <p class='expiry-note'>⏰ Bu kod {_codeSettings.CodeExpirationMinutes} dakika içinde geçerliliğini yitirecektir</p>
                            </div>
            
                            <div class='features-section'>
                                <h3 class='features-title'>🎯 Camply ile Neler Yapabilirsin?</h3>
                                <div class='features-list'>
                                    <div class='feature-item'>
                                        <span>✨</span>
                                        <span>Arkadaşlarınla anlık paylaşımlar yap</span>
                                    </div>
                                    <div class='feature-item'>
                                        <span>📸</span>
                                        <span>Özel anlarını topluluğunla paylaş</span>
                                    </div>
                                    <div class='feature-item'>
                                        <span>🌟</span>
                                        <span>İlgi alanlarına göre yeni insanlar keşfet</span>
                                    </div>
                                    <div class='feature-item'>
                                        <span>🔒</span>
                                        <span>Güvenli ve gizli mesajlaşma deneyimi</span>
                                    </div>
                                </div>
                            </div>
            
                            <div class='social-section'>
                                <p class='social-text'>Heyecan verici bir sosyal medya yolculuğuna hazır mısın? 🚀</p>
                            </div>
            
                            <div class='signature'>
                                <p class='signature-text'>Seni aramızda görmekten mutluluk duyuyoruz!</p>
                                <p class='team-name'>Camply Ekibi</p>
                            </div>
                        </div>
        
                        <div class='email-footer'>
                            <div class='footer-content'>
                                <p>Bu otomatik bir e-postadır, lütfen yanıtlamayınız.</p>
                                <p>© 2025 Camply. Tüm hakları saklıdır.</p>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";
        }
    }
}