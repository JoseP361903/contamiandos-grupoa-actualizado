namespace contaminados_grupoa_backend.Controllers
{
    using contaminados_grupoa_backend.Models;
    using contaminados_grupoa_backend.Services;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Http;
    using System.Text.Json;
    using MongoDB.Driver;

    [ApiController]
    [Route("api/[controller]")]
    public class gamesController : ControllerBase
    {
        private readonly GameService _gameService;
        private readonly IMongoCollection<Round> _roundsCollection;

        public gamesController(GameService gameService, IMongoDatabase database)
        {
            _gameService = gameService;
            _roundsCollection = database.GetCollection<Round>("Rounds");
        }

        // GET /api/games
        [HttpGet]
        public async Task<IActionResult> GetGames(
            [FromQuery] string name = null,
            [FromQuery] string status = null,
            [FromQuery] int page = 0,
            [FromQuery] int limit = 50)
        {
            try
            {
                if (page < 0 || page > 50) page = 0;
                if (limit < 1 || limit > 50) limit = 50;

                var games = await _gameService.SearchGamesAsync(name, status, page, limit);

                var response = new BaseResponse<List<Game_Entity>>(
                    200,
                    games.Any() ? "Games found" : "No games found",
                    games
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // GET /api/games/{gameId}
        [HttpGet("{gameId}")]
        public async Task<IActionResult> GetGameById(
            [FromRoute] string gameId,
            [FromHeader(Name = "player")] string playerName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    return BadRequest(new BaseResponse<object>(400, "Player header is required", null));
                }

                var game = await _gameService.GetGameByIdAsync(gameId);
                if (game == null)
                {
                    return NotFound(new BaseResponse<object>(404, "Game Not Found", null));
                }

                // Crear safe game como en SpringBoot
                var safeGame = new Game_Entity
                {
                    id = game.id,
                    name = game.name,
                    status = game.status,
                    owner = game.owner,
                    players = game.players,
                    currentRound = game.currentRound,
                    createdAt = game.createdAt,
                    updatedAt = game.updatedAt,
                    password = game.password,
                    enemies = game.enemies != null && game.enemies.Contains(playerName)
                            ? game.enemies
                            : new List<string>()
                };

                // IMPORTANTE: passwordValue debe ser null en la respuesta
                safeGame.passwordValue = null;

                return Ok(new BaseResponse<Game_Entity>(200, "Game found", safeGame));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // POST /api/games
        [HttpPost]
        public async Task<IActionResult> CreateGame([FromBody] JsonElement body)
        {
            try
            {
                string name = body.TryGetProperty("name", out var nameProp)
                    ? nameProp.GetString()?.Trim()
                    : null;

                string owner = body.TryGetProperty("owner", out var ownerProp)
                    ? ownerProp.GetString()?.Trim()
                    : null;

                // Manejar passwordValue o password como en SpringBoot
                string passwordValue = null;
                if (body.TryGetProperty("passwordValue", out var pw1) && pw1.ValueKind == JsonValueKind.String)
                {
                    passwordValue = pw1.GetString()?.Trim();
                }
                else if (body.TryGetProperty("password", out var pw2) && pw2.ValueKind == JsonValueKind.String)
                {
                    passwordValue = pw2.GetString()?.Trim();
                }

                // Validaciones
                if (string.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 20)
                {
                    return BadRequest(new BaseResponse<object>(400, "Must be 3 to 20 characters.", null));
                }

                if (string.IsNullOrWhiteSpace(owner) || owner.Length < 3 || owner.Length > 20)
                {
                    return BadRequest(new BaseResponse<object>(400, "Must be 3 to 20 characters.", null));
                }

                if (!string.IsNullOrEmpty(passwordValue) && (passwordValue.Length < 3 || passwordValue.Length > 20))
                {
                    return BadRequest(new BaseResponse<object>(400, "Must be 3 to 20 characters.", null));
                }

                var game = await _gameService.CreateGameAsync(name, owner, passwordValue);

                return Ok(new BaseResponse<Game_Entity>(201, "Game Created", game));
            }
            catch (InvalidOperationException ex) when (ex.Message == "Asset already exists")
            {
                return Conflict(new BaseResponse<object>(409, "Asset already exists", null));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // PUT /api/games/{gameId}
        [HttpPut("{gameId}")]
        public async Task<IActionResult> JoinGame(
            [FromRoute] string gameId,
            [FromHeader(Name = "player")] string playerHeader,
            [FromBody] JsonElement? body = null)
        {
            try
            {
                string playerName = playerHeader;

                if (string.IsNullOrWhiteSpace(playerName) || playerName.Length < 3 || playerName.Length > 20)
                {
                    return BadRequest(new BaseResponse<object>(400, "Must be 3 to 20 characters.", null));
                }

                string passwordValue = null;
                if (body.HasValue)
                {
                    if (body.Value.TryGetProperty("passwordValue", out var pw1) && pw1.ValueKind == JsonValueKind.String)
                    {
                        passwordValue = pw1.GetString()?.Trim();
                    }
                    else if (body.Value.TryGetProperty("password", out var pw2) && pw2.ValueKind == JsonValueKind.String)
                    {
                        passwordValue = pw2.GetString()?.Trim();
                    }
                }

                var game = await _gameService.JoinGameAsync(gameId, playerName, passwordValue);

                return Ok(new BaseResponse<Game_Entity>(200, "Joined successfully", game));
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new BaseResponse<object>(404, "Game not found", null));
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Game is full":
                        return StatusCode(428, new BaseResponse<object>(428, "Game is full", null));
                    case "Player is already part of the game":
                        return Conflict(new BaseResponse<object>(409, "Player is already part of the game", null));
                    default:
                        return BadRequest(new BaseResponse<object>(400, ex.Message, null));
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new BaseResponse<object>(403, ex.Message, null));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // HEAD /api/games/{gameId}/start
        [HttpHead("{gameId}/start")]
        public async Task<IActionResult> StartGame(
            [FromRoute] string gameId,
            [FromHeader(Name = "player")] string playerHeader)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerHeader))
                {
                    Response.Headers.Append("msg", "Player header is required");
                    return BadRequest();
                }

                await _gameService.StartGameAsync(gameId, playerHeader);
                return Ok();
            }
            catch (KeyNotFoundException)
            {
                Response.Headers.Append("msg", "Game not found");
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                Response.Headers.Append("msg", ex.Message);
                return StatusCode(403);
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Need 5 players to start":
                        Response.Headers.Append("msg", "Need 5 players to start");
                        return StatusCode(428);
                    case "Game already started":
                        Response.Headers.Append("msg", "Game already started");
                        return StatusCode(409);
                    default:
                        Response.Headers.Append("msg", ex.Message);
                        return BadRequest();
                }
            }
            catch (Exception ex)
            {
                Response.Headers.Append("msg", $"Error: {ex.Message}");
                return BadRequest();
            }
        }

        // ========== ENDPOINTS DE ROUNDS ==========

        // GET /api/games/{gameId}/rounds
        [HttpGet("{gameId}/rounds")]
        public async Task<IActionResult> GetRounds([FromRoute] string gameId)
        {
            try
            {
                var rounds = await _roundsCollection.Find(r => r.gameId == gameId).ToListAsync();
                return Ok(new BaseResponse<List<Round>>(200, "Results found", rounds));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // GET /api/games/{gameId}/rounds/{roundId}
        [HttpGet("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> GetRound(
            [FromRoute] string gameId,
            [FromRoute] string roundId)
        {
            try
            {
                var round = await _roundsCollection.Find(r => r.id == roundId && r.gameId == gameId).FirstOrDefaultAsync();
                if (round == null)
                {
                    return NotFound(new BaseResponse<object>(404, "Round not found", null));
                }

                return Ok(new BaseResponse<Round>(200, "Round found", round));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }

        // PATCH /api/games/{gameId}/rounds/{roundId}
        [HttpPatch("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> ProposeGroup(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader(Name = "player")] string playerHeader,
            [FromBody] JsonElement body)
        {
            try
            {
                string playerFromBody = body.TryGetProperty("player", out var playerProp)
                    ? playerProp.GetString()
                    : null;

                string playerName = !string.IsNullOrWhiteSpace(playerHeader)
                    ? playerHeader.Trim()
                    : (!string.IsNullOrWhiteSpace(playerFromBody) ? playerFromBody.Trim() : null);

                if (string.IsNullOrWhiteSpace(playerName))
                {
                    return BadRequest(new BaseResponse<object>(400,
                        "Player name is required (header 'player' or body.player)", null));
                }

                var round = await _roundsCollection.Find(r => r.id == roundId && r.gameId == gameId).FirstOrDefaultAsync();
                if (round == null)
                {
                    return NotFound(new BaseResponse<object>(404, "Round not found", null));
                }

                // Validar estado
                if (round.status != "waiting-on-leader")
                {
                    return BadRequest(new BaseResponse<object>(400, "Cannot propose group in current state", null));
                }

                // Validar líder
                if (round.leader != playerName)
                {
                    return BadRequest(new BaseResponse<object>(400, "Only leader can propose group", null));
                }

                // Leer grupo
                List<string> group = new List<string>();
                if (body.TryGetProperty("group", out var groupProp) && groupProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in groupProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            group.Add(item.GetString());
                        }
                    }
                }

                if (group.Count == 0)
                {
                    return BadRequest(new BaseResponse<object>(400, "Group cannot be empty", null));
                }

                // Obtener juego para validaciones
                var game = await _gameService.GetGameByIdAsync(gameId);
                if (game == null)
                {
                    return NotFound(new BaseResponse<object>(404, "Game not found", null));
                }

                // Calcular tamaño requerido (misma lógica SpringBoot)
                int playersCount = game.players.Count;
                long completedRounds = await _roundsCollection.CountDocumentsAsync(r => r.gameId == gameId && r.status == "ended");
                int decade = (int)Math.Min(completedRounds + 1, 5);

                var groupSizes = new Dictionary<int, Dictionary<int, int>>
                {
                    [1] = new() { [5] = 2, [6] = 2, [7] = 2, [8] = 3, [9] = 3, [10] = 3 },
                    [2] = new() { [5] = 3, [6] = 3, [7] = 3, [8] = 4, [9] = 4, [10] = 4 },
                    [3] = new() { [5] = 2, [6] = 4, [7] = 3, [8] = 4, [9] = 4, [10] = 4 },
                    [4] = new() { [5] = 3, [6] = 3, [7] = 4, [8] = 5, [9] = 5, [10] = 5 },
                    [5] = new() { [5] = 3, [6] = 4, [7] = 4, [8] = 5, [9] = 5, [10] = 5 }
                };

                int requiredSize = groupSizes[decade].GetValueOrDefault(playersCount, 2);

                if (group.Count != requiredSize)
                {
                    return StatusCode(428, new BaseResponse<object>(428, "Invalid group size", null));
                }

                // Actualizar round
                round.group = group;
                round.status = "voting";
                round.updatedAt = DateTime.UtcNow;

                await _roundsCollection.ReplaceOneAsync(r => r.id == roundId, round);

                return Ok(new BaseResponse<Round>(200, "Group proposed successfully", round));
            }
            catch (Exception ex)
            {
                return BadRequest(new BaseResponse<object>(400, $"Error: {ex.Message}", null));
            }
        }
    }
}