# BoomBoxOverhaulV2

This is an overhaul of my original BoomboxOverhaul mod. The original mod was designed to work with a specific youtube boombox mod that is no longer maintained. Because of this I decided to implement a working, albeit sometimes temperamental YouTube playback.

BoomBoxOverhaulV2 adds:

- YouTube video playback
- YouTube playlist playback
- Automatic yt-dlp / ffmpeg dependency handling
- Local boombox volume controls
- Scrolling track title in the held-item HUD (This may change in a future version as I am not happy with it)
- In-game boombox URL input UI
- Infinite battery option
- Keep-playing-while-pocketed behavior

### Thunderstore / r2modman
Install through Thunderstore or your mod manager of choice.

## Usage

- Hold a boombox and press `B` to open the URL UI.
- Paste a YouTube video or playlist URL.
- Press `Play`.
- Use `-` and `=` to adjust local volume.

## Notes

- This mod downloads and uses `yt-dlp` and `ffmpeg` when required.
- The boombox HUD shows the current track title.
- All players should have the mod installed for it to work.

## Configuration

Config file (`henreh.boomboxoverhaul.cfg`) is generated through BepInEx and contains options for:
- Infinite battery
- Keep playing while pocketed
- Volume keys
- Cache size
- Playlist autoplay
- Dependency auto-download behavior

## Credits

Henreh. (Developer)
Metaphorreur (Testing)

#Manual Install
-For manual install Place the manual install folder insdie of `BepInEx/plugins` Folder!

## Please do not copy and paste this code and claim it as your own, that is why no License is attached.
