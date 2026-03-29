using MVC = Microsoft.AspNetCore.Mvc;
namespace Roblox.Website.Controllers
{

    [MVC.ApiController]
    [MVC.Route("/")]
    public class WellKnown : ControllerBase
    {
        [HttpGetBypass(".well-known/discord")]
        public string WellKnownDiscord()
        {
            return "No link for u";
        }

        [HttpGetBypass("oauth/.well-known/openid-configuration")]
        public dynamic WellKnownOpenIdConfiguration()
        {
            return new
            {
                issuer = "https://apis.silrev.biz/oauth/",
                authorization_endpoint = "https://apis.silrev.biz/oauth/v1/authorize",
                token_endpoint = "https://apis.silrev.biz/oauth/v1/token",
                introspection_endpoint = "https://apis.silrev.biz/oauth/v1/token/introspect",
                revocation_endpoint = "https://apis.silrev.biz/oauth/v1/token/revoke",
                resources_endpoint = "https://apis.silrev.biz/oauth/v1/token/resources",
                userinfo_endpoint = "https://apis.silrev.biz/oauth/v1/userinfo",
                jwks_uri = "https://apis.silrev.biz/oauth/v1/certs",
                registration_endpoint = "https://create.silrev.biz/dashboard/credentials",
                service_documentation = "https://create.silrev.biz/docs/reference/cloud",
                scopes_supported = new[]
                {
                    "openid",
                    "profile",
                    "email",
                    "verification",
                    "credentials",
                    "age",
                    "premium",
                    "roles",
                },
                response_types_supported = new[] { "none", "code" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "ES256" },
                claims_supported = new[]
                {
                    "sub",
                    "type",
                    "iss",
                    "aud",
                    "exp",
                    "iat",
                    "nonce",
                    "name",
                    "nickname",
                    "preferred_username",
                    "created_at",
                    "profile",
                    "picture",
                    "email",
                    "email_verified",
                    "verified",
                    "age_bracket",
                    "premium",
                    "roles",
                    "internal_user",
                },
                token_endpoint_auth_methods_supported = new[]
                {
                    "client_secret_post",
                    "client_secret_basic",
                },
            };
        }
    }
}
