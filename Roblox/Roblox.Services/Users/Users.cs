using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using Dapper;
using Roblox.Dto;
using Roblox.Dto.Assets;
using Roblox.Dto.Economy;
using Roblox.Dto.Users;
using Roblox.Dto.Authentication;
using Roblox.Exceptions.Services.Users;
using Roblox.Libraries;
using Roblox.Libraries.Exceptions;
using Roblox.Libraries.Password;
using Roblox.Logging;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Models.Economy;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services.DbModels;
using Roblox.Services.Exceptions;
using TwoFactorAuthNet;
using TwoFactorAuthNet.Providers.Qr;
using MultiGetEntry = Roblox.Dto.Users.MultiGetEntry;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services;

public class UsersService : ServiceBase, IService
{
    private static TwoFactorAuth tfa = new TwoFactorAuth("Pekora");
    public async Task<bool> IsNameAvailableForNameChange(long contextUserId, string username)
    {
        var escapedUsername = username
            .Replace("\\", "\\\\") 
            .Replace("%", "\\%") 
            .Replace("_", "\\_");

        var alreadyInUse = await db.QuerySingleOrDefaultAsync(
            "SELECT username FROM \"user\" WHERE username ILIKE :name ESCAPE '\\'",
            new { name = escapedUsername }
        );

        if (alreadyInUse != null)
            return false;

        // Check if it was used previously
        var usedPreviously = await db.QueryAsync<PreviousUsernameEntries>(
            "SELECT username, user_id as userId " +
            "FROM user_previous_username " +
            "WHERE username ILIKE :name ESCAPE '\\'",
            new { name = escapedUsername }
        );

        if (usedPreviously == null || !usedPreviously.Any())
            return true;

        var usedByAnyoneElse = usedPreviously.Where(c => c.userId != contextUserId);
        if (usedByAnyoneElse.Any())
            return false; // Someone else already used it

        return true;
    }

    public async Task<bool> IsNameAvailableForSignup(string username)
    {
        return await IsNameAvailableForNameChange(0, username);
    }

    public async Task<IAsyncDisposable> AcquireEconomyLock(long userId)
    {
        var result = await Cache.redLock.CreateLockAsync("EconomyLockV2:User:" + userId, TimeSpan.FromSeconds(5));
        if (!result.IsAcquired)
            throw new LockNotAcquiredException();
        return result;
    }

    public async Task<IAsyncDisposable> AcquireUserAssetLock(long userAssetId)
    {
        var result = await Cache.redLock.CreateLockAsync("EconomyLockUserAssetV1:UserAssetId:" + userAssetId, TimeSpan.FromSeconds(5));
        if (!result.IsAcquired)
            throw new LockNotAcquiredException();
        return result;
    }

    /// <summary>
    /// Acquire multiple locks and return a CombinedAsyncDisposable (which disposes all locks when disposed).
    /// </summary>
    /// <param name="arrayLength">The amount of times to call cb</param>
    /// <param name="cb">The function to call for each iteration of arrayLength. Must return a disposable lock.</param>
    /// <returns>A <see cref="CombinedAsyncDisposable"/> containing the locks</returns>
    private async Task<CombinedAsyncDisposable> MultiAcquireLock(int arrayLength, Func<int, Task<IAsyncDisposable>> cb)
    {
        var result = new CombinedAsyncDisposable();
        var pendingLocks = new List<Task<IAsyncDisposable>>();
        try
        {
            for (var i = 0; i < arrayLength; i++)
            {
                pendingLocks.Add(cb(i));
            }

            var finished = await Task.WhenAll(pendingLocks);
            result.AddChildren(finished);
            return result;
        }
        catch (Exception)
        {
            foreach (var item in pendingLocks)
            {
                if (!item.IsCompletedSuccessfully || item.Exception != null) continue;
                // This function needs to be very durable, so ignore dispose errors
                try
                {
                    await item.Result.DisposeAsync();
                }
                catch (Exception e)
                {
                    Writer.Info(LogGroup.DistributedLock, "Error releasing distributed lock for MultiAcquireLock after failure. error={0}\n{1}", e.Message, e.StackTrace);
                }
            }
            throw;
        }
    }

    public async Task<CombinedAsyncDisposable> MultiAcquireUserAssetLock(IEnumerable<long> userAssetIds)
    {
        var idsArray = userAssetIds.ToArray();
        return await MultiAcquireLock(idsArray.Length, i => AcquireUserAssetLock(idsArray[i]));
    }

    /// <summary>
    /// VerifyPassword will return true if the password specified matches the password stored in the database for the userId.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="password"></param>
    /// <returns>True if valid, otherwise false</returns>
    public async Task<bool> VerifyPassword(long userId, string password)
    {
        var dbPass = await db.QuerySingleOrDefaultAsync<PasswordEntry>("SELECT password FROM \"user\" WHERE id = :id", new
        {
            id = userId,
        });
        if (dbPass == null || string.IsNullOrEmpty(dbPass.password)) throw new RecordNotFoundException();
        var hasher = new PasswordHasher();
        return hasher.Verify(dbPass.password, password);
    }

    public async Task UpdatePassword(long userId, string newPassword)
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash(newPassword);
        await UpdateAsync("user", userId, new
        {
            password = hash,
        });
    }
    public async Task NullifyPassword(long userId)
    {
        await db.ExecuteAsync("UPDATE \"user\" SET password = '' WHERE id = :id", new
        {
            id = userId,
        });
    }
    public async Task<string> Generate2SVTicket(TwoFactorTicket info)
    {
        string ticket = "pekora2sv-v2:" + Guid.NewGuid().ToString();
        await redis.StringSetAsync(ticket, JsonSerializer.Serialize(info), TimeSpan.FromMinutes(1));
        return ticket;
    }
    public async Task<TwoFactorTicket> GetInfoFrom2SVTicket(string ticket)
    {
        string? info = await redis.StringGetAsync(ticket);
        if (info == null) throw new RecordNotFoundException();
        return JsonSerializer.Deserialize<TwoFactorTicket>(info)!;
    }
    public async Task DeleteTicket(string ticket)
    {
        await redis.KeyDeleteAsync(ticket);
    }
    public async Task<LoginTicet> GetLoginTicketInfo(string ticket)
    {
        string? info = await redis.StringGetAsync(ticket);
        if (info == null) throw new RecordNotFoundException();
        return JsonSerializer.Deserialize<LoginTicet>(info)!;
    }
    public async Task<string> GenerateLoginTicket(LoginTicet info)
    {
        string ticket = "pekora2svloginv2:" + Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
        await redis.StringSetAsync(ticket, JsonSerializer.Serialize(info), TimeSpan.FromSeconds(10));
        return ticket;
    }
    public async Task<TotpStatus> GetTotpStatus(long userId)
    {
        TotpStatus? status = await db.QuerySingleOrDefaultAsync<TotpStatus>("SELECT status FROM user_totp WHERE user_id = :id", new
        {
            id = userId,
        });
        if (status == null) 
            return TotpStatus.Disabled;
        return (TotpStatus)status;
    }
    public async Task<TotpInfo> GetTotp(long userId)
    {
        TotpInfo storedTotp = await db.QuerySingleOrDefaultAsync<TotpInfo>("SELECT secret, user_id as userId, status FROM user_totp WHERE user_id = :id", new
        {
            id = userId,
        });
        return storedTotp;
    }
    public async Task<TotpInfo> GetOrSetTotp(long userId)
    {
        var storedTotp = await db.QuerySingleOrDefaultAsync<TotpInfo>("SELECT secret, user_id as userId, status FROM user_totp WHERE user_id = :id", new
        {
            id = userId,
        });
        // no entry of user in the totp table
        if (storedTotp == null)
        {
            //create a new secret for the user
            string newSecret = tfa.CreateSecret(160);
            // set in db as disabled bcs user hasnt verified it yet
            await InsertAsync("user_totp", new
            {
                user_id = userId,
                secret = newSecret,
                status = (int)TotpStatus.Disabled,
            });
            // return the totpinfo
            return new TotpInfo()
            {
                userId = userId,
                secret = newSecret,
                status = TotpStatus.Disabled,
            };
        }
        // we have a result of the user lets return it
        return storedTotp;
    }

    public async Task UpdateTotpStatus(long userId, TotpStatus status)
    {
        await db.ExecuteAsync("UPDATE user_totp SET status = :status WHERE user_id = :id", new
        {
            id = userId,
            status = (int)status,
        });
    }
    public async Task DeleteTotp(long userId)
    {
        await db.ExecuteAsync("DELETE FROM user_totp WHERE user_id = :id", new
        {
            id = userId,
        });
    }

    public bool VerifyTotp(string secret, string code)
    {
        return tfa.VerifyCode(secret, code);
    }

    public string GetOtpQrCodeBase64(long userId, string secret)
    {
        return tfa.GetQrCodeImageAsDataUri("UserId: " + userId, secret, 600);
    }

    public byte[] GetOtpQrCode(long userId, string secret)
    {
        var qrCodeProvider = new QrServerQrCodeProvider();
        byte[] qrCode = qrCodeProvider.GetQrCodeImage(tfa.GetQrText($"UserId: {userId}", secret), 600);
        return qrCode;
    }

    public async Task UnlockAccount(long userId)
    {
        await db.ExecuteAsync("UPDATE \"user\" SET status = :status WHERE id = :id", new
        {
            id = userId,
            status = AccountStatus.Ok,
        });
    }

    public async Task DeleteUser(long userId, bool skipOnlineCheck)
    {
        var p = (await MultiGetPresence(new[] { userId })).First();
        var maxLastOnline = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
        if (p.lastOnline > maxLastOnline && !skipOnlineCheck)
        {
            throw new AccountLastOnlineTooRecentlyException();
        }
        var newUsername = "[ Account Deleted (" + userId + ") ]";
        var transferId = await GetUserIdFromUsername("BadDecisions");
        await InTransaction(async _ =>
        {
            // Delete user
            // todo: is creation date technically private info? should we be deleting that too?
            
            await db.ExecuteAsync(
                "UPDATE \"user\" SET username = :name, password = '', status = :status, discord_id = NULL, description = '[ Content Deleted ]', online_at = created_at WHERE id = :id",
                new
                {
                    id = userId,
                    name = newUsername,
                    status = AccountStatus.Forgotten,
                });
            // comments
            await db.ExecuteAsync("DELETE FROM asset_comment WHERE user_id = :id", new { id = userId });
            // favorites
            await db.ExecuteAsync("DELETE FROM asset_favorite WHERE user_id = :id", new { id = userId });
            // game votes
            await db.ExecuteAsync("DELETE FROM asset_vote WHERE user_id = :id", new { id = userId });
            // chat messages
            await db.ExecuteAsync("DELETE FROM user_conversation_message WHERE user_id = :id", new { id = userId });
            // chat read receipts
            await db.ExecuteAsync("DELETE FROM user_conversation_message_read WHERE user_id = :id", new { id = userId });
            // forum threads
            await db.ExecuteAsync(
                "UPDATE forum_post SET title = '[ Content Deleted ]' WHERE user_id = :id AND thread_id IS NULL",
                new { id = userId });
            // forum posts
            await db.ExecuteAsync("UPDATE forum_post SET post = '[ Content Deleted ]' WHERE user_id = :id", new
            {
                id = userId,
            });
            // statuses
            await db.ExecuteAsync("DELETE FROM user_status WHERE user_id = :id", new { id = userId });
            // messages
            // we have to do both "to" and "from" since "to" messages might contain quote replies
            await db.ExecuteAsync(
                "UPDATE user_message SET subject = '[ Content Deleted ]', body = '[ Content Deleted ]' WHERE user_id_to = :id OR user_id_from = :id",
                new { id = userId });
            // move items to baddecisions
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :id, price = 0 WHERE user_id = :user_id", new
            {
                id = transferId,
                user_id = userId,
            });
            // delete group stuff
            await db.ExecuteAsync("UPDATE group_wall SET content = '[ Content Deleted ]' where user_id = :user_id", new
            {
                user_id = userId,
            });
            await db.ExecuteAsync("UPDATE group_status SET status = '[ Content Deleted ]' WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            // delete app
            await db.ExecuteAsync("DELETE FROM join_application WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            // delete user previous usernames
            await db.ExecuteAsync("DELETE FROM user_previous_username WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            // delete user 2fa
            await db.ExecuteAsync("DELETE FROM user_totp WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            // delete referral
            await db.ExecuteAsync("DELETE FROM user_referral WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
            // TODO: Should we be deleting assets, games, and play history?
            // todo: should we delete/lock groups?
            // todo: should we delete transaction? not the entire data, but like clear the user_id or something?
            return 0;
        });
    }

    public async Task<long> GetUserIdFromUsername(string username)
    {
        username = username.Replace("%", "");
        var result = await db.QuerySingleOrDefaultAsync<UserId>("SELECT id as userId FROM \"user\" WHERE LOWER(username) = LOWER(:username)", new
        {
            username,
        });
        if (result == null || result.userId == 0) throw new RecordNotFoundException();
        return result.userId;
    }

    public async Task<bool> IsBadUsername(string usernameToCheck)
    {
        var escaped = usernameToCheck
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        var result = await db.QuerySingleOrDefaultAsync<Total>(
            @"SELECT COUNT(*) AS total 
          FROM moderation_bad_username 
          WHERE username ILIKE :name ESCAPE '\'",
            new { name = $"%{escaped}%" });

        return result.total != 0;
    }


    public async Task AddBadUsername(string usernameToAdd)
    {
        if (await IsBadUsername(usernameToAdd))
            return;

        await db.ExecuteAsync("INSERT INTO moderation_bad_username (username) VALUES (:name)", new
        {
            name = usernameToAdd,
        });
    }

    public async Task ResetUsername(long userId, long requesterUserId)
    {
        var newName = $"pekora_user_{userId}";
        await db.ExecuteAsync(
            "INSERT INTO moderation_bad_username_log (username, user_id, author_id) VALUES (:name, :id, :author)", new
            {
                name = newName,
                id = userId,
                author = requesterUserId,
            });
        await db.ExecuteAsync("UPDATE \"user\" SET username = :name WHERE id = :id", new
        {
            id = userId,
            name = newName,
        });
    }

    private static readonly string[] UsernameCannotStartOrEndWith = {
        // roblox rule
        "_",
        // whitespace makes it too easy to impersonate people (e.g. " builderman" and "builderman" would look the same)
        " ",
        // roblox rule
        ".",
    };

    private static readonly Regex UsernameValidationRegex = new Regex(@"^[a-zA-Z0-9_. ]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);


    /// <summary>
    /// Check if the username is valid
    /// </summary>
    /// <param name="nameToCheck"></param>
    /// <returns></returns>
    public async Task<bool> IsUsernameValid(string nameToCheck)
    {
        // check for null/empty/whitespace
        if (string.IsNullOrEmpty(nameToCheck) || string.IsNullOrWhiteSpace(nameToCheck)) return false;
        // check length
        var isInvalidValidLength = nameToCheck.Length >= 21 || nameToCheck.Length < 3;
        if (isInvalidValidLength) return false;
        // check start/end
        foreach (var badCharacter in UsernameCannotStartOrEndWith)
        {
            if (nameToCheck.StartsWith(badCharacter) || nameToCheck.EndsWith(badCharacter)) return false;
        }
        // check for invalid characters
        var normalizedNameArray = UsernameValidationRegex.Match(nameToCheck);
        if (!normalizedNameArray.Success) return false;
        var normalizedName = normalizedNameArray.Value;
        if (normalizedName != nameToCheck) return false;

        // check for duplicate whitespace
        for (var i = 1; i < normalizedName.Length; i++)
        {
            if (normalizedName[i-1] == ' ' && normalizedName[i] == ' ') return false;
        }
        // only one _ allowed
        if (nameToCheck.Count(c => c == '_') > 1) return false;

        // world filter, removing spaces and other words
        var lowerName = string.Join("", nameToCheck.ToLower().Split(" "));
        if (lowerName.Contains("nigg") || lowerName.Contains("n1gg") || lowerName.Contains("niigg") || lowerName.Contains("n11gg") || lowerName.Contains("gga") || lowerName.Contains("gger"))
            return false;
        if (lowerName.Contains("porn") || lowerName.Contains("p0rn"))
            return false;
        if (lowerName.Contains("kike") || lowerName.Contains("goy"))
            return false;
        if (lowerName.Contains("bitch") || lowerName.Contains("b1tch"))
            return false;        
        if (lowerName.Contains("dick") || lowerName.Contains("d1ck"))
            return false;
        if (lowerName.Contains("fuck") || lowerName.Contains("f1ck"))
            return false;
        if (lowerName.Contains("tranny") || lowerName.Contains("fag") || lowerName.Contains("goblina") || lowerName.Contains("dyke") || lowerName.Contains("dick") || lowerName.Contains("cock") || lowerName.Contains("c0ck") || lowerName.Contains("d1ck") || lowerName.Contains("hitler") || lowerName.Contains("hitier"))
            return false;


        // mod blocked && special char block
        if (await IsBadUsername(nameToCheck) || !IsAsciiUsername(nameToCheck))
            return false;


        return true;
    }
    private bool IsAsciiUsername(string username)
    {
        return username.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_'));
    }

    public bool IsPasswordValid(string passwordToValidate)
    {
        if (string.IsNullOrEmpty(passwordToValidate) || string.IsNullOrWhiteSpace(passwordToValidate)) return false;
        if (passwordToValidate.Length < 3) return false;
        return true;
    }

    public async Task<IEnumerable<PreviousUsernameEntry>> GetPreviousUsernames(long userId)
    {
        return await db.QueryAsync<PreviousUsernameEntry>(
            "SELECT username as username, created_at as createdAt FROM user_previous_username WHERE user_id = :id", new
            {
                id = userId,
            });

    }

    /// <summary>
    /// Charge the user username price, and change the user's username
    /// </summary>
    /// <param name="userId">The userId to update</param>
    /// <param name="newUsername">The players new username</param>
    /// <param name="oldUsername">The players previous username</param>
    /// <exception cref="NotEnoughRobuxForPurchaseException">User does not have enough Robux to purchase a username change</exception>
    public async Task ChangeUsername(long userId, string newUsername, string oldUsername)
    {
        await InTransaction(async _ =>
        {
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            var balance = await ec.GetUserBalance(userId);
            if (balance.robux < 1000)
                throw new NotEnoughRobuxForPurchaseException();

            // subtract from balance
            await ec.DecrementCurrency(CreatorType.User, userId, CurrencyType.Robux, 1000);

            // trans
            await InsertAsync("user_transaction", new
            {
                type = PurchaseType.Purchase,
                currency_type = 1,
                amount = 1000,
                // details
                old_username = oldUsername,
                new_username = newUsername,
                sub_type = TransactionSubType.UsernameChange,
                // user data
                user_id_one = userId,
                user_id_two = 1,
            });
            // insert current username
            await InsertAsync("user_previous_username", new
            {
                username = oldUsername,
                user_id = userId,
            });
            // update user table
            await UpdateAsync("user", userId, new
            {
                username = newUsername,
            });

            return 0;
        });
    }

    public async Task<IEnumerable<UserId>> SearchUsers(string? keyword, int limit, int offset)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            keyword = null;

        if (keyword == null)
        {
            // Just get online users
            return await db.QueryAsync<UserId>(
                "SELECT u.id AS userId from \"user\" u WHERE u.status = :status ORDER BY u.online_at DESC LIMIT :limit OFFSET :offset ", new
                {
                    status = AccountStatus.Ok,
                    limit,
                    offset,
                });
        }

        keyword = "%" + keyword + "%";

        var result =
            await db.QueryAsync<UserId>(
                "SELECT DISTINCT(t.userId) AS userId FROM (SELECT DISTINCT(u.id) as userId FROM \"user\" u WHERE u.username ILIKE :keyword AND u.status = :status UNION SELECT DISTINCT(p.user_id) as userId FROM user_previous_username p INNER JOIN \"user\" u2 ON u2.id = p.user_id WHERE p.username ILIKE :keyword AND u2.status = :status) as t", new
                {
                    status = AccountStatus.Ok,
                    keyword,
                    limit,
                    offset,
                });
        return result;
    }
    public async Task<UserInfo> GetUserByName(string username)
    {
        var res = await db.QuerySingleOrDefaultAsync<UserInfo>(
            "SELECT id AS userId, username, status AS accountStatus, created_at AS created, description " +
            "FROM \"user\" " +
            "WHERE LOWER(username) = LOWER(:username)",
            new { username = username }
        );

        if (res == null)
            throw new RecordNotFoundException();
        return res;
    }



    public async Task<UserInfo> GetUserById(long userId)
    {
        using var userInfoCache = ServiceProvider.GetOrCreate<GetUserByIdCache>();
        var (exists, cached) = userInfoCache.Get(userId);
        if (exists && cached != null)
            return cached;

        var res = await db.QuerySingleOrDefaultAsync<UserInfo>("SELECT id as userId, username, status as accountStatus, created_at as created, description, verified as isVerified FROM \"user\" WHERE id = :id", new { id = userId });
        if (res == null) throw new RecordNotFoundException();
        userInfoCache.Set(userId, res);
        return res;
    }
    public async Task<UserInfo> GetUserByDiscordId(string discordId)
    {
        var res = await db.QuerySingleOrDefaultAsync<UserInfo>("SELECT id as userId, username, status as accountStatus, created_at as created, description FROM \"user\" WHERE discord_id = :id", new { id = discordId });
        if (res == null) throw new RecordNotFoundException();
        return res;
    }
    public async Task<bool> IsUserLinked(long userId)
    {
        var res = await db.QuerySingleOrDefaultAsync<string?>("SELECT discord_id FROM \"user\" WHERE id = :id", new { id = userId});
        return res != null;
    }
    public async Task<IEnumerable<MultiGetAccountStatusEntry>> MultiGetAccountStatus(IEnumerable<long> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<MultiGetAccountStatusEntry>();

        var sql = new SqlBuilder();
        var t = sql.AddTemplate("SELECT id as userId, u.status as accountStatus FROM \"user\" u /**where**/");
        sql.OrWhereMulti("u.id = $1", ids);
        return await db.QueryAsync<MultiGetAccountStatusEntry>(t.RawSql, t.Parameters);
    }

    public async Task<IEnumerable<MultiGetEntry>> MultiGetUsersById(IEnumerable<long> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<MultiGetEntry>();

        var sql = new SqlBuilder();
        var t = sql.AddTemplate("SELECT id, u.username as name, u.username as displayName, u.description, u.created_at as created FROM \"user\" u /**where**/");
        sql.OrWhereMulti("u.id = $1", ids);
        return await db.QueryAsync<MultiGetEntry>(t.RawSql, t.Parameters);
    }

    public async Task<IEnumerable<MultiGetEntry>> MultiGetUsersByUsername(IEnumerable<string> usernames)
    {
        var names = usernames
            .Select(n => n.Trim())
            .ToList();

        var sb = new SqlBuilder();
        var selector = sb.AddTemplate(@"
        SELECT id, username, description, created_at
        FROM ""user""
        /**where**/
        ");

        foreach (var name in names)
        {
            sb.Where("LOWER(username) = LOWER(:username)", new { username = name });
        }

        var currentData = (await db.QueryAsync<MultiGetDbEntry>(
            selector.RawSql,
            selector.Parameters
        )).ToList();

        var usersNotInList = names
            .Where(name => currentData.Find(v =>
                v.username.Equals(name, StringComparison.OrdinalIgnoreCase)) == null)
            .ToList();

        if (usersNotInList.Count != 0)
        {
            foreach (var user in usersNotInList)
            {
                var exists = await db.QuerySingleOrDefaultAsync(
                    @"SELECT u.id, 
                         upu.username AS requestedUsername, 
                         u.username
                  FROM user_previous_username upu
                  LEFT JOIN ""user"" u ON u.id = upu.user_id
                  WHERE LOWER(upu.username) = LOWER(:username)
                  LIMIT 1",
                    new { username = user }
                );

                if (exists != null)
                {
                    currentData.Add(new MultiGetDbEntry
                    {
                        username = exists.username,
                        id = exists.id,
                        requestedUsername = user
                    });
                }
            }
        }

        return currentData.Select(c => new MultiGetEntry
        {
            id = c.id,
            name = c.username,
            requestedName = c.requestedUsername ?? c.username
        });
    }

    public async Task<StatusEntry> GetUserStatus(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<StatusEntry>(
            "SELECT status FROM user_status WHERE user_id = :user_id ORDER BY id DESC LIMIT 1", new
            {
                user_id = userId,
            });
        if (result == null) return new StatusEntry();
        return result;
    }

    public async Task SetUserStatus(long userId, string? newStatus)
    {
        var currentStatus = await GetUserStatus(userId);
        if (currentStatus.status == newStatus) return;
        // Only validate if the status is not null

        if (string.IsNullOrWhiteSpace(newStatus))
        {
            newStatus = null;
            // If old status is also empty, don't update it
            if (string.IsNullOrWhiteSpace(currentStatus.status)) return;
        }
        else
        {
            // Validation on non-null status
            if (newStatus.Length > 255) throw new StatusTooLongException();
            if (newStatus.Length <= 3) throw new StatusTooLongException();

            using var filter = ServiceProvider.GetOrCreate<FilterService>(this);
            newStatus = filter.CleanText(newStatus);
        }

        await InsertAsync("user_status", new
        {
            user_id = userId,
            status = newStatus,
        });
    }

    public async Task SetUserDescription(long userId, string newDescription)
    {
        await db.ExecuteAsync("UPDATE \"user\" SET description = :description WHERE id = :id", new
        {
            id = userId,
            description = newDescription,
        });
        using (var s = ServiceProvider.GetOrCreate<GetUserByIdCache>())
        {
            s.Remove(userId);
        }
    }

    private static string redisKeyPrefix = "sess:v1:";

    /// <summary>
    /// Create a session for the user. Returns the session id.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<string> CreateSession(long userId)
    {
        var sess = new SessionEntry
        {
            userId = userId,
            createdAt = DateTime.UtcNow,
        };
        var serialized = JsonSerializer.Serialize(sess);
        var id = Guid.NewGuid().ToString();
        await redis.StringSetAsync(redisKeyPrefix + id, serialized);
        return id;
    }

    public async Task DeleteSession(string sessionId)
    {
        await redis.KeyDeleteAsync(redisKeyPrefix + sessionId);
        using var sess = ServiceProvider.GetOrCreate<UserSessionsCache>();
        sess.Remove(sessionId);
    }

    public async Task<DateTime?> GetSessionExpiration(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<UserSessionExpirationEntry>("SELECT session_expired_at as sessionExpiredAt FROM \"user\" WHERE id = :id", new
        {
            id = userId,
        });
        return result?.sessionExpiredAt;
    }

    public async Task ExpireAllSessions(long userId)
    {
        await db.ExecuteAsync("UPDATE \"user\" SET session_expired_at = :session_expired_at WHERE id = :id", new
        {
            id = userId,
            session_expired_at = DateTime.UtcNow,
        });
    }

    public async Task<SessionEntry> GetSessionById(string sessionId)
    {
        using var sessCache = ServiceProvider.GetOrCreate<UserSessionsCache>();
        SessionEntry? mySess;
        var (exists, cached) = sessCache.Get(sessionId);
        if (exists)
        {
            mySess = cached;
        }
        else
        {
            var result = await redis.StringGetAsync(redisKeyPrefix + sessionId);
            if (result == null)
                throw new RecordNotFoundException();

            mySess = JsonSerializer.Deserialize<SessionEntry>(result);
            sessCache.Set(sessionId, mySess);

            if (mySess != null)
            {
                var expiration = await GetSessionExpiration(mySess.userId);
                if (expiration != null)
                {
                    // if created before expiration time, consider the session invalid
                    if (mySess.createdAt < expiration)
                    {
                        throw new RecordNotFoundException();
                    }
                }
            }
        }
        // If null, or created over a year ago, then force user to login again
        var consideredExpiredOnOrAfter = DateTime.UtcNow.Subtract(TimeSpan.FromDays(365));
        if (mySess == null || mySess.createdAt <= consideredExpiredOnOrAfter)
        {
            throw new RecordNotFoundException();
        }

        return mySess;
    }

    public async Task<bool> IsUserApproved(long userId)
    {
        using var sessCache = ServiceProvider.GetOrCreate<UserInviteCache>();
        var (exists, invite) = sessCache.Get(userId);
        if (!exists)
        {
            invite = await GetUserInvite(userId);
            sessCache.Set(userId, invite);
        }
        if (invite != null)
            return true;

        // Check for app
        using var appCache = ServiceProvider.GetOrCreate<UserApplicationCache>();
        var (appIsCached, app) = appCache.Get(userId);
        if (!appIsCached)
        {
            app = await GetApplicationByUserId(userId);
            appCache.Set(userId, app);
        }

        if (app is {status: UserApplicationStatus.Approved})
            return true;

        // Default
        return true;
    }
    /// <summary>
    /// Check duplicate discord id.
    /// </summary>
    /// <param name="discordId"></param>
    public async Task<bool> CheckDuplicateDiscord(string discordId)
    {
        var isDuplicate = await db.QuerySingleAsync<int>(
            "SELECT COUNT(discord_id) FROM join_application WHERE join_application.discord_id = :discord_id AND (join_application.status = :status1 OR join_application.status = :status2 OR join_application.status = :status3)",
            new { discord_id = discordId, status1 = UserApplicationStatus.Approved, status2 = UserApplicationStatus.SilentlyRejected, status3 = UserApplicationStatus.Pending }
        );

        return isDuplicate > 0;
    }
    public async Task LinkDiscordAccount(string discordId, long userId)
    {
        await db.ExecuteAsync(
            "UPDATE \"user\" SET discord_id = :discordid WHERE id = :uid",
            new
            {
                uid = userId,
                discordid = discordId,
            });

    }
    public async Task UnlinkDiscordAccount(long userId)
    {
        await db.ExecuteAsync(
            "UPDATE \"user\" SET discord_id = null WHERE id = :uid",
            new
            {
                uid = userId,
            });

    }
    public async Task<string> CreateApplication(CreateUserApplicationRequest request)
    {
        var applicationId = Guid.NewGuid().ToString();
        await InsertAsync("join_application", new
        {
            id = applicationId,
            preferred_name = "",
            request.about,
            social_presence = request.socialPresence,
            status = UserApplicationStatus.Pending,
            is_verified = request.isVerified,
            verified_url = request.verifiedUrl,
            verified_id = request.verifiedId,
            verification_phrase = request.verificationPhrase,
            discord_id = request.discordId,
            discord_username = request.discordUsername,
            reffered_by = request.refferedBy,
        });
        return applicationId;
    }

    public async Task<UserApplicationEntry?> GetApplicationById(string applicationId)
    {
        var (q,t) = GetApplicationQuery();
        q.Where("id = :id LIMIT 1", new
        {
            id = applicationId,
        });
        return await db.QuerySingleOrDefaultAsync<UserApplicationEntry>(t.RawSql, t.Parameters);
    }

    public async Task<UserApplicationEntry?> GetApplicationByJoinId(string joinId)
    {
        var (q,t) = GetApplicationQuery();
        q.Where("join_id = :join_id LIMIT 1", new
        {
            join_id = joinId,
        });
        return await db.QuerySingleOrDefaultAsync<UserApplicationEntry>(t.RawSql, t.Parameters);
    }

    public async Task<UserApplicationEntry?> GetApplicationByUserId(long userId)
    {
        var (q,t) = GetApplicationQuery();
        q.Where("user_id = :user_id LIMIT 1", new
        {
            user_id = userId,
        });
        return await db.QuerySingleOrDefaultAsync<UserApplicationEntry>(t.RawSql, t.Parameters);
    }

    public async Task SetApplicationUserIdByJoinId(string joinId, long userId)
    {
        await db.ExecuteAsync("UPDATE join_application SET user_id = :user_id, updated_at = now() WHERE user_id IS NULL AND join_id = :id AND status = :st", new
        {
            st = UserApplicationStatus.Approved,
            id = joinId,
            user_id = userId,
        });
    }

    public async Task<long> CountPendingApplications()
    {
        return (await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM join_application WHERE status = :status",
            new
            {
                status = UserApplicationStatus.Pending,
            })).total;
    }

    private (SqlBuilder, SqlBuilder.Template) GetApplicationQuery()
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate("SELECT id, join_id as joinId, about, social_presence as socialPresence, user_id as userId, author_id as authorId, reject_reason as rejectionReason, status, created_at as createdAt, updated_at as updatedAt, is_verified as isVerified, verified_url as verifiedUrl, verification_phrase as verificationPhrase, discord_id as discordId, discord_username as discordUsername, reffered_by as refferedBy FROM join_application /**where**/ /**orderby**/");
        return (q, t);
    }

    private async Task ReleaseApplicationLocks(long userId)
    {
        await db.ExecuteAsync(
            "UPDATE join_application SET locked_by_user_id  = null, locked_at = null WHERE locked_by_user_id = :user_id",
            new
            {
                user_id = userId,
            });
    }

    public async Task AcquireApplicationLocks(long userId, IEnumerable<string> applicationIds)
    {
        foreach (var id in applicationIds)
        {
            await db.ExecuteAsync(
                "UPDATE join_application SET locked_at = :d, locked_by_user_id = :user_id WHERE id = :id", new
                {
                    d = DateTime.UtcNow,
                    id,
                    user_id = userId,
                });
        }
    }

    public async Task<IEnumerable<UserApplicationEntry>> GetApplications(UserApplicationStatus? status, int offset, SortOrder sortOrder, long? contextUserId, string? searchQuery = null, ApplicationSearchColumn? searchColumn = null)
    {
        await using var getAppsLock = await Cache.redLock.CreateLockAsync("GetApplicationsV1", TimeSpan.FromSeconds(5));
        if (!getAppsLock.IsAcquired)
            throw new LockNotAcquiredException();

        if (contextUserId != null)
            await ReleaseApplicationLocks(contextUserId.Value);
        var (q,t) = GetApplicationQuery();
        q.OrderBy("created_at " + sortOrder.ToSql() + " LIMIT :limit OFFSET :offset", new
        {
            offset,
            limit = 10,
        });
        // admin js will constantly ping when apps are locked
        // if (contextUserId != null)
        // {
        //     var expired = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(30));
        //     q.Where("(locked_at < :e OR locked_at IS NULL)", new
        //     {
        //         e = expired,
        //     });
        // }

        if (status != null)
        {
            q.Where("status = :status", new
            {
                status,
            });
        }
        // intentionally allow wildcards like "%"
        if (searchColumn != null)
        {
            switch (searchColumn)
            {
            case ApplicationSearchColumn.About:
                q.Where("about ILIKE :q", new { q = searchQuery });
                break;
            case ApplicationSearchColumn.Name:
                q.Where("preferred_name ILIKE :q", new { q = searchQuery });
                break;
            case ApplicationSearchColumn.SocialUrl:
                q.Where("social_presence ILIKE :q", new { q = searchQuery });
                break;
            case ApplicationSearchColumn.Actioner:
                q.Where("author_id = :q", new { q = searchQuery });
                break;
            case ApplicationSearchColumn.DiscordId:
                q.Where("discord_id = :q", new { q = searchQuery });
                break;
            }
        }
        var result = (await db.QueryAsync<UserApplicationEntry>(t.RawSql, t.Parameters)).ToList();
        if (contextUserId != null)
        {
            await AcquireApplicationLocks(contextUserId.Value, result.Select(c => c.id));
        }
        return result;
    }

    public async Task ClearApplication(string applicationId)
    {
        await db.ExecuteAsync(
            "UPDATE join_application SET about = '[ Content Deleted ]', social_presence = '[ Content Deleted ]', verified_url = '[ Content Deleted ]' WHERE id = :id",
            new
            {
                id = applicationId
            });
    }

    public async Task<string?> ProcessApplication(string applicationId, long contextUserId, UserApplicationStatus status, string? rejectionReason = null)
    {
        var isAccepted = status == UserApplicationStatus.Approved;
        var acceptId = isAccepted ? Guid.NewGuid().ToString() : null;
        await db.ExecuteAsync("UPDATE join_application SET status = :st, author_id = :author, updated_at = now(), join_id = :join_id, reject_reason = :reason WHERE id = :id", new
        {
            author = contextUserId,
            st = status,
            id = applicationId,
            join_id = acceptId,
            reason = rejectionReason,
        });
        await db.ExecuteAsync(
            "INSERT INTO moderation_change_join_app (application_id, author_user_id, new_status) VALUES (:id, :user_id, :new_status)",
            new
            {
                id = applicationId,
                user_id = contextUserId,
                new_status = status,
            });
        return acceptId;
    }

    public ApplicationRedemptionFailureReason CanRedeemApplication(UserApplicationEntry? app)
    {
        if (app == null)
            return ApplicationRedemptionFailureReason.DoesNotExist;
        if (app.createdAt <= DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)))
            return ApplicationRedemptionFailureReason.Expired;
        if (app.userId is not null)
            return ApplicationRedemptionFailureReason.AlreadyAssociatedWithUser;

        return ApplicationRedemptionFailureReason.Ok;
    }

    public async Task<ApplicationRedemptionFailureReason> CanRedeemApplication(string applicationId)
    {
        var app = await GetApplicationByJoinId(applicationId);
        return CanRedeemApplication(app);
    }
    public async Task<long?> GetUserRefferedBy(string applicationId)
    {
        var app = await GetApplicationByJoinId(applicationId);
        return app?.refferedBy;
    }

    public async Task<bool> IsDuplicateSocialId(string id)
    {
        var (q, t) = GetApplicationQuery();
        q.Where("verified_id ILIKE :url AND status != :status", new
        {
            status = UserApplicationStatus.Rejected, // skip rejected since it could be user re-submitting
            url = "%" + id + "%",
        }).OrderBy("id LIMIT 1");
        var result = await db.QuerySingleOrDefaultAsync<Dto.Users.UserApplicationEntry?>(t.RawSql, t.Parameters);
        if (result != null)
            return true;
        return false;
    }

    public async Task DeleteUnusedApplicationsWithSameUrl(string verifiedIdentifier)
    {
        await db.ExecuteAsync("DELETE FROM join_application WHERE verified_id = :url AND status = :status AND user_id is null", new
        {
            url = verifiedIdentifier,
            status = UserApplicationStatus.Approved,
        });
    }

    public async Task DeleteUnusedAppsWithSameUrlUnverified(string socialUrl)
    {
        await db.ExecuteAsync("DELETE FROM join_application WHERE social_presence = :url AND status = :status AND user_id is null", new
        {
            url = socialUrl,
            status = UserApplicationStatus.Approved,
        });
    }

    public async Task<Dto.Users.UserApplicationEntry?> IsDuplicateSocialUrl(AppSocialMedia entry)
    {
        var ident = entry.identifier;
        var (q, t) = GetApplicationQuery();
        q.Where("social_presence ILIKE :url AND status != :status", new
        {
            status = UserApplicationStatus.Rejected, // skip rejected since it could be user re-submitting
            url = "%" + ident + "%",
        }).OrderBy("id LIMIT 1");
        var result = await db.QuerySingleOrDefaultAsync<Dto.Users.UserApplicationEntry?>(t.RawSql, t.Parameters);
        if (result == null)
        {
            // try verified url
            (q, t) = GetApplicationQuery();
            q.Where("verified_url ILIKE :url AND status != :status AND is_verified", new
            {
                status = UserApplicationStatus.Rejected, // skip rejected since it could be user re-submitting
                url = entry.identifier,
            }).OrderBy("id LIMIT 1");
            result = await db.QuerySingleOrDefaultAsync<Dto.Users.UserApplicationEntry?>(t.RawSql, t.Parameters);
        }
        return result;
    }

    public async Task<UserId> CreateUser(string username, string password, Gender gender, long? overrideUserId = null)
    {
        if (!Enum.IsDefined(gender))
            throw new ArgumentException(nameof(gender) + " is invalid: " + gender);

        var result = await InTransaction(async _ =>
        {
            // Confirm still doesn't exist
            var nameTaken = await db.QuerySingleOrDefaultAsync("SELECT id FROM \"user\" WHERE username ILIKE :username",
                new
                {
                    username,
                });
            if (nameTaken != null)
                throw new Exception("Username was taken");

            var hasher = new PasswordHasher();
            long userId;
            if (overrideUserId != null)
            {
                userId = (long)overrideUserId;
                var exists =
                    await db.QuerySingleOrDefaultAsync("SELECT id FROM \"user\" WHERE id = :id", new { id = userId });
                if (exists != null)
                    throw new Exception("UserID is already taken");

                await InsertAsync("user", new
                {
                    username,
                    password = hasher.Hash(password),
                    id = userId,
                });
            }
            else
            {
                var h = hasher.Hash(password);
                userId = await InsertAsync("user", new
                {
                    username,
                    password = h,
                });
            }

            // Account settings
            await InsertAsync("user_settings", "user_id", new
            {
                user_id = userId,
                theme = 1,
                gender = (int)gender,
                private_message_privacy = GeneralPrivacy.All,
                inventory_privacy = InventoryPrivacy.AllUsers,
                trade_privacy = GeneralPrivacy.All,
            });
            // Balance
            await InsertAsync("user_economy", "user_id", new
            {
                user_id = userId,
                balance_tickets = 0,
                balance_robux = 100,
            });

            // First transaction
            await InsertAsync("user_transaction", new
            {
                amount = 100,
                type = PurchaseType.BuildersClubStipend,
                currency_type = 1,
                user_id_one = userId,
                user_id_two = 1,
            });

            // Default avatar
            await InsertAsync("user_avatar", "user_id", new
            {
                user_id = userId,
                avatar_type = 1,
                thumbnail_url = (string?)null,
                headshot_thumbnail_url = (string?)null,
                head_color_id = 194,
                torso_color_id = 23,
                right_arm_color_id = 194,
                left_arm_color_id = 194,
                right_leg_color_id = 102,
                left_leg_color_id = 102,
            });
			
			// social urls
		    await InsertAsync("user_connections", "user_id", new
            {
                user_id = userId,
                created_at = DateTime.UtcNow,
				updated_at = DateTime.UtcNow,
            });

            foreach (var id in Roblox.Configuration.SignupAssetIds)
            {
                await CreateUserAsset(userId, id);
            }

            return new UserId
            {
                userId = userId,
            };
        });
        // Render outside transaction to prevent deadlock/unnecessary locks
#if !DEBUG
        using var av = ServiceProvider.GetOrCreate<AvatarService>();
        await av.RedrawAvatar(result.userId, Roblox.Configuration.SignupAvatarAssetIds);
#endif
        return result;
    }

    public async Task<long> CreateUserAsset(long userId, long assetId)
    {
        var result =
            await db.QuerySingleOrDefaultAsync(
                "INSERT INTO user_asset (user_id, asset_id) VALUES (:user_id, :asset_id) RETURNING id", new
                {
                    user_id = userId,
                    asset_id = assetId,
                });
        return (long) result.id;
    }
    
    public async Task DeleteUserAsset(long userId, long assetId)
    {
        await db.QuerySingleOrDefaultAsync(
            "DELETE FROM user_asset WHERE user_id = :userId AND asset_id = :assetId", new
            {
                userId,
                assetId,
            });
    }

    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetUserAssets(long userId, long assetId)
    {
        var result = await db.QueryAsync<CollectibleUserAssetEntry>(
            "SELECT id as userAssetId, user_id as userId, asset_id as assetId, price, serial, created_at as createdAt, updated_at as updatedAt FROM user_asset WHERE user_id = :user_id AND asset_id = :asset_id",
            new
            {
                user_id = userId,
                asset_id = assetId,
            });
        return result;
    }

    public async Task<int> CountUserAssetsForAsset(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT count(*) AS total FROM user_asset WHERE asset_id = :asset_id",
            new
            {
                asset_id = assetId,
            });
        return result.total;
    }

    public async Task<int> CountSoldCopiesForAsset(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) as total FROM user_transaction ut WHERE ut.asset_id = :asset_id AND ut.type = :sale_type AND ut.sub_type = :sub_sale_type",
            new
            {
                asset_id = assetId,
                sale_type = PurchaseType.Purchase,
                sub_sale_type = TransactionSubType.ItemPurchase,
            });
        return result.total;
    }

    public async Task<CollectibleUserAssetEntry> GetUserAssetById(long userAssetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<CollectibleUserAssetEntry>(
            "SELECT id as userAssetId, user_id as userId, asset_id as assetId, price, serial, created_at as createdAt, updated_at as updatedAt FROM user_asset WHERE id = :id",
            new
            {
                id = userAssetId,
            });
        if (result == null) throw new RecordNotFoundException();
        return result;
    }

    public async Task SetPriceOfUserAsset(long userAssetId, long userId, long newPrice)
    {
        if (newPrice < 1 && newPrice != 0)
            throw new ArgumentException("Price must be at least 1 Robux or 0");

        await InTransaction(async _ =>
        {
            await using var userAssetLock = await AcquireUserAssetLock(userAssetId);

            var currentOwner = await GetUserAssetById(userAssetId);
            if (currentOwner.userId != userId)
                throw new RobloxException(401, 0, "Cannot change the price of this item");

            await UpdateAsync("user_asset", userAssetId, new
            {
                price = newPrice,
            });
            return 0;
        });
    }

    public async Task<EconomySummary> GetTransactionSummary(long userId, DateTime minCreationDate)
    {
        var allTransactions = await db.QueryAsync<SummaryEntryDb>(
            "SELECT type, amount, currency_type as currency FROM user_transaction WHERE user_id_one = :user_id AND created_at >= :date", new
            {
                user_id = userId,
                date = minCreationDate,
            });
        var response = new EconomySummary();
        foreach (var item in allTransactions)
        {
            if (item.currency != CurrencyType.Robux)
                continue;
            if (item.type == PurchaseType.Sale)
            {
                response.itemSaleRobux += item.amount;
            }
            else if (item.type == PurchaseType.BuildersClubStipend)
            {
                response.recurringRobuxStipend += item.amount;
            }
            else if (item.type == PurchaseType.GroupPayouts)
            {
                response.groupPayoutRobux += item.amount;
            }
        }

        return response;
    }

    public async Task<bool> HasUserPurchasedAssetBefore(long userId, long assetId)
    {
        return (await db.QuerySingleOrDefaultAsync<Dto.Total>("SELECT COUNT(*) AS total FROM user_transaction WHERE user_id_one = :user_id AND asset_id = :asset_id AND type = :type AND sub_type = :sub_type AND deleted = true", new
        {
            user_id = userId,
            asset_id = assetId,
            type = PurchaseType.Purchase,
            sub_type = TransactionSubType.ItemPurchase,
        })).total > 0;
    }

    private async Task<UserEconomy> GetTotalCurrencyExchangedWithInvitedUsers(long userId, TimeSpan period)
    {
        var result = new UserEconomy()
        {
            tickets = 0,
            robux = 0,
        };
        var ts = DateTime.UtcNow.Subtract(period);
        var invited = await GetInvitesByUser(userId);
        var userIdsInvited = invited.Where(c => c.userId != null).Select(c => c.userId).ToArray();
        if (userIdsInvited.Length == 0)
            return result;
        var builder = new SqlBuilder();
        var t = builder.AddTemplate("SELECT amount, currency_type, type FROM user_transaction /**where**/", new
        {
            user_id = userId,
            time = ts,
        });
        builder.OrWhereMulti("(user_transaction.user_id_one = :user_id AND user_transaction.user_id_two = $1 AND user_transaction.created_at >= :time)",
            userIdsInvited);
        foreach (var item in await db.QueryAsync(t.RawSql, t.Parameters))
        {
            var transactionType = (PurchaseType) item.type;
            if (transactionType != PurchaseType.Sale) continue;
            var currency = (CurrencyType) item.currency_type;
            if (currency == CurrencyType.Robux)
            {
                result.robux += (int) item.amount;
            }
            else
            {
                result.tickets += (int) item.amount;
            }
        }
        return result;
    }

    private async Task<PurchaseAbuseFailureReason> CanAssetBePurchased(long assetId, long buyerUserId, CurrencyType currency)
    {
        // basic heuristics to detect abusive activity.
        // this function is only for normal purchases. not to be used with uaid purchases.
        // for a similar function, see TradesService.CanTradeBeCompeted()
        // list of things we currently try to detect:
        //  - people alt hoarding newly released collectible items
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        var details = await assets.GetAssetCatalogInfo(assetId);
        var restrictions = details.itemRestrictions.ToArray();
        var isLimited = restrictions.Contains("Limited") || restrictions.Contains("LimitedUnique");

        // don't check free items
        if (details.isForSale && details.price == 0 && currency == CurrencyType.Robux)
            return PurchaseAbuseFailureReason.Ok;

        var sellerId = details.creatorTargetId;
        // don't check Roblox or UGC items
        if ((sellerId is 1 or 2) && details.creatorType == CreatorType.User && !isLimited)
            return PurchaseAbuseFailureReason.Ok;

        if (details.creatorType == CreatorType.Group)
        {
            // just check group creator for now
            using var groups = ServiceProvider.GetOrCreate<GroupsService>();
            var groupData = await groups.GetGroupById(details.creatorTargetId);
            if (groupData.owner == null)
                return PurchaseAbuseFailureReason.Ok; // TODO
            sellerId = groupData.owner.userId;
        }
 
        Writer.Info(LogGroup.AbuseDetection, "CanAssetBePurchased true");

        return PurchaseAbuseFailureReason.Ok;
    }
    
    // ok most of this can and should be cleaned up this is fucking disgusting
    private async Task<PurchaseAbuseFailureReason> CanDevProductBePurchased(long productId, long buyerUserId, CurrencyType currency)
    {
        // basic heuristics to detect abusive activity.
        // this function is only for normal purchases. not to be used with uaid purchases.
        // for a similar function, see TradesService.CanTradeBeCompeted()
        // list of things we currently try to detect:
        //  - people alt hoarding newly released collectible items
        currency = CurrencyType.Robux;
        using var games = ServiceProvider.GetOrCreate<GamesService>(this);
        var details = await games.GetDeveloperProductInfoFull(productId);
        if (details == null) {
            return PurchaseAbuseFailureReason.Unknown;
        }

        // don't check free items
        if (details.isForSale && details.price == 0)
            return PurchaseAbuseFailureReason.Ok;

        var sellerId = details.creatorId;
        // don't check Roblox or UGC items
        if (sellerId is 1 or 2 && details.creatorType == CreatorType.User)
            return PurchaseAbuseFailureReason.Ok;

        if (details.creatorType == CreatorType.Group) {
            Console.WriteLine("CreatorType cannot be a group!");
            return PurchaseAbuseFailureReason.Unknown;
        }
        Writer.Info(LogGroup.AbuseDetection, "CanAssetBePurchased true");

        return PurchaseAbuseFailureReason.Ok;
    }
    public async Task PurchaseNormalItem(long userIdBuyer, long assetId, CurrencyType expectedCurrency)
    {
        using var log = Writer.CreateWithId(LogGroup.ItemPurchase);
        log.Info($"PurchaseNormalItem start. buyer={userIdBuyer} assetId={assetId}");
       // await BeforePurchase(userIdBuyer, assetId);

        var canPurchase = await CanAssetBePurchased(assetId, userIdBuyer, expectedCurrency);
        if (canPurchase != PurchaseAbuseFailureReason.Ok)
        {
            log.Info("cannot purchase asset. CanAssetBePurchased returned {0}", canPurchase.ToString());
            throw new RobloxException(400, 0, "Cannot purchase asset at this time. Try again later.");
        }
        // Acquire a lock on the assetId before starting
        await using var redLock = await Cache.redLock.CreateLockAsync("PurchaseAsset:" + assetId, TimeSpan.FromSeconds(10));
        if (!redLock.IsAcquired)
            throw new RobloxException(429, 0, "TooManyRequests");
        log.Info($"got PurchaseAsset lock");

        await InTransaction(async _ =>
        {
            // Double check that user still doesn't own item yet
            var ownedCopies = (await GetUserAssets(userIdBuyer, assetId)).ToList();
            if (ownedCopies.Count != 0)
            {
                EconomyMetrics.ReportUserAlreadyOwnsItemDuringPurchase(log.GetLoggedStrings(), userIdBuyer, assetId);
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.UserAlreadyOwnsBeforePurchase);
            }
            log.Info("owned copies len = {0}", ownedCopies.Count);
            // This is ugly but I can't think of another way
            var assetDetails = await db.QuerySingleOrDefaultAsync<MinimalCatalogEntry>("SELECT id as assetId, is_for_sale as isForSale, price_robux as priceRobux, price_tix as priceTickets, is_limited as isLimited, is_limited_unique as isLimitedUnique, sale_count as saleCount, serial_count as serialCount, creator_id as creatorId, creator_type as creatorType, offsale_at as offsaleAt, asset_type as assetType FROM asset WHERE id = :id", new
            {
                id = assetId,
            });
            if (assetDetails == null)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetDoesNotExist);

            var isExpired = assetDetails.offsaleAt != null && assetDetails.offsaleAt <= DateTime.UtcNow;
            if (!assetDetails.isForSale)
            {
                Console.WriteLine(assetId);
                EconomyMetrics.ReportItemNoLongerForSaleDuringPurchase(log.GetLoggedStrings(), userIdBuyer, assetDetails.assetId);
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetNotForSale);
            }

            if (isExpired)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetExpired);

            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            await using var buyerLock = await ec.AcquireEconomyLock(CreatorType.User, userIdBuyer);
            // Check balance
            var userBalance = await ec.GetUserBalance(userIdBuyer);
            var balance = expectedCurrency == CurrencyType.Robux ? userBalance.robux : userBalance.tickets;
            var realPrice = expectedCurrency == CurrencyType.Robux
                ? assetDetails.priceRobux
                : assetDetails.priceTickets;
            // Null means not for sale
            if (realPrice == null)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetPriceIsNull);

            if (realPrice is < 0)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetPriceLessThanZero);

            if (balance < realPrice)
            {
                if (expectedCurrency == CurrencyType.Robux)
                    EconomyMetrics.ReportUserDoesNotHaveEnoughRobuxDuringPurchase(log.GetLoggedStrings(), userIdBuyer, assetId, balance, assetDetails.priceRobux ?? 0);
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.BalanceLessThanPrice);
            }
            log.Info("buyer balance = {0} item price = {1}", balance, assetDetails.priceRobux);

            // Not all groups have an economy yet. Create if required.
            if (assetDetails.creatorType == CreatorType.Group)
            {
                await ec.CreateGroupBalanceIfRequired(assetDetails.creatorId);
                log.Info("seller is group, created balance if required");
            }

            // This is the serialNumber the user will get
            int? serialNumber = null;
            if (assetDetails.isLimitedUnique)
            {
                log.Info("item has a serial");

                // If item has a specific copy count that can be sold, confirm it hasn't run out
                var saleCount = await CountSoldCopiesForAsset(assetId);
                if (assetDetails.serialCount != 0 && saleCount >= assetDetails.serialCount)
                {
                    EconomyMetrics.ReportItemStockExhaustedDuringPurchase(log.GetLoggedStrings(), userIdBuyer,
                        assetId, assetDetails.serialCount, saleCount);
                    throw new InternalPurchaseFailureException(InternalPurchaseFailReason.AssetStockExhausted); // Unlikely to be hit
                }
                // User gets saleCount+1 serial number
                serialNumber = saleCount + 1;
                log.Info("give buyer serial = {0} (saleCount = {1})", serialNumber, saleCount);
                // If this would make the asset the final sale, mark as no longer for sale
                if (assetDetails.serialCount != 0 && saleCount + 1 >= assetDetails.serialCount)
                {
                    log.Info("marking item as no longer for sale. serialCount = {0} saleCount = {1}", assetDetails.serialCount, saleCount+1);
                    await db.ExecuteAsync("UPDATE asset SET is_for_sale = false WHERE id = :id", new
                    {
                        id = assetId,
                    });
                }
            }
            // Create user asset
            var userAssetId = await InsertAsync("user_asset", new
            {
                user_id = userIdBuyer,
                asset_id = assetDetails.assetId,
                serial = serialNumber,
            });
            log.Info("created userAsset id = {0}", userAssetId);
            // If this is a package, we have to grant assetIds
            if (assetDetails.assetType == Type.Package)
            {
                log.Info("this is a package. adding package assets.");
                using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
                foreach (var id in await assets.GetPackageAssets(assetId))
                {
                    var owned = await GetUserAssets(userIdBuyer, id);
                    if (!owned.Any())
                    {
                        var packageUserAssetId = await InsertAsync("user_asset", new
                        {
                            user_id = userIdBuyer,
                            asset_id = id,
                            serial = (int?)null,
                        });
                        log.Info("added assetId {0} to user: {1}", id, packageUserAssetId);
                    }
                    else
                    {
                        log.Info("user already owns an asset from this package: {0}", id);
                    }
                }
            }
            // Only do economy changes on non-free items
            long amountToSeller = 0;
            if (realPrice != 0)
            {
                // Subtract price from buyer
                Debug.Assert(realPrice != null);
                await ec.DecrementCurrency(CreatorType.User, userIdBuyer, expectedCurrency, realPrice.Value);
                log.Info("currency is {0}", expectedCurrency);
                log.Info("subtracted amount from buyer. price = {0}", assetDetails.priceRobux);

                amountToSeller = (long)(0.7 * realPrice);
                log.Info("item is not free. amount to seller = {0}", amountToSeller);

                if (amountToSeller <= 0)
                    amountToSeller = 0;

                if (amountToSeller != 0)
                {
                    // Increment seller balance
                    await ec.IncrementCurrency(assetDetails.creatorType, assetDetails.creatorId, expectedCurrency, amountToSeller);
                }
            }

            // Create buyer transaction
            var buyerTransaction = await ec.InsertTransaction(new AssetPurchaseTransaction(userIdBuyer, assetDetails.creatorType,
                assetDetails.creatorId, expectedCurrency, realPrice ?? 0, assetDetails.assetId, userAssetId));
            log.Info("created buyerTransaction {0}", buyerTransaction);
            // Create transaction for seller
            var sellerTransaction = await ec.InsertTransaction(new AssetSaleTransaction(userIdBuyer, assetDetails.creatorType,
                assetDetails.creatorId, expectedCurrency, amountToSeller, assetDetails.assetId, userAssetId));
            log.Info("created sellerTransaction {0}", sellerTransaction);
            // Increment sales count. Reliability isn't super important here since this is just used for cache.
            using var assetsService = ServiceProvider.GetOrCreate<AssetsService>(this);
            await assetsService.IncrementSaleCount(assetId);
            log.Info("PurchaseItem success");
            // Finally, metrics
            if (assetDetails.priceRobux > 0)
                EconomyMetrics.ReportRobuxVolumeChange(assetDetails.priceRobux.Value);

            return 0;
        });
    }
    
    public async Task<Guid> PurchaseDeveloperProduct(long userIdBuyer, long productId)
    {
        using var log = Writer.CreateWithId(LogGroup.ItemPurchase);
        log.Info($"PurchaseDeveloperProduct start. buyer={userIdBuyer} productId={productId}");

        var canPurchase = await CanDevProductBePurchased(productId, userIdBuyer, CurrencyType.Robux);
        if (canPurchase != PurchaseAbuseFailureReason.Ok)
        {
            log.Info("cannot purchase dev product. CanDevProductBePurchased returned {0}", canPurchase.ToString());
            throw new RobloxException(400, 0, "Cannot purchase developer product at this time. Try again later.");
        }
        // Acquire a lock on the assetId before starting
        await using var redLock = await Cache.redLock.CreateLockAsync("PurchaseDeveloperProduct:" + productId, TimeSpan.FromSeconds(10));
        if (!redLock.IsAcquired)
            throw new RobloxException(429, 0, "TooManyRequests");
        log.Info($"got PurchaseDeveloperProduct lock");

        return await InTransaction(async _ =>
        {
            using var games = ServiceProvider.GetOrCreate<GamesService>(this);
            var productDetails = await games.GetDeveloperProductInfoFull(productId);
            if (productDetails == null)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.DeveloperProductDoesNotExist);

            if (!productDetails.isForSale)
            {
                EconomyMetrics.ReportDevProductNoLongerForSaleDuringPurchase(log.GetLoggedStrings(), userIdBuyer, productDetails.id);
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.DeveloperProductNotForSale);
            }

            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            await using var buyerLock = await ec.AcquireEconomyLock(CreatorType.User, userIdBuyer);
            // Check balance
            var userBalance = await ec.GetUserBalance(userIdBuyer);
            var balance = userBalance.robux;
            var realPrice = productDetails.price;

            if (realPrice < 0)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.DeveloperProductPriceLessThanZero);

            if (balance < realPrice)
            {
                EconomyMetrics.ReportUserDoesNotHaveEnoughRobuxDuringPurchase(log.GetLoggedStrings(), userIdBuyer, productId, balance, productDetails.price);
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.BalanceLessThanPrice);
            }
            log.Info("buyer balance = {0} dev product price = {1}", balance, productDetails.price);
            
            // Only do economy changes on non-free items
            long amountToSeller = 0;
            if (realPrice != 0)
            {
                // Subtract price from buyer
                await ec.DecrementCurrency(CreatorType.User, userIdBuyer, CurrencyType.Robux, productDetails.price);
                log.Info("currency is {0}", CurrencyType.Robux);
                log.Info("subtracted amount from buyer. price = {0}", productDetails.price);

                amountToSeller = (long)(0.7 * realPrice);
                log.Info("item is not free. amount to seller = {0}", amountToSeller);

                if (amountToSeller <= 0)
                    amountToSeller = 0;

                if (amountToSeller != 0)
                {
                    // Increment seller balance
                    await ec.IncrementCurrency(productDetails.creatorType, productDetails.creatorId, CurrencyType.Robux, amountToSeller);
                }
            }
            
            // Create buyer transaction
            var buyerTransaction = await ec.InsertTransaction(new DevProdPurchaseTransaction(userIdBuyer, productDetails.creatorType,
                productDetails.creatorId, CurrencyType.Robux, realPrice, productDetails.name));
            log.Info("created buyerTransaction {0}", buyerTransaction);
            
            // Create transaction for seller
            var sellerTransaction = await ec.InsertTransaction(new DevProdSaleTransaction(userIdBuyer, productDetails.creatorType,
                productDetails.creatorId, CurrencyType.Robux, realPrice, productDetails.name));
            log.Info("created sellerTransaction {0}", sellerTransaction);

            Guid transactionId = Guid.NewGuid();

            await games.IncrementDevProdSales(productId);
            await games.CreateProductReceipt(transactionId, userIdBuyer, productId, realPrice);
            log.Info("PurchaseDeveloperProduct success");

            return transactionId;
        });
    }
    
    /*
    private async Task BeforeResalePurchase(long userIdBuyer, long userAssetId)
    {
        // the purpose of this function is so i can be a snipe botter without ddosing my own site >:D
        const long ownerUserId = 12; // todo: get from appsettings
        if (userIdBuyer == ownerUserId) return;
        var data = await GetUserAssetById(userAssetId);
        if (data.userId != ownerUserId && data.price <= 10)
        {
            try
            {
                await PurchaseResellableItem(ownerUserId, userAssetId);
            }
            catch (Exception e)
            {
                // ignore but log
                Writer.Info(LogGroup.ItemPurchase, "attempt to buy for admin failed: {0}", e.Message);
            }
        }
    }
*/
    public async Task<long> GetMaximumCopyCount(long assetId)
    {
        var totalResult = await db.QuerySingleOrDefaultAsync<Dto.Total>("SELECT COUNT(*) as total FROM user_asset WHERE asset_id = :assetId", new
        {
            assetId = assetId,
        });
        var totalInExistence = totalResult.total;
        var maxCopies = (long) Math.Truncate(totalInExistence * 0.1);
        return Math.Clamp(maxCopies, 2, 100);
    }

    public async Task PurchaseResellableItem(long userIdBuyer, long userAssetId)
    {
        var log = Writer.CreateWithId(LogGroup.ItemPurchaseResale);
        log.Info("PurchaseResellableItem start. buyer = {0} userAssetId = {1}", userIdBuyer, userAssetId);
        // UserAsset lock
        await using var userAssetLock = await AcquireUserAssetLock(userAssetId);
        // Buyer lock
        await using var buyerLock = await AcquireEconomyLock(userIdBuyer);

        await InTransaction(async _ =>
        {
            // Double check that everything is still valid
            var userAsset = await GetUserAssetById(userAssetId);
            if (userAsset.price == 0)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.UserAssetPriceIsZero);
            if (userAsset.userId == userIdBuyer)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.UserAssetBuyerAndSellerAreSame);
            if (userAsset.price < 1)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.UserAssetPriceIsLessThanOne);
            log.Info("price = {0} sellerId = {1}", userAsset.price, userAsset.userId);
            var copies = await GetUserAssets(userIdBuyer, userAsset.assetId);
            var maxPossibleCopies = await GetMaximumCopyCount(userAsset.assetId);
            if (copies.Count() >= maxPossibleCopies)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason
                    .UserWouldExceedMaximumCopiesIfPurchased);

            // Check balance
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            var buyerBalanceOriginal = await ec.GetUserRobux(userIdBuyer);
            if (buyerBalanceOriginal < userAsset.price)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.BalanceLessThanPrice);
            var expectedBuyerBalanceAfterSale = buyerBalanceOriginal - userAsset.price;
            if (expectedBuyerBalanceAfterSale < 0)
                throw new InternalPurchaseFailureException(InternalPurchaseFailReason.BalanceWouldBeLessThanZeroAfterSale);
            var transactionStartTime = DateTime.UtcNow;
            // Transfer the item
            await UpdateAsync("user_asset", userAsset.userAssetId, new
            {
                price = 0,
                user_id = userIdBuyer,
                updated_at = transactionStartTime,
            });
            log.Info("userAsset marked as no longer for sale");
            // Subtract balance from buyer
            await ec.DecrementCurrency(CreatorType.User, userIdBuyer, CurrencyType.Robux, userAsset.price);
            log.Info("subtracted {0} from buyer", userAsset.price);
            // Triple check
            var newBalance = await ec.GetUserRobux(userIdBuyer);;
            log.Info("buyer new balance = {0}",newBalance);
            if (newBalance != expectedBuyerBalanceAfterSale)
                throw new Exception("Branch 4 (Critical) - Somebody REALLY broke a lock!");
            // Add amount to seller
            var amountToAdd = (long)Math.Floor(0.7 * userAsset.price);
            log.Info("add {0} to seller", amountToAdd);
            if (amountToAdd != 0)
            {
                await ec.IncrementCurrency(CreatorType.User, userAsset.userId, CurrencyType.Robux, amountToAdd);
            }
            log.Info("added {0} to seller",amountToAdd);
            // Update item RAP
            var itemRap = await db.QuerySingleOrDefaultAsync<RecentAveragePrice>("SELECT recent_average_price as recentAveragePrice FROM asset WHERE id = :id",
                new
                {
                    id = userAsset.assetId,
                });
            var nullableRap = itemRap.recentAveragePrice;
            var rap = nullableRap ?? 0;
            log.Info("item old RAP was {0}", rap);
            // From TS: "Math.trunc" is used instead of "Math.floor" since it's what Roblox+ uses, and I'm sure webgl3d knows what the actual Roblox code looks like to calculate RAP
            // let newRap = Math.trunc(currentRap - (currentRap - price) / 10);
            var newRap = rap - (rap - userAsset.price) / 10;
            if (rap == 0)
            {
                // apparently this is what roblox does. Rolimons, Roblox+, etc, all do this
                newRap = userAsset.price;
                log.Info("item had RAP of zero, so new rap = {0}", newRap);
            }
            log.Info("new itemRap = {0}", newRap);
            // Update the RAP
            await db.ExecuteAsync("UPDATE asset SET recent_average_price = :rap WHERE id = :id", new
            {
                id = userAsset.assetId,
                rap = newRap,
            });
            log.Info("item RAP successfully updated");
            // Create history entry for chart
            var id = await InsertAsync("collectible_sale_logs", new
            {
                asset_id = userAsset.assetId,
                amount = userAsset.price,
            });
            log.Info("inserted collectible_sale_logs id = {0}", id);
            // Create transaction for buyer
            var buyerTransaction = await ec.InsertTransaction(new AssetResalePurchaseTransaction(userIdBuyer,
                userAsset.userId, CurrencyType.Robux, userAsset.price, userAsset.assetId, userAsset.userAssetId));
            log.Info("created buyerTransaction id = {0}", buyerTransaction);
            // Create transaction for seller
            var sellerTransaction = await ec.InsertTransaction(new AssetReSaleTransaction(userIdBuyer, userAsset.userId,
                CurrencyType.Robux, amountToAdd, userAsset.assetId, userAsset.userAssetId));
            log.Info("created sellerTransaction id = {0}", sellerTransaction);
            log.Info("purchase success");
            // Finally, metrics
            EconomyMetrics.ReportRobuxVolumeChange(userAsset.price);
            return 0;
        });
    }

    public async Task<IEnumerable<UserAssetForSaleEntry>> GetResellers(long assetId)
    {
        return await db.QueryAsync<UserAssetForSaleEntry>(
             "SELECT user_asset.id as userAssetId, user_asset.user_id as userId, serial as serialNumber, price, asset_id as assetId, \"user\".username FROM user_asset INNER JOIN \"user\" ON \"user\".id = user_asset.user_id WHERE user_asset.price > 0 AND asset_id = :asset_id ORDER BY user_asset.price", new
             {
                 asset_id = assetId,
             });
    }

    public async Task<IEnumerable<PresenceEntry>> MultiGetPresence(IEnumerable<long> userIds)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return Array.Empty<PresenceEntry>();

        var sql = new SqlBuilder();
        var t = sql.AddTemplate(@"
        SELECT 
            ""user"".id as userId,
            ""user"".online_at as onlineAt,
            asset_server_player.asset_id as currentPlaceId,
            asset_server_player.server_id as currentGameId,
            ua.universe_id as currentUniverseId,
            u.root_asset_id as rootPlaceId
        FROM ""user""
        LEFT JOIN asset_server_player ON asset_server_player.user_id = ""user"".id
        LEFT JOIN universe_asset ua ON asset_server_player.asset_id = ua.asset_id
        LEFT JOIN universe u ON ua.universe_id = u.id
        /**where**/
        LIMIT 1000");


        foreach (var item in ids)
        {
            sql.OrWhere("\"user\".id = " + item);
        }

        var presenceData = await db.QueryAsync<DbPresenceEntry>(t.RawSql, t.Parameters);
        var results = new List<PresenceEntry>();
        foreach (var item in presenceData)
        {
            var userId = item.userId;
            var isOnline = item.onlineAt >= DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
            var placeId = item.currentPlaceId;
            var universeId = item.currentUniverseId;
            var rootPlaceId = item.rootPlaceId;
            var currentGameId = item.currentGameId;
            var result = new PresenceEntry
            {
                userId = userId,
                userPresenceType = placeId != null ? PresenceType.InGame :
                    isOnline ? PresenceType.Online : PresenceType.Offline,
                lastLocation = placeId != null ? "Playing" : "Website",
                rootPlaceId = rootPlaceId,
                gameId = currentGameId.ToString(),
                placeId = placeId,
                lastOnline = placeId != null ? DateTime.UtcNow : item.onlineAt,
            };
            results.Add(result);
        }

        return results;
    }

    public async Task EarnDailyTickets(long userId)
    {
        // todo: config: daily tickets and timespan should be configurable via appsettings
        var dailyTickets = 10;
        var stipendTimespan = TimeSpan.FromDays(1);

        // redis is faster than opening a transaction on every page visit, so we need to check that first
        var redisKey = "dailytickets:v1:" + userId;
        if ((await redis.StringGetAsync(redisKey)) == null)
        {
            // User already got daily robux in the past timespan, so do nothing
            return;
        }

        // WEB-36
        var l = "TicketsStipendLockV1:" + userId;
        await using var robuxLock = await Cache.redLock.CreateLockAsync(l, TimeSpan.FromSeconds(5));
        if (!robuxLock.IsAcquired) return;

        await InTransaction(async trx =>
        {
            var time = DateTime.UtcNow;
            var lastTransaction = await db.QuerySingleOrDefaultAsync<Total>(
                "SELECT COUNT(*) AS total FROM user_transaction WHERE user_id_one = :id AND type = :type AND created_at >= :time", new
                {
                    id = userId,
                    type = PurchaseType.TicketsStipend,
                    time = time.Subtract(stipendTimespan),
                }, transaction: trx);

            if (lastTransaction.total == 0)
            {
                // increment balance, create transaction
                using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
                await ec.IncrementCurrency(CreatorType.User, userId, CurrencyType.Tickets, dailyTickets);
                await InsertAsync("user_transaction", new
                {
                    type = PurchaseType.TicketsStipend,
                    user_id_one = userId,
                    user_id_two = 1,
                    currency_type = 1,
                    amount = dailyTickets,
                });
                // redis
                await redis.StringSetAsync(redisKey, "{}", stipendTimespan);
            }

            return 0;
        });
    }

    public async Task EarnDailyRobuxNoVirusNoScamHindiSubtitles(long userId, bool isStaff)
    {
        var membershipType = await GetUserMembership(userId);
        if (membershipType == null)
            return;
        var metadata = MembershipMetadata.GetMetadata(membershipType.membershipType);
        var dailyRobux = isStaff ? 85 : metadata.dailyRobux;
        if (dailyRobux == 0)
            return;

        var stipendTimespan = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(55));

        // redis is faster than opening a transaction on every page visit, so we need to check that first
        var redisKey = "dailyrobux:v1:" + userId;
        if ((await redis.StringGetAsync(redisKey)) != null)
        {
            // User already got daily robux in the past timespan, so do nothing
            return;
        }

        // WEB-36
        var l = "BuildersClubStipendLockV1:" + userId;
        await using var robuxLock = await Cache.redLock.CreateLockAsync(l, TimeSpan.FromSeconds(5));
        if (!robuxLock.IsAcquired) return;

        await InTransaction(async trx =>
        {
            var time = DateTime.UtcNow;
            var lastTransaction = await db.QuerySingleOrDefaultAsync<Total>(
                "SELECT COUNT(*) AS total FROM user_transaction WHERE user_id_one = :id AND type = :type AND created_at >= :time", new
                {
                    id = userId,
                    type = PurchaseType.BuildersClubStipend,
                    time = time.Subtract(stipendTimespan),
                }, trx);

            if (lastTransaction.total == 0)
            {
                // increment balance, create transaction
                await db.ExecuteAsync(
                    "UPDATE user_economy SET balance_robux = balance_robux + :amt WHERE user_id = :id", new
                    {
                        id = userId,
                        amt = dailyRobux,
                    });
                await InsertAsync("user_transaction", new
                {
                    type = PurchaseType.BuildersClubStipend,
                    user_id_one = userId,
                    user_id_two = 1,
                    currency_type = 1,
                    amount = dailyRobux,
                });
                // redis
                await redis.StringSetAsync(redisKey, "{}", stipendTimespan);
            }

            return 0;
        });
    }

    private static readonly Mutex OnlineStatusUpdateMux = new();
    private static readonly Dictionary<long, bool> OnlineStatusUpdatedList = new();

    public bool TrySetOnlineTimeUpdated(long userId)
    {
        bool result;
        lock (OnlineStatusUpdateMux)
        {
            if (OnlineStatusUpdatedList.ContainsKey(userId))
                return false;
            OnlineStatusUpdatedList[userId] = true;
            result = true;
        }

        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            lock (OnlineStatusUpdateMux)
            {
                OnlineStatusUpdatedList.Remove(userId);
            }
        });

        return result;
    }

    public async Task UpdateOnlineStatus(long userId)
    {
        await db.ExecuteAsync("UPDATE \"user\" SET online_at = :t WHERE id = :id ", new
        {
            t = DateTime.UtcNow,
            id = userId,
        });
    }

    public async Task<IEnumerable<FeedEntry>> MultiGetLatestStatus(IEnumerable<long> userIds, int limit)
    {
        var ids = userIds.ToList();
        if (ids.Count == 0) return Array.Empty<FeedEntry>();
        var q = new SqlBuilder();
        var t = q.AddTemplate("SELECT user_status.id as feedId, user_status.user_id as userId, user_status.status as content, user_status.created_at as createdAt, u.username FROM user_status INNER JOIN \"user\" AS u ON u.id = user_status.user_id /**where**/ order by user_status.created_at DESC LIMIT :limit", new
        {
            limit,
        });
        q.OrWhereMulti("(user_status.user_id = $1 AND user_status.status IS NOT NULL)", ids);

        return (await db.QueryAsync<UserFeedEntryDb>(t.RawSql, t.Parameters)).Select(c => new FeedEntry
        {
            feedId = c.feedId,
            type = CreatorType.User,
            content = c.content,
            created = c.createdAt,
            user = new()
            {
                id = c.userId,
                name = c.username,
            }
        });
    }

    public async Task<UserBanEntry> GetBanData(long userId)
    {
        var result =
            await db.QuerySingleOrDefaultAsync<UserBanEntry>(
                "SELECT created_at as createdAt, expired_at as expiredAt, reason FROM user_ban WHERE user_id = :id ORDER BY id DESC LIMIT 1", new { id = userId });
        if (result is null) throw new RecordNotFoundException();
        return result;
    }

    public async Task DeleteBan(long userId)
    {
        await db.ExecuteAsync("DELETE FROM user_ban WHERE user_id = :user_id", new
        {
            user_id = userId,
        });
        await db.ExecuteAsync("UPDATE \"user\" SET status = :s WHERE id = :id", new
        {
            id = userId,
            s = AccountStatus.Ok,
        });
    }

    private string GetAlertKey()
    {
        return "GlobalAlert:v2";
    }

    public async Task<Alert?> GetGlobalAlert()
    {
        var result = await redis.StringGetAsync(GetAlertKey());
        if (result == null)
            return null;
        return JsonSerializer.Deserialize<Alert>(result);
    }

    public async Task SetGlobalAlert(string? newMessage, string? newUrl)
    {
        if (newMessage == null)
        {
            await redis.KeyDeleteAsync(GetAlertKey());
            return;
        }

        await redis.StringSetAsync(GetAlertKey(), JsonSerializer.Serialize(new Alert()
        {
            url = newUrl,
            message = newMessage,
        }));
    }

    public async Task<WebsiteYear> GetYear(long userId)
    {
        using var s = ServiceProvider.GetOrCreate<UserYearCache>();
        var (exists, year) = s.Get(userId);
        if (exists)
            return year;

        var result = await redis.StringGetAsync("useryeartheme:v1:" + userId);
        if (result == null)
        {
            s.Set(userId, WebsiteYear.Year2016);
            return WebsiteYear.Year2016;
        }
        var value = Enum.Parse<WebsiteYear>(result);
        s.Set(userId, value);
        return value;
    }

    public async Task SetYear(long userId, WebsiteYear year)
    {
        await redis.StringSetAsync("useryeartheme:v1:" + userId, year.ToString());
        using var s = ServiceProvider.GetOrCreate<UserYearCache>();
        s.Set(userId, year);
    }

    public async Task<long> CountCreatedUsers(TimeSpan? afterSpan)
    {
        if (afterSpan == null)
            return (await db.QuerySingleOrDefaultAsync<Total>("SELECT COUNT(*) AS total FROM \"user\"")).total;
        return (await db.QuerySingleOrDefaultAsync<Total>("SELECT COUNT(*) AS total FROM \"user\" WHERE created_at >= :dt", new
        {
            dt = DateTime.UtcNow.Subtract(afterSpan.Value),
        })).total;
    }

    private static Mutex is18OrOverMapMux { get; } = new();
    private static Dictionary<long, bool> is18OrOver { get; } = new();

    public async Task<bool> Is18Plus(long userId)
    {
        lock (is18OrOverMapMux)
        {
            if (is18OrOver.ContainsKey(userId))
                return is18OrOver[userId];
        }

        var result = await db.QuerySingleOrDefaultAsync<User18OrOver>("SELECT is_18_plus as is18Plus FROM \"user\" WHERE id = :id ", new
        {
            id = userId,
        });

        if (result == null)
            return false;

        lock (is18OrOverMapMux)
        {
            is18OrOver[userId] = result.is18Plus;
        }

        return result.is18Plus;
    }

    public async Task MarkAs18Plus(long userId)
    {
        lock (is18OrOverMapMux)
        {
            is18OrOver[userId] = true;
        }

        await Database.connection.ExecuteAsync("UPDATE \"user\" SET is_18_plus = true WHERE id = :id", new
        {
            id = userId,
        });
    }

    public async Task<IEnumerable<UserInviteEntry>> GetInvitesByUser(long userId)
    {
        return await db.QueryAsync<UserInviteEntry>("SELECT * FROM user_invite WHERE author_id = :id", new
        {
            id = userId,
        });
    }

    public async Task<UserInviteEntry?> GetUserInvite(long userId)
    {
        return await db.QuerySingleOrDefaultAsync<UserInviteEntry>("SELECT * FROM user_invite WHERE user_id = :id", new
        {
            id = userId,
        });
    }

    public async Task DeleteInvite(string inviteId)
    {
        await db.ExecuteAsync("DELETE FROM user_invite WHERE id = :id", new
        {
            id = inviteId,
        });
    }

    public async Task DeleteUserInvite(long userId)
    {
        await db.ExecuteAsync("DELETE FROM user_invite WHERE user_id = :id", new
        {
            id = userId,
        });
    }
    public async Task<bool> IsInviteCreationFloodChecked(long userId)
    {
        var creationCount = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM user_invite WHERE author_id = :author_id AND (created_at >= :created_at OR user_id IS NULL)", new
            {
                created_at = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                author_id = userId,
            });
        return creationCount.total != 0;
    }
    public async Task<bool> CanCreateInvite(long userId)
    {
        var app = await GetApplicationByUserId(userId);
        if (app == null || app.status != UserApplicationStatus.Approved)
            return false;
        return true;
    }

    public async Task CreateInvite(long authorUserId)
    {
#if RELEASE
        var floodChecked = await IsInviteCreationFloodChecked(authorUserId);
        if (floodChecked)
            throw new RobloxException(403, 0, "Too many invites created. Try again tomorrow.");
#endif
        var canCreate = await CanCreateInvite(authorUserId);
        if (!canCreate)
            throw new RobloxException(401, 0, "Unauthorized");
        var userInfo = await GetUserById(authorUserId);
        if (userInfo.created > DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)))
            throw new RobloxException(403, 0, "Account is too new to invite users. Try again tomorrow.");

        await db.ExecuteAsync(
            "INSERT INTO user_invite (id, user_id, author_id, created_at, updated_at) VALUES (:id, :user_id, :author_id, :created_at, :created_at)",
            new
            {
                created_at = DateTime.UtcNow,
                id = Guid.NewGuid().ToString(),
                user_id = (long?) null,
                author_id = authorUserId,
            });
    }

    public async Task<UserInviteEntry?> GetInviteById(string inviteId)
    {
        return await db.QuerySingleOrDefaultAsync<UserInviteEntry?>("SELECT * FROM user_invite WHERE id = :id", new
        {
            id = inviteId,
        });
    }

    public async Task SetUserInviteId(long userId, string inviteId)
    {
        await db.ExecuteAsync("UPDATE user_invite SET user_id = :user_id WHERE id = :id AND user_id IS NULL", new
        {
            user_id = userId,
            id = inviteId,
        });
    }

    public async Task<UserMembershipEntry?> GetUserMembership(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<UserMembershipEntry?>(
            "SELECT user_id as userId, membership_type as membershipType, created_at as createdAt, updated_at as updatedAt FROM user_membership WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
        return result;
    }
    public async Task<Referral?> GetReferralCode(string referralCode)
    {
        var result = await db.QuerySingleOrDefaultAsync<Referral?>(
            "SELECT user_id as userId, referral_code as referralCode FROM user_referral WHERE referral_code = :referral_code", new
            {
                referral_code = referralCode.ToLower(),
            });
        if (result == null)
            return null;
        return result;
    }
    public async Task<Referral?> GetUserReferral(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Referral?>(
            "SELECT user_id as userId, referral_code as referralCode FROM user_referral WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
        if (result == null)
            return null;
        return result;
    }
    public async Task CreateReferralCode(long userId, string referralCode)
    {
        await InTransaction(async _ =>
        {
       
            await db.ExecuteAsync("INSERT INTO user_referral (user_id, referral_code) VALUES (:user_id, :referral_code)", new
            {
                user_id = userId,
                referral_code = referralCode.ToLower(),
            });

            return 0;
        });
    }
    public async Task<long?> GetReferralCodeUseCount(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long?>(
            "SELECT COUNT(*) as total FROM join_application WHERE reffered_by = :id AND status = :status", new
            {
                id = userId,
                status = (int)UserApplicationStatus.Approved,
            });
        return result ?? 0;
    }
    public async Task<string> GetUserMemberShipAsString(long userId)
    {
        UserMembershipEntry? result = await GetUserMembership(userId);
        if (result == null)
            return "None";
        return result.membershipType.ToString();
    }
    private async Task UpdateMembership(long userId, MembershipType newMembershipType)
    {
        await db.ExecuteAsync(
            "UPDATE user_membership SET membership_type = :type, updated_at = :time WHERE user_id = :user_id", new
            {
                type = newMembershipType,
                time = DateTime.UtcNow,
                user_id = userId,
            });
    }

    private async Task InsertMembership(long userId, MembershipType membershipType)
    {
        await db.ExecuteAsync("INSERT INTO user_membership (user_id, membership_type) VALUES (:user_id, :type)", new
        {
            user_id = userId,
            type = membershipType,
        });
    }

    private async Task<IAsyncDisposable> GetUpdateMembershipLock(long userId)
    {
        var result = await Cache.redLock.CreateLockAsync("UpdateUserMembership:V1:" + userId, TimeSpan.FromSeconds(5));
        if (!result.IsAcquired)
            throw new LockNotAcquiredException();
        return result;
    }

    public async Task InsertOrUpdateMembership(long userId, MembershipType newMembershipType)
    {
        if (!Enum.IsDefined(newMembershipType))
            throw new ArgumentException("Invalid " + nameof(newMembershipType));

        await using var memLock = await GetUpdateMembershipLock(userId);
        await InTransaction(async _ =>
        {
            var exists = await GetUserMembership(userId);
            if (exists != null)
            {
                await UpdateMembership(userId, newMembershipType);
            }
            else
            {
                await InsertMembership(userId, newMembershipType);
            }

            return 0;
        });
    }

    private static Object staffMux { get; } = new();
    private static Dictionary<long, bool> staffDic { get; } = new();

    public static bool IsUserStaff(long userId)
    {
        lock (staffMux)
        {
            return staffDic.ContainsKey(userId) && staffDic[userId];
        }
    }

    public void SetIsUserStaff(long userId, bool isStaff)
    {
        lock (staffMux)
        {
            if (isStaff)
                staffDic[userId] = true;
            else
                staffDic.Remove(userId);
        }
    }

    public async Task<IEnumerable<UserId>> GetAllStaff()
    {
        return await db.QueryAsync<UserId>(
            "SELECT distinct user_id as userId FROM user_permission");
    }

    public async Task<IEnumerable<StaffUserPermissionEntry>> GetStaffPermissions(long userId)
    {
        return await db.QueryAsync<StaffUserPermissionEntry>(
            "SELECT user_id as userId, permission FROM user_permission WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
    }

    public async Task AddStaffPermission(long userId, Access permission)
    {
        await db.ExecuteAsync("INSERT INTO user_permission (user_id, permission) VALUES (:user_id, :permission) ON CONFLICT (user_id, permission) DO NOTHING", new
        {
            user_id = userId,
            permission = permission,
        });
    }

    public async Task RemoveStaffPermission(long userId, Access permission)
    {
        await db.ExecuteAsync("DELETE FROM user_permission WHERE user_id = :user_id AND permission = :permission", new
        {
            user_id = userId,
            permission = permission,
        });
    }

    public async Task<string> CreatePasswordResetEntry(long userId, string socialUrl, string verificationPhrase)
    {
        // double check
        if (string.IsNullOrWhiteSpace(socialUrl))
            throw new ArgumentException(nameof(socialUrl) + " cannot be null");
        if (string.IsNullOrWhiteSpace(verificationPhrase))
            throw new ArgumentException(nameof(verificationPhrase) + " cannot be null");

        var uuid = Guid.NewGuid().ToString();
        await db.ExecuteAsync("INSERT INTO user_password_reset (user_id, id, created_at, status, social_url, verification_phrase) VALUES (:user_id, :id, :created_at, :status, :social_url, :verification_phrase)", new
        {
            user_id = userId,
            id = uuid,
            social_url = socialUrl,
            verification_phrase = verificationPhrase,
            created_at = DateTime.UtcNow,
            status = PasswordResetState.Created,
        });
        return uuid;
    }

    public async Task<PasswordResetEntry?> GetPasswordResetEntry(string id)
    {
        return await db.QuerySingleOrDefaultAsync<PasswordResetEntry>(
            "SELECT id, user_id as userId, created_at as createdAt, status FROM user_password_reset WHERE id = :id", new
            {
                id = id,
            });
    }

    public async Task<IAsyncDisposable> GetPasswordResetLock(string id)
    {
        var result = await Cache.redLock.CreateLockAsync("PasswordReset:V1:" + id, TimeSpan.FromSeconds(5));
        if (!result.IsAcquired)
            throw new LockNotAcquiredException();
        return result;
    }

    public async Task RedeemPasswordReset(string id, string newPassword)
    {
        // First, get the ticket and update its state
        var ticket = await GetPasswordResetEntry(id);
        if (ticket == null)
            throw new ArgumentException("Invalid " + nameof(id));

        var updated = await db.ExecuteAsync("UPDATE user_password_reset SET status = :status WHERE id = :id AND status = :old_status", new
        {
            id = id,
            old_status = PasswordResetState.Created,
            status = PasswordResetState.PasswordChanged,
        });
        if (updated != 1)
            throw new ArgumentException("Password reset was already redeemed");

        await UpdatePassword(ticket.userId, newPassword);

        // Unlock the account, if required
        var info = await GetUserById(ticket.userId);
        if (info.accountStatus == AccountStatus.MustValidateEmail)
        {
            // Account can be unlocked now.
            // TODO: is this safe?
            await UnlockAccount(ticket.userId);
        }
    }
    public async Task GiveUserInviterBadge(long userId)
    {
        using var accountService = ServiceProvider.GetOrCreate<AccountInformationService>(this);
        var badges = await accountService.GetUserBadges(userId);
        if (badges.Any(b => b.id == 8))
        {
            return;
        }
        await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
        {
            user_id = userId,
            badge_id = 8,
        });
    }
    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}