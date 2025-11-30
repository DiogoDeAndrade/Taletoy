using System.Collections;
using System.Collections.Generic;
using UC;
using UnityEngine;

public class ActionsPanel : MonoBehaviour
{
    [SerializeField] private RectTransform      container;
    [SerializeField] private ActionPanelElement actionPrefab;

    Player                      player;
    List<ActionPanelElement>    actionPanelElements = new();
    CanvasGroup                 canvasGroup;

    private void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0.0f;
    }

    public void RefreshActions()
    {
        if (player == null)
        {
            player = Globals.playerTag.FindFirst<Player>();
            if (player == null)
            {
                canvasGroup.FadeOut(0.25f);
                return;
            }
        }

        foreach (var action in actionPanelElements)
        {
            Destroy(action.gameObject);
        }
        actionPanelElements.Clear();

        var actions = player.GetActions();
        if ((actions != null) && (actions.Count > 0)) 
        {
            int index = 1;
            foreach (var action in actions)
            {
                var actionElement = Instantiate(actionPrefab, container);
                actionElement.Set(action, index++);

                actionPanelElements.Add(actionElement);
            }

            canvasGroup.FadeIn(0.25f);
        }
        else
        {
            canvasGroup.FadeOut(0.25f);
        }
    }

    public void TriggerAction(Player.Action action)
    {
        var source = player.GetComponent<GridObject>();
        var targetPos = source.GetPositionFacing();
        var target = source.gridSystem.GetFirstGridObjectAt(targetPos);
        action.action.Run(target, targetPos);
    }
}
