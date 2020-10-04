using System;
using System.IO;
using System.Threading.Tasks;

using ConsoleAppFramework;

using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

using Utf8Json;

using ZLogger;

namespace DiVVe.API
{
    public class Spotify : ConsoleAppBase
    {
        private const string CredentialsPath = "credentials.json";
        private static readonly string? ClientId = Environment.GetEnvironmentVariable("DiVVeSpotifyClientId");
        private static readonly Uri ServerUrl = new Uri("http://localhost:5000/callback");
        private static readonly int ServerPort = 5000;
        private static readonly EmbedIOAuthServer server = new EmbedIOAuthServer(ServerUrl, ServerPort);

        public async Task Ping() => this.Context.Logger.ZLogInformation("pong!");

        public async Task Auth()
        {
            if (string.IsNullOrEmpty(ClientId))
            {
                throw new NullReferenceException(nameof(ClientId));
            }

            if (File.Exists(CredentialsPath))
            {
                await this.Start();
            }
            else
            {
                await this.StartAuthentication();
            }
        }

        private async Task Start()
        {
            var json = await File.ReadAllTextAsync(CredentialsPath);
            var token = JsonSerializer.Deserialize<PKCETokenResponse>(json);

            var authenticator = new PKCEAuthenticator(ClientId!, token);
            authenticator.TokenRefreshed += (sender, token) => File.WriteAllBytes(CredentialsPath, JsonSerializer.Serialize(token));

            var config = SpotifyClientConfig.CreateDefault()
              .WithAuthenticator(authenticator);

            var spotify = new SpotifyClient(config);

            var me = await spotify.UserProfile.Current();
            this.Context.Logger.ZLogInformation($"Welcome {me.DisplayName} ({me.Id}), you're authenticated!");

            var playlists = await spotify.PaginateAll(await spotify.Playlists.CurrentUsers().ConfigureAwait(false));
            this.Context.Logger.ZLogInformation($"Total Playlists in your Account: {playlists.Count}");

            await server.Stop();
        }

        private async Task StartAuthentication()
        {
            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await server.Start();
            server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await server.Stop();
                var token = await new OAuthClient().RequestToken(
                  new PKCETokenRequest(ClientId!, response.Code, server.BaseUri, verifier)
                );

                await File.WriteAllBytesAsync(CredentialsPath, JsonSerializer.Serialize(token));
                await this.Start();
            };

            var request = new LoginRequest(server.BaseUri, ClientId!, LoginRequest.ResponseType.Code)
            {
                CodeChallenge = challenge,
                CodeChallengeMethod = "S256",
                Scope = new[]
                {
                    Scopes.UserReadEmail,
                    Scopes.UserReadPrivate,
                    Scopes.PlaylistReadPrivate,
                    Scopes.PlaylistReadCollaborative
                }
            };

            var uri = request.ToUri();
            try
            {
                BrowserUtil.Open(uri);
            }
            catch (Exception)
            {
                this.Context.Logger.ZLogInformation("Unable to open URL, manually open: {0}", uri);
            }
        }
    }
}
