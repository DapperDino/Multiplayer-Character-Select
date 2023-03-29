using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;

public class ServerSingleton : MonoBehaviour
{
    private static ServerSingleton serverSingleton;

    private ServerGameManager gameManager;

    public static ServerSingleton Instance
    {
        get
        {
            if (serverSingleton != null) { return serverSingleton; }

            serverSingleton = FindObjectOfType<ServerSingleton>();

            if (serverSingleton == null)
            {
                Debug.LogError("No ServerSingleton in scene, did you run this from the bootStrap scene?");
                return null;
            }

            return serverSingleton;
        }
    }

    public ServerGameManager Manager
    {
        get
        {
            if (gameManager == null)
            {
                Debug.LogError($"Server Manager is missing, did you run OpenConnection?");
                return null;
            }

            return gameManager;
        }
    }

    public async Task CreateServer()
    {
        await UnityServices.InitializeAsync();

        gameManager = new ServerGameManager(
            ApplicationData.IP(),
            ApplicationData.Port(),
            ApplicationData.QPort(),
            NetworkManager.Singleton);
    }

    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        gameManager?.Dispose();
    }
}
