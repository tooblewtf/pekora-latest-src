using MVC = Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using Roblox.Services.Exceptions;
using Roblox.Website.WebsiteModels.Authentication;
using System.Web;
using Roblox.Dto.Users;
using Roblox.Dto.Authentication;
using Roblox.Services.App.FeatureFlags;
using Roblox.Exceptions;
using System.Security;
using Roblox.Logging;
namespace Roblox.Website.Controllers
{

    [MVC.ApiController]
    [MVC.Route("/")]
    public class RobloxLogin: ControllerBase
    {
        [HttpPostBypass("v1/login")]
        public async Task<dynamic> LoginV1([FromBody] LoginRequest request)
        {
            FeatureCheck();
            await RateLimitCheck();
            string username = request.cvalue;
            string password = request.password;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new BadRequestException((int)LoginError400.UsernamePasswordRequired, "Username or password is missing.");

            // Format: {username}|{2facode}
            string[] splittedUsername = username.Split('|');

            username = splittedUsername[0];
            string totpCode = splittedUsername.Length == 2 ? splittedUsername[1] : "";

            UserInfo userInfo;
            try
            {
                userInfo = await services.users.GetUserByName(username);
            }
            catch (RecordNotFoundException)
            {
                throw new ForbiddenException((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again.");
            }

            if (await Login(userInfo.username, request.password, userInfo.userId, totpCode, isPasswordLeaked))
                await CreateSessionAndSetCookie(userInfo.userId);

            return new
            {
                user = new
                {
                    id = userInfo.userId,
                    name = userInfo.username,
                    displayName = userInfo.username,
                },
                isBanned = userInfo.IsDeleted()
            };

        }

        [HttpPostBypass("v2/login")]
        public async Task<dynamic> LoginV2()
        {
            FeatureCheck();
            await RateLimitCheck();
            string requestBody = await GetRequestBody();
            string? username = "";
            string? password = "";

            if (string.IsNullOrEmpty(requestBody))
                throw new BadRequestException(8, "Empty request body.");

            if (UserAgent == "RobloxStudio/WinInet")
            {
                var keyValuePairs = requestBody.Split('&');
                foreach (var pair in keyValuePairs)
                {
                    var keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        var key = HttpUtility.UrlDecode(keyValue[0]);
                        var value = HttpUtility.UrlDecode(keyValue[1]);
                        if (key == "username") username = value;
                        if (key == "password") password = value;
                    }
                }
            }
            else
            {
                try
                {
                    var loginRequest = JsonConvert.DeserializeObject<LoginRequest>(requestBody);
                    username = loginRequest?.username ?? loginRequest?.cvalue;
                    password = loginRequest?.password;
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to login");
                }
            }

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                throw new BadRequestException(3, "Username and Password are required. Please try again.");


            // Format: {username}|{2facode}
            string[] splittedUsername = username.Split('|');

            username = splittedUsername[0];
            string totpCode = splittedUsername.Length == 2 ? splittedUsername[1] : "";

            UserInfo userInfo;
            try
            {
                userInfo = await services.users.GetUserByName(username);
            }
            catch (RecordNotFoundException)
            {
                throw new ForbiddenException((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again.");
            }
            
            // return new 
            // {
            //     mediaType = "Email",
            //     tl = "a",
            //     message = "TwoStepVerificationRequired",
            // };
            await Login(username, password, userInfo.userId, totpCode, isPasswordLeaked, true);

            if (await services.users.GetTotpStatus(userInfo.userId) == TotpStatus.Enabled)
            {
                TwoFactorTicket info = new TwoFactorTicket
                {
                    userId = userInfo.userId,
                    hashedIp = GetIP(),
                };
                string ticket = await services.users.Generate2SVTicket(info);
                return new
                {
                    message = "TwoStepVerificationRequired",
                    mediaType = "Email",
                    tl = ticket,
                    code = 6,
                    twoStepVerificationData = new
                    {
                        mediaType = "Email",
                        ticket = ticket,
                    },
                    identityVerificationLoginTicket = ticket,
                    user = new
                    {
                        id = userInfo.userId,
                        name = userInfo.username,
                        displayName = userInfo.username
                    },
                };
            }

            await CreateSessionAndSetCookie(userInfo.userId);
            return new
            {
                // TODO: is there any reason why this is hard coded
                membershipType = 4,
                userInfo.username,
                name = userInfo.username,
                isUnder13 = false,
                countryCode = "US",
                userId = userInfo.userId,
                id = userInfo.userId,
                displayName = userInfo.username,
                user = new
                {
                    id = userInfo.userId,
                    name = userInfo.username,
                    displayName = userInfo.username
                },
                isBanned = false
            };
        }
        [HttpPostBypass("v3/users/{userId:long}/two-step-verification/login")]
        public async Task<dynamic> TwoStepVerificationEmailLogin([FromRoute] long userId, [FromBody] TwoFactorEmailLogin request)
        {
            FeatureCheck();
            await RateLimitCheck();
            LoginTicet ticketInfo = await services.users.GetLoginTicketInfo(request.verificationToken);

            if (ticketInfo.userId != userId || ticketInfo.challengeId != request.challengeId)
                throw new BadRequestException(5, "Invalid two step verification ticket.");

            if (ticketInfo.hashedIp != GetIP())
                throw new BadRequestException(5, "Invalid login locaton");

            Writer.Info(LogGroup.Authentication, "User {0} has logged in with 2FA.", userId);

            await services.users.DeleteTicket(request.verificationToken);
            await CreateSessionAndSetCookie(ticketInfo.userId);

            return "{}";
        }
        [HttpPostBypass("/v1/users/{userId}/challenges/email/verify")]
        public async Task<dynamic> TwoStepVerificationEmail([FromRoute] long userId, [FromBody] TwoFactorEmail request)
        {
            FeatureCheck();
            await RateLimitCheck();
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.challengeId);
                // Security check PARANOIA!
                if (info.userId != userId || info.hashedIp != GetIP())
                    throw new BadRequestException(5, "Invalid two step verification ticket.");

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "Failure2SVNotEnabled");

                if (request.code == null)
                    throw new BadRequestException(6, "Failure2SVInvalidCode");

                var totpInfo = await services.users.GetTotp(info.userId);
                if (!services.users.VerifyTotp(totpInfo.secret, request.code))
                    throw new BadRequestException(6, "Failure2SVInvalidCode");

            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }

            await services.users.DeleteTicket(request.challengeId);
            LoginTicet loginTicketInfo = new LoginTicet
            {
                userId = userId,
                challengeId = request.challengeId,
                hashedIp = GetIP(),
            };

            return new
            {
                verificationToken = await services.users.GenerateLoginTicket(loginTicketInfo)
            };
        }

        [HttpPostBypass("v1/twostepverification/verify")]
        [HttpPostBypass("v2/twostepverification/verify")]
        public async Task<dynamic> TwoStepVerification([FromBody] TwoFactor request)
        {
            FeatureCheck();
            await RateLimitCheck();
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.ticket);
                UserInfo userInfo = await services.users.GetUserById(info.userId);
                if (userInfo.username != request.username || info.hashedIp != GetIP())
                    throw new RecordNotFoundException();

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "Failure2SVNotEnabled");

                TotpInfo totpInfo = await services.users.GetTotp(info.userId);

                if (!services.users.VerifyTotp(totpInfo.secret, request.code))
                    throw new BadRequestException(6, "Failure2SVInvalidCode");

            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }
            await services.users.DeleteTicket(request.ticket);

            await CreateSessionAndSetCookie(info.userId);

            return Content("{}", "application/json");
        }
        [HttpPostBypass("v2/twostepverification/login/verify")]
        public async Task<dynamic> TwoStepVerificationLegacy([FromBody] TwoFactorLegacy request)
        {
            FeatureCheck();
            await RateLimitCheck();
            TwoFactorTicket info;
            try
            {
                info = await services.users.GetInfoFrom2SVTicket(request.tl);
                UserInfo userInfo = await services.users.GetUserById(info.userId);
                if (userInfo.username != request.username)
                    throw new RecordNotFoundException();

                if (await services.users.GetTotpStatus(info.userId) != TotpStatus.Enabled)
                    throw new BadRequestException(6, "2FA is not enabled on this account.");
                TotpInfo totpInfo = await services.users.GetTotp(info.userId);
                if (!services.users.VerifyTotp(totpInfo.secret, request.identificationCode))
                    throw new BadRequestException(6, "Incorrect 2FA code. Please try again.");

            }
            catch (RecordNotFoundException)
            {
                throw new BadRequestException(5, "Invalid two step verification ticket.");
            }
            await services.users.DeleteTicket(request.tl);
            await CreateSessionAndSetCookie(info.userId);
            return new
            {
                info.userId,
            };
        }
        [HttpPostBypass("mobileapi/login")]
        public async Task<dynamic> LegacyLogin([FromBody] LegacyLoginRequest request)
        {
            FeatureCheck();
            await RateLimitCheck();
            // Format: {username}|{2facode}
            string[] splittedUsername = request.username.Split('|');

            request.username = splittedUsername[0];
            
            string totpCode = splittedUsername.Length == 2 ? splittedUsername[1] : "";

            if (string.IsNullOrEmpty(request.username) || string.IsNullOrEmpty(request.password))
                throw new BadRequestException((int)LoginError400.UsernamePasswordRequired, "Username and Password are required. Please try again.");

            UserInfo userInfo;
            try
            {
                userInfo = await services.users.GetUserByName(request.username);
            }
            catch (RecordNotFoundException)
            {
                throw new ForbiddenException((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again.");
            }

            if(await Login(request.username, request.password, userInfo.userId, totpCode, isPasswordLeaked))
                await CreateSessionAndSetCookie(userInfo.userId);

            var userBalance = await services.economy.GetUserBalance(userInfo.userId);

            return new
            {
                Status = "OK",
                UserInfo = new
                {
                    UserName = request.username,
                    RobuxBalance = userBalance.robux,
                    TicketsBalance = userBalance.tickets,
                    IsAnyBuildersClubMember = true,
                    ThumbnailUrl = $"{Configuration.BaseUrl}/Thumbs/Avatar.ashx?userId={userInfo.userId}",
                    UserID = userInfo.userId
                }
            };
        }

        [HttpGetBypass("v2/passwords/current-status")]
        public dynamic GetPasswordStatus()
        {
            return new 
            {
                valid = userSession != null
            };
        }
        private async Task<string> CreateSessionAndSetCookie(long userId)
        {
            var sessionCookie = Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
            {
                sessionId = await services.users.CreateSession(userId),
                createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
            });
            // will be removed later this is just a hack to get the website to work :sob:
            HttpContext.Response.Cookies.Append(Middleware.SessionMiddleware.CookieName, sessionCookie, new CookieOptions()
            {
                Domain = ".silrev.biz",
                Secure = false,
                Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(364)),
                IsEssential = true,
                Path = "/",
                SameSite = SameSiteMode.Lax,
            });
            return sessionCookie;
        }
        private async Task<bool> Login(string username, string password, long userId, string? totpCode, bool isPasswordLeaked, bool? skip2FA = false)
        {
            FeatureCheck();
            await RateLimitCheck();
            //get totp info
            try
            {
                if (!await services.users.VerifyPassword(userId, password))
                    throw new ForbiddenException((int)LoginError403.IncorrectCredentials, "Incorrect username or password. Please try again");
            }
            catch (RecordNotFoundException)
            {
                throw new ForbiddenException((int)LoginError403.AccountLocked, "Your account has been locked. Please reset your password to unlock your account.");
            }

            if (skip2FA == true)
                return true;

            if (await services.users.GetTotpStatus(userId) == TotpStatus.Enabled)
            {
                TotpInfo? totpInfo = await services.users.GetTotp(userId);
                //null check
                if (string.IsNullOrEmpty(totpCode))
                    throw new ForbiddenException((int)LoginError403.IncorrectCredentials, $"You have 2FA enabled. Please login with this username format {username}|2FA Code");

                //verify totp code
                if (!services.users.VerifyTotp(totpInfo.secret, totpCode))
                    throw new ForbiddenException((int)LoginError403.IncorrectCredentials, "Incorrect 2FA code. Please try again.");
            }

            return true;
        }
        private async Task RateLimitCheck()
        {
            var loginKey = "LoginAttemptCountV1:" + GetIP();
            var attemptCount = (await services.cooldown.GetBucketDataForKey(loginKey, TimeSpan.FromMinutes(10))).ToArray();

            if (!await services.cooldown.TryIncrementBucketCooldown(loginKey, 15, TimeSpan.FromMinutes(10), attemptCount, true))
            {
                throw new ForbiddenException((int)LoginError403.TooManyAttempts, "Too many attempts please wait 10 minutes before trying again.");
            }
        }
        private void FeatureCheck()
        {
            try
            {
                FeatureFlags.FeatureCheck(FeatureFlag.LoginEnabled);
            }
            catch (RobloxException)
            {
                throw new RobloxException(503, (int)LoginError503.ServiceUnavailable, "Login is currently disabled. Please try again later.");
            }
        }
    }
}