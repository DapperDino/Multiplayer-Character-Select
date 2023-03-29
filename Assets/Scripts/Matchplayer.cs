using System;
using Unity.Netcode;

public class Matchplayer : NetworkBehaviour
{
    public static event Action<Matchplayer> OnServerPlayerSpawned;
    public static event Action<Matchplayer> OnServerPlayerDespawned;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            OnServerPlayerSpawned?.Invoke(this);
        }

        if (IsClient)
        {
            ClientSingleton.Instance.Manager.AddMatchPlayer(this);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            OnServerPlayerDespawned?.Invoke(this);
        }

        if (IsClient)
        {
            ClientSingleton.Instance.Manager.RemoveMatchPlayer(this);
        }
    }
}
