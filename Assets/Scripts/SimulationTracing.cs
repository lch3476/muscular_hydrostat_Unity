using UnityEngine;
using TMPro;

public class SimulationTracing : MonoBehaviour
{
    [SerializeField] ConstrainedDynamic constrainedDynamic;
    [SerializeField] TextMeshProUGUI DataTraceText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (constrainedDynamic == null || constrainedDynamic.Constraints == null)
        {
            DataTraceText.text = "No constraints to trace.";
            return;
        }

        string traceText = "";

        traceText += $"Elapsed Time: {Time.time:F2} seconds\n";
        foreach (var constraint in constrainedDynamic.Constraints)
        {
            traceText += constraint.GenerateDataText() + "\n";
        }
        DataTraceText.text = traceText;
    }
}
