using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain.Common;
using System.Xml.Linq;
using Camply.Domain.Enums;

namespace Camply.Domain.Auth
{
    /// <summary>
    /// Kullanıcı varlığı
    /// </summary>
    public class User : BaseEntity
    {
        /// <summary>
        /// Kullanıcı adı
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// E-posta adresi
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Şifre hash'i
        /// </summary>
        public string PasswordHash { get; set; }

        /// <summary>
        /// Profil resmi URL'i
        /// </summary>
        public string ProfileImageUrl { get; set; }

        /// <summary>
        /// Kullanıcı biyografisi
        /// </summary>
        public string Bio { get; set; }

        /// <summary>
        /// Kullanıcı durumu
        /// </summary>
        public UserStatus Status { get; set; }

        /// <summary>
        /// E-posta doğrulanmış mı?
        /// </summary>
        public bool IsEmailVerified { get; set; }

        /// <summary>
        ///  E-posta doğrulama code
        /// </summary>
        public string EmailVerificationCode { get; set; }

        /// <summary>
        /// E-posta doğrulama code bitiş tarihi
        /// </summary>
        public DateTime? EmailVerificationExpiry { get; set; }
        /// <summary>
        /// Son giriş tarihi
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Kullanıcı ayarları (JSON olarak saklanabilir)
        /// </summary>
        public string Settings { get; set; }

        /// <summary>
        /// Bildirim tercihleri (JSON olarak saklanabilir)
        /// </summary>
        public string NotificationPreferences { get; set; }

        /// <summary>
        /// Şifre sıfırlama token'ı
        /// </summary>
        public string PasswordResetCode { get; set; }

        /// <summary>
        /// Şifre sıfırlama token'ının son kullanma tarihi
        /// </summary>
        public DateTime? PasswordResetCodeExpiry { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool IsPasswordResetCodeVerified { get; set; }  = false;
        /// <summary>
        /// 
        /// </summary>
        public DateTime? CodeVerifiedAt { get; set; }

        /// <summary>
        /// Kullanıcının sosyal gönderileri
        /// </summary>
        public virtual ICollection<Post> Posts { get; set; }

        /// <summary>
        /// Kullanıcının blog yazıları
        /// </summary>
        public virtual ICollection<Blog> Blogs { get; set; }

        /// <summary>
        /// Kullanıcının yorumları
        /// </summary>
        public virtual ICollection<Comment> Comments { get; set; }

        /// <summary>
        /// Kullanıcının beğenileri
        /// </summary>
        public virtual ICollection<Like> Likes { get; set; }

        /// <summary>
        /// Kullanıcıyı takip edenler
        /// </summary>
        public virtual ICollection<Follow> Followers { get; set; }

        /// <summary>
        /// Kullanıcının takip ettikleri
        /// </summary>
        public virtual ICollection<Follow> Following { get; set; }

        /// <summary>
        /// Kullanıcının rolleri
        /// </summary>
        public virtual ICollection<UserRole> UserRoles { get; set; }

        /// <summary>
        /// Kullanıcının sosyal medya hesapları
        /// </summary>
        public virtual ICollection<SocialLogin> SocialLogins { get; set; }

        /// <summary>
        /// Kullanıcının bildirimleri
        /// </summary>
        public virtual ICollection<Notification> Notifications { get; set; }

        public User()
        {
            Posts = new HashSet<Post>();
            Blogs = new HashSet<Blog>();
            Comments = new HashSet<Comment>();
            Likes = new HashSet<Like>();
            Followers = new HashSet<Follow>();
            Following = new HashSet<Follow>();
            UserRoles = new HashSet<UserRole>();
            SocialLogins = new HashSet<SocialLogin>();
            Notifications = new HashSet<Notification>();
        }
    }
}
