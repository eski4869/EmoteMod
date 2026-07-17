# Emote Mod

Emote Mod adds four simple emotes that appear above the Jump King character.

## Controls

| Key | Emote |
| --- | --- |
| `Shift + Up` | Happy |
| `Shift + Left` | Sad |
| `Shift + Right` | Thinking |
| `Shift + Down` | Angry |

The selected emote is displayed above the character for about two seconds.

## Custom Images

The mod creates the following PNG files next to the mod:

- `emote_happy.png`
- `emote_sad.png`
- `emote_thinking.png`
- `emote_angry.png`

Existing PNG files are not overwritten. Replace any file with your own transparent PNG and restart the game to use it.

The images are displayed at `32 x 32` pixels in-game. Square transparent PNG files are recommended.

## HTTP Command Broker

If `JumpKingHttpCommandBroker` is loaded, Emote Mod registers the `emote` target.

Examples:

```text
http://127.0.0.1:8081/command?target=emote&command=random
http://127.0.0.1:8081/command?target=emote&command=happy
http://127.0.0.1:8081/command?target=emote&command=sad
http://127.0.0.1:8081/command?target=emote&command=thinking
http://127.0.0.1:8081/command?target=emote&command=angry
```
