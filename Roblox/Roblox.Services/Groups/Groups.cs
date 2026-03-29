using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Roblox.Dto;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Users;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.Groups;
using Roblox.Services.App.Groups;
using Roblox.Services.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StatusEntry = Roblox.Dto.Groups.StatusEntry;

namespace Roblox.Services;

public class GroupsService : ServiceBase, IService
{
    public async Task<bool> DoesUserHavePermission(long userId, long groupId, GroupPermission permission)
    {
        var groupInfo = await GetGroupById(groupId);
        if (groupInfo.isLocked)
            return false;

        var role = await GetUserRoleInGroup(groupId, userId);
        // Check owner
        if (permission == GroupPermission.Owner)
            return role.rank == 255;

        // Check permissions
        if (role.HasPermission(permission))
            return true;
        // Does not have permission
        return false;
    }

    public async Task<long> GetMemberCount(long groupId)
    {
        var roles = await db.QueryAsync("SELECT member_count FROM group_role WHERE group_id = :id", new
        {
            id = groupId,
        });
        return roles.Select(c => (long) c.member_count).Sum();
    }

    public async Task<IEnumerable<SearchGroupEntry>> SearchGroups(string searchQuery, string? cursor, int limit)
    {
        searchQuery = FilterKeyword(searchQuery);
        var offset = int.Parse(cursor ?? "0");
        var result = (await db.QueryAsync<SearchGroupEntry>(
            "SELECT id, name, description, created_at as created, updated_at as updated FROM \"group\" WHERE name ilike :name LIMIT :limit OFFSET :offset",
            new
            {
                name = "%" + searchQuery + "%",
                limit,
                offset,
            })).ToList();
        foreach (var item in result)
        {
            item.memberCount = await GetMemberCount(item.id);
        }

        return result;
    }

    public async Task<int> CountUserGroups(long userId)
    {
        var groups = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM group_user WHERE user_id = :user_id", new
            {
                user_id = userId,
            });
        return groups.total;
    }
    public async Task<long> GetGroupIdByName(string name)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT id FROM \"group\" g WHERE g.name = :name", new
        {
            name,
        });
        return result.id;
    }
    public async Task<GroupEntry> GetGroupById(long groupId)
    {
        var dbResult = await db.QuerySingleOrDefaultAsync<GroupEntryDb>(
            "SELECT g.id, g.name, g.description, g.user_id as ownerUserId, \"user\".username as ownerUserName, g.locked as isLocked, g.verified as isVerified FROM \"group\" as g LEFT JOIN \"user\" ON \"user\".id = g.user_id WHERE g.id = :group_id",
            new
            {
                group_id = groupId,
            });
        if (dbResult == null) throw new RecordNotFoundException();
        var resp = new GroupEntry(dbResult);
        resp.memberCount = await GetMemberCount(dbResult.id);
        return resp;
    }
    public async Task<bool> IsUserInGroup(long userId, long groupId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT id FROM group_user WHERE user_id = :user_id AND group_id = :group_id", new
        {
            user_id = userId,
            group_id = groupId,
        });
        return result != null;
    }

    private async Task<StatusEntry?> GetGroupStatus(long groupId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT group_status.id, group_status.status, group_status.created_at as created, updated_at as updated, group_status.user_id, username FROM group_status INNER JOIN \"user\" ON \"user\".id = user_id WHERE group_id = :gid ORDER BY group_status.id DESC LIMIT 1", new
        {
            gid = groupId,
        });
        if (result == null) return null;
        return new StatusEntry
        {
            body = result.status,
            poster = new GroupUser
            {
                userId = result.user_id,
                username = result.username,
                displayName = result.username,
            },
            created = result.created,
            updated = result.updated,
        };
    }

    public async Task<GroupWithShout> GetGroupWithShoutById(long groupId)
    {
        var dbResult = await db.QuerySingleOrDefaultAsync<GroupEntryDb>(
            "SELECT g.id, g.name, g.description, g.user_id as ownerUserId, \"user\".username as ownerUserName, g.locked as isLocked, g.verified as isVerified FROM \"group\" as g LEFT JOIN \"user\" ON \"user\".id = g.user_id WHERE g.id = :group_id",
            new
            {
                group_id = groupId,
            });
        if (dbResult == null) throw new RecordNotFoundException();

        var mem = await GetMemberCount(groupId);
        var shout = await GetGroupStatus(groupId);

        return new()
        {
            id = dbResult.id,
            name = dbResult.name,
            description = dbResult.description,
            memberCount = mem,
            isVerified = dbResult.isVerified,
            owner = dbResult.ownerUserId != null
                ? new()
                {
                    userId = (long) dbResult.ownerUserId,
                    username = dbResult.ownerUsername ?? "",
                    displayName = dbResult.ownerUsername ?? "",
                }
                : null,
            shout = shout,
        };
    }

    public async Task<IEnumerable<RoleEntry>> GetRolesInGroup(long groupId)
    {
        return await db.QueryAsync<RoleEntry>(
            "SELECT id, group_id as groupId, name, description, rank, member_count as memberCount FROM group_role WHERE group_id = :group_id", new
            {
                group_id = groupId,
            });
    }

    private async Task<RoleEntry> GetRoleInGroupById(long groupId, long roleSetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<RoleEntry>(
            "SELECT id, group_id as groupId, name, description, rank, member_count as memberCount FROM group_role WHERE group_id = :group_id AND id = :id", new
            {
                group_id = groupId,
                id = roleSetId,
            });
        if (result == null) throw new RecordNotFoundException();
        return result;
    }

    private async Task<RoleWithPermissions> GetRoleInGroupByRank(long groupId, int rankValue)
    {
        var role = await db.QuerySingleOrDefaultAsync<RoleWithPermissions>(
            "SELECT id, group_id as groupId, name, description, rank, member_count as memberCount FROM group_role WHERE group_id = :group_id AND rank = :rank", new
            {
                group_id = groupId,
                rank = rankValue,
            });
        if (role == null) throw new RecordNotFoundException();
        role.permissions = await GetPermissions(role.id);
        return role;
    }

    public async Task<RolePermissions> GetPermissions(long roleSetId)
    {
        return await db.QuerySingleOrDefaultAsync<RolePermissions>(
            "SELECT delete_from_wall as deleteFromWall, post_to_wall as postToWall, invite_members as inviteMembers, post_to_status as postToStatus, remove_members as removeMembers, view_status as viewStatus, view_wall as viewWall, change_rank as changeRank, advertise_group as advertiseGroup, manage_relationships as manageRelationships, add_group_places as addGroupPlaces, view_audit_logs as viewAuditLogs, create_items as createItems, manage_items as manageItems, spend_group_funds as spendGroupFunds, manage_clan as manageClan, manage_group_games as manageGroupGames FROM group_role_permission WHERE group_role_id = :id",
            new
            {
                id = roleSetId,
            });
    }

    public async Task<IEnumerable<RoleWithPermissions>> GetRolesInGroupWithPermissions(long groupId)
    {
        var role = (await db.QueryAsync<RoleWithPermissions>(
            "SELECT id, group_id as groupId, name, description, rank, member_count as memberCount FROM group_role WHERE group_id = :group_id", new
            {
                group_id = groupId,
            })).ToList();
        foreach (var item in role)
        {
            item.permissions = await GetPermissions(item.id);
        }
        return role;
    }

    /// <summary>
    /// GetUserRoleInGroup returns the userId's role in the groupId, or guest if not in the group.
    /// </summary>
    /// <param name="groupId"></param>
    /// <param name="userId"></param>
    public async Task<RoleWithPermissions> GetUserRoleInGroup(long groupId, long userId)
    {
        var role = await db.QuerySingleOrDefaultAsync<RoleWithPermissions>(
            "SELECT gr.id, gr.group_id as groupId, gr.name, gr.description, gr.rank, gr.member_count as memberCount FROM group_user INNER JOIN group_role as gr ON gr.id = group_user.group_role_id WHERE gr.group_id = :group_id AND group_user.user_id = :user_id", new
            {
                group_id = groupId,
                user_id = userId,
            });
        if (role == null)
        {
            return await GetRoleInGroupByRank(groupId, 0);
        }

        role.permissions = await GetPermissions(role.id);
        return role;
    }

    public async Task<IEnumerable<RoleEntry>> GetAllRolesForUser(long userId)
    {
        var role = await db.QueryAsync<RoleEntry>(
            "SELECT gr.id, gr.group_id as groupId, gr.name, gr.description, gr.rank, gr.member_count as memberCount FROM group_user INNER JOIN group_role as gr ON gr.id = group_user.group_role_id WHERE group_user.user_id = :user_id", new
            {
                user_id = userId,
            });

        return role;
    }

    private async Task<IAsyncDisposable> GetGroupOwnerChangeLock(long groupId)
    {
        var groupLock = await Cache.redLock.CreateLockAsync("GroupOwnerChange:" + groupId, TimeSpan.FromMinutes(2));
        if (!groupLock.IsAcquired)
            throw new LockNotAcquiredException();
        return groupLock;
    }

    public async Task ClaimGroup(long groupId, long userId)
    {
        await using var groupLock = await GetGroupOwnerChangeLock(groupId);
        await InTransaction(async _ =>
        {
            var roles = (await db.QueryAsync<SkinnyRank>(
                "SELECT group_role.id as roleId, group_role.rank FROM group_role WHERE group_id = :gid", new
                {
                    gid = groupId,
                })).ToList();
            var roleToPromoteTo = roles.Find(c => c.rank == 255);
            if (roleToPromoteTo == null)
                throw new Exception("CG: Branch 1"); // Group is broken
            if (roleToPromoteTo.rank == 0)
                throw new Exception("GC: Branch 2"); // Rank is broken?

            var userCurrentRole = await GetUserRoleInGroup(groupId, userId);
            if (userCurrentRole.rank == 0)
                throw new Exception("Guests cannot claim groups");
            // dec member count
            await db.ExecuteAsync("UPDATE group_role SET member_count = member_count - 1 WHERE id = :id",
                new {userCurrentRole.id});
            // update user perm
            var updated = await db.ExecuteAsync(
                "UPDATE group_user SET group_role_id = :role_id WHERE user_id = :user_id AND group_role_id = :group_role_id",
                new
                {
                    group_role_id = userCurrentRole.id,
                    role_id = roleToPromoteTo.roleId,
                    user_id = userId,
                });
            if (updated != 1)
            {
                throw new Exception("CG: Branch 3 - Up was " + updated); // User is either in group multiple times or not in it at all (in which case, transactions/locks broke)
            }
            // update member count in owner role
            await db.ExecuteAsync("UPDATE group_role SET member_count = member_count + 1 WHERE id = :id",
                new {id = roleToPromoteTo.roleId});

            // update group owner
            var updatedCount = await db.ExecuteAsync("UPDATE \"group\" SET user_id = :user_id WHERE id = :id AND user_id IS NULL", new
            {
                id = groupId,
                user_id = userId,
            });
            if (updatedCount != 1)
            {
                throw new Exception("CG: Branch 4 - Up was " + updatedCount); // Group already has an owner?
            }
            // audit log
            await InsertAsync("group_audit_log", new
            {
                group_id = groupId,
                user_id = userId,
                new_owner_user_id = userId,
                action = AuditActionType.Claim,
            });

            return 0;
        });
    }

    public async Task<IEnumerable<GroupMemberEntry>> GetGroupMembersByRoleSet(long groupId, long roleSetId, int limit, int offset, string sortOrder)
    {
        if (sortOrder != "asc" && sortOrder != "desc")
            sortOrder = "asc";

        var sq = new SqlBuilder();
        var t = sq.AddTemplate(
            "SELECT group_user.user_id as userId, \"user\".username, group_role.name, group_role.rank, group_role.member_count as memberCount, group_role.id as roleId FROM group_user INNER JOIN \"user\" ON \"user\".id = group_user.user_id INNER JOIN group_role ON group_role.id = group_user.group_role_id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new
            {
                limit, offset,
            });

        sq.OrWhere("group_role_id = " + roleSetId);
        sq.OrderBy("group_user.id " + sortOrder);

        var members = await db.QueryAsync<GroupMemberDb>(t.RawSql, t.Parameters);
        return members.Select(c => new GroupMemberEntry
        {
            role = new ApiRoleEntry
            {
                id = c.roleId,
                name = c.name,
                rank = c.rank,
                memberCount = c.memberCount,
            },
            user = new()
            {
                userId = c.userId,
                username = c.username,
                displayName = c.username,
            },
        });
    }

    public async Task<IEnumerable<GroupMemberEntry>> GetGroupMembers(long groupId, int limit, int offset, string sortOrder)
    {
        if (sortOrder != "asc" && sortOrder != "desc") throw new Exception("Bad sort");
        var roles = (await db.QueryAsync<SkinnyRank>(
            "SELECT id as roleId, rank FROM group_role WHERE group_id = :group_id", new
            {
                group_id = groupId,
            })).Where(c => c.rank != 0);
        var sq = new SqlBuilder();
        var t = sq.AddTemplate(
            "SELECT group_user.user_id as userId, \"user\".username, group_role.name, group_role.rank, group_role.member_count as memberCount, group_role.id as roleId FROM group_user INNER JOIN \"user\" ON \"user\".id = group_user.user_id INNER JOIN group_role ON group_role.id = group_user.group_role_id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new
            {
                limit, offset,
            });
        foreach (var id in roles)
        {
            sq.OrWhere("group_role_id = " + id.roleId);
        }

        sq.OrderBy("group_user.id " + sortOrder);

        var members = await db.QueryAsync<GroupMemberDb>(t.RawSql, t.Parameters);
        return members.Select(c => new GroupMemberEntry
        {
            role = new ApiRoleEntry
            {
                id = c.roleId,
                name = c.name,
                rank = c.rank,
                memberCount = c.memberCount,
            },
            user = new()
            {
                userId = c.userId,
                username = c.username,
                displayName = c.username,
            },
        });
    }

    private async Task<int> GetPostCountByUserInTimeSpan(long userId, TimeSpan duration)
    {
        var tsToDate = DateTime.UtcNow.Subtract(duration);
        var result = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) FROM group_wall WHERE user_id = :user_id AND created_at >= :date", new
            {
                date = tsToDate,
                user_id = userId,
            });
        return result.total;
    }

    private async Task<long> GetUserWallPostCount(long userId, TimeSpan duration)
    {
        var totalPosts = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM group_wall WHERE user_id = :user_id AND created_at > :dt", new
            {
                dt = DateTime.UtcNow.Subtract(duration),
                user_id = userId,
            });
        return totalPosts.total;
    }

    public async Task<bool> IsFloodCheckedForWallPost(long groupId, long userId)
    {
        // User posts
        var pastMinute = await GetUserWallPostCount(userId, TimeSpan.FromMinutes(1));
        if (pastMinute >= 2)
            return true;

        var pastHour = await GetUserWallPostCount(userId, TimeSpan.FromHours(1));
        if (pastHour >= 25)
        {
            GroupMetrics.ReportWallPostFloodCheck(groupId, userId, pastHour);
            return true;
        }

        // All posts in this group, past hour
        var allPostsInGroupPastHour = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM group_wall WHERE group_id = :group_id AND created_at >= :dt", new
            {
                dt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)),
                group_id = groupId,
            });
        if (allPostsInGroupPastHour.total >= 25)
        {
            GroupMetrics.ReportGlobalWallPostFloodCheckForSpecificGroup(groupId, allPostsInGroupPastHour.total);
            return true;
        }

        // All posts across all groups
        var globalPosts = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM group_wall WHERE created_at >= :dt", new
            {
                dt = DateTime.UtcNow.Subtract(TimeSpan.FromHours(1)),
            });
        if (globalPosts.total >= 150)
        {
            // todo: experiment with this limit...
            GroupMetrics.ReportGlobalWallPostFloodCheck(groupId, userId, globalPosts.total);
            return true;
        }

        return false;
    }

    public async Task<WallEntry> CreateWallPost(long groupId, long userId, string body)
    {
        if (await IsFloodCheckedForWallPost(groupId, userId))
            throw new CooldownException();

        var postCount = await GetPostCountByUserInTimeSpan(userId, TimeSpan.FromSeconds(5));
        if (postCount >= 1)
            throw new CooldownException();
        // Hopefully this cleans the text
        using var filter = ServiceProvider.GetOrCreate<FilterService>(this);
        body = filter.CleanText(body);
        var createdAt = DateTime.UtcNow;
        var id = await InsertAsync("group_wall", new
        {
            group_id = groupId,
            content = body,
            created_at = createdAt,
            updated_at = createdAt,
            user_id = userId,
        });
        return new()
        {
            id = id,
            poster = new()
            {
                userId = userId,
            },
            body = body,
            created = createdAt,
            updated = createdAt,
        };
    }

    // Example URL: https://discord.gg/abcd123
    private static readonly Regex DiscordUrlRegex = new Regex("https?:\\/\\/discord.gg\\/[0-9a-zA-Z]+");

    // Example URL (www is optional): https://www.silrev.biz/groups/1/name#!/about
    private static readonly Regex RobloxGroupUrlRegex = new("https:\\/\\/(www\\.)?silrev.biz\\/groups\\/[0-9]+\\/[a-zA-Z\\-0-9]+", RegexOptions.IgnoreCase);

    // Example URL (www is optional): https://silrev.biz/My/Groups.aspx?gid=4
    private static readonly Regex RobloxGroupUrlRegexOld = new("https:\\/\\/(www\\.)?silrev.biz\\/my\\/groups\\.aspx\\?gid=[0-9]+", RegexOptions.IgnoreCase);

    private bool IsLinkValid(SocialLinkType type, string url, string title)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(title))
            return false;
        if (type == SocialLinkType.GooglePlus) return false;
        if (url.Length > 150) return false;
        if (title.Length > 70) return false;
        // url validation
        if (type == SocialLinkType.Discord)
        {
            // server check
            var ok = DiscordUrlRegex.Match(url);
            if (ok.Success && ok.Value == url && url.Length <= 35)
                return true;
            return false; // Bad discord url
        }

        if (type == SocialLinkType.RobloxGroup)
        {
            var ok = RobloxGroupUrlRegex.Match(url);
            if (ok.Success && ok.Value == url)
                return true;

            var oldFormat = RobloxGroupUrlRegexOld.Match(url);
            if (oldFormat.Success && oldFormat.Value == url)
                return true;

            return false;
        }

        // throw new NotImplementedException(); // new social link type?
        return false;
    }

    public async Task<IEnumerable<SocialLinkEntry>> GetSocialLinks(long groupId)
    {
        return (await db.QueryAsync<SocialLinkEntry>("SELECT id, type, url, title FROM group_social_link WHERE group_id = :id",
            new {id = groupId})).Where(c => IsLinkValid(c.type, c.url, c.title));
    }

    public async Task DeleteSocialLink(long groupId, long socialId)
    {
        await db.ExecuteAsync("DELETE FROM group_social_link WHERE id = :id AND group_id = :gid",
            new {gid = groupId, id = socialId});
    }

    public async Task UpdateSocialLink(long groupId, long socialId, SocialLinkType type, string url, string title)
    {
        if (!IsLinkValid(type, url, title))
            throw new Exception("Invalid social media link");
        await UpdateGroup(groupId);
        await db.ExecuteAsync(
            "UPDATE group_social_Link SET title = :title, url = :url, type = :type WHERE id = :id AND group_id = :group_id",
            new
            {
                title,
                url,
                type = (int)type,
                group_id = groupId,
                id = socialId,
            });
    }

    public async Task AddSocialLink(long groupId, SocialLinkType type, string url, string title)
    {
        if (!IsLinkValid(type, url, title))
            throw new Exception("Invalid social media link");

        // method is called "Add" but it's actually "add or update"
        await InTransaction(async _ =>
        {
            var exists = await db.QuerySingleOrDefaultAsync("SELECT id FROM group_social_link WHERE type = :type", new
            {
                type = (int) type,
            });
            if (exists != null)
            {
                await UpdateSocialLink(groupId, exists.id, type, url, title);
            }
            else
            {
                await InsertAsync("group_social_link", new
                {
                    group_id = groupId,
                    title,
                    url,
                    type = (int) type,
                });
            }
            return 0;
        });
    }

    private async Task<int> GetShoutsByUserAndGroup(long groupId, long userId)
    {
        var total = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) as total FROM group_status WHERE group_id = :group_id AND created_at >= :t OR user_id = :user_id AND created_at >= :t",
            new
            {
                t = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(10)),
                user_id = userId,
                group_id = groupId,
            });
        return total.total;
    }

    public async Task<StatusEntry> SetGroupStatus(long groupId, long userId, string status)
    {
        // floodcheck
        var latestPost = await GetShoutsByUserAndGroup(groupId, userId);
        if (latestPost >= 1)
            throw new FloodcheckException();
        // add
        var now = DateTime.UtcNow;
        await InsertAsync("group_status", new
        {
            group_id = groupId,
            user_id = userId,
            created_at = now,
            updated_at = now,
            status,
        });
        return new()
        {
            body = status,
            created = now,
            updated = now,
            poster = new()
            {
                userId = userId,
            },
        };
    }
    public async Task DeleteGroupStatus(long id)
    {
        await db.ExecuteAsync("DELETE FROM group_status WHERE id = :id", new
        {
            id,
        });
    }
    public async Task RemoveUserFromGroup(long groupId, long userId, long userIdPerformingAction)
    {
        await InTransaction(async _ =>
        {
            var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            await using var economyLock = await ec.AcquireEconomyLock(CreatorType.Group, groupId);
            await ec.CreateGroupBalanceIfRequired(groupId);

            var actorRole = await GetUserRoleInGroup(groupId, userIdPerformingAction);
            if (actorRole.rank == 255 && userId == userIdPerformingAction)
            {
                var balance = await ec.GetBalance(CreatorType.Group, groupId);
                if (balance.robux != 0 || balance.tickets != 0)
                    throw new RobloxException(400, 0, "Owner cannot abandon a group with funds in it");
            }

            if (actorRole.rank == 0) return 0;

            var roles = await GetRolesInGroup(groupId);
            var didOwnerLeave = false;
            foreach (var role in roles)
            {
                if (role.rank >= actorRole.rank && userIdPerformingAction != userId)
                {
                    // User might be trying to kick someone above their rank, just ignore the request unless they are removing themselves
                    continue;
                }
                // try to remove the user
                var result = await db.ExecuteAsync("DELETE FROM group_user WHERE user_id = :user_id AND group_role_id = :role_id", new
                {
                    user_id = userId,
                    role_id = role.id,
                });
                if (result != 0)
                {
                    if (role.rank == 255)
                    {
                        didOwnerLeave = true;
                    }

                    await db.ExecuteAsync("UPDATE group_role SET member_count = member_count - 1 WHERE id = :id", new
                    {
                        role.id,
                    });
                    break;
                }
            }

            if (didOwnerLeave)
            {
                // log, then set owner to null
                await db.ExecuteAsync("UPDATE \"group\" SET user_id = null WHERE id = :id", new {id = groupId});
                await InsertAsync("group_audit_log", new
                {
                    group_id = groupId,
                    user_id = userId,
                    action = AuditActionType.Abandon,
                });
            }
            return 0;
        });
    }

    private async Task UpdateRoleSetMemberCount(long roleSetId)
    {
        await db.ExecuteAsync("UPDATE group_role SET member_count = (SELECT COUNT(*) FROM group_user u WHERE u.group_role_id = :id) WHERE id = :id",
            new
            {
                id = roleSetId,
            });
    }

    public async Task SetUserRole(long groupId, long userId, long roleSetId, long userIdPerformingAction)
    {
        await InTransaction(async _ =>
        {
            var actorRole = await GetUserRoleInGroup(groupId, userIdPerformingAction);
            var userRole = await GetUserRoleInGroup(groupId, userId);
            var toPromoteTo = await GetRoleInGroupById(groupId, roleSetId);

            if (toPromoteTo.rank >= actorRole.rank)
            {
                // You can't promote someone to your rank or higher
                throw new ArgumentException("Cannot set user to your current rank or higher");
            }

            if (toPromoteTo.rank == 0)
                throw new ArgumentException("Cannot set user rank to guest");

            if (userRole.rank >= actorRole.rank)
            {
                // If user is trying to promote user at their rank or higher, throw error
                throw new ArgumentException("Cannot update ranks of users above or at your current rank");
            }

            if (userRole.rank == 0)
                throw new ArgumentException("Cannot set user rank of guest");

            // Everything seems ok, so perform actions:
            // Update user's rank
            await db.ExecuteAsync(
                "UPDATE group_user SET group_role_id = :new_role_id WHERE user_id = :user_id AND group_role_id = :role_id", new
                {
                    role_id = userRole.id,
                    new_role_id = toPromoteTo.id,
                    user_id = userId,
                });
            // Decrement users previous role:
            await UpdateRoleSetMemberCount(userRole.id);
            // Increment users new role:
            await UpdateRoleSetMemberCount(toPromoteTo.id);
            // log
            await InsertAsync("group_audit_log", new
            {
                group_id = groupId,
                action = AuditActionType.ChangeRank,
                old_role_id = userRole.id,
                new_role_id = roleSetId,
                user_id = userIdPerformingAction,
                user_id_range_change = userId,
            });

            return 0;
        });
    }

    private static readonly Regex GroupNameRegex = new Regex("[a-zA-Z0-9]+");

    private void ValidateRoleSetInformation(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length >= 100 || string.IsNullOrWhiteSpace(GroupNameRegex.Match(name).Value))
            throw new ArgumentException("Bad name");
        if (description is {Length: > 1000})
            throw new ArgumentException("Bad description");
    }

    public async Task<RoleEntry> CreateRoleSet(long groupId, string name, string? description, int rank, long userIdPerformingAction)
    {
        ValidateRoleSetInformation(name, description);
        // 0 = Guest, 255 = Owner
        if (rank is <= 0 or >= 255)
            throw new ArgumentException("Rank value is out of bounds");

        return await InTransaction(async _ =>
        {
            var existingRoles = (await GetRolesInGroup(groupId)).ToList();
            if (existingRoles.Count >= 40)
                throw new Exception("Limit for roles have been reached on this group");
            var exists = existingRoles.Find(c => c.rank == rank) != null;

            if (exists)
                throw new ArgumentException("Rank value already exists");

            var nameTaken = existingRoles.Find(c => c.name == name) != null;
            if (nameTaken)
                throw new ArgumentException("Role name already exists");

            var roleSetId = await InsertAsync("group_role", new
            {
                name,
                description,
                rank,
                group_id = groupId,
            });
            await InsertAsync("group_role_permission", new
            {
                group_role_id = roleSetId,
            });

            // deduct cost from user
            var updated =await db.ExecuteAsync(
                "UPDATE user_economy SET balance_robux = balance_robux - :robux WHERE user_id = :user_id AND balance_robux >= :robux", new
                {
                    user_id = userIdPerformingAction,
                    robux = 25,
                });
            if (updated != 1)
                throw new Exception("Could not deduct balance from user - they do not have enough robux");
            await InsertAsync("user_transaction", new
            {
                type = PurchaseType.Purchase,
                currency_type = 1,
                amount = 25,
                sub_type = TransactionSubType.GroupRoleSet,
                user_id_one = userIdPerformingAction,
                user_id_two = 1,
            });


            return new RoleEntry
            {
                id = roleSetId,
                name = name,
                description = description,
                groupId = groupId,
                memberCount = 0,
                rank = rank
            };
        });
    }

    public async Task UpdateRoleSet(long groupId, long roleSetId, string name, string? description, int rank,
        long userIdPerformingAction)
    {
        // Rank must be above 0.
        // You cannot update the guest role (0).
        // 255 is maximum rank number (owner).
        if (rank is <= 0 or > 255)
            throw new ArgumentException("Rank value is out of bounds");
        ValidateRoleSetInformation(name, description);
        await InTransaction(async _ =>
        {
            var allRoles = (await GetRolesInGroup(groupId)).ToList();
            var exists = allRoles.Find(c => (c.name == name || c.rank == rank) && c.id != roleSetId) != null;
            if (exists)
                throw new ArgumentException("Name or rank is already taken");

            var oldInfo = allRoles.Find(c => c.id == roleSetId);
            if (oldInfo == null)
                throw new Exception("Tried to update non-existent role");

            if (oldInfo.rank == 0)
                throw new Exception("Cannot update the guest role");

            if (oldInfo.rank == 255 && rank != 255)
                throw new Exception("Cannot update the rank property of this role");

            await UpdateAsync("group_role", roleSetId, new
            {
                name,
                description,
                rank,
            });
            await UpdateGroup(groupId);
            // If name or description updated
            if (oldInfo.name != name || oldInfo.description != description)
            {
                await InsertAsync("group_audit_log", new
                {
                    group_id = groupId,
                    user_id = userIdPerformingAction,
                    role_set_id = roleSetId,
                    old_name = oldInfo.name,
                    new_name = name,
                    old_description = oldInfo.description,
                    new_description = description,
                    action = AuditActionType.UpdateRolesetData,
                });
            }
            // If rank was updated
            if (oldInfo.rank != rank)
            {
                await InsertAsync("group_audit_log", new
                {
                    group_id = groupId,
                    user_id = userIdPerformingAction,
                    role_set_id = roleSetId,
                    old_rank = oldInfo.rank,
                    new_rank = rank,
                    action = AuditActionType.UpdateRolesetRank,
                });
            }

            return 0;
        });
    }

    public async Task DeleteRole(long groupId, long roleSetId, long userIdPerformingAction)
    {
        await InTransaction(async _ =>
        {
            var allRoles = (await GetRolesInGroup(groupId)).ToList();
            var data = allRoles.Find(c => c.id == roleSetId);

            if (data == null)
                throw new Exception("Tried to delete non-existent role");
            if (data.memberCount != 0)
                throw new ArgumentException("Cannot delete a role with members");
            if (allRoles.Count <= 3)
                throw new ArgumentException("Cannot delete the last role in a group");
            await db.ExecuteAsync("DELETE FROM group_role_permission WHERE group_role_id = :id", new
            {
                id = roleSetId,
            });
            await db.ExecuteAsync("DELETE FROM group_role WHERE id = :id", new
            {
                id = roleSetId,
            });

            return 0;
        });
    }

    public async Task UpdateRolePermissions(long groupId, long roleId, Dictionary<string, bool> newPermissions,
        long userIdPerformingAction)
    {
        var roleInfo = await GetRoleInGroupById(groupId, roleId);
        if (roleInfo.rank == 255)
            throw new Exception("Cannot edit owner rank");

        if (roleInfo.rank == 0)
        {
            // Guests can only have ViewWall and ViewStatus permissions changed
            await UpdateAsync("group_role_permission", "group_role_id", roleId, new
            {
                view_wall = newPermissions["viewWall"],
                view_status = newPermissions["viewStatus"],
            });
        }
        else
        {
            var updateObject = new GroupRolesetUpdate(newPermissions);

            await UpdateAsync("group_role_permission", "group_role_id", roleId, updateObject.GetUpdateObject());
        }
    }

    public async Task<IEnumerable<dynamic>> GetAuditLog(long groupId, int offset, int limit)
    {
        // Good luck to anyone who has to edit this...
        var data = await db.QueryAsync("SELECT group_audit_log.user_id, group_audit_log.group_id, group_audit_log.action, group_audit_log.created_at, u1.username, group_audit_log.user_id_range_change, u2.username as rank_change_username, u3.username as new_owner_username, group_audit_log.new_owner_user_id, group_audit_log.role_set_id, group_audit_log.old_rank, group_audit_log.new_rank, group_audit_log.old_name, group_audit_log.new_name, group_audit_log.old_description, group_audit_log.new_description, group_audit_log.old_role_id, group_audit_log.new_role_id, gr1.name as group_role_current_name, gr2.name as group_role_old_rank_name, gr3.name as group_role_new_rank_name, group_audit_log.post_desc, group_audit_log.post_user_id, u4.username as post_username, group_audit_log.fund_recipient_user_id, group_audit_log.currency_type, group_audit_log.currency_amount FROM group_audit_log LEFT JOIN \"user\" AS u2 ON u2.id = group_audit_log.user_id_range_change LEFT JOIN \"user\" AS u3 ON u3.id = group_audit_log.new_owner_user_id LEFT JOIN \"user\" AS u1 ON u1.id = group_audit_log.user_id LEFT JOIN \"user\" AS u4 ON u4.id = group_audit_log.post_user_id LEFT JOIN group_role AS gr1 ON gr1.id = group_audit_log.role_set_id LEFT JOIN group_role AS gr2 ON gr2.id = group_audit_log.old_role_id LEFT JOIN group_role AS gr3 ON gr3.id = group_audit_log.new_role_id WHERE group_audit_log.group_id = :gid ORDER BY group_audit_log.id DESC LIMIT :limit OFFSET :offset", new
        {
            gid = groupId,
            limit,
            offset,
        });
        var newData = new List<dynamic>();
        foreach (var entry in data)
        {
            var role = await GetUserRoleInGroup(groupId, (long)entry.user_id);
            dynamic? desc = null;
            switch ((AuditActionType) entry.action)
            {
                case AuditActionType.UpdateRolesetData:
                    desc = new
                    {
                        RoleSetId = entry.role_set_id,
                        OldDescription = entry.old_description,
                        NewDescription = entry.new_description,
                        OldName = entry.old_name,
                        NewName = entry.new_name,
                        RoleSetName = entry.group_role_current_name,
                    };
                    break;
                case AuditActionType.SpendGroupFunds:
                    desc = new
                    {
                        Amount = entry.currency_amount,
                        CurrencyTypeId = entry.currency_type,
                        ItemDescription = "one-time payout of "+(CurrencyType)entry.currency_type+" from group funds to Username (" + entry.currency_amount + ")",
                        CurrencyTypeName = ((CurrencyType) entry.currency_type).ToString(),
                    };
                    break;
                case AuditActionType.UpdateRolesetRank:
                    desc = new
                    {
                        RoleSetId = entry.role_set_id,
                        OldRank = entry.old_rank,
                        NewRank = entry.new_rank,
                        RoleSetName = entry.group_role_current_name,
                    };
                    break;
                case AuditActionType.ChangeRank:
                    desc = new
                    {
                        TargetId = entry.user_id_range_change,
                        OldRoleSetId = entry.old_role_id,
                        NewRoleSetId = entry.new_role_id,
                        TargetName = entry.rank_change_username,
                        OldRoleSetName = entry.group_role_old_rank_name,
                        NewRoleSetName = entry.group_role_new_rank_name,
                    };
                    break;
                case AuditActionType.ChangeOwner:
                    desc = new
                    {
                        OldOwnerId = entry.user_id,
                        NewOwnerId = entry.new_owner_user_id,
                        IsRoblox = entry.user_id == 1,
                        OldOwnerName = entry.username,
                        NewOwnerName = entry.new_owner_username,
                    };
                    break;
                case AuditActionType.DeletePost:
                    desc = new
                    {
                        PostDesc = entry.post_desc,
                        TargetId = entry.post_user_id,
                        TargetName = entry.post_username,
                    };
                    break;
            }

            var obj = new
            {
                actor = new
                {
                    user = new GroupUser
                    {
                        userId = entry.user_id,
                        username = entry.username,
                        displayName = entry.username,
                    },
                    role = new
                    {
                        role.id,
                        role.name,
                        role.rank,
                        role.memberCount,
                    },
                },
                description = desc,
                actionType = string.Join(" ", Regex.Split(((AuditActionType)entry.action).ToString(), @"(?<!^)(?=[A-Z])")),
                created = (DateTime) entry.created_at,
            };
            newData.Add(obj);
        }

        return newData;
    }

    public async Task JoinGroup(long groupId, long userId)
    {
        var status = await GetGroupById(groupId);
        if (status.isLocked)
            throw new ArgumentException("You cannot join a closed group");
        if (!status.publicEntryAllowed)
            throw new NotImplementedException("TODO: non-public entry groups");

        await using var joinGroupLock = await Cache.redLock.CreateLockAsync("JoinGroup:" + userId, TimeSpan.FromMinutes(2));
        await InTransaction(async _ =>
        {
            var existingGroupCount = await CountUserGroups(userId);
            if (existingGroupCount >= 100)
                throw new Exception("User has reached maximum group count");
            var inGroup = await GetUserRoleInGroup(groupId, userId);
            if (inGroup.rank != 0)
                throw new Exception("You are already a member of this group");
            // Get lowest non-guest rank in group
            var allRoles = (await GetRolesInGroup(groupId)).Where(c => c.rank != 0).ToList();
            allRoles.Sort((a, b) => a.rank > b.rank ? 1 : a.rank == b.rank ? 0 : -1);
            var lowestRank = allRoles.First();
            if (lowestRank.rank == 255)
                throw new Exception("Corrupt group - Does not have a non-owner rank");
            // add user to group
            await InsertAsync("group_user", new
            {
                group_role_id = lowestRank.id,
                user_id = userId,
            });
            await db.ExecuteAsync("UPDATE group_role SET member_count = member_count + 1 WHERE id = :id", new
            {
                lowestRank.id,
            });

            return 0;
        });
    }

    public async Task<long?> GetPrimaryGroupId(long userId)
    {
        var result = await redis.StringGetAsync("PrimaryGroup:V1:" + userId);
        if (result != null) return long.Parse(result);
        return null;
    }

    public async Task SetPrimaryGroupId(long groupId, long userId)
    {
        await redis.StringSetAsync("PrimaryGroup:V1:" + userId, groupId);
    }

    public async Task DeletePrimaryGroupId(long userId)
    {
        await redis.KeyDeleteAsync("PrimaryGroup:V1:" + userId);
    }

    public async Task<RoleSetInGroupDetailed> GetRoleSetInGroupDetailed(long groupId, long userId)
    {
        var role = await GetUserRoleInGroup(groupId, userId);
        var result = new RoleSetInGroupDetailed(role, new GroupUser
        {
            userId = userId,
        })
        {
            canConfigure = role.CanConfigure(),
            areGroupFundsVisible = role.HasPermission(GroupPermission.SpendGroupFunds)
        };
        return result;
    }

    public async Task<GroupSettingsEntry> GetGroupSettings(long groupId)
    {
        var result = await db.QuerySingleOrDefaultAsync<GroupSettingsEntry>(
            "SELECT approval_required as isApprovalRequired, enemies_allowed as areEnemiesAllowed, funds_visible as areGroupFundsVisible, games_visible as areGroupGamesVisible FROM group_settings WHERE group_id = :gid",
            new
            {
                gid = groupId,
            });
        if (result == null) throw new RecordNotFoundException();
        return result;
    }

    public async Task SetGroupSettings(long groupId, GroupSettingsEntry settings)
    {
        await UpdateGroup(groupId);
        await db.ExecuteAsync(
            "UPDATE group_settings SET approval_required=:approval, enemies_allowed=:enemies, funds_visible = :funds, games_visible = :games WHERE group_id = :gid",
            new
            {
                gid = groupId,
                approval = settings.isApprovalRequired,
                enemies = settings.areEnemiesAllowed,
                funds = settings.areGroupFundsVisible,
                games = settings.areGroupGamesVisible,
            });
    }

    public async Task ChangeGroupOwner(long groupId, long userId, long userIdPerformingAction)
    {
        await using var groupLock = await GetGroupOwnerChangeLock(groupId);
        await InTransaction(async _ =>
        {
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            await using var economyLock = await ec.AcquireEconomyLock(CreatorType.Group, groupId);
            await ec.CreateGroupBalanceIfRequired(groupId);
            var groupDetails = await GetGroupById(groupId);
            var userRole = await GetUserRoleInGroup(groupId, userId);
            var actorRole = await GetUserRoleInGroup(groupId, userIdPerformingAction);

            if (userRole.rank == 0)
                throw new ArgumentException("New owner is a guest");

            if (actorRole.rank != 255 || groupDetails.owner == null || groupDetails.owner.userId != userIdPerformingAction)
                throw new ArgumentException("User making request is no longer owner");

            var funds = await ec.GetBalance(CreatorType.Group, groupId);
            if (funds.robux != 0 || funds.tickets != 0)
                throw new RobloxException(403, 0, "Group owner cannot be modified until funds are withdrawn");

            // make owner
            await db.ExecuteAsync("UPDATE \"group\" SET user_id = :user_id WHERE id = :gid", new
            {
                user_id = userId,
                gid = groupId,
            });

            // swap ranks
            await db.ExecuteAsync(
                "UPDATE group_user SET group_role_id = :group_role_id WHERE group_role_id = :old_role_id AND user_id = :user_id",
                new
                {
                    user_id = userId,
                    old_role_id = userRole.id,
                    group_role_id = actorRole.id,
                });
            await db.ExecuteAsync(
                "UPDATE group_user SET group_role_id = :group_role_id WHERE group_role_id = :old_role_id AND user_id = :user_id",
                new
                {
                    user_id = userIdPerformingAction,
                    old_role_id = actorRole.id,
                    group_role_id = userRole.id,
                });
            // audit
            await InsertAsync("group_audit_log", new
            {
                group_id = groupId,
                action = AuditActionType.ChangeOwner,
                user_id = userIdPerformingAction,
                new_owner_user_id = userId,
            });

            var previousOwnerDetails =
                await db.QuerySingleOrDefaultAsync("SELECT username FROM \"user\" WHERE id = :id",
                    new {id = userIdPerformingAction});
            // send message
            await InsertAsync("user_message", new
            {
                subject = "You have been appointed a Group Owner!",
                user_id_to = userId,
                user_id_from = 1,
                body = $"{previousOwnerDetails.username} has appointed you the owner of the group: {groupDetails.name}. you should verify that the group description and status are what you would like them to be.",
                is_read = false,
                is_archived = false,
            });
            await UpdateGroup(groupId);
            return 0;
        });
    }

    public async Task SetGroupDescription(long groupId, string newDescription)
    {
        await UpdateGroup(groupId);
        await db.ExecuteAsync("UPDATE \"group\" SET description = :description WHERE id = :gid", new
        {
            gid = groupId,
            description = newDescription,
        });
    }

    public async Task<IEnumerable<WallEntryV2>> GetWall(long groupId, int limit, int offset)
    {
        var results = (await db.QueryAsync(
            "SELECT gw.id, gw.created_at as created, gw.updated_at as updated, gw.content, gw.user_id, u.username FROM group_wall AS gw INNER JOIN \"user\" u ON u.id = gw.user_id WHERE group_id = :gid ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                gid = groupId,
                limit,
                offset,
            })).ToList();
        var roles = (await Task.WhenAll(results.Select(c => GetUserRoleInGroup(groupId, (long) c.user_id)))).ToList();

        return results.Select((result, idx) => new WallEntryV2
        {
            id = result.id,
            created = result.created,
            updated = result.updated,
            body = result.content,
            poster = new()
            {
                user = new()
                {
                    userId = result.user_id,
                    username = result.username,
                    displayName = result.username,
                },
                role = new()
                {
                    name = roles[idx].name,
                    description = roles[idx].description,
                    memberCount = roles[idx].memberCount,
                    rank = roles[idx].rank,
                },
            }
        });
    }

    public async Task<WallEntry> GetWallPostById(long groupId, long wallPostId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT id, created_at as created, updated_at as updated, content, user_id as user_id FROM group_wall WHERE id = :id AND group_id = :gid", new
        {
            gid = groupId,
            id = wallPostId,
        });
        if (result is null)
            throw new RecordNotFoundException();
        return new()
        {
            id = result.id,
            created = result.created,
            updated = result.updated,
            body = result.content,
            poster = new()
            {
                userId = result.user_id,
            },
        };
    }

    public async Task DeleteWallPostById(long groupId, long wallPostId, long userIdPerformingAction)
    {
        var content = await GetWallPostById(groupId, wallPostId);
        await InTransaction(async _ =>
        {
            await db.ExecuteAsync("DELETE FROM group_wall WHERE id = :id", new
            {
                id = wallPostId,
            });
            if (content.body.Length > 256)
                content.body = "[Content too long to display]";
            await InsertAsync("group_audit_log", new
            {
                post_user_id = content.poster.userId,
                post_desc = content.body,
                user_id = userIdPerformingAction,
                group_id = groupId,
                action = AuditActionType.DeletePost,
            });
            return 0;
        });
    }

    private const int MaxIconFilesSizeByte = 5 * 1024 * 1024;
    private const int MaxIconStartFileSizeBytes = 10 * 1024 * 1024;

    private async Task<GroupIcon> ConvertImageToGroupIcon(Stream rawImageStream)
    {
        if (rawImageStream.Length >= MaxIconStartFileSizeBytes)
            throw new Exception("Stream is too large");

        rawImageStream.Position = 0;
        using var img = await Image.LoadAsync(rawImageStream);
        if (img == null)
            throw new Exception("Bad image");
        // var h = img.Height;
        // var w = img.Width;
        var square = 420;//h > w ? w : h;
        img.Mutate(c => c.Resize(square, square));
        var newStream = new MemoryStream();
        await img.SaveAsPngAsync(newStream);
        if (newStream.Length >= MaxIconFilesSizeByte)
            throw new Exception("Final stream is too large");
        return new GroupIcon(newStream);
    }

    public async Task SetGroupIconFromStream(long groupId, Stream groupIconUnsafe, long userIdPerformingAction)
    {
        await using var safeImage = await ConvertImageToGroupIcon(groupIconUnsafe);
        await SetGroupIcon(groupId, safeImage, userIdPerformingAction);
    }

    public async Task SetGroupIcon(long groupId, GroupIcon groupIconSafe, long userIdPerformingAction)
    {
        if (groupIconSafe.finalIconStream == null)
            throw new ArgumentException("SetGroupIcon finalIconStream cannot be null");

        groupIconSafe.finalIconStream.Position = 0;
        // write image
        var sha = SHA256.Create();
        var bits = await sha.ComputeHashAsync(groupIconSafe.finalIconStream);
        var str = Convert.ToHexString(bits).ToLower() + ".png";
        var fullFilePath = Configuration.GroupIconsDirectory + str;
        // Lock - mostly to prevent "The process cannot access the file 'image.png' because it is being used by another
        // process." errors in integration tests
        await using var groupIconLock =
            await Cache.redLock.CreateLockAsync("GlobalGroupIconUploadV1", TimeSpan.FromSeconds(10));
        if (!groupIconLock.IsAcquired)
            throw new LockNotAcquiredException();

        // only write image file if it doesn't already exist
        if (!File.Exists(fullFilePath))
        {
            groupIconSafe.finalIconStream.Position = 0;
            await using var fs = File.Create(fullFilePath);
            groupIconSafe.finalIconStream.Seek(0, SeekOrigin.Begin);
            await groupIconSafe.finalIconStream.CopyToAsync(fs);
            fs.Close();
        }
        // upsert image
        await db.ExecuteAsync(
            "INSERT INTO group_icon (name, user_id, group_id) VALUES (:name, :user_id, :group_id) ON CONFLICT (group_id) DO UPDATE SET name = :name, user_id = :user_id, is_approved = 0",
            new
            {
                name = str,
                user_id = userIdPerformingAction,
                group_id = groupId,
            });
    }

    public async Task<bool> IsGroupNameTaken(string name)
    {
        var groupsMatchingName = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM \"group\" WHERE name ILIKE :n", new
            {
                n = name,
            });
        return groupsMatchingName.total != 0;
    }

    private static readonly Regex NameValidationRegex = new("[a-zA-Z0-9]+");
    private static readonly Regex RudimentaryTextFilter = new("https?\\:\\/\\/|fuck|nigga|nigger|dick|cock|bitch");

    public async Task<GroupCreationResponse> CreateGroup(string name, string description, Stream iconStream, long userId)
    {
        var nameValidationResult = NameValidationRegex.Match(name);
        if (nameValidationResult.Value.Length == 0 || name.Length > 50 || name.StartsWith("%") || name.EndsWith("%") || name.StartsWith(" ") || name.EndsWith(" "))
            throw new ArgumentException("The name is invalid");
        if (RudimentaryTextFilter.Match(name).Success)
            throw new ArgumentException("The name is moderated");
        if (description is {Length: > 1000})
            throw new ArgumentException("The description is too long");

        await using var safeImageStream = await ConvertImageToGroupIcon(iconStream);
        return await InTransaction(async _ =>
        {
            var created = DateTime.UtcNow;
            using var us = ServiceProvider.GetOrCreate<UsersService>(this);
            await using var currencyLock = await us.AcquireEconomyLock(userId);
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            // check if user in too many groups
            var gc = await CountUserGroups(userId);
            if (gc >= 100)
                throw new Exception("User is in maximum number of groups");
            // check if name taken
            if (await IsGroupNameTaken(name))
                throw new ArgumentException("The name has been taken.");
            // confirm user has enough
            var bal = await ec.GetUserBalance(userId);
            if (bal.robux < 100)
                throw new RobloxException(400, 0, "Insufficient funds");
            // charge user
            await ec.DecrementCurrency(CreatorType.User, userId, CurrencyType.Robux, 100);
            await InsertAsync("user_transaction", new
            {
                type = PurchaseType.Purchase,
                currency_type = CurrencyType.Robux,
                amount = 100,
                sub_type = TransactionSubType.GroupCreation,
                user_id_one = userId,
                user_id_two = 1,
            });
            // create group
            var groupId = await InsertAsync("group", new
            {
                user_id = userId,
                name,
                description,
                locked = false,
                created_at = created,
                updated_at = created,
            });
            // settings
            await InsertAsync("group_settings",  "group_id", new
            {
                group_id = groupId,
            });
            // icon
            await SetGroupIcon(groupId, safeImageStream, userId);
            // roles
            var guestId = await InsertAsync("group_role", new
            {
                rank = 0,
                description = "A non-group member.",
                name = "Guest",
                group_id = groupId,
            });
            await InsertAsync("group_role_permission", new
            {
                group_role_id = guestId,
                view_wall = true,
                view_status = true,
            });
            // member
            var memberId = await InsertAsync("group_role", new
            {
                rank = 1,
                description = "A regular group member.",
                name = "Member",
                group_id = groupId,
            });
            await InsertAsync("group_role_permission", new
            {
                group_role_id = memberId,
                view_wall = true,
                post_to_wall = true,
                view_status = true,
            });
            // admin
            var adminId = await InsertAsync("group_role", new
            {
                rank = 254,
                description = "A group administrator.",
                name = "Admin",
                group_id = groupId,
            });
            await InsertAsync("group_role_permission", new
            {
                view_wall = true,
                post_to_wall = true,
                view_status = true,
                group_role_id = adminId,
            });
            // owner
            var ownerId = await InsertAsync("group_role", new
            {
                rank = 255,
                description = "The group's owner.",
                name = "Owner",
                group_id = groupId,
                member_count = 1,
            });
            await InsertAsync("group_role_permission", new
            {
                group_role_id = ownerId,
                remove_members = true,
                change_rank = true,
                view_wall = true,
                post_to_wall = true,
                delete_from_wall = true,
                view_audit_logs = true,
                view_status = true,
                add_group_places = true,
                advertise_group = true,
                create_items = true,
                manage_clan = true,
                spend_group_funds = true,
                manage_group_games = true,
                invite_members = true,
                manage_items = true,
                manage_relationships = true,
                post_to_status = true,
            });
            // add current user to owner role
            await InsertAsync("group_user", new
            {
                user_id = userId,
                group_role_id = ownerId,
            });

            return new GroupCreationResponse()
            {
                id = groupId,
                name = name,
                description = description,
                owner = new GroupUser()
                {
                    userId = userId,
                },
                memberCount = 1,
                shout = null,
                created = created,
            };
        });
    }

    public async Task<IEnumerable<FeedEntry>> MultiGetGroupStatus(IEnumerable<long> ids, int limit)
    {
        var groupIds = ids.ToList();
        if (groupIds.Count == 0)
            return Array.Empty<FeedEntry>();

        var s = new SqlBuilder();
        var t = s.AddTemplate(
            "SELECT group_status.id as feedId, group_status.user_id as userId, group_status.group_id as groupId, group_status.status as content, group_status.created_at as createdAt, u.username, g.name as groupName, gi.name as groupImage FROM group_status INNER JOIN \"user\" AS u ON u.id = group_status.user_id INNER JOIN \"group\" g on g.id = group_status.group_id INNER JOIN group_icon gi ON gi.group_id = group_status.group_id AND gi.is_approved = 1 /**where**/ ORDER BY group_status.id DESC LIMIT :limit", new
            {
                limit,
            });
        s.OrWhereMulti("group_status.group_id = $1", groupIds);
        return (await db.QueryAsync<GroupFeedEntryDb>(t.RawSql, t.Parameters)).Select(c => new FeedEntry
        {
            feedId = c.feedId,
            content = c.content,
            created = c.createdAt,
            group = new()
            {
                id = c.groupId,
                name = c.groupName,
                image = Roblox.Configuration.CdnBaseUrl + "/images/groups/" + c.groupImage,
            },
            user = new()
            {
                id = c.userId,
                name = c.username,
            },
            type = CreatorType.Group,
        });
    }

    public async Task PayoutGroupFunds(long groupId, long actorUserId, long recipientUserId, long amount, CurrencyType currency)
    {
        var perms = await DoesUserHavePermission(actorUserId, groupId, GroupPermission.Owner);
        if (!perms)
            throw new RobloxException(401, 0, "Unauthorized");
        // limit to group owner for now
        if (recipientUserId != actorUserId)
            throw new RobloxException(400, 0, "Group payouts can only be made to the group owner at this time");
        if (amount <= 0)
            throw new RobloxException(400, 0, "Invalid amount");
        if (!Enum.IsDefined(currency))
            throw new RobloxException(400, 0, "Invalid currency");
        var recipientInGroup = await GetUserRoleInGroup(groupId, recipientUserId);
        if (recipientInGroup.rank == 0)
            throw new RobloxException(400, 0, "Recipient must be a member of this group");

        await InTransaction(async _ =>
        {
            using var ec = ServiceProvider.GetOrCreate<EconomyService>(this);
            await using var economyLock = await ec.AcquireEconomyLock(CreatorType.Group, groupId);
            await ec.CreateGroupBalanceIfRequired(groupId);

            var groupBalance = await ec.GetBalance(CreatorType.Group, groupId);
            if (groupBalance.robux < amount && currency == CurrencyType.Robux)
                throw new RobloxException(400, 0, "Group does not have enough funds");
            if (groupBalance.tickets < amount && currency == CurrencyType.Tickets)
                throw new RobloxException(400, 0, "Group does not have enough funds");
            // transfer funds
            await ec.DecrementCurrency(CreatorType.Group, groupId, currency, amount);
            await ec.IncrementCurrency(CreatorType.User, recipientUserId, currency, amount);
            // log in various places...
            // audit log
            await InsertAsync("group_audit_log", new
            {
                group_id = groupId,
                user_id = actorUserId,
                fund_recipient_user_id = recipientUserId,
                currency_amount = amount,
                currency_type = currency,
                action = AuditActionType.SpendGroupFunds,
            });
            // transaction for recipient
            await ec.InsertTransaction(new GroupFundRecipientTransaction(CreatorType.User, recipientUserId, groupId,
                currency, amount));
            // transaction for group
            await ec.InsertTransaction(new GroupFundPayoutTransaction(groupId, currency, amount, CreatorType.User, recipientUserId));
            return 0;
        });
    }
    private async Task UpdateGroup(long groupId)
    {
        await db.ExecuteAsync("UPDATE \"group\" SET updated_at = NOW() WHERE id = :id", new
        {
            id = groupId,
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