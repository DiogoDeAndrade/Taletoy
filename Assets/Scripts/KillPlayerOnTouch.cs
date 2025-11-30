using UC;
using UnityEngine;

public class KillPlayerOnTouch : MonoBehaviour
{
    [SerializeField] private Hypertag playerTag;
    [SerializeField] private Hypertag deathReason;
    
    GridObject  gridObject;
    Player      player;


    void Start()
    {
        gridObject = GetComponent<GridObject>();

    }

    void Update()
    {
        if (!player)
        {
            player = playerTag.FindFirst<Player>();
            if (player == null) return;
        }
        
        if (player.isDead) return;

        if (player.gridPosition == gridObject.gridPosition)
        {
            player.Kill(deathReason);
        }
    }
}
