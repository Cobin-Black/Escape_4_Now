using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

namespace Escape4Now.Accounts
{
    //Online accounts: restoring Player 1's remembered login, logging in, creating accounts, logging out,
    //and linking Players 2-4. Only one account can be signed in at a time, so Player 1 stays signed in
    //and a linked player's login is kept in that slot's own profile for when their stats are saved.
    public static class AccountService
    {
        //Change this to "production" for the release build.
        private const string TargetEnvironment = "development";
        //private const string TargetEnvironment = "production";
        //Keeps Player 1's remembered login apart from the ones Players 2-4 use.
        private const string PlayerOneProfile = "player1";
        private const int PlayerSlots = 4;
        private const int MaxUsernameLength = 12;
        private const int MinPasswordLength = 8;
        private const int MaxPasswordLength = 30;

        private static string username;
        private static readonly string[] linkedNames = new string[PlayerSlots];
        private static readonly string[] linkedIds = new string[PlayerSlots];

        public static bool IsBusy { get; private set; }
        public static bool IsOffline { get; private set; }
        public static bool IsGuest { get; private set; }

        //Set when the main menu sends the player back to log in, so the start screen opens the login panel.
        public static bool LoginRequested { get; set; }

        public static int UsernameLimit => MaxUsernameLength;
        public static int PasswordLimit => MaxPasswordLength;

        private static IAuthenticationService Auth => AuthenticationService.Instance;
        private static bool ServicesReady => UnityServices.State == ServicesInitializationState.Initialized;

        public static bool IsSignedIn => ServicesReady && Auth.IsSignedIn;
        public static bool CanEnterGame => !IsBusy && (IsSignedIn || IsGuest);
        public static string DisplayName => !IsSignedIn ? "GUEST" : string.IsNullOrEmpty(username) ? "PLAYER 1" : username.ToUpperInvariant();

        //Domain reload is off in this project, so values would otherwise carry over between Play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            username = null;
            IsBusy = false;
            IsOffline = false;
            IsGuest = false;
            LoginRequested = false;
            Array.Clear(linkedNames, 0, PlayerSlots);
            Array.Clear(linkedIds, 0, PlayerSlots);
        }

        //Slot 0 is Player 1, who is linked whenever they are signed in.
        public static bool IsLinked(int slot) => slot == 0 ? IsSignedIn : slot > 0 && slot < PlayerSlots && linkedNames[slot] != null;
        public static string LinkedName(int slot) => slot == 0 ? DisplayName : IsLinked(slot) ? linkedNames[slot].ToUpperInvariant() : "";

        //Starts the online services and signs back in if this PC remembers a login.
        public static async Task ConnectAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            IsOffline = false;
            try
            {
                await StartServices();
                if (!IsSignedIn)
                {
                    //Linked players only last for one game session, unlike Player 1.
                    ForgetLinkedPlayers();
                    if (Auth.SessionTokenExists)
                        await Auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
                }
                if (IsSignedIn && string.IsNullOrEmpty(username))
                    username = (await Auth.GetPlayerInfoAsync()).Username;
            }
            catch (RequestFailedException error) when (!IsNetworkError(error))
            {
                //The remembered login is no longer valid, so the player has to log in again.
                Debug.LogWarning("Could not restore the login: " + error.Message);
                if (ServicesReady) Auth.SignOut(true);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not reach the account service: " + error.Message);
                IsOffline = true;
                if (IsSignedIn) Auth.SignOut();
            }
            finally
            {
                IsBusy = false;
            }
        }

        //These return an error message for the player, or null when it worked.
        public static Task<string> LogInAsync(string name, string password) =>
            SignInAsync(name, () => Auth.SignInWithUsernamePasswordAsync(name, password), "Wrong username or password.");

        public static Task<string> CreateAccountAsync(string name, string password) =>
            SignInAsync(name, () => Auth.SignUpWithUsernamePasswordAsync(name, password), "Could not create that account. Try another username.");

        private static async Task<string> SignInAsync(string name, Func<Task> signIn, string failure)
        {
            if (IsBusy) return null;
            IsBusy = true;
            IsOffline = false;
            try
            {
                await StartServices();
                await signIn();
                username = name;
                IsGuest = false;
                return null;
            }
            catch (RequestFailedException error) when (!IsNetworkError(error))
            {
                Debug.LogWarning("Account request failed: " + error.Message);
                if (error.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) return "That username is already taken.";
                if (error.ErrorCode == AuthenticationErrorCodes.BannedUser) return "This account has been banned.";
                if (error.ErrorCode == CommonErrorCodes.TooManyRequests) return "Too many tries. Wait a moment and try again.";
                return failure;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not reach the account service: " + error.Message);
                IsOffline = true;
                return null;
            }
            finally
            {
                IsBusy = false;
            }
        }

        //Signs a slot's account in once so its login is remembered, then returns to Player 1.
        //Returns an error message for the player, or null when it worked.
        public static async Task<string> LinkPlayerAsync(int slot, string name, string password)
        {
            if (IsBusy || !IsSignedIn) return "Player 1 needs to be logged in first.";
            if (IsNameInUse(name)) return "That account is already linked.";
            IsBusy = true;
            string playerOneId = Auth.PlayerId;
            string error = await SignInLinkedPlayer(slot, name, password, playerOneId);
            try
            {
                if (Auth.IsSignedIn) Auth.SignOut(error != null);
                Auth.SwitchProfile(PlayerOneProfile);
                await Auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
            }
            catch (Exception failure)
            {
                Debug.LogWarning("Could not return to Player 1: " + failure.Message);
                linkedNames[slot] = null;
                linkedIds[slot] = null;
                error = "Lost connection. Restart the game to log back in.";
            }
            IsBusy = false;
            return error;
        }

        private static async Task<string> SignInLinkedPlayer(int slot, string name, string password, string playerOneId)
        {
            try
            {
                Auth.SignOut();
                Auth.SwitchProfile(ProfileFor(slot));
                Auth.ClearSessionToken();
                await Auth.SignInWithUsernamePasswordAsync(name, password);
                if (Auth.PlayerId == playerOneId || Array.IndexOf(linkedIds, Auth.PlayerId) >= 0)
                    return "That account is already linked.";
                linkedNames[slot] = name;
                linkedIds[slot] = Auth.PlayerId;
                return null;
            }
            catch (RequestFailedException error) when (!IsNetworkError(error))
            {
                Debug.LogWarning("Could not link the account: " + error.Message);
                return error.ErrorCode == CommonErrorCodes.TooManyRequests ? "Too many tries. Wait a moment and try again." : "Wrong username or password.";
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not reach the account service: " + error.Message);
                return "No internet connection.";
            }
        }

        //Runs the action once per slot while signed in as that slot's account, then returns to Player 1.
        //Slots must be in order so Player 1 goes first. Returns false if any slot failed.
        public static async Task<bool> RunAsEachAccount(IEnumerable<int> slots, Func<int, Task> action)
        {
            if (IsBusy || !IsSignedIn) return false;
            IsBusy = true;
            bool allWorked = true;
            foreach (int slot in slots)
            {
                try
                {
                    if (slot > 0) await SwitchToLinkedPlayer(slot);
                    await action(slot);
                }
                catch (Exception error)
                {
                    Debug.LogWarning("Could not finish the request for Player " + (slot + 1) + ": " + error.Message);
                    allWorked = false;
                    //Player 1 is still signed in here, so stop before losing that if the internet is down.
                    if (slot == 0) break;
                }
            }
            try
            {
                if (Auth.Profile != PlayerOneProfile)
                {
                    if (Auth.IsSignedIn) Auth.SignOut();
                    Auth.SwitchProfile(PlayerOneProfile);
                    await Auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Could not return to Player 1: " + error.Message);
                allWorked = false;
            }
            IsBusy = false;
            return allWorked;
        }

        private static async Task SwitchToLinkedPlayer(int slot)
        {
            if (Auth.IsSignedIn) Auth.SignOut();
            Auth.SwitchProfile(ProfileFor(slot));
            await Auth.SignInAnonymouslyAsync(new SignInOptions { CreateAccount = false });
            if (Auth.PlayerId != linkedIds[slot]) throw new InvalidOperationException("The saved login belongs to a different account.");
        }

        //The slot goes back to being a guest. Its saved login is erased on log out, restart or the next link.
        public static void UnlinkPlayer(int slot)
        {
            if (slot <= 0 || slot >= PlayerSlots) return;
            linkedNames[slot] = null;
            linkedIds[slot] = null;
        }

        private static bool IsNameInUse(string name)
        {
            if (string.Equals(name, username, StringComparison.OrdinalIgnoreCase)) return true;
            return linkedNames.Any(linked => string.Equals(name, linked, StringComparison.OrdinalIgnoreCase));
        }

        //Erases every saved login for Players 2-4. Only works while nobody is signed in.
        private static void ForgetLinkedPlayers()
        {
            for (int slot = 1; slot < PlayerSlots; slot++)
            {
                UnlinkPlayer(slot);
                Auth.SwitchProfile(ProfileFor(slot));
                Auth.ClearSessionToken();
            }
            Auth.SwitchProfile(PlayerOneProfile);
        }

        //Forgets this PC's logins, so the next group must enter their own.
        public static void LogOut()
        {
            if (IsSignedIn)
            {
                Auth.SignOut(true);
                ForgetLinkedPlayers();
            }
            username = null;
            IsGuest = false;
            LoginRequested = true;
        }

        public static void PlayAsGuest()
        {
            IsGuest = true;
        }

        //Checks the same rules as Unity's server, so the player gets a clear message before anything is sent.
        public static string CheckNewAccount(string name, string password, string confirmPassword)
        {
            if (!Regex.IsMatch(name, "^[A-Za-z0-9.@_-]{3," + MaxUsernameLength + "}$"))
                return "Usernames need 3-" + MaxUsernameLength + " letters, numbers or . - @ _";
            if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength
                || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit)
                || password.All(char.IsLetterOrDigit))
                return "Passwords need " + MinPasswordLength + "-" + MaxPasswordLength + " characters with an uppercase letter, lowercase letter, number and symbol.";
            return password == confirmPassword ? null : "The passwords don't match.";
        }

        private static async Task StartServices()
        {
            if (!ServicesReady)
                await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(TargetEnvironment));
            if (!Auth.IsSignedIn && Auth.Profile != PlayerOneProfile)
                Auth.SwitchProfile(PlayerOneProfile);
        }

        private static string ProfileFor(int slot) => "player" + (slot + 1);

        private static bool IsNetworkError(RequestFailedException error) =>
            error.ErrorCode == CommonErrorCodes.TransportError || error.ErrorCode == CommonErrorCodes.Timeout
            || error.ErrorCode == CommonErrorCodes.ServiceUnavailable;
    }
}
