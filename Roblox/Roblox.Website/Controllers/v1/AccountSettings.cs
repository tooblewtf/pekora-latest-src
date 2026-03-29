using System.ComponentModel.DataAnnotations;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Roblox.Website.WebsiteModels;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/accountsettings/v1")]
public class AccountSettingsControllerV1 : ControllerBase
{
    [HttpGet("email")]
    public dynamic GetEmail()
    {
        return new
        {
            emailAddress = "marine@silrev.biz",
            verified = true,
        };
    }

    [HttpGet("themes/types")]
    public dynamic GetThemes()
    {
        return new
        {
            data = Enum.GetNames<Models.Users.ThemeTypes>(),
        };
    }

    [HttpGet("themes/user")]
    public async Task<dynamic> GetUserTheme()
    {
        var settings = await services.accountInformation.GetUserTheme(safeUserSession.userId);
        return new
        {
            themeType = settings,
        };
    }

    [HttpPatch("themes/user")]
    public async Task SetUserTheme([Required, FromBody] SetThemeRequest request)
    {
        await services.accountInformation.SetUserTheme(safeUserSession.userId, request.themeType);
    }

    [HttpGet("content-restriction")]
    public dynamic GetContentRestriction()
    {
        return new
        {
            contentRestrictionLevel = "NoRestrictions",
        };
    }

    [HttpGet("inventory-privacy")]
    public async Task<dynamic> GetInventoryPrivacy()
    {
        return await services.accountInformation.GetUserInventoryPrivacy(safeUserSession.userId);
    }

    [HttpPost("inventory-privacy")]
    public async Task SetInventoryPrivacy([Required, FromBody] SetInventoryPrivacyRequest request)
    {
        await services.accountInformation.SetUserInventoryPrivacy(safeUserSession.userId, request.inventoryPrivacy);
    }

    [HttpGet("trade-privacy")]
    public async Task<dynamic> GetTradePrivacy()
    {
        var res = await services.accountInformation.GetUserTradePrivacy(safeUserSession.userId);
        return new
        {
            tradePrivacy = res,
        };
    }

    [HttpPost("trade-privacy")]
    public async Task SetTradePrivacy([Required, FromBody] SetTradePrivacyRequest request)
    {
        await services.accountInformation.SetUserTradePrivacy(safeUserSession.userId, request.tradePrivacy);
    }

    [HttpGet("trade-value")]
    public async Task<dynamic> GetTradeValue()
    {
        var res = await services.accountInformation.GetUserTradeValue(safeUserSession.userId);
        return new
        {
            tradeValue = res,
        };
    }

    [HttpPost("trade-value")]
    public async Task SetTradeValue([Required, FromBody] SetTradeValueRequest request)
    {
        await services.accountInformation.SetUserTradeValue(safeUserSession.userId, request.tradeValue);
    }

    [HttpGet("private-message-privacy")]
    public async Task<dynamic> GetPrivateMessagePrivacy()
    {
        var res = await services.accountInformation.GetUserPrivateMessagePrivacy(safeUserSession.userId);
        return new
        {
            privateMessagePrivacy = res,
        };
    }

    [HttpPost("private-message-privacy")]
    public async Task SetPrivateMessagePrivacy([Required, FromBody] PrivateMessagePrivacyRequest request)
    {
        await services.accountInformation.SetUserPrivateMessagePrivacy(safeUserSession.userId, request.privateMessagePrivacy);
    }
}