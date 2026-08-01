using System;
using System.Reflection;
using EntityComponent;
using JumpKing.Player;

namespace EmoteMod
{
    /// <summary>
    /// Optional binding to Local Multiplayer Mod.
    ///
    /// An emote belongs to whoever asked for it, and it is drawn in world space
    /// above that player's head. Both of those need answers this mod cannot give
    /// on its own once more than one player exists: which player a chat user
    /// controls, and whether that player appears in the view currently being
    /// drawn. Without the second check a split-screen view draws every emote,
    /// including ones belonging to a player it is not showing.
    ///
    /// When the multiplayer mod is absent, every call falls back to the single
    /// player and the behaviour is unchanged.
    /// </summary>
    internal static class TargetPlayerResolver
    {
        private const string ApiTypeName =
            "LocalMultiplayerMod.LocalMultiplayerApi";

        private delegate PlayerEntity ResolvePlayerDelegate(string user);
        private delegate bool IsPlayerInCurrentViewDelegate(PlayerEntity player);

        private static int _lastResolveAssemblyCount = -1;
        private static ResolvePlayerDelegate _resolvePlayer;
        private static IsPlayerInCurrentViewDelegate _isPlayerInCurrentView;

        public static void ResolveApi()
        {
            if (_resolvePlayer != null && _isPlayerInCurrentView != null)
            {
                return;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_lastResolveAssemblyCount == assemblies.Length)
            {
                return;
            }

            _lastResolveAssemblyCount = assemblies.Length;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type apiType = assemblies[i].GetType(ApiTypeName, false);
                if (apiType == null)
                {
                    continue;
                }

                _resolvePlayer = CreateDelegate<ResolvePlayerDelegate>(
                    apiType,
                    "ResolvePlayer"
                );
                _isPlayerInCurrentView =
                    CreateDelegate<IsPlayerInCurrentViewDelegate>(
                        apiType,
                        "IsPlayerInCurrentView"
                    );
                return;
            }
        }

        /// <summary>
        /// The player a chat user controls, or the only player when the
        /// multiplayer mod is absent.
        /// </summary>
        public static PlayerEntity ResolvePlayer(string user)
        {
            ResolveApi();
            if (_resolvePlayer != null)
            {
                return _resolvePlayer(user);
            }

            return PrimaryPlayer;
        }

        /// <summary>
        /// The player the local keyboard drives.
        /// </summary>
        public static PlayerEntity PrimaryPlayer
        {
            get
            {
                return EntityManager.instance == null ? null :
                    EntityManager.instance.Find<PlayerEntity>();
            }
        }

        public static bool IsPlayerInCurrentView(PlayerEntity player)
        {
            ResolveApi();
            if (_isPlayerInCurrentView != null)
            {
                return _isPlayerInCurrentView(player);
            }

            return player != null && ReferenceEquals(player, PrimaryPlayer);
        }

        private static T CreateDelegate<T>(Type apiType, string methodName)
            where T : class
        {
            MethodInfo method = apiType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static
            );
            return method == null ? null :
                Delegate.CreateDelegate(typeof(T), method, false) as T;
        }
    }
}
