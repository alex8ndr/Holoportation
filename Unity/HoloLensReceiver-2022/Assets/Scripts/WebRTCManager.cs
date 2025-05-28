using Fusion;
using Fusion.Sockets;
using Microsoft.MixedReality.WebRTC;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class WebRTCManager : NetworkBehaviour
{
    private PeerConnection peerConnection;
    private DataChannel documentChannel;
    private DataChannel pointCloudChannel;

    // Room management
    private NetworkRunner networkRunner;
    private PlayerRef senderPlayer;
    public bool isSender = false; // Set to false for the receiver
    private List<byte> documentBuffer = new List<byte>();
    private byte[] documentData;
    private bool hasNewDocument = false;

    List<byte> pointCloudBuffer = new List<byte>();
    private Vector3[] receivedVertices;
    private Color32[] receivedColors;
    private bool hasNewPointCloud = false;

    private bool isFusionInitialized = false;
    private readonly List<IceCandidate> pendingIceCandidates = new();
    private bool remoteSdpSet = false;
    private string pendingSdpType;
    private string pendingSdpContent;
    private bool peerConnectionInitialized = false;

    private const int MAX_CHUNK_SIZE = 250000; // Maximum chunk size in bytes
    private const int MAX_DATA_QUEUE_SIZE = 5;

    // Quantization range per axis (must match the sender)
    float xMin = -0.32f, xMax = 0.32f;
    float yMin = -0.32f, yMax = 0.32f;
    float zMin = 0.00f, zMax = 0.64f;

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

    public void PlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        this.networkRunner = networkRunner;
        if (!isFusionInitialized)
        {
            isFusionInitialized = true;
            Debug.Log("PlayerJoined called. Fusion is now initialized.");
        }
    }

    private IEnumerator WaitForFusionConnection()
    {
        while (!isFusionInitialized)
        {
            yield return null;
        }
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef sender, ReliableKey key, ArraySegment<byte> data)
    {
        networkRunner = runner;
        var json = System.Text.Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
        var message = JsonUtility.FromJson<SignalMessage>(json);

        switch (message.Type)
        {
            case SignalType.Sdp:
                Debug.Log("Received SDP via reliable data.");
                senderPlayer = PlayerRef.FromIndex(message.SenderPlayerId);
                HandleReceivedSdp(message.Payload);
                break;

            case SignalType.Ice:
                Debug.Log("Received ICE via reliable data.");
                HandleReceivedIceCandidate(message.Payload, message.SdpMid, message.SdpMlineIndex);
                break;
        }
    }

    private void HandleReceivedSdp(string message)
    {
        string[] parts = message.Split(new[] { "::" }, 2, StringSplitOptions.None);
        if (parts.Length != 2)
        {
            Debug.LogError("Invalid SDP message format.");
            return;
        }

        pendingSdpType = parts[0];
        pendingSdpContent = parts[1];

        _ = SetupPeerConnection(); // This will apply SDP once ready
    }

    private async Task SetupPeerConnection()
    {
        var peer = new PeerConnection();

        peer.IceGatheringStateChanged += state => Debug.Log($"ICE {senderPlayer.PlayerId}: {state}");
        peer.LocalSdpReadytoSend += msg => SendSdpMessage(msg);
        peer.IceCandidateReadytoSend += cand => SendIceCandidate(cand);
        peer.DataChannelAdded += channel => OnDataChannelAdded(channel);

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

        peerConnection = peer;
        peerConnectionInitialized = true;

        var sdpMsg = new SdpMessage
        {
            Type = pendingSdpType == "offer" ? SdpMessageType.Offer : SdpMessageType.Answer,
            Content = pendingSdpContent
        };

        await peerConnection.SetRemoteDescriptionAsync(sdpMsg);
        remoteSdpSet = true;
        Debug.Log("Remote SDP set successfully.");

        if (pendingSdpType == "offer" && !isSender)
        {
            Debug.Log("Creating answer...");
            peerConnection.CreateAnswer();
        }

        foreach (var candidate in pendingIceCandidates)
        {
            peerConnection.AddIceCandidate(candidate);
        }
        pendingIceCandidates.Clear();
    }

    private void HandleReceivedIceCandidate(string content, string sdpMid, int sdpMlineIndex)
    {
        var candidate = new IceCandidate
        {
            Content = content,
            SdpMid = sdpMid,
            SdpMlineIndex = sdpMlineIndex
        };

        if (!peerConnectionInitialized || !remoteSdpSet)
        {
            Debug.Log("Queuing ICE candidate...");
            pendingIceCandidates.Add(candidate);
            return;
        }

        peerConnection.AddIceCandidate(candidate);
    }

    private void SendSdpMessage(SdpMessage message)
    {
        var msg = new SignalMessage
        {
            Type = SignalType.Sdp,
            Payload = $"{(message.Type == SdpMessageType.Offer ? "offer" : "answer")}::{message.Content}",
            SenderPlayerId = networkRunner.LocalPlayer.PlayerId
        };

        var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        Debug.Log("Sending an SDP message to sender player " + senderPlayer.PlayerId);
        networkRunner.SendReliableDataToPlayer(senderPlayer, ReliableKey.FromInts(0), bytes);
    }

    private void SendIceCandidate(IceCandidate candidate)
    {
        var message = new SignalMessage
        {
            Type = SignalType.Ice,
            Payload = candidate.Content,
            SdpMid = candidate.SdpMid,
            SdpMlineIndex = candidate.SdpMlineIndex,
            SenderPlayerId = networkRunner.LocalPlayer.PlayerId
        };

        string json = JsonUtility.ToJson(message);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        networkRunner.SendReliableDataToPlayer(senderPlayer, ReliableKey.FromInts(1), bytes);
    }

    private void OnDataChannelAdded(DataChannel channel)
    {
        Debug.Log($"Data channel added: {channel.Label}");

        if (channel.Label.StartsWith("doc_"))
        {
            documentChannel = channel;
            documentChannel.StateChanged += () => Debug.Log($"Document channel state changed: {documentChannel.State}");
            documentChannel.MessageReceived += HandleDocumentMessage;
        }
        else if (channel.Label.StartsWith("pc_"))
        {
            pointCloudChannel = channel;
            pointCloudChannel.StateChanged += () => Debug.Log($"Point cloud channel state changed: {pointCloudChannel.State}");
            pointCloudChannel.MessageReceived += HandlePointCloudMessage;
        }
    }

    // Handling Received Data (For Debugging and Validation)
    private void HandleDocumentMessage(byte[] data)
    {
        if (data.Length == 1 && data[0] == 1) // Check for completion flag
        {
            Debug.Log("Document transfer complete.");
            documentData = documentBuffer.ToArray();
            hasNewDocument = true;
            documentBuffer.Clear();
        }
        else
        {
            Debug.Log($"Received document data of size {data.Length} bytes");
            documentBuffer.AddRange(data);
        }
    }

    private void HandlePointCloudMessage(byte[] data)
    {
        if (data.Length == 1 && data[0] == 1) // Check for completion flag
        {
            Debug.Log("Point Cloud transfer complete.");
            DeserializePointCloud(pointCloudBuffer.ToArray(), out receivedVertices, out receivedColors);
            hasNewPointCloud = true;
            pointCloudBuffer.Clear();
        }
        else
        {
            Debug.Log($"Received point cloud data of size {data.Length} bytes");
            pointCloudBuffer.AddRange(data);
        }
    }

    private void DeserializePointCloud(byte[] data, out Vector3[] vertices, out Color32[] colors)
    {
        // Read header
        int headerSize = 10;
        if (data.Length < headerSize)
        {
            Debug.LogError("Point cloud data too small to contain header.");
            vertices = new Vector3[0];
            colors = new Color32[0];
            return;
        }

        int nPoints = BitConverter.ToInt32(data, 0);

        // Min/max encoded as bytes (centimeter precision in range [0, 1])
        xMin = DecodeByteToFloat(data[4], 0, 1);
        xMax = DecodeByteToFloat(data[5], 0, 1);
        yMin = DecodeByteToFloat(data[6], 0, 1);
        yMax = DecodeByteToFloat(data[7], 0, 1);
        zMin = DecodeByteToFloat(data[8], 0, 1);
        zMax = DecodeByteToFloat(data[9], 0, 1);

        // Allocate arrays
        vertices = new Vector3[nPoints];
        colors = new Color32[nPoints];

        int vertexStart = headerSize;
        int colorStart = vertexStart + nPoints * 3;

        if (data.Length < colorStart + nPoints * 3)
        {
            Debug.LogError("Point cloud data is too short for declared number of points.");
            return;
        }

        // Decode vertices and colors
        for (int i = 0; i < nPoints; i++)
        {
            int vi = vertexStart + i * 3;
            byte bx = data[vi + 0];
            byte by = data[vi + 1];
            byte bz = data[vi + 2];

            float x = DecodeByteToFloat(bx, xMin, xMax);
            float y = DecodeByteToFloat(by, yMin, yMax);
            float z = DecodeByteToFloat(bz, zMin, zMax);

            vertices[i] = new Vector3(x, y, z);

            int ci = colorStart + i * 3;
            byte r = data[ci + 0];
            byte g = data[ci + 1];
            byte b = data[ci + 2];

            colors[i] = new Color32(r, g, b, 255);
        }
    }

    private float DecodeByteToFloat(byte b, float min, float max)
    {
        return (b / 255f) * (max - min) + min;
    }

    public bool HasNewDocument() => hasNewDocument;

    public byte[] GetReceivedDocument()
    {
        hasNewDocument = false;
        return documentData;
    }

    public bool HasNewPointCloud() => hasNewPointCloud;

    public (Vector3[], Color32[]) GetReceivedPointCloud()
    {
        hasNewPointCloud = false;
        return (receivedVertices, receivedColors);
    }

    private void OnDestroy()
    {
        peerConnection?.Close();
        peerConnection?.Dispose();
    }
}