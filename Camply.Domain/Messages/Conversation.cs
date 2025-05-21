using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Camply.Domain.Messages
{
    public class Conversation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        // Konuşmaya katılan kullanıcıların ID'leri
        public List<string> ParticipantIds { get; set; } = new List<string>();

        // Son mesajın ID'si
        [BsonRepresentation(BsonType.ObjectId)]
        public string LastMessageId { get; set; }

        // Son mesajın içeriği (önizleme için)
        public string LastMessagePreview { get; set; }

        // Son mesajı gönderen kişi
        public string LastMessageSenderId { get; set; }

        // Son aktivite tarihi
        public DateTime LastActivityDate { get; set; } = DateTime.UtcNow;

        // Grup konuşması için başlık (birebir mesajlaşmada null)
        public string Title { get; set; }

        // Konuşmanın resmi (grup konuşması veya DM için)
        public string ImageUrl { get; set; }

        // Grup konuşması mı?
        public bool IsGroup { get; set; }

        // Oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Gizli/Kaybolan mesaj modu (Instagram'daki vanish mode)
        public bool IsVanish { get; set; } = false;

        // Geçici mesajlar modu
        public bool IsTemporary { get; set; } = false;

        // Konuşma durumu (active, archived, deleted)
        public string Status { get; set; } = "active";

        // Kullanıcıların sessize alma bilgisi (kullanıcı ID -> sessiz mi?)
        public Dictionary<string, bool> MutedBy { get; set; } = new Dictionary<string, bool>();

        // Kullanıcı bazında okunmamış mesaj sayacı
        public Dictionary<string, int> UnreadCount { get; set; } = new Dictionary<string, int>();
    }
}