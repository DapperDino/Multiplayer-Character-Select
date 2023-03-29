using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchplayNetworkClient : IDisposable
{
    public event Action<ConnectStatus> OnLocalConnection;
    public event Action<ConnectStatus> OnLocalDisconnection;

    private const int TimeoutDuration = 10;
    private NetworkManager networkManager;
    private RelayJoinData relayJoinData;

    private DisconnectReason DisconnectReason { get; } = new DisconnectReason();

    private const string MenuSceneName = "Menu";

    public MatchplayNetworkClient()
    {
        networkManager = NetworkManager.Singleton;
        networkManager.OnClientDisconnectCallback += RemoteDisconnect;
    }

    public void StartClient(string ip, int port)
    {
        var unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        unityTransport.SetConnectionData(ip, (ushort)port);
        ConnectClient();
    }

    public async Task<JoinAllocation> StartClient(string joinCode)
    {
        JoinAllocation allocation = null;

        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return null;
        }

        relayJoinData = new RelayJoinData
        {
            Key = allocation.Key,
            Port = (ushort)allocation.RelayServer.Port,
            AllocationID = allocation.AllocationId,
            AllocationIDBytes = allocation.AllocationIdBytes,
            ConnectionData = allocation.ConnectionData,
            HostConnectionData = allocation.HostConnectionData,
            IPv4Address = allocation.RelayServer.IpV4
        };

        UnityTransport unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();

        unityTransport.SetRelayServerData(relayJoinData.IPv4Address,
            relayJoinData.Port,
            relayJoinData.AllocationIDBytes,
            relayJoinData.Key,
            relayJoinData.ConnectionData,
            relayJoinData.HostConnectionData);

        ConnectClient();

        return allocation;
    }

    public void DisconnectClient()
    {
        DisconnectReason.SetDisconnectReason(ConnectStatus.UserRequestedDisconnect);
        NetworkShutdown();
    }

    private void ConnectClient()
    {
        UserData userData = ClientSingleton.Instance.Manager.User.Data;

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        networkManager.NetworkConfig.ConnectionData = payloadBytes;
        networkManager.NetworkConfig.ClientConnectionBufferTimeout = TimeoutDuration;

        if (networkManager.StartClient())
        {
            Debug.Log("Starting Client!");

            RegisterListeners();
        }
        else
        {
            Debug.LogWarning("Could not start Client!");
            OnLocalDisconnection?.Invoke(ConnectStatus.Undefined);
        }
    }

    public void RegisterListeners()
    {
        MatchplayNetworkMessenger.RegisterListener(NetworkMessage.LocalClientConnected,
            ReceiveLocalClientConnectStatus);
        MatchplayNetworkMessenger.RegisterListener(NetworkMessage.LocalClientDisconnected,
            ReceiveLocalClientDisconnectStatus);
    }

    private void ReceiveLocalClientConnectStatus(ulong clientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ConnectStatus status);

        Debug.Log("ReceiveLocalClientConnectStatus: " + status);

        if (status != ConnectStatus.Success)
        {
            DisconnectReason.SetDisconnectReason(status);
        }

        OnLocalConnection?.Invoke(status);
    }

    private void ReceiveLocalClientDisconnectStatus(ulong clientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ConnectStatus status);
        Debug.Log("ReceiveLocalClientDisconnectStatus: " + status);
        DisconnectReason.SetDisconnectReason(status);
    }

    private void RemoteDisconnect(ulong clientId)
    {
        Debug.Log($"Got Client Disconnect callback for {clientId}");

        if (clientId != 0 && clientId != networkManager.LocalClientId) { return; }

        NetworkShutdown();
    }

    private void NetworkShutdown()
    {
        // Take client back to the main menu if they aren't already there
        if (SceneManager.GetActiveScene().name != MenuSceneName)
        {
            SceneManager.LoadScene(MenuSceneName);
        }

        // If we are already on the main menu then it means we timed-out
        if (networkManager.IsConnectedClient)
        {
            networkManager.Shutdown();
        }

        OnLocalDisconnection?.Invoke(DisconnectReason.Reason);
        MatchplayNetworkMessenger.UnRegisterListener(NetworkMessage.LocalClientConnected);
        MatchplayNetworkMessenger.UnRegisterListener(NetworkMessage.LocalClientDisconnected);
    }

    public void Dispose()
    {
        if (networkManager != null && networkManager.CustomMessagingManager != null)
        {
            networkManager.OnClientConnectedCallback -= RemoteDisconnect;
        }
    }
}

public struct RelayJoinData
{
    public string JoinCode;
    public string IPv4Address;
    public ushort Port;
    public Guid AllocationID;
    public byte[] AllocationIDBytes;
    public byte[] ConnectionData;
    public byte[] HostConnectionData;
    public byte[] Key;
}