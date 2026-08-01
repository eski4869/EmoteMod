using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EntityComponent;
using JumpKing;
using JumpKing.Mods;
using JumpKing.Player;
using JumpKing.Util.Tags;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace EmoteMod
{
    [JumpKingMod("eski4869.EmoteMod")]
    public static class ModEntry
    {
        private static readonly EmoteDefinition[] Emotes =
        {
            new EmoteDefinition("emote_happy.png", "EmoteMod.Assets.Defaults.happy.png"),
            new EmoteDefinition("emote_sad.png", "EmoteMod.Assets.Defaults.sad.png"),
            new EmoteDefinition("emote_thinking.png", "EmoteMod.Assets.Defaults.thinking.png"),
            new EmoteDefinition("emote_angry.png", "EmoteMod.Assets.Defaults.angry.png")
        };
        private const string CommandTarget = "emote";

        private static string _modDirectory;

        internal static string ModDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_modDirectory))
                {
                    _modDirectory = Path.GetDirectoryName(
                        Assembly.GetExecutingAssembly().Location
                    );
                }

                return _modDirectory;
            }
        }

        internal static IReadOnlyList<EmoteDefinition> Definitions
        {
            get { return Emotes; }
        }

        [BeforeLevelLoad]
        public static void BeforeLevelLoad()
        {
            BrokerCommandClient.Register(CommandTarget);
            WriteDefaultImagesIfMissing();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
            BrokerCommandClient.Register(CommandTarget);
            TargetPlayerResolver.ResolveApi();
            EmoteDisplay.EnsureAdded();
        }

        private static void WriteDefaultImagesIfMissing()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            for (int i = 0; i < Emotes.Length; i++)
            {
                EmoteDefinition definition = Emotes[i];
                string outputPath = Path.Combine(ModDirectory, definition.FileName);

                if (File.Exists(outputPath))
                {
                    continue;
                }

                try
                {
                    using (Stream resource = assembly.GetManifestResourceStream(
                        definition.ResourceName
                    ))
                    {
                        if (resource == null)
                        {
                            continue;
                        }

                        using (FileStream output = File.Create(outputPath))
                        {
                            resource.CopyTo(output);
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }

    public sealed class EmoteDefinition
    {
        public EmoteDefinition(string fileName, string resourceName)
        {
            FileName = fileName;
            ResourceName = resourceName;
        }

        public string FileName { get; private set; }
        public string ResourceName { get; private set; }
    }

    public sealed class EmoteDisplay : Entity, IForeground
    {
        private const float DisplayDurationSeconds = 2f;
        private const int DrawSize = 32;
        private const int HeadOffset = 6;
        private const string CommandTarget = "emote";

        private static EmoteDisplay _instance;
        private static readonly Random Random = new Random();

        private readonly Dictionary<string, Texture2D> _textures =
            new Dictionary<string, Texture2D>();

        /// <summary>
        /// One active emote per player. A single field would mean the players
        /// share one emote, so whoever asked last would overwrite the others.
        /// </summary>
        private readonly Dictionary<PlayerEntity, ActiveEmote> _active =
            new Dictionary<PlayerEntity, ActiveEmote>();

        private KeyboardState _previousKeyboardState;

        private sealed class ActiveEmote
        {
            public Texture2D Texture;
            public float RemainingSeconds;
        }

        public static void EnsureAdded()
        {
            if (EntityManager.instance == null)
            {
                return;
            }

            if (_instance != null && _instance.IsAlive)
            {
                return;
            }

            _instance = new EmoteDisplay();
            EntityManager.instance.AddObject(_instance);
        }

        private EmoteDisplay()
        {
            LoadTextures();
            _previousKeyboardState = Keyboard.GetState();
        }

        protected override void Update(float delta)
        {
            KeyboardState keyboardState = Keyboard.GetState();
            ProcessBrokerCommand();

            bool shiftDown =
                keyboardState.IsKeyDown(Keys.LeftShift) ||
                keyboardState.IsKeyDown(Keys.RightShift);

            if (shiftDown)
            {
                // The keyboard drives the player sitting at this machine.
                PlayerEntity local = TargetPlayerResolver.PrimaryPlayer;

                if (WasKeyPressed(keyboardState, Keys.Up))
                {
                    Show(local, "emote_happy.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Left))
                {
                    Show(local, "emote_sad.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Right))
                {
                    Show(local, "emote_thinking.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Down))
                {
                    Show(local, "emote_angry.png");
                }
            }

            _previousKeyboardState = keyboardState;
            Expire(delta);
        }

        private void Expire(float delta)
        {
            List<PlayerEntity> finished = null;

            foreach (KeyValuePair<PlayerEntity, ActiveEmote> entry in _active)
            {
                ActiveEmote emote = entry.Value;
                emote.RemainingSeconds = Math.Max(
                    0f,
                    emote.RemainingSeconds - delta
                );

                if (emote.RemainingSeconds > 0f && entry.Key.IsAlive)
                {
                    continue;
                }

                if (finished == null)
                {
                    finished = new List<PlayerEntity>();
                }

                finished.Add(entry.Key);
            }

            if (finished == null)
            {
                return;
            }

            for (int i = 0; i < finished.Count; i++)
            {
                _active.Remove(finished[i]);
            }
        }

        public void ForegroundDraw()
        {
            if (Game1.instance == null || _active.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<PlayerEntity, ActiveEmote> entry in _active)
            {
                PlayerEntity player = entry.Key;
                ActiveEmote emote = entry.Value;

                if (player == null || !player.IsAlive ||
                    emote.Texture == null || emote.RemainingSeconds <= 0f)
                {
                    continue;
                }

                // Split screen draws this once per view. Without asking whose
                // view it is, every view would show every emote - and because
                // the position is clamped on screen below, an emote belonging to
                // a player this view is not showing would still be pinned to the
                // edge rather than falling outside it.
                if (!TargetPlayerResolver.IsPlayerInCurrentView(player))
                {
                    continue;
                }

                Rectangle hitbox = Camera.TransformRect(player.m_body.GetHitbox());
                int x = hitbox.Center.X - DrawSize / 2;
                int y = hitbox.Top - DrawSize - HeadOffset;

                x = Math.Max(2, Math.Min(Game1.WIDTH - DrawSize - 2, x));
                y = Math.Max(2, Math.Min(Game1.HEIGHT - DrawSize - 2, y));

                Game1.spriteBatch.Draw(
                    emote.Texture,
                    new Rectangle(x, y, DrawSize, DrawSize),
                    Color.White
                );
            }
        }

        protected override void OnDestroy()
        {
            foreach (Texture2D texture in _textures.Values)
            {
                if (texture != null)
                {
                    texture.Dispose();
                }
            }

            _textures.Clear();
            _active.Clear();

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void LoadTextures()
        {
            IReadOnlyList<EmoteDefinition> definitions = ModEntry.Definitions;

            for (int i = 0; i < definitions.Count; i++)
            {
                EmoteDefinition definition = definitions[i];
                string path = Path.Combine(ModEntry.ModDirectory, definition.FileName);

                try
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        _textures[definition.FileName] = Texture2D.FromStream(
                            Game1.instance.GraphicsDevice,
                            stream
                        );
                    }
                }
                catch
                {
                }
            }
        }

        private void Show(PlayerEntity owner, string fileName)
        {
            Texture2D texture;

            if (owner == null || !owner.IsAlive ||
                !_textures.TryGetValue(fileName, out texture))
            {
                return;
            }

            ActiveEmote emote;
            if (!_active.TryGetValue(owner, out emote))
            {
                emote = new ActiveEmote();
                _active[owner] = emote;
            }

            emote.Texture = texture;
            emote.RemainingSeconds = DisplayDurationSeconds;
        }

        private void ProcessBrokerCommand()
        {
            IReadOnlyDictionary<string, string> parameters;
            string command;

            if (!BrokerCommandClient.TryDequeue(
                CommandTarget,
                out parameters
            ) ||
                !parameters.TryGetValue("command", out command) ||
                string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            // The emote belongs to the user who asked for it. In single player
            // every user resolves to the only player, so this is a no-op there.
            string user;
            parameters.TryGetValue("user", out user);
            PlayerEntity owner = TargetPlayerResolver.ResolvePlayer(user);

            if (owner == null)
            {
                return;
            }

            if (string.Equals(command, "random", StringComparison.OrdinalIgnoreCase))
            {
                ShowRandom(owner);
                return;
            }

            if (string.Equals(command, "happy", StringComparison.OrdinalIgnoreCase))
            {
                Show(owner, "emote_happy.png");
                return;
            }

            if (string.Equals(command, "sad", StringComparison.OrdinalIgnoreCase))
            {
                Show(owner, "emote_sad.png");
                return;
            }

            if (string.Equals(command, "thinking", StringComparison.OrdinalIgnoreCase))
            {
                Show(owner, "emote_thinking.png");
                return;
            }

            if (string.Equals(command, "angry", StringComparison.OrdinalIgnoreCase))
            {
                Show(owner, "emote_angry.png");
            }
        }

        private void ShowRandom(PlayerEntity owner)
        {
            IReadOnlyList<EmoteDefinition> definitions = ModEntry.Definitions;

            if (definitions.Count == 0)
            {
                return;
            }

            int index = Random.Next(definitions.Count);
            Show(owner, definitions[index].FileName);
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) &&
                !_previousKeyboardState.IsKeyDown(key);
        }
    }

    internal static class BrokerCommandClient
    {
        private const string RegistryTypeName = "JumpKingHttpCommandBroker.CommandQueueRegistry";

        private static object _registry;
        private static MethodInfo _registerMethod;
        private static MethodInfo _tryDequeueMethod;
        private static int _lastResolveAssemblyCount = -1;
        private static bool _loggedMissingBroker;
        private static bool _registered;

        public static void Register(string target)
        {
            if (_registered)
            {
                return;
            }

            if (!Resolve())
            {
                return;
            }

            try
            {
                _registerMethod.Invoke(_registry, new object[] { target });
                _registered = true;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "EmoteMod broker register failed: " + ex.Message
                );
            }
        }

        public static bool TryDequeue(
            string target,
            out IReadOnlyDictionary<string, string> parameters
        )
        {
            parameters = null;

            if (!_registered)
            {
                Register(target);
            }

            if (!_registered || !Resolve())
            {
                return false;
            }

            try
            {
                object[] args = new object[] { target, null };
                bool dequeued = (bool)_tryDequeueMethod.Invoke(_registry, args);
                parameters = args[1] as IReadOnlyDictionary<string, string>;
                return dequeued;
            }
            catch (Exception ex)
            {
                JumpKing.Program.crashLog.AddErrorMessage(
                    "EmoteMod broker dequeue failed: " + ex.Message
                );
                return false;
            }
        }

        private static bool Resolve()
        {
            if (_registry != null)
            {
                return true;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (_lastResolveAssemblyCount == assemblies.Length)
            {
                return false;
            }

            _lastResolveAssemblyCount = assemblies.Length;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type registryType = assemblies[i].GetType(RegistryTypeName, false);
                if (registryType == null)
                {
                    continue;
                }

                FieldInfo instanceField = registryType.GetField(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static
                );
                MethodInfo registerMethod = registryType.GetMethod(
                    "Register",
                    new Type[] { typeof(string) }
                );
                MethodInfo tryDequeueMethod = registryType.GetMethod(
                    "TryDequeue",
                    new Type[]
                    {
                        typeof(string),
                        typeof(IReadOnlyDictionary<string, string>).MakeByRefType()
                    }
                );

                if (instanceField == null || registerMethod == null || tryDequeueMethod == null)
                {
                    continue;
                }

                _registry = instanceField.GetValue(null);
                _registerMethod = registerMethod;
                _tryDequeueMethod = tryDequeueMethod;
                return _registry != null;
            }

            if (!_loggedMissingBroker)
            {
                _loggedMissingBroker = true;
                JumpKing.Program.crashLog.AddErrorMessage(
                    "EmoteMod: JumpKingHttpCommandBroker is not loaded."
                );
            }

            return false;
        }
    }
}
