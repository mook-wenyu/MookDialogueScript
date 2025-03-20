using MookDialogueScript;
using UnityEngine;

public class DialogueMgr : MonoBehaviour
{
    public static DialogueMgr Instance { get; private set; }

    // 创建对话管理器
    public Runner RunMgrs { get; private set; }
    
    void Awake()
    {
        Instance = this;
        Initialize();
    }
    
    public void Initialize()
    {
        Debug.Log("开始初始化对话系统");
        RunMgrs = new Runner(new UnityDialogueLoader());
    }
    
}
