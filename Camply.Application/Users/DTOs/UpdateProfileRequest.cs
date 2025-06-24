using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Users.DTOs
{
    public class UpdateProfileRequest
    {
        [StringLength(50, MinimumLength = 3)]
        public string Name { get; set; }
        [StringLength(50, MinimumLength = 3)]
        public string Surname { get; set; }

        public DateTime? BirthDate { get; set; }

        [StringLength(50, MinimumLength = 3)]
        [Required]
        public string Username { get; set; }

        [StringLength(500)]
        public string Bio { get; set; }
    }
}
