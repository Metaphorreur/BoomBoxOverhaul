# BoomBoxOverhaulV2

This is an overhaul of my original BoomboxOverhaul mod. The original mod was designed to work with a specific youtube boombox mod that is no longer maintained. Because of this I decided to implement a working, albeit sometimes temperamental YouTube playback.

BoomBoxOverhaulV2 adds:

- YouTube video playback
- YouTube playlist playback
- Automatic yt-dlp / ffmpeg dependency handling
- Local boombox volume controls
- Scrolling track title in the held-item HUD (This may change in a future version as I am not entirely happy with it)
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

- Henreh. (Developer)
- Bananas (Very good tester!)
- Metaphorreur (Testing)
- Wozzie (Testing)
- Coheesion (Very good guinea pig!)
- Langerz (Strange tester!)
- Jvggr (Testing)

## Planned content

- I am looking to add back the volume change ability when looking at a place boombox, I recently found my source for V1 so expect this soon
- I am looking to improve performance with this mod soon
- I am looking to make the mod compatiable with Spotify and potentially soundcloud links

## Issues

There may be issues because I am still trying to figure out YTDL and it has been a while since I created modifications for this game. If there are any ping me in the Lethal company modding discord (@henreh.) or use the github issues page :D
