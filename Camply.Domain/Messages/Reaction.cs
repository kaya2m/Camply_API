using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace Camply.Domain.Messages
{
    public class Reaction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string MessageId { get; set; }

        public string UserId { get; set; }

        public string ReactionType { get; set; } // ❤️, 👍, 😂, 😮, 😢, 😠 vb.

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}