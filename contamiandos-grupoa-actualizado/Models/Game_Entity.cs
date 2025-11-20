using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System;

namespace contaminados_grupoa_backend.Models
{
    public class Game_Entity
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("gameId")]
        public string GameId { get; set; } = Guid.NewGuid().ToString().ToUpper();

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("status")]
        public string Status { get; set; } = "lobby";

        [BsonElement("password")]
        public string Password { get; set; }

        [BsonElement("owner")]
        public string Owner { get; set; }

        [BsonElement("players")]
        public List<string> Players { get; set; } = new List<string>();

        [BsonElement("enemies")]
        public List<string> Enemies { get; set; } = new List<string>();

        [BsonElement("currentRoundId")]
        public string CurrentRoundId { get; set; } = "0000000000000000000000000";

        [BsonElement("currentRoundLeader")]
        public string CurrentRoundLeader { get; set; }

        [BsonElement("currentRoundStatus")]
        public string CurrentRoundStatus { get; set; } = "waiting-on-leader";

        [BsonElement("currentRoundResult")]
        public string CurrentRoundResult { get; set; } = "none";

        [BsonElement("currentRoundPhase")]
        public string CurrentRoundPhase { get; set; } = "vote1";

        [BsonElement("currentRoundGroup")]
        public List<string> CurrentRoundGroup { get; set; } = new List<string>();

        [BsonElement("failedVoteCount")]
        public int? FailedVoteCount { get; set; } = 0;

        [BsonElement("currentRoundVotes")]
        public List<bool> CurrentRoundVotes { get; set; } = new List<bool>();

        // CAMBIO: Usar List<RoundAction> en lugar de Dictionary<string, object>
        [BsonElement("currentRoundActions")]
        public List<RoundAction> CurrentRoundActions { get; set; } = new List<RoundAction>();

        // CAMBIO: Usar List<RoundHistory> en lugar de Dictionary<string, object>
        [BsonElement("allRounds")]
        public List<RoundHistory> AllRounds { get; set; } = new List<RoundHistory>();

        // CAMBIO: Usar List<GroupProposal> en lugar de Dictionary<string, object>
        [BsonElement("groupProposals")]
        public List<GroupProposal> GroupProposals { get; set; } = new List<GroupProposal>();

        // CAMBIO: Usar List<PlayerVote> en lugar de Dictionary<string, object>
        [BsonElement("playerVotes")]
        public List<PlayerVote> PlayerVotes { get; set; } = new List<PlayerVote>();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("startedAt")]
        public DateTime? StartedAt { get; set; }

        [BsonElement("endedAt")]
        public DateTime? EndedAt { get; set; }

        [BsonIgnore]
        public bool HasPassword => !string.IsNullOrEmpty(Password);

        [BsonIgnore]
        public int PlayerCount => Players?.Count ?? 0;

        [BsonIgnore]
        public int EnemyCount => Enemies?.Count ?? 0;
    }

    // CLASES AUXILIARES - Agrega estas al final del mismo archivo

    public class GroupProposalRequest
    {
        public List<string> group { get; set; }
    }

    public class VoteRequest
    {
        public bool vote { get; set; }
    }

    public class ActionRequest
    {
        public bool action { get; set; }
    }

    public class GroupProposal
    {
        [BsonElement("roundId")]
        public string RoundId { get; set; }

        [BsonElement("player")]
        public string Player { get; set; }

        [BsonElement("proposedGroup")]
        public List<string> ProposedGroup { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class PlayerVote
    {
        [BsonElement("roundId")]
        public string RoundId { get; set; }

        [BsonElement("player")]
        public string Player { get; set; }

        [BsonElement("vote")]
        public bool? Vote { get; set; } // null = no ha votado, true = acuerdo, false = desacuerdo

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class RoundAction
    {
        [BsonElement("player")]
        public string Player { get; set; }

        [BsonElement("action")]
        public bool Action { get; set; } // true = collaborate, false = sabotage

        [BsonElement("roundId")]
        public string RoundId { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class RoundHistory
    {
        [BsonElement("roundId")]
        public string RoundId { get; set; }

        [BsonElement("leader")]
        public string Leader { get; set; }

        [BsonElement("status")]
        public string Status { get; set; }

        [BsonElement("result")]
        public string Result { get; set; }

        [BsonElement("phase")]
        public string Phase { get; set; }

        [BsonElement("group")]
        public List<string> Group { get; set; }

        [BsonElement("votes")]
        public List<bool> Votes { get; set; }
    }
}