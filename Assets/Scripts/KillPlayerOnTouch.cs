using UC;
using UnityEngine;

public class KillPlayerOnTouch : MonoBehaviour
{
    [SerializeField] private Hypertag _playerTag;
    [SerializeField] private Hypertag _deathReason;
    
    GridObject  gridObject;
    Player      player;

    public Hypertag playerTag { get { return _playerTag; } set { _playerTag = value; } }
    public Hypertag deathReason { get { return _deathReason; } set { _deathReason = value; } }

    void Start()
    {
        gridObject = GetComponent<GridObject>();

    }

    void Update()
    {
        if (!player)
        {
            player = _playerTag.FindFirst<Player>();
            if (player == null) return;
        }
        
        if (player.isDead) return;

        if (player.gridPosition == gridObject.gridPosition)
        {
            player.Kill(_deathReason);
        }
    }
}
