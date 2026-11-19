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
        private string guestProfile;
        private string previousProfile;
        private bool destroyed;

        // Start the required Unity service environment and sign in with the diagnostic guest profile.
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
                var options = new InitializationOptions().SetEnvironmentName(environment);
                await UnityServices.InitializeAsync(options);
            }
            initializedEnvironment = environment;
            if (destroyed) return;

            stage("Anonymous authentication");
            var authentication = AuthenticationService.Instance;
            if (authentication.IsSignedIn) return;

            // Keep temporary Relay guests out of the profile used for remembered game accounts.
            if (guestProfile == null)
            {
                previousProfile = authentication.Profile;
                guestProfile = "relay-" + Guid.NewGuid().ToString("N").Substring(0, 24);
            }
            authentication.SwitchProfile(guestProfile);
            try
            {
                await authentication.SignInAnonymouslyAsync();
            }
            finally
            {
                if (destroyed) ReleaseGuestProfile();
            }
        }

        // Release the diagnostic identity when this scene is removed.
        private void OnDestroy()
        {
            destroyed = true;
            if (signIn == null || signIn.IsCompleted) ReleaseGuestProfile();
        }

        // Return from the temporary Relay-test profile without keeping it as the game account.
        private void ReleaseGuestProfile()
        {
            if (guestProfile == null || UnityServices.State != ServicesInitializationState.Initialized) return;
            var authentication = AuthenticationService.Instance;
            if (authentication.Profile != guestProfile) return;
            if (authentication.IsSignedIn) authentication.SignOut(true);
            else authentication.ClearSessionToken();
            authentication.SwitchProfile(previousProfile);
        }
    }
}
