using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ConsoleAppFramework;

using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

using Utf8Json;

using ZLogger;

namespace DiVVe.API.Spotify
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
                throw new NullReferenceException(nameof(ClientId));

            var spotify = File.Exists(CredentialsPath) ? await this.GetClient() : await this.GetClientWithAuth();
        }

        public async Task Update()
        {
            throw new NotImplementedException();
        }

        public async Task RemoveDuplicates()
        {
            throw new NotImplementedException();
        }

        public async Task Sort()
        {
            throw new NotImplementedException();
        }

        public async Task Export()
        {
            throw new NotImplementedException();
        }

        public async Task Import()
        {
            throw new NotImplementedException();
        }

        private async Task<SpotifyClient> GetClient(PKCETokenResponse? token = null)
        {
            if (token is null)
            {
                using var stream = new FileStream(CredentialsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                token = await JsonSerializer.DeserializeAsync<PKCETokenResponse>(stream);
            }

            var authenticator = new PKCEAuthenticator(ClientId!, token);
            authenticator.TokenRefreshed += async (sender, token) =>
            {
                using var stream = new FileStream(CredentialsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, token);
            };

            var config = SpotifyClientConfig.CreateDefault()
              .WithAuthenticator(authenticator);

            var spotify = new SpotifyClient(config);
            var me = await spotify.UserProfile.Current();
            this.Context.Logger.ZLogInformation($"Login: {me.DisplayName} ({me.Id})");

            return spotify;
        }

        private async Task<SpotifyClient> GetClientWithAuth()
        {
            var source = new TaskCompletionSource<SpotifyClient>();

            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            await server.Start();
            server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await server.Stop();
                var token = await new OAuthClient().RequestToken(
                  new PKCETokenRequest(ClientId!, response.Code, server.BaseUri, verifier)
                );

                using var stream = new FileStream(CredentialsPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, token);
                source.SetResult(await this.GetClient(token));
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

            return await source.Task;
        }
    }
}
