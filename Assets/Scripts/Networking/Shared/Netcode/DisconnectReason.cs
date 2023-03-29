using UnityEngine;

public class DisconnectReason
{
    public ConnectStatus Reason { get; private set; } = ConnectStatus.Undefined;

    public void SetDisconnectReason(ConnectStatus reason)
    {
        Debug.Assert(reason != ConnectStatus.Success);
        Reason = reason;
    }

    public void Clear()
    {
        Reason = ConnectStatus.Undefined;
    }

    public bool HasTransitionReason => Reason != ConnectStatus.Undefined;
}