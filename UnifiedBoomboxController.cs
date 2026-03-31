using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

namespace BoomBoxOverhaul
{
    public class UnifiedBoomboxController : MonoBehaviour
    {
        public BoomboxItem Boombox;
        public AudioSource Audio;

        private float localVolume = 1f;
        private bool uiOpen = false;
        private string pendingUrl = "";
        private string statusText = "Idle";

        private PlaylistState playlist = new PlaylistState();
        private string currentVideoId = "";
        private bool isPreparing = false;
        private bool isPlayingCustom = false;

        private readonly HashSet<ulong> readyClients = new HashSet<ulong>();
        private readonly HashSet<ulong> expectedClients = new HashSet<ulong>();
        private readonly HashSet<ulong> failedClients = new HashSet<ulong>();

        private Coroutine localLoadRoutine;
        private Coroutine serverWaitRoutine;
        private bool suppressVanillaStopOnce = false;
        private bool cameraLocked = false;

        private float tooltipScrollTimer = 0f;
        private int tooltipScrollIndex = 0;

        private UnityEngine.Audio.AudioMixer mixer;
        private UnityEngine.Audio.AudioMixerGroup mixerGroup;

        private void Awake()
        {
            Plugin.Log("UnifiedBoomboxController attached to boombox.");

            Boombox = GetComponent<BoomboxItem>();
            ApplyWeightSettings();

            AudioSource[] sources = GetComponents<AudioSource>();
            Audio = null;

            int i;
            for (i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != Boombox.boomboxAudio && sources[i].name == "BoomBoxOverhaulAudio")
                {
                    Audio = sources[i];
                    break;
                }
            }

            if (Audio == null)
            {
                Audio = gameObject.AddComponent<AudioSource>();
                Audio.name = "BoomBoxOverhaulAudio";
            }

            Audio.playOnAwake = false;
            Audio.loop = false;
            ApplyAudioModeSettings();

            localVolume = Mathf.Clamp(Plugin.DefaultVolume.Value, 0f, 2f);
            ApplyLocalVolume();
            RefreshHeldItemTooltip();
        }

        private void Update()
        {
            HandleInput();

            bool didScroll = false;

            tooltipScrollTimer += Time.deltaTime;
            if (tooltipScrollTimer >= 0.25f)
            {
                tooltipScrollTimer = 0f;
                tooltipScrollIndex++;
                didScroll = true;
            }

            if (uiOpen && !isPlayingCustom)
            {
                StopVanillaBoomboxAudio();
            }

            UpdateTooltip();

            if (didScroll)
            {
                RefreshHeldItemTooltip();
            }

            if (uiOpen)
            {
                UpdateCameraLock();
            }
            else if (cameraLocked)
            {
                SetCameraLocked(false);
            }

            if (isPlayingCustom && Audio != null && !Audio.isPlaying && !isPreparing)
            {
                OnTrackEndedLocal();
            }
        }

        private void HandleInput()
        {
            if (IsConfiguredKeyPressed(Plugin.OpenUiKey.Value) && IsHeldByLocalPlayer())
            {
                uiOpen = !uiOpen;

                if (uiOpen)
                {
                    StopAllBoomboxAudioForUi();
                }

                SetCameraLocked(uiOpen);
                Plugin.Log("Toggled boombox UI: " + (uiOpen ? "Open" : "Closed"));
            }

            if (IsConfiguredKeyPressed(Plugin.VolumeUpKey.Value) && IsRelevantToLocalPlayer())
            {
                float nextVolume = Mathf.Clamp(localVolume + Plugin.VolumeStep.Value, 0f, 2f);
                localVolume = nextVolume;

                if (Plugin.UseLocalVolumeOnly())
                {
                    ApplyLocalVolume();
                    Plugin.Log("Applied local-only volume up: " + localVolume);
                }
                else
                {
                    if (Boombox != null && Boombox.NetworkObject != null)
                    {
                        Plugin.Log("Sending server volume: " + localVolume);
                        BoomBoxOverhaulNet.SendSetVolume(Boombox.NetworkObject.NetworkObjectId, localVolume);
                    }
                    else
                    {
                        ApplyLocalVolume();
                        Plugin.Warn("Object missing, fell back to local volume, sorry!");
                    }
                }
            }

            if (IsConfiguredKeyPressed(Plugin.VolumeDownKey.Value) && IsRelevantToLocalPlayer())
            {
                float nextVolume = Mathf.Clamp(localVolume - Plugin.VolumeStep.Value, 0f, 2f);
                localVolume = nextVolume;

                if (Plugin.UseLocalVolumeOnly())
                {
                    ApplyLocalVolume();
                    Plugin.Log("Applied local-only volume down: " + localVolume);
                }
                else
                {
                    if (Boombox != null && Boombox.NetworkObject != null)
                    {
                        Plugin.Log("Sending server volume: " + localVolume);
                        BoomBoxOverhaulNet.SendSetVolume(Boombox.NetworkObject.NetworkObjectId, localVolume);
                    }
                    else
                    {
                        ApplyLocalVolume();
                        Plugin.Warn("Object missing, fell back to local volume, sorry!");
                    }
                }
            }
        }

        private bool IsConfiguredKeyPressed(KeyCode keyCode)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            switch (keyCode)
            {
                case KeyCode.A: return keyboard.aKey.wasPressedThisFrame;
                case KeyCode.B: return keyboard.bKey.wasPressedThisFrame;
                case KeyCode.C: return keyboard.cKey.wasPressedThisFrame;
                case KeyCode.D: return keyboard.dKey.wasPressedThisFrame;
                case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
                case KeyCode.G: return keyboard.gKey.wasPressedThisFrame;
                case KeyCode.H: return keyboard.hKey.wasPressedThisFrame;
                case KeyCode.I: return keyboard.iKey.wasPressedThisFrame;
                case KeyCode.J: return keyboard.jKey.wasPressedThisFrame;
                case KeyCode.K: return keyboard.kKey.wasPressedThisFrame;
                case KeyCode.L: return keyboard.lKey.wasPressedThisFrame;
                case KeyCode.M: return keyboard.mKey.wasPressedThisFrame;
                case KeyCode.N: return keyboard.nKey.wasPressedThisFrame;
                case KeyCode.O: return keyboard.oKey.wasPressedThisFrame;
                case KeyCode.P: return keyboard.pKey.wasPressedThisFrame;
                case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
                case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
                case KeyCode.S: return keyboard.sKey.wasPressedThisFrame;
                case KeyCode.T: return keyboard.tKey.wasPressedThisFrame;
                case KeyCode.U: return keyboard.uKey.wasPressedThisFrame;
                case KeyCode.V: return keyboard.vKey.wasPressedThisFrame;
                case KeyCode.W: return keyboard.wKey.wasPressedThisFrame;
                case KeyCode.X: return keyboard.xKey.wasPressedThisFrame;
                case KeyCode.Y: return keyboard.yKey.wasPressedThisFrame;
                case KeyCode.Z: return keyboard.zKey.wasPressedThisFrame;

                case KeyCode.Alpha0: return keyboard.digit0Key.wasPressedThisFrame;
                case KeyCode.Alpha1: return keyboard.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return keyboard.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return keyboard.digit3Key.wasPressedThisFrame;
                case KeyCode.Alpha4: return keyboard.digit4Key.wasPressedThisFrame;
                case KeyCode.Alpha5: return keyboard.digit5Key.wasPressedThisFrame;
                case KeyCode.Alpha6: return keyboard.digit6Key.wasPressedThisFrame;
                case KeyCode.Alpha7: return keyboard.digit7Key.wasPressedThisFrame;
                case KeyCode.Alpha8: return keyboard.digit8Key.wasPressedThisFrame;
                case KeyCode.Alpha9: return keyboard.digit9Key.wasPressedThisFrame;

                case KeyCode.Minus: return keyboard.minusKey.wasPressedThisFrame;
                case KeyCode.Equals: return keyboard.equalsKey.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Return: return keyboard.enterKey.wasPressedThisFrame;
                case KeyCode.KeypadEnter: return keyboard.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.Backspace: return keyboard.backspaceKey.wasPressedThisFrame;
                case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.LeftBracket: return keyboard.leftBracketKey.wasPressedThisFrame;
                case KeyCode.RightBracket: return keyboard.rightBracketKey.wasPressedThisFrame;
                case KeyCode.Semicolon: return keyboard.semicolonKey.wasPressedThisFrame;
                case KeyCode.Quote: return keyboard.quoteKey.wasPressedThisFrame;
                case KeyCode.Comma: return keyboard.commaKey.wasPressedThisFrame;
                case KeyCode.Period: return keyboard.periodKey.wasPressedThisFrame;
                case KeyCode.Slash: return keyboard.slashKey.wasPressedThisFrame;
                case KeyCode.Backslash: return keyboard.backslashKey.wasPressedThisFrame;
                default: return false;
            }
        }

        private void OnGUI()
        {
            if (!uiOpen || !IsHeldByLocalPlayer())
            {
                if (cameraLocked)
                {
                    SetCameraLocked(false);
                }
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GUI.Box(new Rect(20, 20, 520, 180), "BoomBoxOverhaulV2 By Henreh :D");
            GUI.Label(new Rect(35, 50, 460, 20), "Paste YouTube video or playlist URL:");
            pendingUrl = GUI.TextField(new Rect(35, 72, 470, 22), pendingUrl, 1000);
            GUI.Label(new Rect(35, 97, 470, 20), "State: " + statusText);
            GUI.Label(new Rect(35, 115, 470, 20), "Dependencies: " + DependencyBootstrapper.GetStatus());
            GUI.Label(new Rect(35, 133, 470, 20), "Volume: " + Mathf.RoundToInt(localVolume * 100f) + "%");

            bool wasEnabled = GUI.enabled;
            GUI.enabled = DependencyBootstrapper.GetState().IsReady();

            if (GUI.Button(new Rect(35, 155, 80, 20), "Play"))
            {
                Plugin.Log("Play button pressed.");

                if (string.IsNullOrEmpty(pendingUrl))
                {
                    Plugin.Warn("Play failed: URL empty");
                }
                else if (Boombox == null)
                {
                    Plugin.Warn("Play failed: Boombox null");
                }
                else if (Boombox.NetworkObject == null)
                {
                    Plugin.Warn("Play failed: Boombox.NetworkObject null");
                }
                else
                {
                    Plugin.Log("Sending play request → NetworkObjectId=" + Boombox.NetworkObject.NetworkObjectId + " URL=" + pendingUrl.Trim());
                    BoomBoxOverhaulNet.SendRequestPlay(Boombox.NetworkObject.NetworkObjectId, pendingUrl.Trim());
                }
            }

            GUI.enabled = wasEnabled;

            if (GUI.Button(new Rect(125, 155, 80, 20), "Stop"))
            {
                Plugin.Log("Stop button pressed.");

                if (Boombox != null && Boombox.NetworkObject != null)
                {
                    Plugin.Log("Sending stop request → NetworkObjectId=" + Boombox.NetworkObject.NetworkObjectId);
                    BoomBoxOverhaulNet.SendRequestStop(Boombox.NetworkObject.NetworkObjectId);
                }
                else
                {
                    Plugin.Warn("Stop failed: Boombox or NetworkObject missing");
                }
            }
        }

        private void ApplyLocalVolume()
        {
            if (Audio == null)
            {
                return;
            }

            float clamped = Mathf.Clamp(localVolume, 0f, 2f);
            float appliedVolume;

            if (clamped <= 1f)
            {
                appliedVolume = clamped;
            }
            else
            {
                //Something like that I think :/
                appliedVolume = 1f + ((clamped - 1f) * 1.2f);
            }

            Audio.volume = appliedVolume;

            UpdateTooltip();
            RefreshHeldItemTooltip();
        }

        public void ServerHandleSetVolume( float volume)
        {
            if (Plugin.LocalVolumeOnly.Value)
            {
                Plugin.Log("Server rejected server volume change, the host has it set to Local changes only!");
                return;
            }

            float clamped = Mathf.Clamp(volume, 0f, 2f);

            if (Boombox != null && Boombox.NetworkObject != null)
            {
                Plugin.Log("Sending shared volume: " + clamped);
            }
        }

        public void ClientApplyNetworkVolume(float volume)
        {
            Plugin.Log("ClientApplyNetworkVolume > " + volume);
            localVolume = Mathf.Clamp(volume, 0f, 2f);
            ApplyLocalVolume();
        }

        ///////////////////////////
        //Audio mode (experiment)//
        ///////////////////////////
        private void ApplyAudioModeSettings()
        {
            try
            {
                if (Audio == null)
                {
                    return;
                }




                switch (Plugin.UseAudioMode())
                {
                    case AudioModeType.Realistic:
                        Audio.spatialBlend = 1f;
                        Audio.rolloffMode = AudioRolloffMode.Logarithmic;
                        Audio.minDistance = 1.5f;
                        Audio.maxDistance = 35f;
                        break;

                    case AudioModeType.PureMusic:
                        Audio.spatialBlend = 0.2f;
                        Audio.rolloffMode = AudioRolloffMode.Logarithmic;
                        Audio.minDistance = 3f;
                        Audio.maxDistance = 60f;
                        break;

                    default:  //Default is balanced "Because it just works" - Todd howard
                        Audio.spatialBlend = 0.6f;
                        Audio.rolloffMode = AudioRolloffMode.Logarithmic;
                        Audio.minDistance = 2f;
                        Audio.maxDistance = 50f;
                        break;
                }

                Plugin.Log("Applied Audio Preset: " + Plugin.UseAudioMode());
                UpdateTooltip();
                RefreshHeldItemTooltip();
            }
            catch (Exception ex)
            {
                Plugin.Warn("Failed to apply Audio Preset: " + ex);
            }
        }
        private void RefreshHeldItemTooltip()
        {
            try
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
                if (localPlayer == null || localPlayer.currentlyHeldObjectServer == null)
                {
                    return;
                }

                if (localPlayer.currentlyHeldObjectServer == Boombox)
                {
                    localPlayer.currentlyHeldObjectServer.EquipItem();
                }
            }
            catch (Exception ex)
            {
                Plugin.Warn("Failed to refresh held item tooltip: " + ex);
            }
        }

        private void StopVanillaBoomboxAudio()
        {
            try
            {
                if (Boombox == null)
                {
                    return;
                }

                if (Boombox.boomboxAudio != null && Boombox.boomboxAudio != Audio)
                {
                    Boombox.boomboxAudio.Stop();
                }

                Boombox.isPlayingMusic = false;
            }
            catch (Exception ex)
            {
                Plugin.Warn("Failed to stop vanilla boombox audio: " + ex);
            }
        }

        private void StopAllBoomboxAudioForUi()
        {
            StopVanillaBoomboxAudio();

            if (Audio != null && Audio != Boombox.boomboxAudio && !isPlayingCustom)
            {
                Audio.Stop();
            }
        }
        private void SetCameraLocked(bool locked)
        {
            cameraLocked = locked;

            try
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
                if (localPlayer != null && localPlayer.playerActions != null)
                {
                    if (locked)
                    {
                        localPlayer.playerActions.Disable();
                    }
                    else
                    {
                        localPlayer.playerActions.Enable();
                    }
                }

                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = locked;
            }
            catch (Exception ex)
            {
                Plugin.Warn("SetCameraLocked failed: " + ex);
            }
        }

        private void UpdateCameraLock()
        {
            if (!uiOpen)
            {
                if (cameraLocked)
                {
                    SetCameraLocked(false);
                }
                return;
            }

            if (!IsHeldByLocalPlayer())
            {
                uiOpen = false;
                SetCameraLocked(false);
                return;
            }

            try
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
                if (localPlayer != null && localPlayer.playerActions != null)
                {
                    localPlayer.playerActions.Disable();
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch (Exception ex)
            {
                Plugin.Warn("Camera lock failed: " + ex);
            }
        }

        private void ApplyWeightSettings()
        {
            try
            {
                if (Boombox == null || Boombox.itemProperties == null)
                {
                    return;
                }

                if (Plugin.WeightlessBoombox.Value)
                {
                    Boombox.itemProperties.weight = 1f;
                    Plugin.Log("The boombox is as light as a feather!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Warn("Unfortunately the boombox is made of tungsten: " + ex);
            }
        }
        public void RefreshWeightSettingsFromSync()
        {
            ApplyWeightSettings();
        }



        private string GetScrollingTrackText()
        {
            string title = "None";

            if (Audio != null && Audio.clip != null && !string.IsNullOrEmpty(Audio.clip.name))
            {
                title = Audio.clip.name;
            }
            else if (!string.IsNullOrEmpty(currentVideoId))
            {
                title = currentVideoId;
            }

            if (string.IsNullOrEmpty(title))
            {
                return "None";
            }

            const int visibleLength = 28;

            if (title.Length <= visibleLength)
            {
                return title;
            }

            string padded = title + "   ";
            string scrolling = padded + padded;

            if (tooltipScrollIndex >= padded.Length)
            {
                tooltipScrollIndex = 0;
            }

            return scrolling.Substring(tooltipScrollIndex, visibleLength);
        }

        private void UpdateTooltip()
        {
            if (Boombox == null || Boombox.itemProperties == null)
            {
                return;
            }

            string scrollingTitle = GetScrollingTrackText();

            Boombox.itemProperties.toolTips = new string[]
            {
                "[" + Plugin.OpenUiKey.Value + "] URL Menu",
                "[" + Plugin.VolumeDownKey.Value + "/" + Plugin.VolumeUpKey.Value + "] Volume: " + Mathf.RoundToInt(localVolume * 100f) + "%",
                "Track: " + scrollingTitle,
                "State: " + statusText,
                "Audio: " + Plugin.UseAudioMode()
            };
        }

        private bool IsHeldByLocalPlayer()
        {
            try
            {
                return Boombox != null
                    && Boombox.playerHeldBy != null
                    && GameNetworkManager.Instance != null
                    && GameNetworkManager.Instance.localPlayerController != null
                    && Boombox.playerHeldBy == GameNetworkManager.Instance.localPlayerController;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRelevantToLocalPlayer()
        {
            if (IsHeldByLocalPlayer())
            {
                return true;
            }

            try
            {
                PlayerControllerB local = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
                if (local == null)
                {
                    return false;
                }

                return Vector3.Distance(local.transform.position, transform.position) <= 15f;
            }
            catch
            {
                return false;
            }
        }

        private void PopulateExpectedClients()
        {
            expectedClients.Clear();

            if (NetworkManager.Singleton == null || NetworkManager.Singleton.ConnectedClients == null)
            {
                return;
            }

            foreach (KeyValuePair<ulong, NetworkClient> kvp in NetworkManager.Singleton.ConnectedClients)
            {
                expectedClients.Add(kvp.Key);
            }

            Plugin.Log("Expected clients for boombox playback: " + expectedClients.Count);
        }

        public void ServerHandlePlay(ulong requesterClientId, string url)
        {
            Plugin.Log("ServerHandlePlay called from client " + requesterClientId + " URL=" + url);

            if (!DependencyBootstrapper.GetState().IsReady())
            {
                Plugin.Warn("ServerHandlePlay rejected: dependencies not ready");
                if (Boombox != null && Boombox.NetworkObject != null)
                {
                    BoomBoxOverhaulNet.SendRejectPlay(requesterClientId, Boombox.NetworkObject.NetworkObjectId, "Dependencies not ready");
                }
                return;
            }

            if (!UrlHelpers.IsLikelyYoutubeUrl(url))
            {
                Plugin.Warn("ServerHandlePlay rejected: invalid URL");
                if (Boombox != null && Boombox.NetworkObject != null)
                {
                    BoomBoxOverhaulNet.SendRejectPlay(requesterClientId, Boombox.NetworkObject.NetworkObjectId, "Invalid URL");
                }
                return;
            }

            currentVideoId = "";
            isPreparing = true;
            isPlayingCustom = false;
            statusText = "Resolving...";
            readyClients.Clear();
            expectedClients.Clear();
            failedClients.Clear();
            playlist = new PlaylistState();
            tooltipScrollIndex = 0;
            tooltipScrollTimer = 0f;

            StopVanillaBoomboxAudio();

            if (UrlHelpers.IsPlaylistOnlyUrl(url))
            {
                List<string> ids;
                if (!YtDlpBridge.ResolvePlaylistIds(url, out ids) || ids.Count == 0)
                {
                    Plugin.Warn("Playlist resolve failed");
                    if (Boombox != null && Boombox.NetworkObject != null)
                    {
                        BoomBoxOverhaulNet.SendRejectPlay(requesterClientId, Boombox.NetworkObject.NetworkObjectId, "Playlist resolve failed");
                    }
                    isPreparing = false;
                    return;
                }

                playlist.VideoIds.AddRange(ids);
                playlist.Index = 0;
                currentVideoId = playlist.GetCurrentId() ?? "";
            }
            else
            {
                string videoId = UrlHelpers.TryExtractVideoId(url);
                if (string.IsNullOrEmpty(videoId))
                {
                    Plugin.Warn("Could not parse video id");
                    if (Boombox != null && Boombox.NetworkObject != null)
                    {
                        BoomBoxOverhaulNet.SendRejectPlay(requesterClientId, Boombox.NetworkObject.NetworkObjectId, "Could not parse video id");
                    }
                    isPreparing = false;
                    return;
                }

                playlist.VideoIds.Add(videoId);
                playlist.Index = 0;
                currentVideoId = videoId;
            }

            if (Boombox == null || Boombox.NetworkObject == null)
            {
                Plugin.Warn("ServerHandlePlay aborted: Boombox or NetworkObject missing");
                isPreparing = false;
                return;
            }

            PopulateExpectedClients();

            string currentSourceUrl = "https://www.youtube.com/watch?v=" + currentVideoId;
            Plugin.Log("Broadcasting prepare track for " + currentVideoId);
            BoomBoxOverhaulNet.BroadcastPrepareTrack(Boombox.NetworkObject.NetworkObjectId, currentSourceUrl, currentVideoId, playlist.Index, playlist.VideoIds.ToArray());

            if (serverWaitRoutine != null)
            {
                StopCoroutine(serverWaitRoutine);
            }

            serverWaitRoutine = StartCoroutine(ServerWaitForReadyThenPlay());
        }

        public void ClientRejectPlay(string reason)
        {
            Plugin.Warn("ClientRejectPlay: " + reason);
            statusText = reason;
            isPreparing = false;
        }

        public void ClientPrepareTrack(string canonicalUrl, string videoId, int playlistIndex, string[] playlistIds)
        {
            Plugin.Log("ClientPrepareTrack START → " + videoId);

            currentVideoId = videoId;
            playlist.VideoIds.Clear();
            playlist.VideoIds.AddRange(playlistIds);
            playlist.Index = playlistIndex;
            statusText = "Preparing...";
            isPreparing = true;
            isPlayingCustom = false;
            tooltipScrollIndex = 0;
            tooltipScrollTimer = 0f;

            StopVanillaBoomboxAudio();

            if (localLoadRoutine != null)
            {
                StopCoroutine(localLoadRoutine);
            }

            localLoadRoutine = StartCoroutine(PrepareTrackLocalCoroutine(canonicalUrl, videoId));
        }

        private IEnumerator PrepareTrackLocalCoroutine(string canonicalUrl, string videoId)
        {
            bool fetchOk = false;
            TrackInfo info = new TrackInfo();

            Plugin.Log("Preparing local track download for: " + videoId);
            Plugin.Log("Downloading track: " + videoId);

            yield return RunBlockingTask(delegate
            {
                fetchOk = YtDlpBridge.FetchTrack(canonicalUrl, videoId, out info);
            });

            if (!fetchOk || !File.Exists(info.cachePath))
            {
                statusText = "Download failed please try a new link, sorry!";
                Plugin.Warn("Download failed for: " + videoId);

                if (Boombox != null && Boombox.NetworkObject != null)
                {
                    BoomBoxOverhaulNet.SendNotifyReady(Boombox.NetworkObject.NetworkObjectId, false);
                }
                yield break;
            }

            Plugin.Log("Download COMPLETE: " + videoId);
            Plugin.Log("Local track download complete for: " + videoId);

            statusText = "Loading clip...";
            string fileUrl = "file:///" + info.cachePath.Replace("\\", "/");

            using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
            {
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    statusText = "Clip load failed";
                    Plugin.Warn("Clip load failed for: " + videoId);

                    if (Boombox != null && Boombox.NetworkObject != null)
                    {
                        BoomBoxOverhaulNet.SendNotifyReady(Boombox.NetworkObject.NetworkObjectId, false);
                    }
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                clip.name = string.IsNullOrEmpty(info.title) ? videoId : info.title;

                if (Audio.clip != null && Audio.clip != clip)
                {
                    try
                    {
                        Destroy(Audio.clip);
                    }
                    catch
                    {
                    }
                }

                Audio.clip = clip;
                tooltipScrollIndex = 0;
                tooltipScrollTimer = 0f;
                ApplyLocalVolume();
                statusText = "Ready: " + clip.name;
                Plugin.Log("Clip READY: " + videoId);
                Plugin.Log("Local clip ready for: " + videoId);

                if (Boombox != null && Boombox.NetworkObject != null)
                {
                    BoomBoxOverhaulNet.SendNotifyReady(Boombox.NetworkObject.NetworkObjectId, true);
                }
            }
        }

        public void ServerNotifyReady(ulong clientId, bool success)
        {
            if (success)
            {
                readyClients.Add(clientId);
                failedClients.Remove(clientId);
                Plugin.Log("Client ready for boombox playback: " + clientId);
            }
            else
            {
                failedClients.Add(clientId);
                readyClients.Remove(clientId);
                Plugin.Warn("Client failed to prepare boombox playback: " + clientId);
            }
        }

        private IEnumerator ServerWaitForReadyThenPlay()
        {
            float timeout = Mathf.Max(1f, Plugin.ReadyTimeoutSeconds.Value);
            float start = Time.time;

            Plugin.Log("Waiting for clients to prepare track. Expected: " + expectedClients.Count);

            while (Time.time - start < timeout)
            {
                if (expectedClients.Count > 0 && readyClients.Count >= expectedClients.Count)
                {
                    Plugin.Log("All expected clients are ready. Starting playback immediately.");
                    break;
                }

                yield return null;
            }

            if (readyClients.Count == 0)
            {
                ClientRejectPlay("No client prepared track");
                isPreparing = false;
                Plugin.Warn("No clients were ready before timeout.");
                yield break;
            }

            if (readyClients.Count < expectedClients.Count)
            {
                Plugin.Warn("Timeout reached. Starting playback for ready clients only. Ready: " + readyClients.Count + " / Expected: " + expectedClients.Count);
            }
            else
            {
                Plugin.Log("Starting playback for all expected clients.");
            }

            if (Boombox != null && Boombox.NetworkObject != null)
            {
                BoomBoxOverhaulNet.BroadcastBeginPlaybackReadyOnly(Boombox.NetworkObject.NetworkObjectId, currentVideoId, readyClients);
            }

            isPreparing = false;
            isPlayingCustom = true;
            statusText = "Playing (" + readyClients.Count + "/" + expectedClients.Count + " ready)";
        }

        public void ClientBeginPlayback(string videoId)
        {
            Plugin.Log("ClientBeginPlayback → " + videoId);

            ApplyAudioModeSettings();
            currentVideoId = videoId;
            isPreparing = false;
            isPlayingCustom = true;
            statusText = "Playing";
            tooltipScrollIndex = 0;
            tooltipScrollTimer = 0f;

            if (Audio != null && Audio.clip != null)
            {
                suppressVanillaStopOnce = true;
                StopVanillaBoomboxAudio();
                Audio.Stop();
                Audio.time = 0f;
                Audio.Play();
            }
            else
            {
                Plugin.Warn("ClientBeginPlayback received but Audio or Audio.clip was null");
            }
        }

        public void ServerHandleStop()
        {
            Plugin.Log("ServerHandleStop called");

            if (Boombox != null && Boombox.NetworkObject != null)
            {
                BoomBoxOverhaulNet.BroadcastStopPlayback(Boombox.NetworkObject.NetworkObjectId);
            }
        }

        public void ClientStopPlayback()
        {
            Plugin.Log("ClientStopPlayback called");

            isPreparing = false;
            isPlayingCustom = false;
            statusText = "Stopped";

            if (localLoadRoutine != null)
            {
                StopCoroutine(localLoadRoutine);
                localLoadRoutine = null;
            }

            if (Audio != null)
            {
                Audio.Stop();
            }
        }

        private void OnTrackEndedLocal()
        {
            if (!Plugin.AutoplayPlaylist.Value || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                isPlayingCustom = false;
                statusText = "Finished";
                return;
            }

            if (playlist.VideoIds.Count <= 1)
            {
                isPlayingCustom = false;
                statusText = "Finished";
                return;
            }

            string next = playlist.Advance();
            if (string.IsNullOrEmpty(next))
            {
                isPlayingCustom = false;
                statusText = "Playlist ended";
                return;
            }

            currentVideoId = next;
            statusText = "Next track...";
            readyClients.Clear();
            expectedClients.Clear();
            failedClients.Clear();
            tooltipScrollIndex = 0;
            tooltipScrollTimer = 0f;

            if (Boombox == null || Boombox.NetworkObject == null)
            {
                return;
            }

            StopVanillaBoomboxAudio();
            PopulateExpectedClients();

            string currentSourceUrl = "https://www.youtube.com/watch?v=" + currentVideoId;
            Plugin.Log("Broadcasting next prepare track for " + currentVideoId);
            BoomBoxOverhaulNet.BroadcastPrepareTrack(Boombox.NetworkObject.NetworkObjectId, currentSourceUrl, currentVideoId, playlist.Index, playlist.VideoIds.ToArray());

            if (serverWaitRoutine != null)
            {
                StopCoroutine(serverWaitRoutine);
            }

            serverWaitRoutine = StartCoroutine(ServerWaitForReadyThenPlay());
        }

        private IEnumerator RunBlockingTask(Action action)
        {
            bool done = false;
            Exception caught = null;

            Thread thread = new Thread(delegate ()
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    done = true;
                }
            });

            thread.IsBackground = true;
            thread.Start();

            while (!done)
            {
                yield return null;
            }

            if (caught != null)
            {
                Plugin.Error("Background task failed: " + caught);
            }
        }

        public bool ShouldSuppressVanillaStop()
        {
            if (!Plugin.KeepPlayingPocketed.Value)
            {
                return false;
            }

            if (suppressVanillaStopOnce)
            {
                suppressVanillaStopOnce = false;
                return true;
            }

            return isPlayingCustom;
        }
    }
}