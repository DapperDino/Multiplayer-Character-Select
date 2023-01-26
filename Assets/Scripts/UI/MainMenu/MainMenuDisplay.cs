using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuDisplay : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string gameplaySceneName = "Gameplay";

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        NetworkManager.Singleton.SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
    }
}
