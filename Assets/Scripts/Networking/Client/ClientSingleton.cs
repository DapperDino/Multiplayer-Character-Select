using System.Threading.Tasks;
using UnityEngine;

public class ClientSingleton : MonoBehaviour
{
    private static ClientSingleton clientSingleton;

    private ClientGameManager gameManager;

    public static ClientSingleton Instance
    {
        get
        {
            if (clientSingleton != null) return clientSingleton;

            clientSingleton = FindObjectOfType<ClientSingleton>();

            if (clientSingleton == null)
            {
                Debug.LogError("No ClientSingleton in scene, did you run this from the bootStrap scene?");
                return null;
            }

            return clientSingleton;
        }
    }

    public ClientGameManager Manager
    {
        get
        {
            if (gameManager == null)
            {
                Debug.LogError($"ClientGameManager is missing, did you run StartClient()?", gameObject);
                return null;
            }

            return gameManager;
        }
    }

    public async Task CreateClient()
    {
        gameManager = new ClientGameManager();
        
        await gameManager.InitAsync();
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