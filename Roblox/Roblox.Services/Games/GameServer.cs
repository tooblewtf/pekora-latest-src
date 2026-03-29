using System.Diagnostics;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Dapper;
using Roblox.Dto.Games;
using Roblox.Libraries.EasyJwt;
using Roblox.Libraries.Password;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.GameServer;
using Roblox.Services.Exceptions;
using Roblox.Logging;
using System.Collections.Concurrent;
using Roblox.Dto.Users;

namespace Roblox.Services;



public class GameServerService : ServiceBase
{
    public class ArbiterHttpClient : HttpClient
    {
        
        public ArbiterHttpClient()
        {
            // BaseAddress should be the url to your game-arbiter host itself
            // i don't really know why pekora's developers decided to hardcode this except for
            // making a configuration on AppSettings.json (i gotta do that soon)
            this.BaseAddress = new Uri($"http://gamearbiterserverhere.com/");
            this.DefaultRequestHeaders.Add("PJX-ArbiterAUTH", Configuration.ArbiterAuthorization);
        }
        public async Task<bool> StartGameServer(StartGameServerRequest request)
        {
            var result = await this.PostAsync("start-game-server", new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));
            return result.IsSuccessStatusCode;
        }
        public async Task<bool> EvictPlayer(EvictPlayerRequest request)
        {
            /*
                This is temporary because the JSON doesnt format well
            */
            var jsonRequest = $"{{ \"gameId\": \"{request.gameId}\", \"userId\": {request.userId}, \"messageVersionId\": {request.messageVersionId} }}";
            var result = await this.PostAsync("evict-player", new StringContent(jsonRequest, Encoding.UTF8, "application/json"));
            return result.IsSuccessStatusCode;
        }
        public async Task<bool> KillGameServer(KillGameServerRequest request, CancellationToken cancellationToken)
        {
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var result = await this.PostAsync("kill-game-server", content, cancellationToken);
            return result.IsSuccessStatusCode;
        }
        public static EvictPlayerRequest CreateEvictPlayerRequest(Guid jobId, long userId)
        {
            return new EvictPlayerRequest
            {
                gameId = jobId,
                userId = userId,
                messageVersionId = 0
            };
        }
        public static StartGameServerRequest CreateGameServerRequest(PlaceEntry placeInfo, int rccPort, int networkServerPort, int proxyPort, Guid jobId, int matchmaking)
        {
            return new StartGameServerRequest
            {
                jobId = jobId,
                placeId = placeInfo.placeId,
                universeId = placeInfo.universeId,
                maxPlayerCount = placeInfo.maxPlayerCount,
                gameServerPort = networkServerPort,
                rccPort = rccPort,
                proxyPort = proxyPort,
                creatorId = placeInfo.builderId,
                placeVersion = 1,
                matchmakingContextId = matchmaking,
                year = placeInfo.year,
            };
        }
        public static KillGameServerRequest CreateKillGameServerRequest(Guid jobId)
        {
            return new KillGameServerRequest
            {
                jobId = jobId,
            };
        }
        public class EvictPlayerRequest
        {
            public Guid gameId { get; set; }
            public long userId { get; set; }
            public int messageVersionId { get; set; }
        }
        public class StartGameServerRequest
        {
            public Guid jobId { get; set; }
            public long placeId { get; set; }
            public long universeId { get; set; }
            public int maxPlayerCount { get; set; }
            public long gameServerPort { get; set; }
            public long rccPort { get; set; }
            public long proxyPort { get; set; }
            public long creatorId { get; set; }
            public long placeVersion { get; set; }
            public int matchmakingContextId { get; set; }
            public long year { get; set; }
        }

        public class KillGameServerRequest
        {
            public Guid jobId { get; set; }
        }
    }

    private const string ClientJoinTicketType = "GameJoinTicketV1.1";
    private const string ServerJoinTicketType = "GameServerTicketV2";
    private static ArbiterHttpClient arbiterClient = new ArbiterHttpClient();
    private static GamesService games = new GamesService();
    private static string jwtKey { get; set; } = string.Empty;
    private static EasyJwt jwt { get; } = new();
    private static Random RandomComponent = new Random();
    private static PasswordHasher hasher { get; } = new();
    private static Dictionary<long, long> gamePlayerCounts = new Dictionary<long, long>(); // placeid, playercount
    private static Dictionary<string, Process> jobRccs = new Dictionary<string, Process>(); // jobid, rcc process
    public static Dictionary<string, int> currentGameServerPorts = new Dictionary<string, int>() {}; // networkserver ports, jobid, port
    private static Dictionary<long, string> currentPlaceIdsInUse = new Dictionary<long, string>(); // placeid, jobid
    public static ConcurrentDictionary<long, long> CurrentPlayersInGame = new ConcurrentDictionary<long, long>() { }; // userid, placeid
    public static Dictionary<Process, int> mainRCCPortsInUse = new Dictionary<Process, int>(); // Process, main RCC soap port
    public static Dictionary<string, int> unreadyGameServers = new Dictionary<string, int>(); // Process, network server port
    public static void Configure(string newJwtKey)
    {
        jwtKey = "6EwhjPzM9Kf9pPRgYyCnhmM3v6j9d95vCg+NBeUThw49dcLLs1BvNnErFItWCEjS0e85QaYRcE38sTgkRg";
    }

    private string HashIpAddress(string hashedIpAddress)
    {
        return hasher.Hash(hashedIpAddress);
    }

    private bool VerifyIpAddress(string hashedIpAddress, string providedIpAddress)
    {
        return hasher.Verify(hashedIpAddress, providedIpAddress);
    }

    /// <summary>
    /// Create a ticket for joining a game
    /// </summary>
    /// <param name="userId">The ID of the user</param>
    /// <param name="placeId">The ID of the place</param>
    /// <param name="ipHash">The IP Address from ControllerBase.GetIP()</param>
    /// <returns></returns>
    public string CreateTicket(long userId, long placeId, string ipHash)
    {
        var entry = new GameServerJwt
        {
            t = ClientJoinTicketType,
            userId = userId,
            placeId = placeId,
            ip = HashIpAddress(ipHash),
            iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        return jwt.CreateJwt(entry, jwtKey);
    }

    public bool IsExpired(long issuedAt)
    {
        var createdAt = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(issuedAt);
        var notExpired = createdAt.Add(TimeSpan.FromMinutes(5)) > DateTime.UtcNow;
        if (!notExpired)
        {
            return true;
        }

        return false;
    }

    public GameServerJwt DecodeTicket(string ticket)
    {
        var value = jwt.DecodeJwt<GameServerJwt>(ticket, jwtKey);
        if (value.t != ClientJoinTicketType) throw new ArgumentException("Invalid ticket");
        if (IsExpired(value.iat))
        {
            throw new ArgumentException("Invalid ticket");
        }
        return value;
    }

    public string CreateGameServerTicket(long placeId, string domain)
    {
        var ticket = new GameServerTicketJwt
        {
            t = ServerJoinTicketType,
            placeId = placeId,
            domain = domain,
            iat = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        return jwt.CreateJwt(ticket, jwtKey);
    }

    public GameServerTicketJwt DecodeGameServerTicket(string ticket)
    {
        var value = jwt.DecodeJwt<GameServerTicketJwt>(ticket, jwtKey);
        if (value.t != ServerJoinTicketType) throw new ArgumentException("Invalid ticket");
        if (IsExpired(value.iat))
        {
            throw new ArgumentException("Invalid ticket");
        }

        return value;
    }

    public async Task OnPlayerJoin(long userId, long placeId, Guid serverId)
    {
        CurrentPlayersInGame.AddOrUpdate(userId, placeId, (key, oldValue) => placeId);
        await db.ExecuteAsync(
            "INSERT INTO asset_server_player (asset_id, user_id, server_id) VALUES (:asset_id, :user_id, :server_id::uuid)",
            new
            {
                asset_id = placeId,
                user_id = userId,
                server_id = serverId,
            });
        await InsertAsync("asset_play_history", new
        {
            asset_id = placeId,
            user_id = userId,
        });
        await db.ExecuteAsync("UPDATE asset_place SET visit_count = visit_count + 1 WHERE asset_id = :id", new
        {
            id = placeId,
        });
        // give ticket to creator
        await InTransaction(async _ =>
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
            var placeDetails = await assets.GetAssetCatalogInfo(placeId);
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            using var cooldown = ServiceProvider.GetOrCreate<CooldownService>(this);
            // Per 100 users there is a 1 day cooldown to earn tickets from visits
            if (await cooldown.TryIncrementBucketCooldown("TicketCreatorPlaceVisit:" + placeId, 100, TimeSpan.FromDays(1)))
            {
                if (placeDetails.creatorType == CreatorType.Group)
                {
                    await InsertAsync("user_transaction", new
                    {
                        amount = 10,
                        currency_type = CurrencyType.Tickets,
                        user_id_one = (long?)null,
                        user_id_two = userId,
                        group_id_one = placeDetails.creatorTargetId,
                        type = PurchaseType.PlaceVisit,
                        // store id of the game as well
                        asset_id = placeId,
                    });
                }
                else
                {
                    if (placeDetails.creatorTargetId == userId)
                    {
                        return 0;
                    }
                    await ec.IncrementCurrency(CreatorType.User, placeDetails.creatorTargetId, CurrencyType.Tickets, 10);
                    await InsertAsync("user_transaction", new
                    {
                        amount = 10,
                        currency_type = CurrencyType.Tickets,
                        user_id_one = placeDetails.creatorTargetId,
                        user_id_two = userId,
                        type = PurchaseType.PlaceVisit,
                        // store id of the game as well
                        asset_id = placeId,
                    });
                    /* 
                        Homestead = 6
                        Bricksmith = 7
                    */
                    using var accountService = ServiceProvider.GetOrCreate<AccountInformationService>(this);
                    var badges = await accountService.GetUserBadges(placeDetails.creatorTargetId);
                    switch (await games.GetTotalVisitsFromUser(placeDetails.creatorTargetId))
                    {
                        case 100:
                            if (badges.Any(b => b.id == 6))
                            {
                                return 0;
                            }
                            await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
                            {
                                user_id = placeDetails.creatorTargetId,
                                badge_id = 6,
                            });
                            break;
                        case 1000:
                            if (badges.Any(b => b.id == 7))
                            {
                                return 0;
                            }
                            await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
                            {
                                user_id = placeDetails.creatorTargetId,
                                badge_id = 7,
                            });
                            break;
                        default:
                            break;
                    }
                }
            }


            return 0;
        });
    }

    public async Task OnPlayerLeave(long userId, long placeId, Guid serverId)
    {
        CurrentPlayersInGame.TryRemove(userId, out _);

        await db.ExecuteAsync(
            "DELETE FROM asset_server_player WHERE user_id = :user_id AND server_id = :server_id::uuid", new
            {
                server_id = serverId,
                user_id = userId,
            });
        Console.WriteLine("deleted from db line 195 onplayerleave");
        var latestSession = await db.QuerySingleOrDefaultAsync<AssetPlayEntry>(
            "SELECT id, created_at as createdAt FROM asset_play_history WHERE user_id = :user_id AND asset_id = :asset_id AND ended_at IS NULL ORDER BY asset_play_history.id DESC LIMIT 1",
            new
            {
                user_id = userId,
                asset_id = placeId,
            });
        if (latestSession != null)
        {
            await db.ExecuteAsync("UPDATE asset_play_history SET ended_at = now() WHERE id = :id", new
            {
                id = latestSession.id,
            });

            if (latestSession.createdAt.Year != DateTime.UtcNow.Year) return;

            var playTimeMinutes = (long)Math.Truncate((DateTime.UtcNow - latestSession.createdAt).TotalMinutes);
            var earnedTickets = Math.Min(playTimeMinutes * 10, 60); // temp cap, might reduce in the future?
            // cap is 10k tickets per 12 hours (about 1k robux)
            const long maxEarningsPerPeriod = 10000;
            using (var ec = ServiceProvider.GetOrCreate<EconomyService>(this))
            {
                var earningsToday =
                    await ec.CountTransactionEarningsOfType(userId, PurchaseType.PlayingGame, null, TimeSpan.FromHours(12));

                if (earningsToday >= maxEarningsPerPeriod)
                    return;
            }

            await InTransaction(async _ =>
            {
                using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
                await ec.IncrementCurrency(CreatorType.User, userId, CurrencyType.Tickets, earnedTickets);
                await InsertAsync("user_transaction", new
                {
                    amount = earnedTickets,
                    currency_type = CurrencyType.Tickets,
                    user_id_one = userId,
                    user_id_two = 1,
                    type = PurchaseType.PlayingGame,
                    // store id of the game they played as well
                    asset_id = placeId,
                });

                return 0;
            });
        }
    }

    // private async Task<T> PostToGameServer<T>(string ipAddress, string port, string methodName, List<dynamic>? args = null, CancellationToken? cancelToken = null)
    // {
    //     var jsonRequest = new
    //     {
    //         method = methodName,
    //         arguments = args ?? new List<dynamic>(),
    //     };
    //     var content = new StringContent(JsonSerializer.Serialize(jsonRequest));
    //     content.Headers.Add("roblox-server-authorization", Configuration.GameServerAuthorization);
    //     content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    //     if (cancelToken == null)
    //     {
    //         var source = new CancellationTokenSource();
    //         source.CancelAfter(TimeSpan.FromSeconds(30));
    //         cancelToken = source.Token;
    //     }

    //     var result = await client.PostAsync("http://" + ipAddress + ":" + port + "/api/public-method/", content,
    //         cancelToken.Value);
    //     if (!result.IsSuccessStatusCode) throw new Exception("Unexpected statusCode: " + result.StatusCode + "\nIP = " + ipAddress + "\nPort = " + port);
    //     var response = JsonSerializer.Deserialize<T>(await result.Content.ReadAsStringAsync(cancelToken.Value));
    //     if (response == null)
    //     {
    //         throw new Exception("Null response from PostToGameServer");
    //     }
    //     return response;
    // }

    // public async Task<GameServerInfoResponse?> GetGameServerInfo(string ipAddress, string port)
    // {
    //     try
    //     {
    //         using var cancelToken = new CancellationTokenSource();
    //         cancelToken.CancelAfter(TimeSpan.FromSeconds(5));
    //         return await PostToGameServer<GameServerInfoResponse>(ipAddress, port, "getStatus", default, cancelToken.Token);
    //     }
    //     catch (Exception e) when (e is TaskCanceledException or TimeoutException or HttpRequestException)
    //     {
    //         return null;
    //     }
    // }
    public async Task KickPlayer(long userId, Guid? jobId = null)
    {
        try
        {
            if (jobId == null)
            {
                jobId = await GetJobIdByUserId(userId);
            }

        }
        catch (Exception e)
        {
            Console.WriteLine($"Error getting jobId for user {userId}: {e.Message}");
            return;
        }
        await arbiterClient.EvictPlayer(ArbiterHttpClient.CreateEvictPlayerRequest(jobId.Value, userId));
    }
    // public async Task StartGame(string ipAddress, string port, long placeId, string gameServerId, int gameServerPort)
    // {
    //     await PostToGameServer<GameServerEmptyResponse>(ipAddress, port, "startGame",
    //         new List<dynamic> {placeId, gameServerId, gameServerPort});
    // }

    public async Task ShutDownServerAsync(Guid serverId)
    {
        using var serverCreationLock = await Cache.redLock.CreateLockAsync($"CloseGameServer:{serverId.ToString()}", TimeSpan.FromSeconds(10));
        if (!serverCreationLock.IsAcquired)
        {
            // Silence.
            return;
        }
        try
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                await arbiterClient.KillGameServer(
                    ArbiterHttpClient.CreateKillGameServerRequest(serverId),
                    cts.Token);
            }

            Console.WriteLine($"Gameserver {serverId} was successfully closed in {stopwatch.ElapsedMilliseconds}ms!");

            await DeleteGameServer(serverId);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine($"KillGameServer timed out after 15 seconds for server {serverId}.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error shutting down server {serverId}: {ex}");
        }
    }


    public static void RemoveAllPlayersFromPlaceId(long placeId)
    {
        List<long> playersToRemove = CurrentPlayersInGame.Where(kvp => kvp.Value == placeId).Select(kvp => kvp.Key).ToList();

        foreach (var userId in playersToRemove)
        {
            CurrentPlayersInGame.TryRemove(userId, out _);
        }
    }

    public static long GetUserPlaceId(long userId) // get user game is in
    {
        bool isInGame = CurrentPlayersInGame.ContainsKey(userId);
        if (!isInGame)
            return 0;

        return CurrentPlayersInGame[userId];
    }

    public static long GetPlaceIdByJobId(string jobId)
    {
        foreach (var kvp in currentPlaceIdsInUse)
        {
            if (kvp.Value == jobId)
            {
                return kvp.Key;
            }
        }

        return 0; // we never throw exceptions. EVER.
    }

    public async Task<DateTime> GetLastServerPing(string serverId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT updated_at FROM asset_server WHERE id = :id::uuid", new
        {
            id = serverId,
        });

        return (DateTime) result.updated_at;
    }
    public async Task<long> GetServerStat(Guid serverId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long>("SELECT ping FROM asset_server WHERE id = :id::uuid", new
        {
            id = serverId,
        });

        if (result == 0)
            return -1;

        return result;
    }

    public async Task SetServerStats(string serverId, long ping, long fps)
    {
        await db.ExecuteAsync("UPDATE asset_server SET ping = :ping, fps = :fps WHERE id = :id::uuid", new
        {
            ping,
            fps,
            id = serverId,
        });
    }
    public async Task SetServerPing(Guid serverId)
    {
        await db.ExecuteAsync("UPDATE asset_server SET updated_at = :u, status = :stat WHERE id = :id::uuid", new
        {
            u = DateTime.UtcNow,
            stat = ServerStatus.Ready,
            id = serverId,
        });
    }

    public async Task DeleteGameServer(Guid serverId)
    {
        await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new {id = serverId});
        await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new {id = serverId});
    }

    private static readonly IEnumerable<int> GameServerPorts = new []
    {
        // this must always stay in sync with nginx config file
        53640, // es1-1
        53641, // es1-2, etc
        53642, // 3
        53643, // 4
        53644, // 5
        53645, // 6
        53646, // 7
        53647, // 8
        53648, // 9
        53649, // 10
#if false
        53650,
        53651,
        53652,
        53653,
        53654,
        53655,
#endif
    };

    private GameServerPort GetPreferredPortForGameServer(IEnumerable<GameServerMultiRunEntry> runningGames)
    {
        var games = runningGames.ToList();
        var ports = GameServerPorts.ToArray();
        // Find a port that's not in use
        int port = 0;
        int id = 0;
        for (var i = 0; i < ports.Length; i++)
        {
            var portOk = games.Find(c => c.port == ports[i]) == null;
            if (portOk)
            {
                port = ports[i];
                id = i + 1;
                break;
            }
        }

        if (port == 0)
        {
            throw new Exception("Cannot find a free port for game server");
        }

        return new GameServerPort(port, id);
    }

    private GameServerPort GetPortByPortNumber(int port)
    {
        var ports = GameServerPorts.ToArray();
        for (int i = 0; i < ports.Length; i++)
        {
            if (ports[i] == port)
            {
                return new GameServerPort(ports[i], i + 1);
            }
        }

        throw new ArgumentOutOfRangeException();
    }

    // public async Task<List<Tuple<GameServerInfoResponse,GameServerConfigEntry>>> GetAllGameServers()
    // {
    //     var getServerDataTasks = new List<Task<GameServerInfoResponse?>>();
    //     foreach (var entry in Configuration.GameServerIpAddresses)
    //     {
    //         var data = entry.ip.Split(":");
    //         var ip = data[0];
    //         var port = data[1];
    //         getServerDataTasks.Add(GetGameServerInfo(ip, port));
    //     }

    //     var getServerDataResults = await Task.WhenAll(getServerDataTasks);

    //     var serverData =getServerDataResults.Select((c, idx) =>
    //             new Tuple<GameServerInfoResponse?, GameServerConfigEntry>(c, Configuration.GameServerIpAddresses.ToArray()[idx]))
    //         .Where(v => v.Item1 != null)
    //         .ToList();
    //     return serverData!;
    // }
/*
    public async Task<GameServerGetOrCreateResponse> GetServerForPlaceV2(long placeId, long year)
    {
        await using var serverCreationLock = await Cache.redLock.CreateLockAsync("CreateGameServerV1", TimeSpan.FromSeconds(30));
        if (!serverCreationLock.IsAcquired)
            return new GameServerGetOrCreateResponse
            {
                status = JoinStatus.Waiting,
            };

        var serverData = await GetAllGameServers();

        long maxPlayerCount;
        using (var gs = ServiceProvider.GetOrCreate<GamesService>())
        {
            maxPlayerCount = await gs.GetMaxPlayerCount(placeId);
        }
        // First, try to see if this game is already running. If it is, we should make the player join that.
        foreach (var (serverInfo, entry) in serverData)
        {
            var runningGames = serverInfo!.data.ToList();
            var runningPlaces = runningGames.ToArray();
            if (runningPlaces.Length == 0) continue;
            foreach (var runningPlace in runningPlaces)
            {
                // check if this is the right place
                if (runningPlace.placeId != placeId)
                    continue;
                // check if server has too many players
                var currentPlayerCount = await GetGameServerPlayers(runningPlace.id);
                if (currentPlayerCount.Count() >= maxPlayerCount)
                    continue;
                // We found a good place! Tell them to join...
                var joinUrl = GetPortByPortNumber(runningPlace.port).ApplyIdToUrl(entry.domain);
                Writer.Info(LogGroup.GameServerJoin, "Found a good place! placeId = {0} port = {1} url = {2}", placeId, runningPlace.port, joinUrl);
                return new()
                {
                    status = JoinStatus.Joining,
                    job = CreateGameServerTicket(placeId, joinUrl),
                };
            }
        }
        // Sort by least loaded
        serverData = serverData.Where(a => a.Item1 != null && a.Item1.data != null).ToList();
        serverData.Sort((a, b) =>
        {
            var cOne = a.Item1!.data.Count();
            var cTwo = b.Item1!.data.Count();
            return cOne > cTwo ? 1 : cOne == cTwo ? 0 : -1;
        });
        Writer.Info(LogGroup.GameServerJoin, "Least loaded server is {0} with {1} games running", serverData[0].Item2.ip, serverData[0].Item1!.data.Count());
        foreach (var (serverInfo, entry) in serverData)
        {

            string ip = "85.125.186.154";

            int mainRCCPort = RandomComponent.Next(30000, 40000);
            int networkServerPort = RandomComponent.Next(50000, 60000);
            var runningCount = serverInfo!.data.Count();
            if (runningCount >= entry.maxServerCount)
            {
                Writer.Info(LogGroup.GameServerJoin, "cannot start server on {0} since it has too many games running ({1} vs {2})", entry.ip, runningCount, entry.maxServerCount);
                continue;
            }
            // Create the server
            var id = Guid.NewGuid().ToString();
            var gamePort = GetPreferredPortForGameServer(serverInfo.data);
            await db.ExecuteAsync(
                "INSERT INTO asset_server (id, asset_id, ip, port, server_connection) VALUES (:id::uuid, :asset_id, :ip, :port, :server_connection)",
                new
                {
                    id,
                    asset_id = placeId,
                    ip,
                    networkServerPort,
                    server_connection = $"{ip}:{networkServerPort}", // ip:port
                });
            try
            {
                var watch = new Stopwatch();
                watch.Start();
                await StartGameServer(placeId, mainRCCPort, networkServerPort, id, year, 43200);
                //await StartGame(ip, port, placeId, id, gamePort.port);
                watch.Stop();
                //GameMetrics.ReportTimeToStartGameServer(ip, mainRCCPort, watch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new {id});
                throw new Exception("Cannot start game server", e);
            }

            Writer.Info(LogGroup.GameServerJoin, "Created server for {0} at {1}:{2}. Join url = {3}", placeId, entry.domain, gamePort.port, gamePort.ApplyIdToUrl(entry.domain));

            return new()
            {
                status = JoinStatus.Joining,
                job = CreateGameServerTicket(placeId, gamePort.ApplyIdToUrl(entry.domain)),
            };
        }

        // Default
        return new()
        {
            status = JoinStatus.Waiting,
        };
    }
*/

    public async Task<Guid> GetJobIdByUserId(long userId)
    {
        var result = await db.QueryFirstOrDefaultAsync<Guid?>("SELECT server_id FROM asset_server_player WHERE user_id = :userId", new
        {
            userId
        });

        return result ?? throw new RecordNotFoundException();
    }
    public async Task<GameServerDb> GetGameServer(Guid jobId)
    {
        return await db.QueryFirstOrDefaultAsync<GameServerDb>(
            "SELECT id, asset_id as assetId, port, updated_at as updatedAt, status, type FROM asset_server WHERE id = :id::uuid",
            new
            {
                id = jobId,
            });
    }

    public async Task<bool> IsPortTaken(int port)
    {
        int result = await db.QueryFirstOrDefaultAsync<int>(
            "SELECT port FROM asset_server WHERE port = :gsport",
            new
            {
                gsport = port,
            });
        return result != 0;
    }
    public async Task<IEnumerable<GameServerDb>> GetGameServersForPlace(long placeId, int? matchmaking = 1)
    {
        var result = await db.QueryAsync<GameServerDb>(
            "SELECT id, asset_id as assetId, port, updated_at as updatedAt, status, type FROM asset_server WHERE asset_id = :assetid AND type = :type",
            new
            {
                assetid = placeId,
                type = matchmaking,
            });
        if (result == null)
            return new List<GameServerDb>();
        return result;
    }

    public async Task<GameServerGetOrCreateResponse> GetServerForPlace(PlaceEntry placeInfo, int matchmaking)
    {
        var gameServers = await GetGameServersForPlace(placeInfo.placeId, matchmaking);
        foreach (var server in gameServers)
        {
            var currentPlayerCount = await GetGameServerPlayers(server.id);

            // if the server is full continue the search for a good one
            if (currentPlayerCount.Count() >= placeInfo.maxPlayerCount)
            {
                continue;
            }
            // if the server is older than 5 minutes then shutdown the server
            if (server.updatedAt.AddMinutes(5) < DateTime.UtcNow)
            {
                await ShutDownServerAsync(server.id);
                continue;
            }

            //dict check!!! if it doesnt contain it lets kill it!
            //if (!currentGameServerPorts.ContainsKey(jobid))
            //{
                //_ = ShutDownServerAsync(jobid);
                //continue;
            //}

            // we found a server to join or.... its loading depending
            return new GameServerGetOrCreateResponse()
            {
                job = server.id,
                ip = Configuration.GameServerIp,
                port = server.port,
                status = server.status == ServerStatus.Ready ? JoinStatus.Joining : JoinStatus.Loading
            };
            
        }

        int mainRCCPort = RandomComponent.Next(30000, 40000);
        int networkServerPort =  RandomComponent.Next(50000, 60000);;
        int proxyPort = 0;
        do
        {
            proxyPort = RandomComponent.Next(7000, 8000);
            if (!await IsPortTaken(proxyPort))
                break;
            
        } while (true);

        Guid jobId = Guid.NewGuid();
        // We need to create a lock to prevent multiple requests from creating the same game server
        using var serverCreationLock = await Cache.redLock.CreateLockAsync($"CreateGameServerV1:{placeInfo.placeId}", TimeSpan.FromSeconds(3));
        if (!serverCreationLock.IsAcquired)
        {
            return new GameServerGetOrCreateResponse
            {
                status = JoinStatus.Loading,
            };
        }

        _ = Task.Run(async () => await StartGameServer(placeInfo, mainRCCPort, networkServerPort, proxyPort, jobId, matchmaking));

        await db.ExecuteAsync(
            "INSERT INTO asset_server (id, asset_id, ip, port, server_connection, type) VALUES (:id::uuid, :asset_id, :ip, :port, :server_connection, :type)",
        new
        {
            id = jobId,
            asset_id = placeInfo.placeId,
            ip = Configuration.GameServerIp,
            port = networkServerPort,
            server_connection = $"{Configuration.GameServerIp}:{networkServerPort}",
            type = matchmaking
        });

        return new GameServerGetOrCreateResponse()
        {
            job = jobId,
            ip = Configuration.GameServerIp,
            port = proxyPort,
            status = JoinStatus.Waiting
        };
    }


    public async Task<string> StartGameServer(PlaceEntry placeInfo, int RCCPort, int networkServerPort, int proxyPort, Guid jobId, int matchmaking)
    {
        Console.WriteLine("Starting Gameserver");
        var request = ArbiterHttpClient.CreateGameServerRequest(placeInfo, RCCPort, networkServerPort, proxyPort, jobId, matchmaking);
        await arbiterClient.StartGameServer(request);
        return "OK";
    }

    [Obsolete]
    public async Task DeleteOldGameServers()
    {
        // first part, do game servers
        var serversToDelete = (await db.QueryAsync<GameServerEntry>("SELECT id::uuid, asset_id as assetId FROM asset_server WHERE updated_at <= :t", new
        {
            t = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(2)),
        })).ToList();
        Console.WriteLine("[info] there are {0} bad servers", serversToDelete.Count);
        foreach (var server in serversToDelete)
        {
            var players = await GetGameServerPlayers(server.id);
            foreach (var player in players)
            {
                await OnPlayerLeave(player.userId, server.assetId, server.id);
            }
            Console.WriteLine("[info] deleting server {0}", server.id);
            await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new
            {
                id = server.id,
            });
            //Console.WriteLine("deleted from db line 706 deleteoldgameservers");
            await db.ExecuteAsync("DELETE FROM asset_server WHERE id = :id::uuid", new
            {
                id = server.id,
            });
        }
        // second part, do game server players
        // this is so ugly jeez
        var orphanedPlayers =
            await db.QueryAsync(
                "SELECT s.id, p.server_id FROM asset_server_player p LEFT JOIN asset_server s ON s.id = p.server_id WHERE s.id IS NULL");
        foreach (var deadbeatDad in orphanedPlayers.Select(c => ((Guid) c.server_id).ToString()).Distinct())
        {
            Console.WriteLine("[info] deleting all orphans for serverId = {0}",deadbeatDad);
            await db.ExecuteAsync("DELETE FROM asset_server_player WHERE server_id = :id::uuid", new
            {
                id = deadbeatDad,
            });
            Console.WriteLine("deleted from db line 724 DeleteOldGameServers");
        }
    }

    public async Task<IEnumerable<GameServerPlayer>> GetGameServerPlayers(Guid serverId)
    {
        return await db.QueryAsync<GameServerPlayer>(
            "SELECT user_id as userId, u.username FROM asset_server_player INNER JOIN \"user\" u ON u.id = asset_server_player.user_id WHERE server_id = :id::uuid", new
            {
               id = serverId,
            });
    }

    public async Task<IEnumerable<GameServerEntryWithPlayers>> GetGameServers(long placeId, int offset, int limit, int type = 1)
    {
        var result = (await db.QueryAsync<GameServerEntryWithPlayers>("SELECT id::uuid, asset_id as assetId FROM asset_server WHERE asset_id = :id AND type = :type LIMIT :limit OFFSET :offset", new
        {
            id = placeId,
            type,
            limit,
            offset,
        })).ToList();

        foreach (var server in result)
        {
            server.players = await GetGameServerPlayers(server.id);
        }
        return result;
    }

    static Task WaitForPort(int RCCPort)
    {
        while (true)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(IPAddress.Parse("127.0.0.1"), RCCPort);
                    //Console.WriteLine("did not find port");
                    break;
                }
            }
            catch (SocketException)
            {
                Thread.Sleep(0);
            }
        }
        //Console.WriteLine($"found port: {RCCPort}");
        return Task.CompletedTask;
    }
    public async Task<IEnumerable<GameServerEntry>> GetGamesUserIsPlaying(long userId)
    {
       return await db.QueryAsync<GameServerEntry>(
            "SELECT s.id::uuid, s.asset_id as assetId FROM asset_server_player p INNER JOIN asset_server s ON s.id = p.server_id WHERE p.user_id = :id",
            new
            {
                id = userId,
            });
    }

}
