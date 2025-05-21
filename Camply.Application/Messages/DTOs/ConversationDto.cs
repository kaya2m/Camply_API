using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Messages.DTOs
{
    public class ConversationDto
    {
        public string Id { get; set; }
        public List<UserMinimalDto> Participants { get; set; }
        public string LastMessagePreview { get; set; }
        public UserMinimalDto LastMessageSender { get; set; }
        public DateTime LastActivityDate { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public bool IsGroup { get; set; }
        public bool IsVanish { get; set; }
        public int UnreadCount { get; set; }
        public bool IsMuted { get; set; }
        public string Status { get; set; }
    }
}
