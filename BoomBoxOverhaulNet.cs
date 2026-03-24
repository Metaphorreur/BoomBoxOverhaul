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

        private static MonoBehaviour coroutineHost;
        private static bool initialized;

        public static void Initialize(MonoBehaviour host)
        {
            if (initialized)
            {
                return;
            }

            coroutineHost = host;
            initialized = true;
            host.StartCoroutine(RegisterWhenReady());
        }

        private static IEnumerator RegisterWhenReady()
        {
            while (NetworkManager.Singleton == null)
            {
                yield return null;
            }

            RegisterHandlers();
            Plugin.Log("BoomBoxOverhaul network applied (I promise this time) -Henreh.");
        }

        private static void RegisterHandlers()
        {
            CustomMessagingManager mm = NetworkManager.Singleton.CustomMessagingManager;

            mm.RegisterNamedMessageHandler(MsgRequestPlay, OnRequestPlay);
            mm.RegisterNamedMessageHandler(MsgPrepareTrack, OnPrepareTrack);
            mm.RegisterNamedMessageHandler(MsgNotifyReady, OnNotifyReady);
            mm.RegisterNamedMessageHandler(MsgBeginPlayback, OnBeginPlayback);
            mm.RegisterNamedMessageHandler(MsgRequestStop, OnRequestStop);
            mm.RegisterNamedMessageHandler(MsgStopPlayback, OnStopPlayback);
        }

        public static UnifiedBoomboxController GetController(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null)
            {
                return null;
            }

            NetworkObject netObj;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out netObj))
            {
                return null;
            }

            return netObj.GetComponent<UnifiedBoomboxController>();
        }

        public static void SendRequestPlay(ulong networkObjectId, string url)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                return;
            }

            FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp);
            writer.WriteValueSafe(networkObjectId);
            writer.WriteValueSafe(url);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgRequestPlay, NetworkManager.ServerClientId, writer);
            writer.Dispose();
        }

        public static void BroadcastPrepareTrack(ulong networkObjectId, string canonicalUrl, string videoId, int playlistIndex, string[] playlistIds)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                FastBufferWriter writer = new FastBufferWriter(8192, Allocator.Temp);
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

                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgPrepareTrack, clientIds[i], writer);
                writer.Dispose();
            }
        }

        public static void SendNotifyReady(ulong networkObjectId, bool success)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                return;
            }

            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(networkObjectId);
            writer.WriteValueSafe(success);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgNotifyReady, NetworkManager.ServerClientId, writer);
            writer.Dispose();
        }

        public static void BroadcastBeginPlayback(ulong networkObjectId, string videoId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp);
                writer.WriteValueSafe(networkObjectId);
                writer.WriteValueSafe(videoId);

                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgBeginPlayback, clientIds[i], writer);
                writer.Dispose();
            }
        }

        public static void SendRequestStop(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            {
                return;
            }

            FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(networkObjectId);

            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgRequestStop, NetworkManager.ServerClientId, writer);
            writer.Dispose();
        }

        public static void BroadcastStopPlayback(ulong networkObjectId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            ulong[] clientIds = GetConnectedClientIds();
            int i;
            for (i = 0; i < clientIds.Length; i++)
            {
                FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
                writer.WriteValueSafe(networkObjectId);

                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgStopPlayback, clientIds[i], writer);
                writer.Dispose();
            }
        }

        private static void OnRequestPlay(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            string url;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out url);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerHandlePlay(url);
            }
        }

        private static void OnPrepareTrack(ulong senderClientId, FastBufferReader reader)
        {
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
        }

        private static void OnNotifyReady(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            bool success;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out success);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ServerNotifyReady(senderClientId, success);
            }
        }

        private static void OnBeginPlayback(ulong senderClientId, FastBufferReader reader)
        {
            ulong networkObjectId;
            string videoId;

            reader.ReadValueSafe(out networkObjectId);
            reader.ReadValueSafe(out videoId);

            UnifiedBoomboxController controller = GetController(networkObjectId);
            if (controller != null)
            {
                controller.ClientBeginPlayback(videoId);
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
        }

        private static ulong[] GetConnectedClientIds()
        {
            List<ulong> ids = new List<ulong>();
            foreach (KeyValuePair<ulong, NetworkClient> kvp in NetworkManager.Singleton.ConnectedClients)
            {
                ids.Add(kvp.Key);
            }
            return ids.ToArray();
        }
    }
}
