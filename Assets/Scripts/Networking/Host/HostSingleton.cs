using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class HostSingleton : MonoBehaviour
{
    [SerializeField] private int maxConnections = 4;

    private static HostSingleton hostSingleton;

    public static HostSingleton Instance
    {
        get
        {
            if (hostSingleton != null) return hostSingleton;

            hostSingleton = FindObjectOfType<HostSingleton>();

            if (hostSingleton == null)
            {
                Debug.LogError("No HostSingleton in scene, did you run this from the bootStrap scene?");
                return null;
            }

            return hostSingleton;
        }
    }

    public MatchplayNetworkServer NetworkServer { get; private set; }
    public RelayHostData RelayHostData => relayHostData;
    private RelayHostData relayHostData;
    private string lobbyId;

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    public async Task<bool> StartHostAsync()
    {
        Allocation allocation = null;

        try
        {
            //Ask Unity Services to allocate a Relay server
            allocation = await Relay.Instance.CreateAllocationAsync(maxConnections);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return false;
        }

        //Populate the hosting data
        relayHostData = new RelayHostData
        {
            Key = allocation.Key,
            Port = (ushort)allocation.RelayServer.Port,
            AllocationID = allocation.AllocationId,
            AllocationIDBytes = allocation.AllocationIdBytes,
            ConnectionData = allocation.ConnectionData,
            IPv4Address = allocation.RelayServer.IpV4
        };

        try
        {
            //Retrieve the Relay join code for our clients to join our party
            relayHostData.JoinCode = await Relay.Instance.GetJoinCodeAsync(RelayHostData.AllocationID);

            Debug.Log(RelayHostData.JoinCode);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            return false;
        }

        //Retrieve the Unity transport used by the NetworkManager
        UnityTransport transport = NetworkManager.Singleton.gameObject.GetComponent<UnityTransport>();

        transport.SetRelayServerData(RelayHostData.IPv4Address,
            RelayHostData.Port,
            RelayHostData.AllocationIDBytes,
            RelayHostData.Key,
            RelayHostData.ConnectionData);

        try
        {
            var createLobbyOptions = new CreateLobbyOptions();
            createLobbyOptions.IsPrivate = false;
            createLobbyOptions.Data = new Dictionary<string, DataObject>()
            {
                {
                    "JoinCode", new DataObject(
                        visibility: DataObject.VisibilityOptions.Member,
                        value: RelayHostData.JoinCode
                    )
                }
            };

            Lobby lobby = await Lobbies.Instance.CreateLobbyAsync("My Lobby", maxConnections, createLobbyOptions);
            lobbyId = lobby.Id;
            StartCoroutine(HeartbeatLobbyCoroutine(15));
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            return false;
        }

        UserData userData = ClientSingleton.Instance.Manager.User.Data;

        string payload = JsonUtility.ToJson(userData);
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

        NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;

        NetworkServer = new MatchplayNetworkServer(NetworkManager.Singleton);

        NetworkManager.Singleton.StartHost();

#pragma warning disable 4014
        await NetworkServer.ConfigureServer(new GameInfo
        {
            map = Map.Default
        });
#pragma warning restore 4014

        ClientSingleton.Instance.Manager.NetworkClient.RegisterListeners();

        NetworkServer.OnClientLeft += OnClientDisconnect;

        return true;
    }

    private IEnumerator HeartbeatLobbyCoroutine(float waitTimeSeconds)
    {
        var delay = new WaitForSecondsRealtime(waitTimeSeconds);
        while (true)
        {
            Lobbies.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }

    private async void OnClientDisconnect(string authId)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, authId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async void Shutdown()
    {
        StopCoroutine(nameof(HeartbeatLobbyCoroutine));

        if (string.IsNullOrEmpty(lobbyId)) { return; }

        try
        {
            await Lobbies.Instance.DeleteLobbyAsync(lobbyId);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }

        lobbyId = string.Empty;

        NetworkServer.OnClientLeft -= OnClientDisconnect;

        NetworkServer?.Dispose();
    }
}

public struct RelayHostData
{
    public string JoinCode;
    public string IPv4Address;
    public ushort Port;
    public Guid AllocationID;
    public byte[] AllocationIDBytes;
    public byte[] ConnectionData;
    public byte[] Key;
}
