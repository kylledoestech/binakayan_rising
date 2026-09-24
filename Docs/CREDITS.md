# Credits: music and sound effects

Every track and effect here is **CC0 1.0 Universal** (public domain), so none of them needs
attribution. They are listed anyway so that the defense panel can see where each one came from.
All were converted to Ogg Vorbis with ffmpeg, and each one is under 3 MB.

## Music (`Assets/_Project/Resources/Music/`)

| File | Plays on | Title | Author | Source | License |
| --- | --- | --- | --- | --- | --- |
| `music_menu.ogg` | Splash, main menu | Laments of the War | cethiel | https://opengameart.org/content/laments-of-the-war | CC0 |
| `music_camp.ogg` | Encampment and its panels | Kingdom of a Million Elephants under a White Parasol | spring-spring | https://opengameart.org/content/kingdom-of-a-million-elephants-under-a-white-parasol | CC0 |
| `music_battle.ogg` | Deployment, battle, quiz | Call to War | umplix | https://opengameart.org/content/call-to-war | CC0 |

## Sound effects (`Assets/_Project/Resources/Sfx/`)

| File | Used for | Original file | Pack | Author | Source | License |
| --- | --- | --- | --- | --- | --- | --- |
| `sfx_coin.ogg` | Reales gained or spent | `handleCoins2.ogg` | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 |
| `sfx_hit.ogg` | A hit landing in battle | `chop.ogg` | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 |
| `sting_victory.ogg` | Battle won | `jingles_STEEL02.ogg` | 85 Short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 |
| `sting_defeat.ogg` | Battle lost | `jingles_STEEL01.ogg` | 85 Short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 |

The earlier interface sounds (clicks, confirms) are in `Assets/_Project/Art/LICENSES.md`.

## Title splash

`Assets/_Project/Resources/Splash/splash.png` is original work. It is rendered by
`Tools/splash/splash.py` in Blender:

```
blender -b --factory-startup -P Tools/splash/splash.py -- --out Assets/_Project/Resources/Splash/splash.png
```
