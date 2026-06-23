using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Benchmark;

/// <summary>
/// Computes accuracy, macro-F1, per-label precision/recall/F1, and confusion matrix
/// for benchmark model comparison. Metrics computed on-demand (not stored).
/// </summary>
public sealed class MetricsCalculator
{
    private static readonly string[] Labels =
    {
        "sale", "appointment_booked", "offered", "offer_lost",
        "no_response", "abandoned", "return_or_complaint"
    };

    /// <summary>
    /// Compute metrics for a model's predictions against ground truth.
    /// Skips entries where either prediction or ground truth is null.
    /// </summary>
    public ModelMetrics Compute(string modelName, IReadOnlyList<string?> predictions, IReadOnlyList<string?> groundTruth)
    {
        if (predictions.Count != groundTruth.Count)
            throw new ArgumentException("Predictions and ground truth must have the same count");

        var total = predictions.Count;
        var classified = 0;
        var correct = 0;
        var perLabel = new Dictionary<string, (int tp, int fp, int fn)>();

        foreach (var label in Labels)
            perLabel[label] = (0, 0, 0);

        for (var i = 0; i < total; i++)
        {
            var pred = predictions[i];
            var truth = groundTruth[i];

            // Skip if either is null
            if (pred == null || truth == null) continue;
            classified++;

            if (pred == truth)
            {
                correct++;
                if (perLabel.ContainsKey(truth))
                    perLabel[truth] = (perLabel[truth].tp + 1, perLabel[truth].fp, perLabel[truth].fn);
            }
            else
            {
                // False positive for predicted label
                if (perLabel.ContainsKey(pred))
                    perLabel[pred] = (perLabel[pred].tp, perLabel[pred].fp + 1, perLabel[pred].fn);
                // False negative for true label
                if (perLabel.ContainsKey(truth))
                    perLabel[truth] = (perLabel[truth].tp, perLabel[truth].fp, perLabel[truth].fn + 1);
            }
        }

        var accuracy = classified > 0 ? (double)correct / classified : 0;

        // Per-label metrics
        var labelMetrics = new Dictionary<string, LabelMetrics>();
        var f1Sum = 0.0;
        var labelsWithSupport = 0;

        foreach (var label in Labels)
        {
            var (tp, fp, fn) = perLabel[label];
            var support = tp + fn;
            var precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
            var recall = support > 0 ? (double)tp / support : 0;
            var f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : 0;

            labelMetrics[label] = new LabelMetrics
            {
                Precision = Math.Round(precision, 4),
                Recall = Math.Round(recall, 4),
                F1 = Math.Round(f1, 4),
                Support = support
            };

            if (support > 0)
            {
                f1Sum += f1;
                labelsWithSupport++;
            }
        }

        var macroF1 = labelsWithSupport > 0 ? f1Sum / labelsWithSupport : 0;

        return new ModelMetrics
        {
            ModelName = modelName,
            Accuracy = Math.Round(accuracy, 4),
            MacroF1 = Math.Round(macroF1, 4),
            Total = total,
            Classified = classified,
            PerLabel = labelMetrics
        };
    }

    /// <summary>
    /// Compute label distribution for a model (no ground truth needed).
    /// </summary>
    public static Dictionary<string, int> ComputeDistribution(IReadOnlyList<string?> labels)
    {
        var dist = new Dictionary<string, int>();
        foreach (var label in labels)
        {
            if (label == null) continue;
            dist.TryGetValue(label, out var count);
            dist[label] = count + 1;
        }
        return dist;
    }
}
