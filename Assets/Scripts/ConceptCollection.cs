using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConceptCollection : ScriptableObject, IEnumerable<ConceptDef>
{
    public ConceptDef[] icons;

    public ConceptDef GetRandom()
    {
        return icons[Random.Range(0, icons.Length)];
    }

    public IEnumerator<ConceptDef> GetEnumerator()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            yield return icons[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
