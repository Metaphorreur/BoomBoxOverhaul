using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BoomBoxOverhaul
{
    internal static class BoomBoxOverhaulNet
    {
        private const string MsgRequestPlay = "BoomBoxOverhaul_RequestPlay";
        private const string MsgPrepareTrack = "BoomBoxOverhaul_PrepareTrack";
        private const string MsgNotifyReady = "BoomBoxOverhaul_NotifyReady";
        private const string MsgBeginPlayback = "BoomBoxOverhaul_BeginPlayback";
        private const string MsgRequestStop = "BoomBoxOverhaul_RequestStop";
        private const string MsgStopPlayback = "BoomBoxOverhaul_StopPlayback";
        private const string MsgRejectPlay = "BoomBoxOverhaul_RejectPlay";
        private const string MsgSetVolume = "BoomBoxOverhaul_SetVolume";
        private const string MsgApplyVolume = "BoomBoxOverhaul_ApplyVolume";
        private const string MsgSyncSettings = "BoomBoxOverhaul_SyncSettings";

        private static MonoBehaviour host;
        private static bool initialized;
        private static bool handlersRegistered;
        private static NetworkManager boundManager;

        public static void Initialize(MonoBehaviour coroutineHost)
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            host = coroutineHost;
            host.StartCoroutine(BootLoop());
        }

        private static IEnumerator BootLoop()
        {
            while (true)
            {
                TryBindToNetworkManager();

                if (boundManager != null && handlersRegistered && boundManager.IsServer)
                {
                    BroadcastSyncSettings(Plugin.LocalVolumeOnly.Value);
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private static void TryBindToNetworkManager()
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null)
            {
                return;
            }

            if (boundManager != nm)
            {
                UnregisterHandlers();
                boundManager = nm;
                handlersRegistered = false;
                Plugin.Log("BoomBoxOverhaul bound to NetworkManager.");
            }

            if (!handlersRegistered)
            {
                RegisterHandlers();
            }
        }

        private static void RegisterHandlers()
        {
            if (boundManager == null || boundManager.CustomMessagingManager == null)
            {
                return;
            }

            CustomMessagingManager mm = boundManager.CustomMessagingManager;

            mm.RegisterNamedMessageHandler(MsgRequestPlay, OnRequestPlay);
            mm.RegisterNamedMessageHandler(MsgPrepareTrack, OnPrepareTrack);
            mm.RegisterNamedMessageHandler(MsgNotifyReady, OnNotifyReady);
            mm.RegisterNamedMessageHandler(MsgBeginPlayback, OnBeginPlayback);
            mm.RegisterNamedMessageHandler(MsgRequestStop, OnRequestStop);
            mm.RegisterNamedMessageHandler(MsgStopPlayback, OnStopPlayback);
            mm.RegisterNamedMessageHandler(MsgRejectPlay, OnRejectPlay);
            mm.RegisterNamedMessageHandler(MsgSetVolume, OnSetVolume);
            mm.RegisterNamedMessageHandler(MsgApplyVolume, OnApplyVolume);
            mm.RegisterNamedMessageHandler(MsgSyncSettings, OnSyncSettings);

            handlersRegistered = true;
            Plugin.Log("BoomBoxOverhaul network handlers registered.");
        }

        private static void UnregisterHandlers()
        {
            if (boundManager == null || boundManager.CustomMessagingManager == null || !handlersRegistered)
            {
                return;
            }

            CustomMessagingManager mm = boundManager.CustomMessagingManager;

            mm.UnregisterNamedMessageHandler(MsgRequestPlay);
            mm.UnregisterNamedMessageHandler(MsgPrepareTrack);
            mm.UnregisterNamedMessageHandler(MsgNotifyReady);
            mm.UnregisterNamedMessageHandler(MsgBeginPlayback);
            mm.UnregisterNamedMessageHandler(MsgRequestStop);
            mm.UnregisterNamedMessageHandler(MsgStopPlayback);
            mm.UnregisterNamedMessageHandler(MsgRejectPlay);
            mm.UnregisterNamedMessageHandler(MsgSetVolume);
            mm.UnregisterNamedMessageHandler(MsgApplyVolume);
            mm.UnregisterNamedMessageHandler(MsgSyncSettings);

            handlersRegistered = false;
            Plugin.Log("BoomBoxOverhaul network handlers unregistered.");
        }

        public static UnifiedBoomboxController GetController(ulong networkObjectId)
        {
            if (boundManager == null || boundManager.SpawnManager == null)
            {
                return null;
            }

            NetworkObject netObj;
            if (!boundManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out netObj))
            {
                return null;
            }

            return netObj.GetComponent<UnifiedBoomboxController>();
        }

        public static void SendRequestPlay(ulong networkObjectId, string url)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsClient)
            {
                Plugin.Warn("SendRequestPlay failed: network not ready.");
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp))
            {
                writer.WriteValueSafe(networkObjectId);
                writer.WriteValueSafe(url);
                boundManager.CustomMessagingManager.SendNamedMessage(MsgRequestPlay, NetworkManager.ServerClientId, writer);
            }

            Plugin.Log("Sent play request for object " + networkObjectId);
        }

        public static void BroadcastPrepareTrack(ulong networkObjectId, string canonicalUrl, string videoId, int playlistIndex, string[] playlistIds)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                Plugin.Warn("BroadcastPrepareTrack failed: server network not ready.");
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                using (FastBufferWriter writer = new FastBufferWriter(16384, Allocator.Temp))
                {
                    writer.WriteValueSafe(networkObjectId);
                    writer.WriteValueSafe(canonicalUrl);
                    writer.WriteValueSafe(videoId);
                    writer.WriteValueSafe(playlistIndex);
                    writer.WriteValueSafe(playlistIds.Length);

                    int j;
                    for (j = 0; j < playlistIds.Length; j++)
                    {
                        writer.WriteValueSafe(playlistIds[j]);
                    }

                    boundManager.CustomMessagingManager.SendNamedMessage(MsgPrepareTrack, clientIds[i], writer);
                }
            }

            Plugin.Log("Broadcast prepare track for object " + networkObjectId);
        }

        public static void SendNotifyReady(ulong networkObjectId, bool success)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsClient)
            {
                Plugin.Warn("SendNotifyReady failed: network not ready.");
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteValueSafe(networkObjectId);
                writer.WriteValueSafe(success);
                boundManager.CustomMessagingManager.SendNamedMessage(MsgNotifyReady, NetworkManager.ServerClientId, writer);
            }

            Plugin.Log("Sent ready state " + success + " for object " + networkObjectId);
        }

        public static void BroadcastBeginPlaybackReadyOnly(ulong networkObjectId, string videoId, HashSet<ulong> readyClientIds)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                Plugin.Warn("BroadcastBeginPlaybackReadyOnly failed: server network not ready.");
                return;
            }

            foreach (ulong clientId in readyClientIds)
            {
                using (FastBufferWriter writer = new FastBufferWriter(2048, Allocator.Temp))
                {
                    writer.WriteValueSafe(networkObjectId);
                    writer.WriteValueSafe(videoId);
                    boundManager.CustomMessagingManager.SendNamedMessage(MsgBeginPlayback, clientId, writer);
                }
            }

            Plugin.Log("Broadcast begin playback to ready clients only: " + readyClientIds.Count);
        }

        public static void SendRequestStop(ulong networkObjectId)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsClient)
            {
                Plugin.Warn("SendRequestStop failed: network not ready.");
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(networkObjectId);
                boundManager.CustomMessagingManager.SendNamedMessage(MsgRequestStop, NetworkManager.ServerClientId, writer);
            }

            Plugin.Log("Sent stop request for object " + networkObjectId);
        }

        public static void BroadcastStopPlayback(ulong networkObjectId)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                Plugin.Warn("BroadcastStopPlayback failed: server network not ready.");
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
                {
                    writer.WriteValueSafe(networkObjectId);
                    boundManager.CustomMessagingManager.SendNamedMessage(MsgStopPlayback, clientIds[i], writer);
                }
            }

            Plugin.Log("Broadcast stop playback for object " + networkObjectId);
        }

        public static void SendRejectPlay(ulong targetClientId, ulong networkObjectId, string reason)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(2048, Allocator.Temp))
            {
                writer.WriteValueSafe(networkObjectId);
                writer.WriteValueSafe(reason);
                boundManager.CustomMessagingManager.SendNamedMessage(MsgRejectPlay, targetClientId, writer);
            }
        }

        public static void SendSetVolume(ulong networkObjectId, float volume)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsClient)
            {
                Plugin.Warn("SendSetVolume failed: network not ready.");
                return;
            }

            using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteValueSafe(networkObjectId);
                writer.WriteValueSafe(volume);
                boundManager.CustomMessagingManager.SendNamedMessage(MsgSetVolume, NetworkManager.ServerClientId, writer);
            }
        }

        public static void BroadcastApplyVolume(ulong networkObjectId, float volume)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                Plugin.Warn("BroadcastApplyVolume failed: server network not ready.");
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
                {
                    writer.WriteValueSafe(networkObjectId);
                    writer.WriteValueSafe(volume);
                    boundManager.CustomMessagingManager.SendNamedMessage(MsgApplyVolume, clientIds[i], writer);
                }
            }
        }

        public static void BroadcastSyncSettings(bool localVolumeOnly)
        {
            if (boundManager == null || !handlersRegistered || !boundManager.IsServer)
            {
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
                {
                    writer.WriteValueSafe(localVolumeOnly);
                    boundManager.CustomMessagingManager.SendNamedMessage(MsgSyncSettings, clientIds[i], writer);
                }
            }

            Plugin.Log("Broadcast synced settings. LocalVolumeOnly = " + localVolumeOnly);
        }

        private static void OnRequestPlay(ulong senderClientId, FastBufferReader reader)
        {
            Plugin.Log("OnRequestPlay received from client " + senderClientId);

            ulong networkObjectId;
            string url;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out url);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerHandlePlay(senderClientId, url);
            }
            else
            {
                Plugin.Warn("OnRequestPlay could not find controller for object " + networkObjectId);
            }
        }

        private static void OnPrepareTrack(ulong senderClientId, FastBufferReader reader)
        {
            Plugin.Log("OnPrepareTrack received");

            ulong networkObjectId;
            string canonicalUrl;
            string videoId;
            int playlistIndex;
            int playlistCount;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out canonicalUrl);
            reader.ReadValueSafe(out videoId);
            reader.ReadValueSafe(out playlistIndex);
            reader.ReadValueSafe(out playlistCount);

            string[] playlistIds = new string[playlistCount];
            int i;
            for (i = 0; i < playlistCount; i++)
            {
                reader.ReadValueSafe(out playlistIds[i]);
            }

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientPrepareTrack(canonicalUrl, videoId, playlistIndex, playlistIds);
            }
            else
            {
                Plugin.Warn("OnPrepareTrack could not find controller for object " + networkObjectId);
            }
        }

        private static void OnNotifyReady(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            bool success;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out success);

            Plugin.Log("OnNotifyReady received from " + senderClientId + " success=" + success);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerNotifyReady(senderClientId, success);
            }
            else
            {
                Plugin.Warn("OnNotifyReady could not find controller for object " + networkObjectId);
            }
        }

        private static void OnBeginPlayback(ulong senderClientId, FastBufferReader reader)
        {
            Plugin.Log("OnBeginPlayback received");

            ulong networkObjectId;
            string videoId;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out videoId);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientBeginPlayback(videoId);
            }
            else
            {
                Plugin.Warn("OnBeginPlayback could not find controller for object " + networkObjectId);
            }
        }

        private static void OnRequestStop(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            reader.ReadValueSafe(out networkObjectId);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerHandleStop();
            }
            else
            {
                Plugin.Warn("OnRequestStop could not find controller for object " + networkObjectId);
            }
        }

        private static void OnStopPlayback(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            reader.ReadValueSafe(out networkObjectId);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientStopPlayback();
            }
            else
            {
                Plugin.Warn("OnStopPlayback could not find controller for object " + networkObjectId);
            }
        }

        private static void OnRejectPlay(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            string reason;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out reason);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientRejectPlay(reason);
            }
        }

        private static void OnSetVolume(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            float volume;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out volume);

            Plugin.Log("OnSetVolume received from " + senderClientId + " volume=" + volume);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerHandleSetVolume(volume);
            }
            else
            {
                Plugin.Warn("OnSetVolume could not find controller for object " + networkObjectId);
            }
        }

        private static void OnApplyVolume(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            float volume;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out volume);

            Plugin.Log("OnApplyVolume received volume=" + volume);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientApplyNetworkVolume(volume);
            }
            else
            {
                Plugin.Warn("OnApplyVolume could not find controller for object " + networkObjectId);
            }
        }

        private static void OnSyncSettings(ulong senderClientId, FastBufferReader reader)
        {
            bool localVolumeOnly;
            reader.ReadValueSafe(out localVolumeOnly);

            Plugin.SyncedLocalVolumeOnly = localVolumeOnly;
            Plugin.HasSyncedVolumeMode = true;

            Plugin.Log("Received synced settings. LocalVolumeOnly = " + localVolumeOnly);
        }

        private static ulong[] GetConnectedClientIds()
        {
            List<ulong> ids = new List<ulong>();

            if (boundManager == null || boundManager.ConnectedClients == null)
            {
                return ids.ToArray();
            }

            foreach (KeyValuePair<ulong, NetworkClient> kvp in boundManager.ConnectedClients)
            {
                ids.Add(kvp.Key);
            }

            return ids.ToArray();
        }
    }
}
