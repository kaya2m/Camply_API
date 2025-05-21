using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Camply.Domain.Messages
{
    public class Message
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ConversationId { get; set; }

        public string SenderId { get; set; }

        public string Content { get; set; }

        // Mesaj tipi (text, image, video, audio, file, heart, story_reply, vb.)
        public string MessageType { get; set; } = "text";

        [BsonRepresentation(BsonType.ObjectId)]
        public string ReplyToMessageId { get; set; }

        public bool IsSaved { get; set; } = false;

        public List<string> LikedBy { get; set; } = new List<string>();

        public string StoryId { get; set; }

        public List<MediaAttachment> Media { get; set; } = new List<MediaAttachment>();

        public Dictionary<string, DateTime> ReadBy { get; set; } = new Dictionary<string, DateTime>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public bool IsEdited { get; set; } = false;

        public DateTime? EditedAt { get; set; }

        public bool ShowSeenStatus { get; set; } = true;

        public int? ExpiresIn { get; set; }
    }

    public class MediaAttachment
    {
        public string MediaType { get; set; }

        public string Url { get; set; }

        public string ThumbnailUrl { get; set; }

        public string FileName { get; set; }

        public long FileSize { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Duration { get; set; }
    }
}