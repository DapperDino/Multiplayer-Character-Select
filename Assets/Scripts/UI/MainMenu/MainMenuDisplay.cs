using System;
using TMPro;
using UnityEngine;

public class MainMenuDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TMP_Text findMatchButtonText;
    [SerializeField] private TMP_Text queueTimerText;
    [SerializeField] private TMP_Text queueStatusText;

    private float timeInQueue;
    private bool isMatchmaking;
    private bool isCancelling;
    private ClientGameManager gameManager;

    private void Start()
    {
        if (ClientSingleton.Instance == null) { return; }

        queueStatusText.text = string.Empty;
        queueTimerText.text = string.Empty;

        gameManager = ClientSingleton.Instance.Manager;
    }

    private void Update()
    {
        if (isMatchmaking && !isCancelling)
        {
            timeInQueue += Time.deltaTime;
            TimeSpan ts = TimeSpan.FromSeconds(timeInQueue);
            queueTimerText.text = string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
        }
        else
        {
            queueTimerText.text = string.Empty;
        }
    }

    public async void FindMatchPressed()
    {
        if (isCancelling) { return; }

        if (isMatchmaking)
        {
            queueStatusText.text = "Cancelling";
            isCancelling = true;

            await gameManager.CancelMatchmaking();

            isCancelling = false;
            isMatchmaking = false;
            findMatchButtonText.text = "Find Match";
            queueStatusText.text = string.Empty;
            return;
        }

        _ = gameManager.MatchmakeAsync(OnMatchMade);

        findMatchButtonText.text = "Cancel";
        queueStatusText.text = "Searching...";
        isMatchmaking = true;
        timeInQueue = 0f;
    }

    private void OnMatchMade(MatchmakerPollingResult result)
    {
        switch (result)
        {
            case MatchmakerPollingResult.Success:
                queueStatusText.text = "Connecting";
                break;
            case MatchmakerPollingResult.TicketCreationError:
                queueStatusText.text = "TicketCreationError";
                break;
            case MatchmakerPollingResult.TicketCancellationError:
                queueStatusText.text = "TicketCancellationError";
                break;
            case MatchmakerPollingResult.TicketRetrievalError:
                queueStatusText.text = "TicketRetrievalError";
                break;
            case MatchmakerPollingResult.MatchAssignmentError:
                queueStatusText.text = "MatchAssignmentError";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }

    public async void StartHost()
    {
        await HostSingleton.Instance.StartHostAsync();
    }

    public async void StartClient()
    {
        await ClientSingleton.Instance.Manager.BeginConnection(joinCodeInputField.text);
    }
}
