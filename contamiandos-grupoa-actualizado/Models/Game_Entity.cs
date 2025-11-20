namespace contaminados_grupoa_backend.Models
{
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    public class Game_Entity
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string id { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("name")]
        public string name { get; set; }

        [BsonElement("owner")]
        public string owner { get; set; }

        [BsonElement("passwordValue")]
        public string passwordValue { get; set; }

        [BsonElement("password")]
        public bool password { get; set; }

        [BsonElement("players")]
        public List<string> players { get; set; } = new List<string>();

        [BsonElement("enemies")]
        public List<string> enemies { get; set; } = new List<string>();

        [BsonElement("status")]
        public string status { get; set; } = "lobby";

        [BsonElement("currentRound")]
        public string currentRound { get; set; } = "0000000000000000000000000";

        [BsonElement("createdAt")]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("startedAt")]
        public DateTime? startedAt { get; set; }
    }

    public class Round
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string id { get; set; } = Guid.NewGuid().ToString();

        [BsonElement("gameId")]
        public string gameId { get; set; }

        [BsonElement("leader")]
        public string leader { get; set; }

        [BsonElement("status")]
        public string status { get; set; } = "waiting-on-leader";

        [BsonElement("phase")]
        public string phase { get; set; } = "vote1";

        [BsonElement("result")]
        public string result { get; set; } = "none";

        [BsonElement("group")]
        public List<string> group { get; set; } = new List<string>();

        [BsonElement("votes")]
        public List<bool> votes { get; set; } = new List<bool>();

        [BsonElement("votedPlayers")]
        public List<string> votedPlayers { get; set; } = new List<string>();

        [BsonElement("actions")]
        public List<bool> actions { get; set; } = new List<bool>();

        [BsonElement("actionPlayers")]
        public List<string> actionPlayers { get; set; } = new List<string>();

        [BsonElement("createdAt")]
        public DateTime createdAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime updatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BaseResponse<T>
    {
        public int status { get; set; }
        public string msg { get; set; }
        public T data { get; set; }
        public Dictionary<string, object> meta { get; set; } = new Dictionary<string, object>();

        public BaseResponse(int status, string msg, T data)
        {
            this.status = status;
            this.msg = msg;
            this.data = data;
        }
    }
}