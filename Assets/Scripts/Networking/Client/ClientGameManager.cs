using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientGameManager : IDisposable
{
    public event Action<Matchplayer> MatchPlayerSpawned;
    public event Action<Matchplayer> MatchPlayerDespawned;
    public MatchplayUser User { get; private set; }
    public MatchplayNetworkClient NetworkClient { get; private set; }

    public MatchplayMatchmaker Matchmaker { get; private set; }
    public bool Initialized { get; private set; } = false;

    public ClientGameManager()
    {
        User = new MatchplayUser();
    }

    public async Task InitAsync()
    {
        await UnityServices.InitializeAsync();

        NetworkClient = new MatchplayNetworkClient();
        Matchmaker = new MatchplayMatchmaker();
        AuthState authenticationResult = await AuthenticationWrapper.DoAuth();

        if (authenticationResult == AuthState.Authenticated)
        {
            User.AuthId = AuthenticationWrapper.PlayerID();
        }
        else
        {
            User.AuthId = Guid.NewGuid().ToString();
        }

        Debug.Log($"did Auth?{authenticationResult} {User.AuthId}");
        Initialized = true;
    }

    public void BeginConnection(string ip, int port)
    {
        Debug.Log($"Starting networkClient @ {ip}:{port}\nWith : {User}");
        NetworkClient.StartClient(ip, port);
    }

    public async Task<JoinAllocation> BeginConnection(string joinCode)
    {
        Debug.Log($"Starting networkClient with join code {joinCode}\nWith : {User}");
        return await NetworkClient.StartClient(joinCode);
    }

    public void Disconnect()
    {
        NetworkClient.DisconnectClient();
    }

    public async Task MatchmakeAsync(Action<MatchmakerPollingResult> onMatchmakeResponse)
    {
        if (Matchmaker.IsMatchmaking)
        {
            Debug.LogWarning("Already matchmaking, please wait or cancel.");
            return;
        }

        MatchmakerPollingResult matchResult = await GetMatchAsync();
        onMatchmakeResponse?.Invoke(matchResult);
    }

    private async Task<MatchmakerPollingResult> GetMatchAsync()
    {
        Debug.Log($"Beginning Matchmaking with {User}");
        MatchmakingResult matchmakingResult = await Matchmaker.Matchmake(User.Data);

        if (matchmakingResult.result == MatchmakerPollingResult.Success)
        {
            BeginConnection(matchmakingResult.ip, matchmakingResult.port);
        }
        else
        {
            Debug.LogWarning($"{matchmakingResult.result} : {matchmakingResult.resultMessage}");
        }

        return matchmakingResult.result;
    }

    public async Task CancelMatchmaking()
    {
        await Matchmaker.CancelMatchmaking();
    }

    public void ToMainMenu()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
    }

    public void AddMatchPlayer(Matchplayer player)
    {
        MatchPlayerSpawned?.Invoke(player);
    }

    public void RemoveMatchPlayer(Matchplayer player)
    {
        MatchPlayerDespawned?.Invoke(player);
    }

    public void SetGameQueue(GameQueue queue)
    {
        User.QueuePreference = queue;
    }

    public void ExitGame()
    {
        Dispose();
        Application.Quit();
    }

    public void Dispose()
    {
        NetworkClient?.Dispose();
        Matchmaker?.Dispose();
    }
}