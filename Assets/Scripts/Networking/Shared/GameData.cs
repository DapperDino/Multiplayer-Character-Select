using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum Map
{
    Default
}

public enum GameMode
{
    Default
}

public enum GameQueue
{
    Casual,
    Competitive
}

public class MatchplayUser
{
    public UserData Data { get; }

    public Action<string> OnNameChanged;

    public MatchplayUser()
    {
        string tempId = Guid.NewGuid().ToString();

        Data = new UserData(
            "Player",
            tempId,
            0,
            new GameInfo());
    }

    public string Name
    {
        get => Data.userName;
        set
        {
            Data.userName = value;
            OnNameChanged?.Invoke(Data.userName);
        }
    }

    public string AuthId
    {
        get => Data.userAuthId;
        set => Data.userAuthId = value;
    }

    public Map MapPreferences
    {
        get => Data.userGamePreferences.map;
        set { Data.userGamePreferences.map = value; }
    }

    public GameMode GameModePreferences
    {
        get => Data.userGamePreferences.gameMode;
        set => Data.userGamePreferences.gameMode = value;
    }

    public GameQueue QueuePreference
    {
        get => Data.userGamePreferences.gameQueue;
        set => Data.userGamePreferences.gameQueue = value;
    }

    public override string ToString()
    {
        var userData = new StringBuilder("MatchplayUser: ");
        userData.AppendLine($"- {Data}");
        return userData.ToString();
    }
}

[Serializable]
public class UserData
{
    public string userName;
    public string userAuthId;
    public ulong clientId;
    public GameInfo userGamePreferences;
    
    public int characterId = -1;

    public UserData(string userName, string userAuthId, ulong clientId, GameInfo userGamePreferences)
    {
        this.userName = userName;
        this.userAuthId = userAuthId;
        this.clientId = clientId;
        this.userGamePreferences = userGamePreferences;
    }
}

[Serializable]
public class GameInfo
{
    public Map map;
    public GameMode gameMode;
    public GameQueue gameQueue;

    public int MaxUsers = 20;
    public string ToSceneName => ConvertToScene(map);

    private const string multiplayCasualQueue = "casual-queue";
    private const string multiplayCompetitiveQueue = "competitive-queue";
    private static readonly Dictionary<string, GameQueue> multiplayToLocalQueueNames = new Dictionary<string, GameQueue>
    {
        { multiplayCasualQueue, GameQueue.Casual },
        { multiplayCompetitiveQueue, GameQueue.Competitive }
    };

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("GameInfo: ");
        sb.AppendLine($"- map:        {map}");
        sb.AppendLine($"- gameMode:   {gameMode}");
        sb.AppendLine($"- gameQueue:  {gameQueue}");
        return sb.ToString();
    }

    public static string ConvertToScene(Map map)
    {
        switch (map)
        {
            case Map.Default:
                return "Gameplay";
            default:
                Debug.LogWarning($"{map} - is not supported.");
                return "";
        }
    }

    public string ToMultiplayQueue()
    {
        return gameQueue switch
        {
            GameQueue.Casual => multiplayCasualQueue,
            GameQueue.Competitive => multiplayCompetitiveQueue,
            _ => multiplayCasualQueue
        };
    }

    public static GameQueue ToGameQueue(string multiplayQueue)
    {
        if (!multiplayToLocalQueueNames.ContainsKey(multiplayQueue))
        {
            Debug.LogWarning($"No QueuePreference that maps to {multiplayQueue}");
            return GameQueue.Casual;
        }

        return multiplayToLocalQueueNames[multiplayQueue];
    }
}