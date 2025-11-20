using contaminados_grupoa_backend.Models;
using contaminados_grupoa_backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace contaminados_grupoa_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class gamesController : ControllerBase
    {
        private readonly GameService _gameService;

        public gamesController(GameService gameService)
        {
            _gameService = gameService;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateGame([FromBody] GameCreateRequest request)
        {
            try
            {
                // Validar que el request no sea nulo
                if (request == null)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Request body is required" });
                }

                // Validar campos requeridos
                if (string.IsNullOrWhiteSpace(request.name) || string.IsNullOrWhiteSpace(request.owner))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Name and Owner are required" });
                }

                // Validar longitud del nombre
                if (request.name.Trim().Length < 3 || request.name.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Name must be between 3 and 20 characters" });
                }

                // Validar longitud del owner
                if (request.owner.Trim().Length < 3 || request.owner.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Owner must be between 3 and 20 characters" });
                }

                // Validar password si existe
                if (!string.IsNullOrEmpty(request.password) &&
                    (request.password.Trim().Length < 3 || request.password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Crear el juego
                var game = await _gameService.CreateGameAsync(
                    request.name.Trim(),
                    request.owner.Trim(),
                    request.password?.Trim()
                );

                // Preparar respuesta - CORREGIDO: El frontend espera data directamente
                var gameData = new
                {
                    id = game.GameId,
                    name = game.Name,
                    status = game.Status,
                    password = !string.IsNullOrEmpty(game.Password),
                    currentRound = game.CurrentRoundId,
                    players = game.Players,
                    enemies = game.Enemies
                };

                var response = new
                {
                    status = 201,
                    msg = "Game Created",
                    data = gameData  // ← CORREGIDO: Enviar objeto directo, no array
                };

                return StatusCode(201, response);
            }
            catch (InvalidOperationException ex) when (ex.Message == "Asset already exists")
            {
                return Conflict(new { status = 409, msg = "Asset already exists" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SearchGames(
            [FromQuery] string name = null,
            [FromQuery] int page = 0,
            [FromQuery] int limit = 50)
        {
            try
            {
                // Validar parámetros
                if (page < 0)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Page must be 0 or greater" });
                }

                if (limit < 0 || limit > 100)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Limit must be between 0 and 100" });
                }

                // Buscar juegos (solo por nombre)
                var (games, totalCount) = await _gameService.SearchGamesAsync(name, page, limit);

                // CORREGIDO: Envolver en estructura { data: array } que espera el frontend
                var result = games.Select(game => new
                {
                    id = game.GameId,
                    name = game.Name,
                    status = game.Status,
                    password = !string.IsNullOrEmpty(game.Password),
                    currentRound = game.CurrentRoundId,
                    players = game.Players,
                    enemies = game.Enemies
                }).ToList();

                var response = new
                {
                    status = 200,
                    msg = $"Search returned {result.Count} results",
                    data = result  // ← CORREGIDO: Envolver en data
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        // NUEVO ENDPOINT: Obtener juego específico con autenticación
        [HttpGet("{gameId}/")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetGame(
            [FromRoute] string gameId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null)
        {
            try
            {
                // Validar headers requeridos
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                // Validar password solo si se proporciona
                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Obtener juego con autenticación
                var game = await _gameService.GetGameWithAuthAsync(gameId, player.Trim(), password?.Trim());

                // Determinar si mostrar enemies (solo si el player es un enemy)
                var showEnemies = game.Enemies.Contains(player);

                // Preparar respuesta
                var response = new
                {
                    status = 200,
                    msg = "Game Found",
                    data = new
                    {
                        id = game.GameId,
                        name = game.Name,
                        status = game.Status,
                        password = !string.IsNullOrEmpty(game.Password),
                        currentRound = game.CurrentRoundId,
                        players = game.Players,
                        enemies = showEnemies ? game.Enemies : new List<string>(),
                        owner = game.Owner  // ← AGREGA ESTA LÍNEA
                    }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpPut("{gameId}/")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
        public async Task<IActionResult> JoinGame(
        [FromRoute] string gameId,
        [FromHeader] string player,
        [FromHeader(Name = "password")] string password = null,
        [FromBody] JoinGameRequest request = null)
        {
            try
            {
                // Validar headers requeridos
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                // Validar request body
                if (request == null || string.IsNullOrWhiteSpace(request.player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player in request body is required" });
                }

                // Validar longitudes
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header must be between 3 and 20 characters" });
                }

                if (request.player.Trim().Length < 3 || request.player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player in request body must be between 3 and 20 characters" });
                }

                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Unirse al juego
                var game = await _gameService.JoinGameAsync(
                    gameId,
                    player.Trim(),
                    password?.Trim(),
                    request.player.Trim()
                );

                // Determinar si mostrar enemies (solo si el player que hace la request es un enemy)
                var showEnemies = game.Enemies.Contains(player);

                // CORREGIDO: El frontend espera data como array de un elemento
                var gameData = new
                {
                    id = game.GameId,
                    name = game.Name,
                    status = game.Status,
                    password = !string.IsNullOrEmpty(game.Password),
                    currentRound = game.CurrentRoundId,
                    players = game.Players,
                    enemies = showEnemies ? game.Enemies : new List<string>(),
                    owner = game.Owner  // ← AGREGA ESTA LÍNEA
                };

                var response = new
                {
                    status = 200,
                    msg = "Joined Game",
                    data = new[] { gameData }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Asset already exists":
                        return Conflict(new { status = 409, msg = "Asset already exists" });
                    case "This action is not allowed at this time":
                        return StatusCode(428, new { status = 428, msg = "This action is not allowed at this time" });
                    case "Game is full":
                        return StatusCode(428, new { status = 428, msg = "Game is full" });
                    default:
                        return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpHead("{gameId}/start")]
        public async Task<IActionResult> StartGame(
            [FromRoute] string gameId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null)
        {
            try
            {
                // Validar headers requeridos
                if (string.IsNullOrWhiteSpace(player))
                {
                    Response.Headers.Append("X-msg", "Player header is required");
                    return StatusCode(400);
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    Response.Headers.Append("X-msg", "Player must be between 3 and 20 characters");
                    return StatusCode(400);
                }

                // Validar password solo si se proporciona
                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    Response.Headers.Append("X-msg", "Password must be between 3 and 20 characters");
                    return StatusCode(400);
                }

                // Iniciar el juego
                await _gameService.StartGameAsync(gameId, player.Trim(), password?.Trim());

                // Respuesta exitosa - HEAD no tiene body
                return Ok();
            }
            catch (KeyNotFoundException)
            {
                Response.Headers.Append("X-msg", "Game not found");
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        Response.Headers.Append("X-msg", "Invalid credentials");
                        return Unauthorized();
                    case "Only the game owner can start the game":
                        Response.Headers.Append("X-msg", "Only the game owner can start the game");
                        return StatusCode(403);
                    case "Password required":
                        Response.Headers.Append("X-msg", "Password required");
                        return Unauthorized();
                    default:
                        Response.Headers.Append("X-msg", ex.Message);
                        return Unauthorized();
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Game already started":
                        Response.Headers.Append("X-msg", "Game already started");
                        return StatusCode(409);
                    case "Need 5 players to start":
                        Response.Headers.Append("X-msg", "Need 5 players to start");
                        return StatusCode(428);
                    default:
                        Response.Headers.Append("X-msg", ex.Message);
                        return StatusCode(400);
                }
            }
            catch (Exception ex)
            {
                Response.Headers.Append("X-msg", $"Client Error: {ex.Message}");
                return StatusCode(400);
            }
        }

        [HttpGet("{gameId}/rounds")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRounds(
            [FromRoute] string gameId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null)
        {
            try
            {
                // Validar headers requeridos
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                // Validar password solo si se proporciona
                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Obtener las rondas
                var rounds = await _gameService.GetRoundsAsync(gameId, player.Trim(), password?.Trim());

                // Preparar respuesta
                var response = new
                {
                    status = 200,
                    msg = rounds.Count == 0 ? "No rounds found" : "Rounds found",
                    data = rounds.Select(round => new
                    {
                        id = round.RoundId,
                        leader = round.Leader,
                        status = round.Status,
                        result = round.Result,
                        phase = round.Phase,
                        group = round.Group,
                        votes = round.Votes
                    }).ToList()
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpGet("{gameId}/rounds/{roundId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRound(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null)
        {
            try
            {
                // Validar headers requeridos
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                // Validar password solo si se proporciona
                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Obtener la ronda específica
                var round = await _gameService.GetRoundAsync(gameId, roundId, player.Trim(), password?.Trim());

                // Preparar respuesta
                var response = new
                {
                    status = 200,
                    msg = "Round found",
                    data = new
                    {
                        id = round.RoundId,
                        leader = round.Leader,
                        status = round.Status,
                        result = round.Result,
                        phase = round.Phase,
                        group = round.Group,
                        votes = round.Votes
                    }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        // NUEVOS ENDPOINTS PARA RONDAS

        [HttpPatch("{gameId}/rounds/{roundId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
        public async Task<IActionResult> ProposeGroup(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null,
            [FromBody] GroupProposalRequest request = null)
        {
            try
            {
                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                if (request?.group == null || !request.group.Any())
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Group is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                // Validar cada jugador en el grupo
                foreach (var groupPlayer in request.group)
                {
                    if (groupPlayer.Trim().Length < 3 || groupPlayer.Trim().Length > 20)
                    {
                        return BadRequest(new { status = 400, msg = "Client Error: Each player in group must be between 3 and 20 characters" });
                    }
                }

                // Proponer grupo
                var round = await _gameService.ProposeGroupAsync(
                    gameId, roundId, player.Trim(), password?.Trim(), request.group);

                var response = new
                {
                    status = 200,
                    msg = "Group Created",
                    data = new
                    {
                        id = round.RoundId,
                        leader = round.Leader,
                        status = round.Status,
                        result = round.Result,
                        phase = round.Phase,
                        group = round.Group,
                        votes = round.Votes
                    }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    case "Only the round leader can propose groups":
                        return StatusCode(403, new { status = 403, msg = "Only the round leader can propose groups" });
                    case "Not part of the proposed group":
                        return StatusCode(403, new { status = 403, msg = "Not part of the proposed group" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Asset already exists":
                        return Conflict(new { status = 409, msg = "Asset already exists" });
                    case "This action is not allowed at this time":
                        return StatusCode(428, new { status = 428, msg = "This action is not allowed at this time" });
                    case "Group must have between 2 and 6 players":
                        return BadRequest(new { status = 400, msg = "Group must have between 2 and 6 players" });
                    case "Player has already voted":
                        return Conflict(new { status = 409, msg = "Player has already voted" });
                    case "Player has already submitted an action":
                        return Conflict(new { status = 409, msg = "Player has already submitted an action" });
                    default:
                        return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpPost("{gameId}/rounds/{roundId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
        public async Task<IActionResult> SubmitVote(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null,
            [FromBody] VoteRequest request = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                if (request == null)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Vote is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                var round = await _gameService.SubmitVoteAsync(
                    gameId, roundId, player.Trim(), password?.Trim(), request.vote);

                var response = new
                {
                    status = 200,
                    msg = "Voted successfully",
                    data = new
                    {
                        id = round.RoundId,
                        leader = round.Leader,
                        status = round.Status,
                        result = round.Result,
                        phase = round.Phase,
                        group = round.Group,
                        votes = round.Votes
                    }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    case "Not part of the proposed group":
                        return StatusCode(403, new { status = 403, msg = "Not part of the proposed group" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Asset already exists":
                        return Conflict(new { status = 409, msg = "Asset already exists" });
                    case "This action is not allowed at this time":
                        return StatusCode(428, new { status = 428, msg = "This action is not allowed at this time" });
                    case "Player has already voted":
                        return Conflict(new { status = 409, msg = "Player has already voted" });
                    default:
                        return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }

        [HttpPut("{gameId}/rounds/{roundId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
        public async Task<IActionResult> SubmitAction(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader] string player,
            [FromHeader(Name = "password")] string password = null,
            [FromBody] ActionRequest request = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(player))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player header is required" });
                }

                if (request == null)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Action is required" });
                }

                // Validar longitud del player
                if (player.Trim().Length < 3 || player.Trim().Length > 20)
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Player must be between 3 and 20 characters" });
                }

                if (!string.IsNullOrEmpty(password) && (password.Trim().Length < 3 || password.Trim().Length > 20))
                {
                    return BadRequest(new { status = 400, msg = "Client Error: Password must be between 3 and 20 characters" });
                }

                var round = await _gameService.SubmitActionAsync(
                    gameId, roundId, player.Trim(), password?.Trim(), request.action);

                var response = new
                {
                    status = 200,
                    msg = "Action registered",
                    data = new
                    {
                        id = round.RoundId,
                        leader = round.Leader,
                        status = round.Status,
                        result = round.Result,
                        phase = round.Phase,
                        group = round.Group,
                        votes = round.Votes
                    }
                };

                return Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { status = 404, msg = "The specified resource was not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                switch (ex.Message)
                {
                    case "Invalid credentials":
                        return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                    case "Not part of the game":
                        return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                    case "Password required":
                        return Unauthorized(new { status = 401, msg = "Password required" });
                    case "Not part of the round group":
                        return StatusCode(403, new { status = 403, msg = "Not part of the round group" });
                    default:
                        return Unauthorized(new { status = 401, msg = ex.Message });
                }
            }
            catch (InvalidOperationException ex)
            {
                switch (ex.Message)
                {
                    case "Asset already exists":
                        return Conflict(new { status = 409, msg = "Asset already exists" });
                    case "This action is not allowed at this time":
                        return StatusCode(428, new { status = 428, msg = "This action is not allowed at this time" });
                    case "Player has already submitted an action":
                        return Conflict(new { status = 409, msg = "Player has already submitted an action" });
                    default:
                        return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { status = 400, msg = $"Client Error: {ex.Message}" });
            }
        }
    }

    public class GameCreateRequest
    {
        public string name { get; set; }
        public string owner { get; set; }
        public string password { get; set; }
    }

    public class JoinGameRequest
    {
        public string player { get; set; }
    }

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
}