using UC;
using UnityEngine;

public class Player : MonoBehaviour
{
    private int _age;

    public int age => _age;

    void Start()
    {
        _age = Globals.startAge;

        var gridObject = GetComponent<GridObject>();
        gridObject.onMoveEnd += GridObject_onMoveEnd;
    }

    private void GridObject_onMoveEnd(Vector2Int sourcePos, Vector2Int destPos, bool success)
    {
        if (success)
        {
            _age++;
        }
    }
}
