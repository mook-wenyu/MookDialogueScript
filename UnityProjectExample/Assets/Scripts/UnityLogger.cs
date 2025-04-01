using UnityEngine;
using ILogger = MookDialogueScript.ILogger;

public class UnityLogger : ILogger
{
    public void Log(object message, string member = "", string file = "", int line = 0)
        => Debug.Log(message);
    public void LogWarning(object message, string member = "", string file = "", int line = 0)
        => Debug.LogWarning(message);
    public void LogError(object message, string member = "", string file = "", int line = 0)
        => Debug.LogError(message);
}
