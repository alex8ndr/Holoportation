using Assets.Scripts.Utils;
using Fusion;
using Fusion.Sockets;
using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class WebRTCManager : NetworkBehaviour
{
    private NetworkRunner networkRunner;
    public bool isSender = true; // This is the sender

    private byte[] documentData;
    private bool hasNewDocument = false;

    private Vector3[] receivedVertices;
    private Color[] receivedColors;
    private bool hasNewPointCloud = false;

    private bool isFusionInitialized = false;

    private const float POSITION_SCALE = 1000f;
    private const int MAX_CHUNK_SIZE = 250000; // Maximum chunk size in bytes
    private const int MAX_DATA_QUEUE_SIZE = 5;

    private PlayerRef currentPlayer;
    private List<PlayerRef> otherPlayers = new();
    public List<NetworkObject> networkObjects = new List<NetworkObject>();

    private Dictionary<PlayerRef, PeerConnection> peerConnections = new();
    private ConcurrentDictionary<PlayerRef, PlayerChannels> allPlayerChannels = new();

    private enum SignalType : byte
    {
        Sdp,
        Ice
    }

    private struct SignalMessage
    {
        public SignalType Type;
        public string Payload;
        public string SdpMid;
        public int SdpMlineIndex;
        public int SenderPlayerId;
    }

    void OnEnable()
    {
        StartCoroutine(WaitForFusionConnection());
    }

    private void Update()
    {
        if (isFusionInitialized)
        {
            SendQueuedDataForAllChannels();
        }
    }

    private void SendQueuedDataForAllChannels()
    {
        foreach (var playerChannels in allPlayerChannels.Values)
        {
            if (playerChannels.DocumentChannel.CanSend && playerChannels.DocumentChannel.DataQueue.TryDequeue(out var documentData))
            {
                SendInChunksOnChannel(playerChannels.DocumentChannel.Channel, documentData);
            }

            if (playerChannels.PointCloudChannel.CanSend && playerChannels.PointCloudChannel.DataQueue.TryDequeue(out var frameData))
            {
                SendInChunksOnChannel(playerChannels.PointCloudChannel.Channel, frameData);
            }
        }
    }

    public void SpawnNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (isFusionInitialized)
        {
            NetworkObject networkedObject = networkRunner.Spawn(prefab.GetComponent<NetworkObject>(), position, rotation, currentPlayer);
            networkObjects.Add(networkedObject);
        }
    }

    public void DestroyNetworkObjects(int nObjects)
    {
        for (int i = 0; i < nObjects; i++)
        {
            NetworkObject toRemove = networkObjects[0];
            networkRunner.Despawn(toRemove);
            networkObjects.Remove(networkObjects[0]);
        }
    }
    private IEnumerator WaitForFusionConnection()
    {
        while (!isFusionInitialized)
        {
            yield return null;
        }
    }

    public void PlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        this.networkRunner = networkRunner;

        if (!isFusionInitialized && player.PlayerId == networkRunner.LocalPlayer.PlayerId)
        {
            isFusionInitialized = true;
            currentPlayer = player;
            Debug.Log("Fusion initialized for current player.");
        }
        else
        {
            otherPlayers.Add(player);
            _ = SetupPeerConnectionForPlayer(player); // async void
        }
    }

    public void PlayerLeft(NetworkRunner networkRunner, PlayerRef player)
    {
        this.networkRunner = networkRunner;

        if (peerConnections.TryGetValue(player, out var connection))
        {
            connection.Close();
            connection.Dispose();
        }

        peerConnections.Remove(player);
        allPlayerChannels.Remove(player, out var removed);
        otherPlayers.Remove(player);
    }

    private async Task SetupPeerConnectionForPlayer(PlayerRef player)
    {
        Debug.Log("Setting up a peer connection for player " + player.PlayerId);

        var peer = new PeerConnection();

        peer.IceGatheringStateChanged += state => Debug.Log($"ICE {player.PlayerId}: {state}");
        peer.LocalSdpReadytoSend += msg => SendSdpMessage(player, msg);
        peer.IceCandidateReadytoSend += cand => SendIceCandidate(player, cand);

        peer.DataChannelAdded += channel => Debug.Log($"Channel added: {channel.Label}");

        await peer.InitializeAsync(new PeerConnectionConfiguration
        {
            IceServers = new List<IceServer>
            {
                new IceServer
                {
                    Urls = { "turn:168.138.76.12:5056" },
                    TurnUserName = "holoportationuser",
                    TurnPassword = "hlR4g7&52phqwe568142+"
                }
            }
        });

        var docChannel = await peer.AddDataChannelAsync($"doc_{player.PlayerId}", true, true);
        var pcChannel = await peer.AddDataChannelAsync($"pc_{player.PlayerId}", true, true);

        docChannel.StateChanged += () => Debug.Log($"Document channel state for {player.PlayerId}: {docChannel.State}");
        docChannel.MessageReceived += HandleDocumentMessage;
        docChannel.BufferingChanged += (ulong previous, ulong current, ulong limit) => BufferingChanged(docChannel, previous, current, limit);

        pcChannel.StateChanged += () => Debug.Log($"Point cloud channel state for {player.PlayerId}: {pcChannel.State}");
        pcChannel.MessageReceived += HandlePointCloudMessage;
        pcChannel.BufferingChanged += (ulong previous, ulong current, ulong limit) => BufferingChanged(pcChannel, previous, current, limit);

        allPlayerChannels[player] = new PlayerChannels
        {
            DocumentChannel = new PlayerDataChannel
            {
                Channel = docChannel,
                CanSend = true,
                DataQueue = new()
            },

            PointCloudChannel = new PlayerDataChannel
            {
                Channel = pcChannel,
                CanSend = true,
                DataQueue = new()
            },
        };

        peerConnections[player] = peer;

        peer.CreateOffer();

        Debug.Log("Peer connection for player " + player.PlayerId + " initialized");
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef sender, ReliableKey key, ArraySegment<byte> data)
    {
        networkRunner = runner;
        var json = System.Text.Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        var msg = JsonUtility.FromJson<SignalMessage>(json);

        switch (msg.Type)
        {
            case SignalType.Sdp:
                Debug.Log("Received SDP via reliable data.");
                HandleReceivedSdp(PlayerRef.FromIndex(msg.SenderPlayerId), msg.Payload);
                break;
            case SignalType.Ice:
                Debug.Log("Received ICE via reliable data.");
                HandleReceivedIceCandidate(PlayerRef.FromIndex(msg.SenderPlayerId), msg.Payload, msg.SdpMid, msg.SdpMlineIndex);
                break;
        }
    }

    private async void HandleReceivedSdp(PlayerRef player, string payload)
    {
        var parts = payload.Split(new[] { "::" }, 2, StringSplitOptions.None);
        var type = parts[0];
        var sdp = parts[1];

        if (!peerConnections.TryGetValue(player, out var peer))
        {
            Debug.LogError($"No PeerConnection found for {player}");
            return;
        }

        var msg = new SdpMessage
        {
            Type = type == "offer" ? SdpMessageType.Offer : SdpMessageType.Answer,
            Content = sdp
        };

        await peer.SetRemoteDescriptionAsync(msg);

        if (type == "offer" && !isSender)
        {
            peer.CreateAnswer();
        }
    }

    private void HandleReceivedIceCandidate(PlayerRef player, string content, string sdpMid, int sdpMlineIndex)
    {
        if (!peerConnections.TryGetValue(player, out var peer))
        {
            Debug.LogWarning($"ICE candidate received for unknown player {player.PlayerId}. Deferring...");
            return;
        }

        var candidate = new IceCandidate
        {
            Content = content,
            SdpMid = sdpMid,
            SdpMlineIndex = sdpMlineIndex
        };

        peer.AddIceCandidate(candidate);
        Debug.Log($"ICE candidate applied to player {player.PlayerId}");
    }

    private void SendSdpMessage(PlayerRef player, SdpMessage message)
    {
        var msg = new SignalMessage
        {
            Type = SignalType.Sdp,
            Payload = $"{(message.Type == SdpMessageType.Offer ? "offer" : "answer")}::{message.Content}",
            SenderPlayerId = networkRunner.LocalPlayer.PlayerId
        };

        var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        networkRunner.SendReliableDataToPlayer(player, ReliableKey.FromInts(0), bytes);
    }

    private void SendIceCandidate(PlayerRef player, IceCandidate candidate)
    {
        var msg = new SignalMessage
        {
            Type = SignalType.Ice,
            Payload = candidate.Content,
            SdpMid = candidate.SdpMid,
            SdpMlineIndex = candidate.SdpMlineIndex
        };

        var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        networkRunner.SendReliableDataToPlayer(player, ReliableKey.FromInts(0), bytes);
    }

    public void SendDocument(byte[] documentData)
    {
        foreach (var player in otherPlayers)
        {
            SendDocumentTo(player, documentData);
        }
    }

    // Sending Document Data in Chunks
    public void SendDocumentTo(PlayerRef player, byte[] documentData)
    {
        if (!allPlayerChannels.TryGetValue(player, out var channels) || channels.DocumentChannel.Channel.State != DataChannel.ChannelState.Open)
        {
            Debug.LogWarning($"No open document channel for player {player.PlayerId}");
            return;
        }

        // Enqueue the data for later sending
        if (channels.DocumentChannel.DataQueue.Count < MAX_DATA_QUEUE_SIZE)
        {
            channels.DocumentChannel.DataQueue.Enqueue(documentData);
        }

        hasNewDocument = true;
        this.documentData = documentData;
    }

    public void SendPointCloud(Vector3[] vertices, Color[] colors)
    {
        foreach (var player in otherPlayers)
        {
            SendPointCloudTo(player, vertices, colors);
        }
    }

    // Sending Point Cloud Data in Chunks
    public void SendPointCloudTo(PlayerRef player, Vector3[] vertices, Color[] colors)
    {
        if (!allPlayerChannels.TryGetValue(player, out var channels) || channels.PointCloudChannel.Channel.State != DataChannel.ChannelState.Open)
        {
            Debug.LogWarning($"No open point cloud channel for player {player.PlayerId}");
            return;
        }
        
        // Serialize into a byte array
        byte[] data = SerializePointCloud(vertices, colors);

        // Enqueue the data for later sending
        if (channels.PointCloudChannel.DataQueue.Count < MAX_DATA_QUEUE_SIZE)
        {
            channels.PointCloudChannel.DataQueue.Enqueue(data);
        }

        hasNewPointCloud = true;
        receivedVertices = vertices;
        receivedColors = colors;
    }

    private void SendInChunksOnChannel(DataChannel channel, byte[] data)
    {
        var chunks = SplitDataIntoChunks(data);
        foreach (var chunk in chunks)
        {
            channel.SendMessage(chunk);
        }

        SendCompletionFlag(channel);
    }

    // Helper function to split data into chunks
    private byte[][] SplitDataIntoChunks(byte[] data)
    {
        int chunkCount = Mathf.CeilToInt((float)data.Length / MAX_CHUNK_SIZE);
        byte[][] chunks = new byte[chunkCount][];

        for (int i = 0; i < chunkCount; i++)
        {
            int offset = i * MAX_CHUNK_SIZE;
            int chunkSize = Mathf.Min(MAX_CHUNK_SIZE, data.Length - offset);
            byte[] chunk = new byte[chunkSize];
            Array.Copy(data, offset, chunk, 0, chunkSize);
            chunks[i] = chunk;
        }

        return chunks;
    }

    // Send a completion flag after all chunks are sent
    private void SendCompletionFlag(DataChannel channel)
    {
        byte[] completionMessage = new byte[] { 1 }; // Flag indicating completion (1 for done)
        channel.SendMessage(completionMessage);
    }

    private byte[] SerializePointCloud(Vector3[] vertices, Color[] colors)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(vertices.Length);

            foreach (var vertex in vertices)
            {
                writer.Write((short)(vertex.x * POSITION_SCALE));
                writer.Write((short)(vertex.y * POSITION_SCALE));
                writer.Write((short)(vertex.z * POSITION_SCALE));
            }

            foreach (var color in colors)
            {
                writer.Write((byte)(color.r * 255));
                writer.Write((byte)(color.g * 255));
                writer.Write((byte)(color.b * 255));
            }

            return stream.ToArray();
        }
    }

    private void BufferingChanged(DataChannel datachannel, ulong previous, ulong current, ulong limit)
    {
        if (current + 2 * MAX_CHUNK_SIZE >= limit)
        {
            var keys = allPlayerChannels.Keys.ToList();

            foreach (var player in keys)
            {
                var playerChannels = allPlayerChannels[player];

                if (playerChannels.DocumentChannel.Channel.Label.Equals(datachannel.Label))
                {
                    playerChannels.DocumentChannel.CanSend = false;
                    return;
                }

                if (playerChannels.PointCloudChannel.Channel.Label.Equals(datachannel.Label))
                {
                    playerChannels.PointCloudChannel.CanSend = false;
                    return;
                }
            }
        }
        else
        {
            var keys = allPlayerChannels.Keys.ToList();

            foreach (var player in keys)
            {
                var playerChannels = allPlayerChannels[player];

                if (playerChannels.DocumentChannel.Channel.Label.Equals(datachannel.Label))
                {
                    var docChannel = playerChannels.DocumentChannel;
                    if (!docChannel.CanSend) // Only update if needed
                    {
                        docChannel.CanSend = true;;
                    }
                    return;
                }

                if (playerChannels.PointCloudChannel.Channel.Label.Equals(datachannel.Label))
                {
                    var pcChannel = playerChannels.PointCloudChannel;
                    if (!pcChannel.CanSend) // Only update if needed
                    {
                        pcChannel.CanSend = true;;
                    }
                    return;
                }
            }
        }
    }

    // Handling Received Data (For Debugging and Validation)
    private void HandleDocumentMessage(byte[] data)
    {
        if (data.Length == 1 && data[0] == 1) // Check for completion flag
        {
            Debug.Log("Document transfer complete.");
            hasNewDocument = true;
        }
        else
        {
            Debug.Log($"Received document data of size {data.Length} bytes");
        }
    }

    private void HandlePointCloudMessage(byte[] data)
    {
        if (data.Length == 1 && data[0] == 1) // Check for completion flag
        {
            Debug.Log("Point Cloud transfer complete.");
            hasNewPointCloud = true;
        }
        else
        {
            Debug.Log($"Received point cloud data of size {data.Length} bytes");
        }
    }

    public bool HasNewDocument() => hasNewDocument;

    public byte[] GetReceivedDocument()
    {
        hasNewDocument = false;
        return documentData;
    }

    public bool HasNewPointCloud() => hasNewPointCloud;

    public (Vector3[], Color[]) GetReceivedPointCloud()
    {
        hasNewPointCloud = false;
        return (receivedVertices, receivedColors);
    }

    private void OnDestroy()
    {
        foreach (var (playerRef, peerConnection) in peerConnections)
        {
            try
            {
                peerConnection?.Close();
                peerConnection?.Dispose();
            }
            catch { }
        }
    }
}