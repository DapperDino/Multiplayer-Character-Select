using Unity.Netcode;
using UnityEngine;

public class MainMenuDisplay : MonoBehaviour
{
    public void StartHost()
    {
        ServerManager.Instance.StartHost();
    }

    public void StartServer()
    {
        ServerManager.Instance.StartServer();
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
}
