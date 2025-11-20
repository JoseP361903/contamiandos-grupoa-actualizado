using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace contaminados_grupoa_backend.Models
{
    public class Round
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("roundId")]
        public string RoundId { get; set; } = Guid.NewGuid().ToString().ToUpper();

        [BsonElement("leader")]
        public string Leader { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "waiting-on-leader";

        [BsonElement("result")]
        public string Result { get; set; } = "none";

        [BsonElement("phase")]
        public string Phase { get; set; } = "vote1";

        [BsonElement("group")]
        public List<string> Group { get; set; } = new List<string>();

        [BsonElement("votes")]
        public List<bool> Votes { get; set; } = new List<bool>();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}