using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchplayNetworkServer : IDisposable
{
    public static MatchplayNetworkServer Instance { get; private set; }

    public Action<Matchplayer> OnServerPlayerAdded;
    public Action<Matchplayer> OnServerPlayerRemoved;

    public Action<UserData> OnPlayerJoined;
    public Action<UserData> OnPlayerLeft;

    private SynchedServerData synchedServerData;
    private NetworkManager networkManager;

    public Action<string> OnClientLeft;

    private const int MaxConnectionPayload = 1024;

    private bool gameHasStarted;

    public Dictionary<string, UserData> ClientData { get; private set; } = new Dictionary<string, UserData>();
    public Dictionary<ulong, string> ClientIdToAuth { get; private set; } = new Dictionary<ulong, string>();

    public MatchplayNetworkServer(NetworkManager networkManager)
    {
        this.networkManager = networkManager;

        this.networkManager.ConnectionApprovalCallback += ApprovalCheck;
        this.networkManager.OnServerStarted += OnNetworkReady;

        Instance = this;
    }

    public bool OpenConnection(string ip, int port, GameInfo startingGameInfo)
    {
        var unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        networkManager.NetworkConfig.NetworkTransport = unityTransport;
        unityTransport.SetConnectionData(ip, (ushort)port);
        Debug.Log($"Starting server at {ip}:{port}\nWith: {startingGameInfo}");

        return networkManager.StartServer();
    }

    public async Task<SynchedServerData> ConfigureServer(GameInfo startingGameInfo)
    {
        networkManager.SceneManager.LoadScene("CharacterSelect", LoadSceneMode.Single);

        bool localNetworkedSceneLoaded = false;
        networkManager.SceneManager.OnLoadComplete += CreateAndSetSynchedServerData;

        void CreateAndSetSynchedServerData(ulong clientId, string sceneName, LoadSceneMode sceneMode)
        {
            if (clientId != networkManager.LocalClientId) { return; }
            localNetworkedSceneLoaded = true;
            networkManager.SceneManager.OnLoadComplete -= CreateAndSetSynchedServerData;
        }

        var waitTask = WaitUntilSceneLoaded();

        async Task WaitUntilSceneLoaded()
        {
            while (!localNetworkedSceneLoaded)
            {
                await Task.Delay(50);
            }
        }

        if (await Task.WhenAny(waitTask, Task.Delay(5000)) != waitTask)
        {
            Debug.LogWarning($"Timed out waiting for Server Scene Loading: Not able to Load Scene");
            return null;
        }

        synchedServerData = GameObject.Instantiate(Resources.Load<SynchedServerData>("SynchedServerData"));
        synchedServerData.GetComponent<NetworkObject>().Spawn();

        synchedServerData.map.Value = startingGameInfo.map;
        synchedServerData.gameMode.Value = startingGameInfo.gameMode;
        synchedServerData.gameQueue.Value = startingGameInfo.gameQueue;

        Debug.Log(
            $"Synched Server Values: {synchedServerData.map.Value} - {synchedServerData.gameMode.Value} - {synchedServerData.gameQueue.Value}",
            synchedServerData.gameObject);

        return synchedServerData;
    }

    public void SetCharacter(ulong clientId, int characterId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out string auth))
        {
            if (ClientData.TryGetValue(auth, out UserData data))
            {
                data.characterId = characterId;
            }
        }
    }

    public void StartGame()
    {
        gameHasStarted = true;

        NetworkManager.Singleton.SceneManager.LoadScene("Gameplay", LoadSceneMode.Single);
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (request.Payload.Length > MaxConnectionPayload || gameHasStarted)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Position = null;
            response.Rotation = null;
            response.Pending = false;

            return;
        }

        string payload = System.Text.Encoding.UTF8.GetString(request.Payload);
        UserData userData = JsonUtility.FromJson<UserData>(payload);
        userData.clientId = request.ClientNetworkId;
        Debug.Log($"Host ApprovalCheck: connecting client: ({request.ClientNetworkId}) - {userData}");

        if (ClientData.ContainsKey(userData.userAuthId))
        {
            ulong oldClientId = ClientData[userData.userAuthId].clientId;
            Debug.Log($"Duplicate ID Found : {userData.userAuthId}, Disconnecting Old user");

            SendClientDisconnected(request.ClientNetworkId, ConnectStatus.LoggedInAgain);
            WaitToDisconnect(oldClientId);
        }

        SendClientConnected(request.ClientNetworkId, ConnectStatus.Success);

        ClientIdToAuth[request.ClientNetworkId] = userData.userAuthId;
        ClientData[userData.userAuthId] = userData;
        OnPlayerJoined?.Invoke(userData);

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Rotation = Quaternion.identity;
        response.Pending = false;

        var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
        Task.Factory.StartNew(
            async () => await SetupPlayerPrefab(request.ClientNetworkId),
            System.Threading.CancellationToken.None,
            TaskCreationOptions.None, scheduler
        );
    }

    private void OnNetworkReady()
    {
        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnClientDisconnect(ulong clientId)
    {
        SendClientDisconnected(clientId, ConnectStatus.GenericDisconnect);
        if (ClientIdToAuth.TryGetValue(clientId, out string authId))
        {
            ClientIdToAuth?.Remove(clientId);
            OnPlayerLeft?.Invoke(ClientData[authId]);

            if (ClientData[authId].clientId == clientId)
            {
                ClientData.Remove(authId);
                OnClientLeft?.Invoke(authId);
            }
        }

        Matchplayer matchPlayerInstance = GetNetworkedMatchPlayer(clientId);
        OnServerPlayerRemoved?.Invoke(matchPlayerInstance);
    }

    private void SendClientConnected(ulong clientId, ConnectStatus status)
    {
        var writer = new FastBufferWriter(sizeof(ConnectStatus), Allocator.Temp);
        writer.WriteValueSafe(status);
        Debug.Log($"Send Network Client Connected to : {clientId}");
        MatchplayNetworkMessenger.SendMessageTo(NetworkMessage.LocalClientConnected, clientId, writer);
    }

    private void SendClientDisconnected(ulong clientId, ConnectStatus status)
    {
        var writer = new FastBufferWriter(sizeof(ConnectStatus), Allocator.Temp);
        writer.WriteValueSafe(status);
        Debug.Log($"Send networkClient Disconnected to : {clientId}");
        MatchplayNetworkMessenger.SendMessageTo(NetworkMessage.LocalClientDisconnected, clientId, writer);
    }

    private async void WaitToDisconnect(ulong clientId)
    {
        await Task.Delay(500);
        networkManager.DisconnectClient(clientId);
    }

    private async Task SetupPlayerPrefab(ulong clientId)
    {
        NetworkObject playerNetworkObject;

        do
        {
            playerNetworkObject = networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            await Task.Delay(100);
        }
        while (playerNetworkObject == null);

        OnServerPlayerAdded?.Invoke(GetNetworkedMatchPlayer(clientId));
    }

    public UserData GetUserDataByClientId(ulong clientId)
    {
        if (ClientIdToAuth.TryGetValue(clientId, out string authId))
        {
            if (ClientData.TryGetValue(authId, out UserData data))
            {
                return data;
            }

            return null;
        }

        return null;
    }

    private Matchplayer GetNetworkedMatchPlayer(ulong clientId)
    {
        NetworkObject playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        return playerObject.GetComponent<Matchplayer>();
    }

    public void Dispose()
    {
        if (networkManager == null) { return; }

        networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
        networkManager.OnServerStarted -= OnNetworkReady;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
    }
}