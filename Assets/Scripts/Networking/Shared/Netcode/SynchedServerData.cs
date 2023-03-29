using System;
using Unity.Collections;
using Unity.Netcode;

public class SynchedServerData : NetworkBehaviour
{
    public NetworkVariable<FixedString32Bytes> serverId = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<Map> map = new NetworkVariable<Map>();
    public NetworkVariable<GameMode> gameMode = new NetworkVariable<GameMode>();
    public NetworkVariable<GameQueue> gameQueue = new NetworkVariable<GameQueue>();

    public Action OnNetworkSpawned;

    public override void OnNetworkSpawn()
    {
        OnNetworkSpawned?.Invoke();
    }
}