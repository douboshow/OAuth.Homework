using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OAuth.Homework.Models;
using System.Text.Json;

namespace OAuth.Homework.Controllers
{
    public class LINENotifyController : Controller
    {
        private IConfiguration _config { get; }
        private IHttpContextAccessor _httpContextAccessor;
        private SubscriberContext _db;
        private IHttpClientFactory _httpClientFactory;

        public LINENotifyController(IConfiguration config, IHttpContextAccessor httpContextAccessor, SubscriberContext db, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Subscribe()
        {
            string RedirectUri = GetRedirectUri();

            var qb = new QueryBuilder();
            qb.Add("response_type", "code");
            qb.Add("client_id", _config["LINENotify:client_id"]);
            qb.Add("scope", _config["LINENotify:scope"]);
            qb.Add("redirect_uri", RedirectUri);

            var state = Guid.NewGuid().ToString();
            _httpContextAccessor.HttpContext.Session.SetString("state", state);
            qb.Add("state", state);

            var authUrl = _config["LINENotify:authURL"] + qb.ToQueryString().Value;

            return Redirect(authUrl);
        }

        private string GetRedirectUri()
        {
            var currentUrl = _httpContextAccessor.HttpContext.Request.GetEncodedUrl();
            var authority = new Uri(currentUrl).GetLeftPart(UriPartial.Authority);

            var RedirectUri = _config["LINENotify:redirect_uri"];
            if (Uri.IsWellFormedUriString(RedirectUri, UriKind.Relative))
            {
                RedirectUri = authority + RedirectUri;
            }

            return RedirectUri;
        }

        public async Task<IActionResult> SubscribeCallback(AuthCallback auth)
        {
            if (auth.State != _httpContextAccessor.HttpContext.Session.GetString("state"))
            {
                return BadRequest();
            }

            var RedirectUri = GetRedirectUri();

            var http = _httpClientFactory.CreateClient();

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type",    "authorization_code"),
                new KeyValuePair<string, string>("code",          auth.Code),
                new KeyValuePair<string, string>("client_id",     _config["LINENotify:client_id"]),
                new KeyValuePair<string, string>("client_secret", _config["LINENotify:client_secret"]),
                new KeyValuePair<string, string>("redirect_uri",  RedirectUri),
            });

            var response = await http.PostAsync(_config["LINENotify:tokenURL"], content);
            var jsonString = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var result = JsonSerializer.Deserialize<LINENotifyTokenResponse>(jsonString);

                var userId = _httpContextAccessor.HttpContext.User.Identity.Name;

                var profile = await _db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == userId);
                if (profile is null)
                {
                    return RedirectToAction("SignOutLine", "LINELogin");
                }
                else
                {
                    profile.LINENotifyAccessToken = result.AccessToken;
                    await _db.SaveChangesAsync();

                    #region 歡迎訂閱訊息

                    var httpMeaagse = _httpClientFactory.CreateClient();
                    httpMeaagse.DefaultRequestHeaders.Add("Authorization", "Bearer " + result.AccessToken);

                    var contentMeaagse = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("message", "Welcome to subscribe!")
                    });

                    var responseMeaagse = await httpMeaagse.PostAsync(_config["LINENotify:notifyURL"], contentMeaagse);
                    //var jsonStringMeaagse = await responseMeaagse.Content.ReadAsStringAsync();
                    //var resultMeaagse = JsonSerializer.Deserialize<LINENotifyResult>(jsonStringMeaagse);

                    //if (responseMeaagse.StatusCode == System.Net.HttpStatusCode.OK)
                    //{

                    //}

                    #endregion

                    return RedirectToAction("Index", "Home");

                }
            }
            else
            {
                var result = JsonSerializer.Deserialize<LINELoginTokenError>(jsonString);
                return BadRequest(result);
            }
        }

        public async Task<IActionResult> Unsubscribe()
        {
            var revokeURL = _config["LINENotify:revokeURL"];

            var profile = await _db.Subscribers.FirstOrDefaultAsync(p => p.Id.ToString() == _httpContextAccessor.HttpContext.User.Identity.Name);
            if (profile is null)
            {
                return RedirectToAction("SignOutLine", "LINELogin");
            }
            else
            {
                var http = _httpClientFactory.CreateClient();

                if (String.IsNullOrEmpty(profile.LINENotifyAccessToken))
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    http.DefaultRequestHeaders.Add("Authorization", "Bearer " + profile.LINENotifyAccessToken);
                    var response = await http.PostAsync(_config["LINENotify:revokeURL"], null);
                    var jsonString = await response.Content.ReadAsStringAsync();

                    profile.LINENotifyAccessToken = "";
                    _db.SaveChanges();

                    var result = JsonSerializer.Deserialize<LINENotifyResult>(jsonString);
                    return RedirectToAction("Index", "Home");
                }
            }
        }

        public async Task<IResult> NotifyAll()
        {
            var all = _db.Subscribers.Where(p => !String.IsNullOrEmpty(p.LINENotifyAccessToken))
                                     .Select(p => new { p.LINENotifyAccessToken, p.Username }).ToList();

            if (all.Count > 0)
            {
                var results = new List<string>();
                foreach (var item in all)
                {
                    var http = _httpClientFactory.CreateClient();
                    http.DefaultRequestHeaders.Add("Authorization", "Bearer " + item.LINENotifyAccessToken);

                    var content = new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("message", "Hello LINENotify!")
                });

                    var response = await http.PostAsync(_config["LINENotify:notifyURL"], content);
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<LINENotifyResult>(jsonString);

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        results.Add($"Sending to {item.Username}: {result.Message}");
                    }
                    else
                    {
                        results.Add($"Sending to {item.Username} failed: {result.Message} ({result.Status})");
                    }
                }

                results.Add($"We already notified {all.Count} subscribers!");

                return Results.Ok(results);
            }
            else
            {
                return Results.Ok("No subscribers!");
            }
        }
    }
}
