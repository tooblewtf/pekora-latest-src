using Roblox.Dto.Users;
using Roblox.Libraries.RobloxApi;
using Roblox.Logging;

namespace Roblox.Services;

public class ApplicationProcessorService : ServiceBase, IService
{

    public async Task AttemptBackgroundApplicationProcess(UserApplicationEntry entry, AppSocialMedia media)
    {
        if (entry == null || media == null || string.IsNullOrWhiteSpace(entry.id))
            return;

        // This is the only supported site right now
        if (media.site == SocialMediaSite.RobloxUserId)
        {
            var api = new RobloxApi();
            var userId = long.Parse(media.identifier);
            // account must be at least a year old for our algs to run.
            var robloxBadges = (await api.GetRobloxBadges(userId)).ToArray();
            var isYearOld = robloxBadges.FirstOrDefault(a => a.id == 12) != null;
            if (!isYearOld)
                return; // SKIP
            using var us = ServiceProvider.GetOrCreate<UsersService>();
            await us.AcquireApplicationLocks(1, new[] { entry.id });

            var hasOneHundredVisits = robloxBadges.FirstOrDefault(a => a.id == 6) != null;
            var hasOneThousandVisits = robloxBadges.FirstOrDefault(a => a.id == 7) != null;
            var isAdmin = robloxBadges.FirstOrDefault(a => a.id == 1) != null;
            if (isAdmin)
            {
                // uhh
                throw new Exception("This user has the administrator badge - wtf?");
            }

            var currentAvatar = await api.GetAvatar(userId);
            var assets = currentAvatar.assets.ToArray();
            var nonFreeItemCount = 0;
            var nonFreeUgcCount = 0;

            if (assets.Length > 0)
            {
                var details = await api.MultiGetAssetDetails(assets.Select(c => c.id).Select(c =>
                    new MultiGetDetailsRequestEntry()
                    {
                        itemType = "Asset",
                        id = c,
                    }));
                foreach (var item in details.data)
                {
                    if (item.price != 0)
                    {
                        nonFreeItemCount++;
                        if (item.creatorTargetId != 1)
                            nonFreeUgcCount++;
                    }
                }
            }

            if (nonFreeUgcCount == 0 && nonFreeItemCount < 3)
            {
                // Try inventory now
                try
                {
                    var inventory = await api.GetInventory(userId);
                    var data = await api.MultiGetAssetDetails(inventory.data.Select(c =>
                        new MultiGetDetailsRequestEntry()
                        {
                            itemType = "Asset",
                            id = c.assetId,
                        }));
                    foreach (var item in data.data)
                    {
                        if (item.price != 0)
                        {
                            nonFreeItemCount++;
                            if (item.creatorTargetId != 1)
                                nonFreeUgcCount++;
                        }
                    }
                }
                catch (InvalidUserException)
                {
                    Writer.Info(LogGroup.RealRobloxApi, "user {0} is private/terminated, ignore", userId);
                    // User has no items, just give up
                    return;
                }
            }

            if (nonFreeUgcCount == 0 && nonFreeItemCount < 3)
            {
                // Give up
                return;
            }

            var followers = await api.CountFollowers(userId);
            var friends = await api.CountFriends(userId);
            var isRichMindset = false; // user is rich/wellknown/other, don't turn this to true unless it's debugging purposes, this will make every application get accepted

            if (followers >= 20000)
            {
                // botted - this is probably somebody's main (which is a good thing!)
                isRichMindset = false;
            }

            if (hasOneThousandVisits)
                isRichMindset = true;

            if (friends > 100 && followers > 100)
                isRichMindset = true;

            if (!isRichMindset)
            {
                // check for premium/other stuff?
                var profile = await api.GetProfile(userId);
                if (profile.isPremium)
                    isRichMindset = false;

                var previousNames = profile.previousUsernames.Split("\r\n");
                if (previousNames.Length > 2)
                    isRichMindset = true;
            }
            //why u may ask people love them alts.
            isRichMindset = true;
            // Finally...
            if (isRichMindset)
            {
                Writer.Info(LogGroup.AbuseDetection, "application {0} has a rich mindset. it will be accepted", entry.id);
                await us.ProcessApplication(entry.id, 1, UserApplicationStatus.Approved);
            }
            //await us.ProcessApplication(entry.id, 1, UserApplicationStatus.Approved);
        }
    }
    
    public bool IsThreadSafe()
    {
        return false;
    }

    public bool IsReusable()
    {
        return false;
    }
}