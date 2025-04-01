using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MookDialogueScript;
using UnityEngine;
using UnityEngine.UI;

public class NormalDialogueUI : MonoBehaviour
{
    public GameObject normalDialogue;
    public Text speakerText;
    public Text contentText;

    public Transform optionContainer;
    public Button optionPrefab;

    public GameObject btnRoot;
    public InputField inputField;
    public Button nDialogueBtn;
    public Button lDialogueBtn;

    private Button _clickHandler;

    // Start is called before the first frame update
    void Start()
    {
        // 初始化点击处理
        _clickHandler = normalDialogue.GetComponent<Button>();
        if (_clickHandler == null)
        {
            _clickHandler = normalDialogue.AddComponent<Button>();
        }
        _clickHandler.onClick.AddListener(() => DialogueMgr.Instance.RunMgrs.Continue());

        DialogueMgr.Instance.RunMgrs.OnDialogueStarted += HandleDialogueStarted;
        DialogueMgr.Instance.RunMgrs.OnDialogueDisplayed += HandleDialogueDisplayed;
        DialogueMgr.Instance.RunMgrs.OnChoicesDisplayed += HandleChoicesDisplayed;
        DialogueMgr.Instance.RunMgrs.OnOptionSelected += HandleOptionSelected;
        DialogueMgr.Instance.RunMgrs.OnDialogueCompleted += HandleDialogueCompleted;
        
        nDialogueBtn.onClick.AddListener(OnNormalClickDialogue);
        lDialogueBtn.onClick.AddListener(OnListClickDialogue);
    }

    private void HandleDialogueStarted()
    {
        Debug.Log("对话开始");
        normalDialogue.SetActive(true);
    }

    private void HandleDialogueDisplayed(ContentNode content)
    {
        for (int i = 0; i < optionContainer.childCount; i++)
        {
            Destroy(optionContainer.GetChild(i).gameObject);
        }
        HandleDialogueDisplayedAsync(content).Wait();
    }

    private async Task HandleDialogueDisplayedAsync(ContentNode content)
    {
        if (content is DialogueNode dialogue)
        {
            Debug.Log(dialogue.Speaker);
            string text = await DialogueMgr.Instance.RunMgrs.BuildDialogueText(dialogue);
            speakerText.text = !string.IsNullOrEmpty(dialogue.Speaker) ? dialogue.Speaker : "";
            contentText.text = text;
        }
        else if (content is NarrationNode narration)
        {
            string text = await DialogueMgr.Instance.RunMgrs.BuildNarrationText(narration);
            speakerText.text = "";
            contentText.text = text;
        }
    }

    private void HandleChoicesDisplayed(List<ChoiceNode> choices)
    {
        HandleChoicesDisplayedAsync(choices).Wait();
    }

    private async Task HandleChoicesDisplayedAsync(List<ChoiceNode> choices)
    {
        for (int i = 0; i < optionContainer.childCount; i++)
        {
            Destroy(optionContainer.GetChild(i).gameObject);
        }

        if (choices.Count <= 0) return;
        foreach (var option in choices)
        {
            string text = await DialogueMgr.Instance.RunMgrs.BuildChoiceText(option);
            var go = Instantiate(optionPrefab, optionContainer);
            go.GetComponentInChildren<Text>().text = text;
            go.onClick.AddListener(() =>
            {
                DialogueMgr.Instance.RunMgrs.SelectChoice(choices.IndexOf(option));
            });
        }
    }

    private void HandleOptionSelected(ChoiceNode choice, int index)
    {
        DialogueMgr.Instance.RunMgrs.BuildText(choice.Text, s => Debug.Log("选择：" + (index + 1) + ". " + s));
    }

    private void HandleDialogueCompleted()
    {
        Debug.Log("对话结束");
        normalDialogue.SetActive(false);
        btnRoot.SetActive(true);
    }

    public void OnNormalClickDialogue()
    {
        btnRoot.SetActive(false);
        DialogueMgr.Instance.RunMgrs.StartDialogue(inputField.text.Trim());
    }
    
    public void OnListClickDialogue()
    {
        btnRoot.SetActive(false);
        DialogueMgr.Instance.RunMgrs.StartDialogue(inputField.text.Trim());
    }

    private void OnDestroy()
    {
        DialogueMgr.Instance.RunMgrs.OnDialogueStarted -= HandleDialogueStarted;
        DialogueMgr.Instance.RunMgrs.OnDialogueDisplayed -=  HandleDialogueDisplayed;
        DialogueMgr.Instance.RunMgrs.OnChoicesDisplayed -= HandleChoicesDisplayed;
        DialogueMgr.Instance.RunMgrs.OnOptionSelected -= HandleOptionSelected;
        DialogueMgr.Instance.RunMgrs.OnDialogueCompleted -= HandleDialogueCompleted;
    }

}
