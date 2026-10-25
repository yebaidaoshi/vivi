using System.Linq;
using UnityEngine;

public class SortingDump : MonoBehaviour
{
    public Transform model;

    [ContextMenu("Dump")]
    void Dump()
    {
        string Key(Renderer r) =>
            $"{SortingLayer.GetLayerValueFromID(r.sortingLayerID)}:{r.sortingLayerName}";

        var mine = model.GetComponentsInChildren<Renderer>();
        Debug.Log($"MODEL layers: {string.Join(", ", mine.Select(Key).Distinct())} " +
                  $"order {mine.Min(r => r.sortingOrder)}..{mine.Max(r => r.sortingOrder)}");

        foreach (var r in FindObjectsOfType<Renderer>().Where(r => !r.transform.IsChildOf(model)))
            Debug.Log($"{Key(r)} order {r.sortingOrder}  {r.name}", r);
    }
}