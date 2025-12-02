using System;
using TMPro;
using UC.Interaction;
using UnityEngine;
using UnityEngine.UI;

public class ActionPanelElement : MonoBehaviour
{
    [SerializeField] private Image              buttonImage;
    [SerializeField] private TextMeshProUGUI    text;
    [SerializeField] private Color              normalColor = Color.white;
    [SerializeField] private Color              highlightColor = Color.yellow;

    Player.Action   _action;
    int             keyIndex;
    ActionsPanel    panel;

    public Player.Action action => _action;

    public void Set(Player.Action action, int keyIndex)
    {
        _action = action;

        text.text = string.Format(keyIndex + ". " + action.action.name.ToDisplayName());
        this.keyIndex = keyIndex;        
    }

    private void Start()
    {
        panel = GetComponentInParent<ActionsPanel>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0 + keyIndex))
        {
            TriggerAction();
        }
    }

    public void TriggerAction()
    {
        panel.TriggerAction(_action);
    }

    public void Highlight(bool b)
    {
        if (buttonImage) buttonImage.color = (b) ? (highlightColor) : (normalColor);
        if (text) text.color = (b) ? (highlightColor) : (normalColor);
    }
}
