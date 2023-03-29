using System.Threading.Tasks;
using UnityEngine;

public class ApplicationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ServerSingleton serverPrefab;
    [SerializeField] private ClientSingleton clientPrefab;
    [SerializeField] private HostSingleton hostSingleton;

    private ApplicationData appData;
    public static bool IsServer;

    private async void Start()
    {
        Application.targetFrameRate = 60;
        DontDestroyOnLoad(gameObject);

        await LaunchInMode(SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null);
    }

    private async Task LaunchInMode(bool isServer)
    {
        appData = new ApplicationData();
        IsServer = isServer;

        if (isServer)
        {
            ServerSingleton serverSingleton = Instantiate(serverPrefab);
            await serverSingleton.CreateServer();

            var defaultGameInfo = new GameInfo
            {
                gameMode = GameMode.Default,
                map = Map.Default,
                gameQueue = GameQueue.Casual
            };

            await serverSingleton.Manager.StartGameServerAsync(defaultGameInfo);
        }
        else
        {
            ClientSingleton clientSingleton = Instantiate(clientPrefab);
            Instantiate(hostSingleton);

            await clientSingleton.CreateClient();

            clientSingleton.Manager.ToMainMenu();
        }
    }
}
