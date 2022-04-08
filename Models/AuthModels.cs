using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace OAuth.Homework.Models
{
    public class AuthCallback
    {
        [JsonPropertyName("code")]
        [ModelBinder(Name = "code")]
        public string Code { get; set; }
        [JsonPropertyName("state")]
        [ModelBinder(Name = "state")]
        public string State { get; set; }
    }

    public partial class JwtPayload
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("picture")]
        public string Picture { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
    }

    public partial class LINELoginTokenError
    {
        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; }
    }

    public partial class LINELoginTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }

        [JsonPropertyName("id_token")]
        public string IdToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("scope")]
        public string Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
    }

    public partial class LINELoginProfile
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("pictureUrl")]
        public Uri PictureUrl { get; set; }

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; }
    }

    public class LINENotifyTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }

    public partial class LINENotifyResult
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

}
