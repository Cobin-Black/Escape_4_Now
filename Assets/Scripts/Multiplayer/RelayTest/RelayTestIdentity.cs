using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Escape4Now.Multiplayer.RelayTesting
{
    // Authentication stays separate so future account login can replace this test identity.
    public sealed class RelayTestIdentity : MonoBehaviour
    {
        private Task signIn;
        private string initializedEnvironment;

        public Task SignInAsync(string environment, Action<string> stage)
        {
            if (signIn != null && !signIn.IsCompleted) return signIn;
            if (initializedEnvironment != null && initializedEnvironment != environment)
                throw new InvalidOperationException("Restart the test app after changing its services environment.");
            signIn = InitializeAsync(environment, stage);
            return signIn;
        }

        private async Task InitializeAsync(string environment, Action<string> stage)
        {
            stage("Unity Services initialization");
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Two test processes must not accidentally reuse the same cached anonymous account.
                var options = new InitializationOptions()
                    .SetEnvironmentName(environment)
                    .SetProfile("relay-" + Guid.NewGuid().ToString("N").Substring(0, 24));
                await UnityServices.InitializeAsync(options);
            }
            initializedEnvironment = environment;
            stage("Anonymous authentication");
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
