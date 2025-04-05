namespace Camply.Application.Auth.Models
{
    public class SocialLoginSettings
    {
        public GoogleSettings Google { get; set; }
        public FacebookSettings Facebook { get; set; }
        public TwitterSettings Twitter { get; set; }
    }
}
