namespace contaminados_grupoa_backend.Services
{
    using contaminados_grupoa_backend.Models;
    using MongoDB.Driver;
    using Microsoft.Extensions.Options;
    public class GameService
    {
        private readonly IMongoCollection<Game_Entity> _gamesCollection;

        public GameService(IOptions<MongoDBSettings> mongoDBSettings)
        {
            var client = new MongoClient(mongoDBSettings.Value.ConnectionString);
            var database = client.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _gamesCollection = database.GetCollection<Game_Entity>("Games");
        }

        public async Task<Game_Entity> CreateGameAsync(string name, string owner, string password)
        {
            var existingGame = await _gamesCollection
                .Find(g => g.Name.ToLower() == name.ToLower() && g.Status != "ended")
                .FirstOrDefaultAsync();

            if (existingGame != null)
            {
                throw new InvalidOperationException("Asset already exists");
            }

            // Crear nuevo juego
            var game = new Game_Entity
            {
                Name = name,
                Owner = owner,
                Password = password,
                Players = new List<string> { owner },
                Enemies = new List<string>(),
                Status = "lobby",  
                CurrentRoundId = "0000000000000000000000000",
                CurrentRoundLeader = owner,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _gamesCollection.InsertOneAsync(game);
            return game;
        }
        public async Task<Game_Entity> GetGameByIdAsync(string gameId)
        {
            return await _gamesCollection
                .Find(g => g.GameId == gameId)  
                .FirstOrDefaultAsync();
        }

        public async Task<bool> GameExistsAsync(string name)
        {
            var existingGame = await _gamesCollection
                .Find(g => g.Name.ToLower() == name.ToLower() && g.Status != "ended")
                .FirstOrDefaultAsync();
            return existingGame != null;
        }

        public async Task<(List<Game_Entity> games, long totalCount)> SearchGamesAsync(
         string name = null,
         string status = null,  
         int page = 0,
         int limit = 50)
        {
            var filterBuilder = Builders<Game_Entity>.Filter;
            var filters = new List<FilterDefinition<Game_Entity>>();

            // Filtro por nombre
            if (!string.IsNullOrWhiteSpace(name))
            {
                var nameFilter = filterBuilder.Regex(g => g.Name, new MongoDB.Bson.BsonRegularExpression(name, "i"));
                filters.Add(nameFilter);
            }

            // Filtro por estado
            if (!string.IsNullOrWhiteSpace(status))
            {
                var statusFilter = filterBuilder.Eq(g => g.Status, status.ToLower());
                filters.Add(statusFilter);
            }

            var finalFilter = filters.Any() ? filterBuilder.And(filters) : filterBuilder.Empty;

            var skip = page * limit;
            limit = Math.Min(limit, 100);

            var games = await _gamesCollection
                .Find(finalFilter)
                .SortByDescending(g => g.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();

            var totalCount = await _gamesCollection.CountDocumentsAsync(finalFilter);

            return (games, totalCount);
        }

        public async Task<Game_Entity> GetGameWithAuthAsync(string gameId, string player, string password)
        {
            var game = await _gamesCollection
                .Find(g => g.GameId == gameId)  
                .FirstOrDefaultAsync();

            if (game == null)
            {
                throw new KeyNotFoundException("Game not found");
            }

            if (!game.Players.Contains(player))
            {
                throw new UnauthorizedAccessException("Not part of the game");
            }

            if (!string.IsNullOrEmpty(game.Password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new UnauthorizedAccessException("Password required");
                }

                if (game.Password != password)
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }
            }

            return game;
        }

        public async Task<Game_Entity> JoinGameAsync(string gameId, string player, string password, string newPlayer)
        {
            var game = await _gamesCollection
                .Find(g => g.GameId == gameId)
                .FirstOrDefaultAsync();

            if (game == null)
            {
                throw new KeyNotFoundException("Game not found");
            }

            if (game.Status != "lobby")
            {
                throw new InvalidOperationException("This action is not allowed at this time");
            }

            if (!string.IsNullOrEmpty(game.Password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new UnauthorizedAccessException("Password required");
                }

                if (game.Password != password)
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }
            }

            if (game.Players.Contains(newPlayer))
            {
                throw new InvalidOperationException("Asset already exists");
            }

            if (game.Players.Count >= 10)
            {
                throw new InvalidOperationException("Game is full");
            }

            var updatedPlayers = new List<string>(game.Players) { newPlayer };

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.Players, updatedPlayers)
                .Set(g => g.UpdatedAt, DateTime.UtcNow);

            var result = await _gamesCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
            {
                throw new Exception("Failed to join game");
            }

            return await _gamesCollection
                .Find(g => g.GameId == gameId)
                .FirstOrDefaultAsync();
        }

        public async Task StartGameAsync(string gameId, string player, string password)
        {
            var game = await _gamesCollection
                .Find(g => g.GameId == gameId)
                .FirstOrDefaultAsync();

            if (game == null)
            {
                throw new KeyNotFoundException("Game not found");
            }

            if (game.Owner != player)
            {
                throw new UnauthorizedAccessException("Only the game owner can start the game");
            }

            if (!string.IsNullOrEmpty(game.Password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new UnauthorizedAccessException("Password required");
                }

                if (game.Password != password)
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }
            }

            if (game.Status != "lobby")
            {
                throw new InvalidOperationException("Game already started");
            }

            if (game.Players.Count < 5)
            {
                throw new InvalidOperationException("Need 5 players to start");
            }

            var rol = rols(game.Players.Count);
            var roundPlayer = game.Players.OrderBy(x => Guid.NewGuid()).ToList();
            var enemies = new List<string>();

            int enemyCount = rol["enemy"];
            for (int i = 0; i < enemyCount; i++)
            {
                enemies.Add(roundPlayer[i]);
            }

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.Status, "rounds")
                .Set(g => g.StartedAt, DateTime.UtcNow)
                .Set(g => g.UpdatedAt, DateTime.UtcNow)
                .Set(g => g.Enemies, enemies); // Asignar los enemigos (psicópatas)

            var result = await _gamesCollection.UpdateOneAsync(filter, update);

            if (result.ModifiedCount == 0)
            {
                throw new Exception("Failed to start game");
            }
        }

        // agregue esto jp
        private Dictionary<string, int> rols(int numberPlayers)
        {
            var rol = new Dictionary<string, int>();

            switch (numberPlayers)
            {
                case 5:
                    rol.Add("exemplar", 3);
                    rol.Add("enemy", 2);
                    break;
                case 6:
                    rol.Add("exemplar", 4);
                    rol.Add("enemy", 2);
                    break;
                case 7:
                    rol.Add("exemplar", 4);
                    rol.Add("enemy", 3);
                    break;
                case 8:
                    rol.Add("exemplar", 5);
                    rol.Add("enemy", 3);
                    break;
                case 9:
                    rol.Add("exemplar", 6);
                    rol.Add("enemy", 3);
                    break;
                case 10:
                    rol.Add("exemplar", 6);
                    rol.Add("enemy", 4);
                    break;
                default:
                    throw new ArgumentException($"Need more players");
            }

            return rol;
        }
        public async Task<List<Round>> GetRoundsAsync(string gameId, string player, string password)
        {
            var game = await _gamesCollection
                .Find(g => g.GameId == gameId)
                .FirstOrDefaultAsync();

            if (game == null)
            {
                throw new KeyNotFoundException("Game not found");
            }

            if (!game.Players.Contains(player))
            {
                throw new UnauthorizedAccessException("Not part of the game");
            }

            if (!string.IsNullOrEmpty(game.Password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new UnauthorizedAccessException("Password required");
                }

                if (game.Password != password)
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }
            }

            bool needsUpdate = false;
            var currentRoundId = game.CurrentRoundId;

            if (currentRoundId == "0000000000000000000000000" && game.Status == "rounds")
            {
                currentRoundId = Guid.NewGuid().ToString().ToUpper();
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
                var update = Builders<Game_Entity>.Update
                    .Set(g => g.CurrentRoundId, currentRoundId)
                    .Set(g => g.UpdatedAt, DateTime.UtcNow);

                await _gamesCollection.UpdateOneAsync(filter, update);
            }

            var rounds = new List<Round>();

            if (game.Status == "rounds" && currentRoundId != "0000000000000000000000000")
            {
                rounds.Add(new Round
                {
                    RoundId = currentRoundId,
                    Leader = game.CurrentRoundLeader,
                    Status = game.CurrentRoundStatus,
                    Result = game.CurrentRoundResult,
                    Phase = game.CurrentRoundPhase,
                    Group = game.CurrentRoundGroup ?? new List<string>(),
                    Votes = game.CurrentRoundVotes ?? new List<bool>()
                });
            }

            if (game.AllRounds != null && game.AllRounds.Any())
            {
                foreach (var roundHistory in game.AllRounds)
                {
                    rounds.Add(new Round
                    {
                        RoundId = roundHistory.RoundId,
                        Leader = roundHistory.Leader,
                        Status = roundHistory.Status ?? "ended",
                        Result = roundHistory.Result ?? "none",
                        Phase = roundHistory.Phase ?? "vote1",
                        Group = roundHistory.Group ?? new List<string>(),
                        Votes = roundHistory.Votes ?? new List<bool>()
                    });
                }
            }

            return rounds;
        }

        public async Task<Round> GetRoundAsync(string gameId, string roundId, string player, string password)
        {
            var game = await _gamesCollection
                .Find(g => g.GameId == gameId)
                .FirstOrDefaultAsync();

            if (game == null)
            {
                throw new KeyNotFoundException("Game not found");
            }

            if (!game.Players.Contains(player))
            {
                throw new UnauthorizedAccessException("Not part of the game");
            }

            if (!string.IsNullOrEmpty(game.Password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    throw new UnauthorizedAccessException("Password required");
                }

                if (game.Password != password)
                {
                    throw new UnauthorizedAccessException("Invalid credentials");
                }
            }

            if (game.CurrentRoundId == roundId && game.Status == "rounds")
            {
                return new Round
                {
                    RoundId = game.CurrentRoundId,
                    Leader = game.CurrentRoundLeader,
                    Status = game.CurrentRoundStatus,
                    Result = game.CurrentRoundResult,
                    Phase = game.CurrentRoundPhase,
                    Group = game.CurrentRoundGroup ?? new List<string>(),
                    Votes = game.CurrentRoundVotes ?? new List<bool>()
                };
            }

            if (game.AllRounds != null && game.AllRounds.Any())
            {
                var roundFromHistory = game.AllRounds
                    .FirstOrDefault(r => r.RoundId == roundId);

                if (roundFromHistory != null)
                {
                    return new Round
                    {
                        RoundId = roundFromHistory.RoundId,
                        Leader = roundFromHistory.Leader,
                        Status = roundFromHistory.Status ?? "ended",
                        Result = roundFromHistory.Result ?? "none",
                        Phase = roundFromHistory.Phase ?? "vote1",
                        Group = roundFromHistory.Group ?? new List<string>(),
                        Votes = roundFromHistory.Votes ?? new List<bool>()
                    };
                }
            }

            throw new KeyNotFoundException("Round not found");
        }

        public async Task<Round> ProposeGroupAsync(string gameId, string roundId, string player, string password, List<string> group)
        {
            var game = await GetGameWithAuthAsync(gameId, player, password);

            if (game.CurrentRoundId != roundId)
            {
                throw new KeyNotFoundException("Round not found");
            }

            if (game.CurrentRoundLeader != player)
            {
                throw new UnauthorizedAccessException("Only the round leader can propose groups");
            }

            if (game.CurrentRoundStatus != "waiting-on-leader")
            {
                throw new InvalidOperationException("This action is not allowed at this time");
            }

            if (group == null || group.Count < 2 || group.Count > 6)
            {
                throw new InvalidOperationException("Group must have between 2 and 6 players");
            }

            var invalidPlayers = group.Except(game.Players).ToList();
            if (invalidPlayers.Any())
            {
                throw new InvalidOperationException($"Players not in game: {string.Join(", ", invalidPlayers)}");
            }

            var failedVoteCount = game.FailedVoteCount ?? 0;
            var currentPhase = GetVotingPhase(failedVoteCount);

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.CurrentRoundGroup, group)
                .Set(g => g.CurrentRoundStatus, "voting")
                .Set(g => g.CurrentRoundPhase, currentPhase) 
                .Set(g => g.CurrentRoundVotes, new List<bool>()) 
                .Set(g => g.UpdatedAt, DateTime.UtcNow);

            await _gamesCollection.UpdateOneAsync(filter, update);

            return await GetRoundAsync(gameId, roundId, player, password);
        }

        public async Task<Round> SubmitVoteAsync(string gameId, string roundId, string player, string password, bool vote)
        {
            var game = await GetGameWithAuthAsync(gameId, player, password);

            if (game.CurrentRoundId != roundId)
            {
                throw new InvalidOperationException("Can only vote on current round");
            }

            if (game.CurrentRoundStatus != "voting")
            {
                throw new InvalidOperationException("This action is not allowed at this time");
            }

            var existingVote = game.PlayerVotes?.FirstOrDefault(v => v.RoundId == roundId && v.Player == player);
            if (existingVote?.Vote != null)
            {
                throw new InvalidOperationException("Player has already voted");
            }

            if (!game.Players.Contains(player))
            {
                throw new UnauthorizedAccessException("Player is not part of this game");
            }

            var playerVotes = game.PlayerVotes ?? new List<PlayerVote>();
            playerVotes.Add(new PlayerVote
            {
                RoundId = roundId,
                Player = player,
                Vote = vote,
                Timestamp = DateTime.UtcNow
            });

            var currentVotes = game.CurrentRoundVotes ?? new List<bool>();
            currentVotes.Add(vote);

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.PlayerVotes, playerVotes)
                .Set(g => g.CurrentRoundVotes, currentVotes)
                .Set(g => g.UpdatedAt, DateTime.UtcNow);

            if (currentVotes.Count >= game.Players.Count)
            {
                var approved = currentVotes.Count(v => v) > (currentVotes.Count / 2);
                var failedVoteCount = game.FailedVoteCount ?? 0;

                if (approved)
                {

                    update = update
                        .Set(g => g.CurrentRoundStatus, "waiting-on-group")
                        .Set(g => g.CurrentRoundPhase, "vote1")
                        .Set(g => g.FailedVoteCount, 0);
                }
                else
                {
                    failedVoteCount++;

                    if (failedVoteCount >= 3)
                    {
                        update = update
                            .Set(g => g.CurrentRoundStatus, "ended")
                            .Set(g => g.CurrentRoundResult, "enemies")
                            .Set(g => g.CurrentRoundPhase, "vote1") 
                            .Set(g => g.FailedVoteCount, 0); 

                        var roundHistory = new RoundHistory
                        {
                            RoundId = game.CurrentRoundId,
                            Leader = game.CurrentRoundLeader,
                            Status = "ended",
                            Result = "enemies",
                            Phase = GetVotingPhase(failedVoteCount),
                            Group = game.CurrentRoundGroup ?? new List<string>(),
                            Votes = game.CurrentRoundVotes ?? new List<bool>()
                        };

                        var allRounds = game.AllRounds ?? new List<RoundHistory>();
                        allRounds.Add(roundHistory);
                        update = update.Set(g => g.AllRounds, allRounds);

                        if (ShouldEndGame(game, "enemies"))
                        {
                            update = update.Set(g => g.Status, "ended");
                        }
                        else
                        {
                            var nextLeader = GetNextLeader(game);
                            update = update
                                .Set(g => g.CurrentRoundId, Guid.NewGuid().ToString().ToUpper())
                                .Set(g => g.CurrentRoundLeader, nextLeader)
                                .Set(g => g.CurrentRoundStatus, "waiting-on-leader")
                                .Set(g => g.CurrentRoundResult, "none")
                                .Set(g => g.CurrentRoundPhase, "vote1")
                                .Set(g => g.CurrentRoundGroup, new List<string>())
                                .Set(g => g.CurrentRoundVotes, new List<bool>())
                                .Set(g => g.CurrentRoundActions, new List<RoundAction>())
                                .Set(g => g.FailedVoteCount, 0);
                        }
                    }
                    else
                    {
                        var nextPhase = GetVotingPhase(failedVoteCount);

                        var updatedPlayerVotes = game.PlayerVotes?.Where(v => v.RoundId != roundId).ToList() ?? new List<PlayerVote>();

                        update = update
                            .Set(g => g.CurrentRoundStatus, "waiting-on-leader")
                            .Set(g => g.CurrentRoundPhase, nextPhase)
                            .Set(g => g.CurrentRoundGroup, new List<string>())
                            .Set(g => g.CurrentRoundVotes, new List<bool>())
                            .Set(g => g.PlayerVotes, updatedPlayerVotes)
                            .Set(g => g.FailedVoteCount, failedVoteCount);
                    }
                }
            }

            await _gamesCollection.UpdateOneAsync(filter, update);
            return await GetRoundAsync(gameId, roundId, player, password);
        }

        private string GetVotingPhase(int failedVoteCount)
        {
            return failedVoteCount switch
            {
                0 => "vote1",
                1 => "vote2",
                2 => "vote3",
                _ => "vote1"
            };
        }

        public async Task<Round> SubmitActionAsync(string gameId, string roundId, string player, string password, bool action)
        {
            var game = await GetGameWithAuthAsync(gameId, player, password);

            if (game.CurrentRoundId != roundId)
            {
                throw new InvalidOperationException("Can only submit actions on current round");
            }

            if (game.CurrentRoundStatus != "waiting-on-group")
            {
                throw new InvalidOperationException("This action is not allowed at this time");
            }

            if (!game.CurrentRoundGroup.Contains(player))
            {
                throw new UnauthorizedAccessException("Not part of the round group");
            }

            var existingAction = game.CurrentRoundActions?.FirstOrDefault(a => a.Player == player);
            if (existingAction != null)
            {
                throw new InvalidOperationException("Player has already submitted an action");
            }

            var actions = game.CurrentRoundActions ?? new List<RoundAction>();
            actions.Add(new RoundAction
            {
                Player = player,
                Action = action,
                RoundId = roundId,
                Timestamp = DateTime.UtcNow
            });

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.GameId, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.CurrentRoundActions, actions)
                .Set(g => g.UpdatedAt, DateTime.UtcNow);

            if (actions.Count >= game.CurrentRoundGroup.Count)
            {
                var collaborateCount = actions.Count(a => a.Action);
                var sabotageCount = actions.Count(a => !a.Action);

                string result = "none";
                if (sabotageCount >= 1)
                {
                    result = "enemies";
                }
                else if (collaborateCount >= 2)
                {
                    result = "citizens";
                }

                update = update
                    .Set(g => g.CurrentRoundStatus, "ended")
                    .Set(g => g.CurrentRoundResult, result);

                var roundHistory = new RoundHistory
                {
                    RoundId = game.CurrentRoundId,
                    Leader = game.CurrentRoundLeader,
                    Status = "ended",
                    Result = result,
                    Phase = game.CurrentRoundPhase,
                    Group = game.CurrentRoundGroup,
                    Votes = game.CurrentRoundVotes
                };

                var allRounds = game.AllRounds ?? new List<RoundHistory>();
                allRounds.Add(roundHistory);

                update = update.Set(g => g.AllRounds, allRounds);

                if (ShouldEndGame(game, result))
                {
                    update = update.Set(g => g.Status, "ended");
                }
                else
                {
                    var nextLeader = GetNextLeader(game);
                    update = update
                        .Set(g => g.CurrentRoundId, Guid.NewGuid().ToString().ToUpper())
                        .Set(g => g.CurrentRoundLeader, nextLeader)
                        .Set(g => g.CurrentRoundStatus, "waiting-on-leader")
                        .Set(g => g.CurrentRoundResult, "none")
                        .Set(g => g.CurrentRoundGroup, new List<string>())
                        .Set(g => g.CurrentRoundVotes, new List<bool>())
                        .Set(g => g.CurrentRoundActions, new List<RoundAction>());
                }
            }

            await _gamesCollection.UpdateOneAsync(filter, update);

            return await GetRoundAsync(gameId, roundId, player, password);
        }

        private bool ShouldEndGame(Game_Entity game, string roundResult)
        {
            var citizenWins = game.AllRounds?.Count(r => r.Result == "citizens") ?? 0;
            var enemyWins = game.AllRounds?.Count(r => r.Result == "enemies") ?? 0;

            return citizenWins >= 3 || enemyWins >= 3;
        }

        private string GetNextLeader(Game_Entity game)
        {
            var random = new Random();
            var availablePlayers = game.Players.Where(p => p != game.CurrentRoundLeader).ToList();

            if (availablePlayers.Count == 0)
                return game.Players[random.Next(game.Players.Count)];

            return availablePlayers[random.Next(availablePlayers.Count)];
        }

       
    }
}