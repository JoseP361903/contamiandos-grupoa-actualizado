namespace contaminados_grupoa_backend.Services
{
    using contaminados_grupoa_backend.Models;
    using MongoDB.Driver;
    using Microsoft.Extensions.Options;

    public class GameService
    {
        private readonly IMongoCollection<Game_Entity> _gamesCollection;
        private readonly IMongoCollection<Round> _roundsCollection;

        public GameService(IOptions<MongoDBSettings> mongoDBSettings, IMongoDatabase database)
        {
            var client = new MongoClient(mongoDBSettings.Value.ConnectionString);
            var database = client.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _gamesCollection = database.GetCollection<Game_Entity>("Games");
            _roundsCollection = database.GetCollection<Round>("Rounds");
        }

        public async Task<List<Game_Entity>> SearchGamesAsync(string name = null, string status = null, int page = 0, int limit = 50)
        {
            var allGames = await _gamesCollection.Find(_ => true).ToListAsync();

            var filteredGames = allGames.AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                filteredGames = filteredGames.Where(g =>
                    g.name != null && g.name.ToLower().Contains(name.ToLower()));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                filteredGames = filteredGames.Where(g =>
                    g.status != null && g.status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            var result = filteredGames
                .Skip(page)
                .Take(limit)
                .ToList();

            return result;
        }

        public async Task<Game_Entity> GetGameByIdAsync(string gameId)
        {
            return await _gamesCollection
                .Find(g => g.id == gameId)
                .FirstOrDefaultAsync();
        }

        public async Task<Game_Entity> CreateGameAsync(string name, string owner, string passwordValue)
        {
            var existingGame = await _gamesCollection
                .Find(g => g.name.ToLower() == name.ToLower() && g.status != "ended")
                .FirstOrDefaultAsync();

            if (existingGame != null)
            {
                throw new InvalidOperationException("Asset already exists");
            }

            var game = new Game_Entity
            {
                name = name,
                owner = owner,
                players = new List<string> { owner },
                enemies = new List<string>(),
                status = "lobby",
                currentRound = "0000000000000000000000000",
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };

            // Manejar contraseña como en SpringBoot
            if (!string.IsNullOrEmpty(passwordValue))
            {
                game.passwordValue = passwordValue;
                game.password = true;
            }
            else
            {
                game.passwordValue = null;
                game.password = false;
            }

            await _gamesCollection.InsertOneAsync(game);
            return game;
        }

        public async Task<Game_Entity> JoinGameAsync(string gameId, string playerName, string passwordValue)
        {
            var game = await GetGameByIdAsync(gameId);
            if (game == null) throw new KeyNotFoundException("Game not found");

            // Validar límite de jugadores
            if (game.players.Count >= 10)
                throw new InvalidOperationException("Game is full");

            // Validar jugador duplicado
            if (game.players.Contains(playerName))
                throw new InvalidOperationException("Player is already part of the game");

            // Validar contraseña
            if (game.password)
            {
                if (string.IsNullOrEmpty(passwordValue))
                    throw new UnauthorizedAccessException("Password required to join this game");

                if (game.passwordValue != passwordValue)
                    throw new UnauthorizedAccessException("Incorrect password");
            }

            // Agregar jugador
            game.players.Add(playerName);
            game.updatedAt = DateTime.UtcNow;

            var filter = Builders<Game_Entity>.Filter.Eq(g => g.id, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.players, game.players)
                .Set(g => g.updatedAt, game.updatedAt);

            await _gamesCollection.UpdateOneAsync(filter, update);
            return game;
        }

        public async Task StartGameAsync(string gameId, string playerName)
        {
            var game = await GetGameByIdAsync(gameId);
            if (game == null) throw new KeyNotFoundException("Game not found");

            // Validar que sea el owner
            if (game.owner != playerName)
                throw new UnauthorizedAccessException("Only the game owner can start the game");

            // Validar mínimo de jugadores
            if (game.players.Count < 5)
                throw new InvalidOperationException("Need 5 players to start");

            // Asignar enemigos
            if (game.enemies == null || !game.enemies.Any())
            {
                game.enemies = AssignEnemies(game.players);
            }

            // Crear primera ronda
            var random = new Random();
            var firstLeader = game.players[random.Next(game.players.Count)];

            var round = new Round
            {
                gameId = gameId,
                leader = firstLeader,
                status = "waiting-on-leader",
                phase = "vote1",
                result = "none",
                group = new List<string>(),
                votes = new List<bool>(),
                votedPlayers = new List<string>(),
                actions = new List<bool>(),
                actionPlayers = new List<string>(),
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow
            };

            await _roundsCollection.InsertOneAsync(round);

            // Actualizar juego
            var filter = Builders<Game_Entity>.Filter.Eq(g => g.id, gameId);
            var update = Builders<Game_Entity>.Update
                .Set(g => g.status, "rounds")
                .Set(g => g.enemies, game.enemies)
                .Set(g => g.currentRound, round.id)
                .Set(g => g.startedAt, DateTime.UtcNow)
                .Set(g => g.updatedAt, DateTime.UtcNow);

            await _gamesCollection.UpdateOneAsync(filter, update);
        }

        private List<string> AssignEnemies(List<string> players)
        {
            var random = new Random();
            var shuffledPlayers = players.OrderBy(x => random.Next()).ToList();
            var enemies = new List<string>();

            int enemyCount = players.Count switch
            {
                5 => 2,
                6 => 2,
                7 => 2,
                8 => 3,
                9 => 3,
                10 => 3,
                _ => players.Count / 3
            };

            for (int i = 0; i < enemyCount && i < shuffledPlayers.Count; i++)
            {
                enemies.Add(shuffledPlayers[i]);
            }

            return enemies;
        }
    }
}