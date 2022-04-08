using JWT;
using JWT.Serializers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OAuth.Homework.Models;
using System.Security.Claims;
using System.Text.Json;

namespace OAuth.Homework.Controllers
{
    public class LINELoginController : Controller
    {
        private IConfiguration _config { get; }
        private IHttpContextAccessor _httpContextAccessor;
        private SubscriberContext _db;
        private IHttpClientFactory _httpClientFactory;

        public LINELoginController(IConfiguration config, IHttpContextAccessor httpContextAccessor, SubscriberContext db, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult SignIn()
        {
            string RedirectUri = GetRedirectUri();

            var qb = new QueryBuilder();
            qb.Add("response_type", "code");
            qb.Add("client_id", _config["LINELogin:client_id"]);
            qb.Add("scope", _config["LINELogin:scope"]);
            qb.Add("redirect_uri", RedirectUri);

            var state = Guid.NewGuid().ToString();
            _httpContextAccessor.HttpContext.Session.SetString("state", state);
            qb.Add("state", state);

            var authUrl = _config["LINELogin:authURL"] + qb.ToQueryString().Value;

            return Redirect(authUrl);
        }

        private string GetRedirectUri()
        {
            var currentUrl = _httpContextAccessor.HttpContext.Request.GetEncodedUrl();
            var authority = new Uri(currentUrl).GetLeftPart(UriPartial.Authority);

            var RedirectUri = _config["LINELogin:redirect_uri"];
            if (Uri.IsWellFormedUriString(RedirectUri, UriKind.Relative))
            {
                RedirectUri = authority + RedirectUri;
            }

            return RedirectUri;
        }

        public async Task<IActionResult> SigninCallback(AuthCallback auth)
        {
            if (auth.State != _httpContextAccessor.HttpContext.Session.GetString("state"))
            {
                return BadRequest();
                //return Results.BadRequest();
            }

            var RedirectUri = GetRedirectUri();

            var http = _httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new[]
            {
               new KeyValuePair<string, string>("grant_type",    "authorization_code"),
               new KeyValuePair<string, string>("code",          auth.Code),
               new KeyValuePair<string, string>("client_id",     _config["LINELogin:client_id"]),
               new KeyValuePair<string, string>("client_secret", _config["LINELogin:client_secret"]),
               new KeyValuePair<string, string>("redirect_uri",  RedirectUri),
           });

            var response = await http.PostAsync(_config["LINELogin:tokenURL"], content);
            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var result = JsonSerializer.Deserialize<LINELoginTokenResponse>(jsonString);

                // 解析 ID Token 直接取得 JWT 中的 Payload 資訊
                IJsonSerializer serializer = new JsonNetSerializer();
                IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
                IJwtDecoder decoder = new JwtDecoder(serializer, urlEncoder);

                // 將 ID Token 解開，取得重要的 ID 資訊！
                var payload = decoder.DecodeToObject<JwtPayload>(result.IdToken);

                // 呼叫 Profile API 取得個人資料，我們主要需拿到 UserId 資訊
                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + result.AccessToken);
                var profileResult = await http.GetFromJsonAsync<LINELoginProfile>(_config["LINELogin:profileURL"]);
                if (String.IsNullOrEmpty(profileResult.UserId))
                {
                    return BadRequest(profileResult);
                    //return Results.BadRequest(profileResult);
                }

                // LINE 帳號的 UserId 是不會變的資料，可以用來當成登入驗證的參考資訊
                var profile = await _db.Subscribers.FirstOrDefaultAsync(p => p.LINEUserId == profileResult.UserId);
                if (profile is null)
                {
                    // Create new account
                    profile = new Subscriber()
                    {
                        LINELoginAccessToken = result.AccessToken,
                        LINELoginIDToken = result.IdToken,
                        LINEUserId = profileResult.UserId,
                        Username = payload.Name,
                        //Email = payload.Email,
                        Photo = payload.Picture
                    };
                    _db.Subscribers.Add(profile);
                    _db.SaveChanges();
                }
                else
                {
                    profile.LINELoginAccessToken = result.AccessToken;
                    profile.LINELoginIDToken = result.IdToken;
                    profile.Username = payload.Name;
                    //profile.Email = payload.Email;
                    profile.Photo = payload.Picture;
                    _db.SaveChanges();
                }

                var claims = new List<Claim>
               {
                   new Claim(ClaimTypes.Name, profile.Id.ToString()),
                   //new Claim(ClaimTypes.Email, payload.Email),
                   new Claim("FullName", payload.Name),
                   new Claim(ClaimTypes.Role, (profile.Id == 1) ? "Admin" : "User"),
               };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await _httpContextAccessor.HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
            }
            else
            {
                var result = JsonSerializer.Deserialize<LINELoginTokenError>(jsonString);
                return BadRequest(result);
            }
        }

        public async Task<IActionResult> SignOutLine()
        {
            var revokeURL = _config["LINELogin:revokeURL"];
            var clientId = _config["LINELogin:client_id"];
            var clientSecret = _config["LINELogin:client_secret"];
            var userId = _httpContextAccessor.HttpContext.User.Identity.Name;

            var http = _httpClientFactory.CreateClient();

            // https://developers.line.biz/en/reference/line-login/#revoke-access-token
            /*
                curl -v -X POST https://api.line.me/oauth2/v2.1/revoke \
                -H "Content-Type: application/x-www-form-urlencoded" \
                -d "client_id={channel id}&client_secret={channel secret}&access_token={access token}"
            */

            var profile = await _db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == userId);
            if (profile is not null)
            {
                var content = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("client_id",     _config["LINELogin:client_id"]),
                new KeyValuePair<string, string>("client_secret", _config["LINELogin:client_secret"]),
                new KeyValuePair<string, string>("access_token",  profile.LINELoginAccessToken),
                });

                var response = await http.PostAsync(_config["LINELogin:revokeURL"], content);
                var jsonString = await response.Content.ReadAsStringAsync();

                profile.LINELoginAccessToken = "";
                profile.LINELoginIDToken = "";
                _db.SaveChanges();
            }

            await _httpContextAccessor.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
