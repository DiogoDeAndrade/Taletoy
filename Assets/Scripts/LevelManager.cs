using System.Collections.Generic;
using UC;
using UnityEngine;
using UnityEngine.Tilemaps;

public class LevelManager : MonoBehaviour
{
    [SerializeField] private Tilemap            grid;
    [SerializeField] private ConceptCollection  conceptCollection;
    [SerializeField] private Hypertag           playerTag;
    [SerializeField] private Concept            conceptPrefab;
    [SerializeField] private float              baseElementCount = 3.0f;
    [SerializeField] private float              elementsPerYear = 0.075f;

    Player                  player;
    List<Concept>           activeConcepts = new();
    ProbList<ConceptDef>    concepts;

    void FetchPlayer()
    {
        if (player == null)
        {
            player = playerTag.FindFirst<Player>();
        }

        concepts = new(false, true);
        foreach (var concept in conceptCollection)
        {
            concepts.Add(concept, 1.0f);
        }
    }

    void ClearElements()
    {
        if (activeConcepts != null)
        {
            foreach (var concept in activeConcepts)
            {
                Destroy(concept.gameObject);
            }
        }
        activeConcepts.Clear();
    }

    public void SpawnElements()
    {
        FetchPlayer();
        if (player == null) return;

        if (player.isDead) return;

        ClearElements();

        int nElements = GetElementsByAge(player.age);
        for (int i = 0; i < nElements; i++)
        {
            SpawnElement();
        }
    }

    private int GetElementsByAge(int age)
    {
        return Mathf.FloorToInt(baseElementCount + age * elementsPerYear);
    }

    private void SpawnElement()
    {
        GridSystem  mainGrid = grid.GetComponentInParent<GridSystem>();
        var         cellBounds = grid.cellBounds;

        Vector3Int  cellPos;
        Vector3     position;
        int         nTries = 0;

        // Try until we hit a cell that actually has a tile
        do
        {
            nTries++;

            cellPos = new Vector3Int(Random.Range(cellBounds.xMin, cellBounds.xMax), Random.Range(cellBounds.yMin, cellBounds.yMax), 0);
            position = grid.GetCellCenterWorld(cellPos);

            var actualCellPos = mainGrid.WorldToGrid(position);

            // Check if the player is on this cell
            if (actualCellPos == player.gridPosition) continue;

            // Check if there's already an item there
            bool found = false;
            foreach (var concept in activeConcepts)
            {
                if (mainGrid.WorldToGrid(concept.transform.position) == actualCellPos)
                {
                    found = true;
                    break;
                }
            }

            if (found) continue;

            if (!grid.HasTile(cellPos)) continue;

            break;

        } while (nTries < 50);
;
        if (nTries == 50) return;

        // Use the cell center, not the corner
        var conceptInstance = Instantiate(conceptPrefab, position, Quaternion.identity);
        conceptInstance.Set(concepts.Get());

        activeConcepts.Add(conceptInstance);
    }
}
