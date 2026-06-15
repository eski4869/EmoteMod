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
            WriteDefaultImagesIfMissing();
        }

        [OnLevelStart]
        public static void OnLevelStart()
        {
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

        private static EmoteDisplay _instance;

        private readonly Dictionary<string, Texture2D> _textures =
            new Dictionary<string, Texture2D>();

        private KeyboardState _previousKeyboardState;
        private Texture2D _activeTexture;
        private float _remainingSeconds;

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
            bool shiftDown =
                keyboardState.IsKeyDown(Keys.LeftShift) ||
                keyboardState.IsKeyDown(Keys.RightShift);

            if (shiftDown)
            {
                if (WasKeyPressed(keyboardState, Keys.Up))
                {
                    Show("emote_happy.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Left))
                {
                    Show("emote_sad.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Right))
                {
                    Show("emote_thinking.png");
                }
                else if (WasKeyPressed(keyboardState, Keys.Down))
                {
                    Show("emote_angry.png");
                }
            }

            _previousKeyboardState = keyboardState;

            if (_remainingSeconds > 0f)
            {
                _remainingSeconds = Math.Max(0f, _remainingSeconds - delta);
            }
        }

        public void ForegroundDraw()
        {
            if (_activeTexture == null || _remainingSeconds <= 0f)
            {
                return;
            }

            PlayerEntity player = EntityManager.instance.Find<PlayerEntity>();

            if (player == null || Game1.instance == null)
            {
                return;
            }

            Rectangle hitbox = Camera.TransformRect(player.m_body.GetHitbox());
            int x = hitbox.Center.X - DrawSize / 2;
            int y = hitbox.Top - DrawSize - HeadOffset;

            x = Math.Max(2, Math.Min(Game1.WIDTH - DrawSize - 2, x));
            y = Math.Max(2, Math.Min(Game1.HEIGHT - DrawSize - 2, y));

            Game1.spriteBatch.Draw(
                _activeTexture,
                new Rectangle(x, y, DrawSize, DrawSize),
                Color.White
            );
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
            _activeTexture = null;

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

        private void Show(string fileName)
        {
            Texture2D texture;

            if (!_textures.TryGetValue(fileName, out texture))
            {
                return;
            }

            _activeTexture = texture;
            _remainingSeconds = DisplayDurationSeconds;
        }

        private bool WasKeyPressed(KeyboardState keyboardState, Keys key)
        {
            return keyboardState.IsKeyDown(key) &&
                !_previousKeyboardState.IsKeyDown(key);
        }
    }
}
