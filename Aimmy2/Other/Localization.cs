using Aimmy2.Config;
using Microsoft.ML.OnnxRuntime;

namespace Aimmy2.Other;

public static class Localization
{
    public static string GetDescription(this ActionTrigger trigger)
    {
        var description = new List<string>();

        // Helper function to join keys with correct localization for "and" or "or"
        string JoinKeys(IEnumerable<string> keys, string conjunction)
        {
            var keyList = keys.ToList();
            if (keyList.Count > 1)
            {
                var lastKey = keyList.Last();
                keyList.RemoveAt(keyList.Count - 1);
                return string.Join(", ", keyList) + $" {conjunction} " + lastKey;
            }
            return keyList.FirstOrDefault() ?? string.Empty;
        }

        // Handle Actions (these are the actual keys being sent)
        if (trigger.Actions.Any(t => t is { IsValid: true }))
        {
            var actionsDescription = JoinKeys(trigger.Actions.Where(t => t is { IsValid: true }).Select(action => action.Key.ToString()), Locale.And); // Locale.And is the localized version of "and"
            description.Add(string.Format(Locale.ActionsFormat, actionsDescription));
            // ActionsFormat might be something like: "Sends {0}"
        }

        // Handle Execution Mode (simultaneous or sequential)
        description.Add(trigger.ExecutionMode == TriggerExecutionMode.Sequential
            ? Locale.ExecutionModeSequential
            : Locale.ExecutionModeSimultaneous); // Localization key for "simultaneously" or "in sequential order"

        // Handle Trigger Keys (these are the conditions for the trigger)
        if (trigger.TriggerKeys.Any(t => t is { IsValid: true }))
        {
            var triggerKeysDescription = JoinKeys(trigger.TriggerKeys.Where(t => t is { IsValid: true }).Select(key => key.Key.ToString()), Locale.TriggerKeysOr); // Use Locale.TriggerKeysOr or Locale.TriggerKeysAnd based on operator
            var operatorKey = trigger.TriggerKeysOperator == KeyOperator.And
                ? Locale.TriggerKeysAnd // Localization key for "and"
                : Locale.TriggerKeysOr;  // Localization key for "or"
            description.Add(string.Format(Locale.TriggerKeysFormat, triggerKeysDescription, operatorKey));
            // TriggerKeysFormat might be something like: "when {0} {1} pressed"
        }

        // Handle Anti Trigger Keys (keys that must NOT be pressed)
        if (trigger.AntiTriggerKeys.Any(t => t is { IsValid: true }))
        {
            var antiKeysDescription = JoinKeys(trigger.AntiTriggerKeys.Where(t => t is { IsValid: true }).Select(key => key.Key.ToString()), Locale.AntiTriggerKeysOr);
            var operatorKey = trigger.AntiTriggerKeysOperator == KeyOperator.And
                ? Locale.AntiTriggerKeysAnd // Localization key for "and not"
                : Locale.AntiTriggerKeysOr;  // Localization key for "or not"
            description.Add(string.Format(Locale.AntiTriggerKeysFormat, operatorKey, antiKeysDescription));
            // AntiTriggerKeysFormat might be something like: "{0} {1} held"
        }

        // Handle Detection and Intersection (optional condition)
        if (trigger.NeedsDetection)
        {
            if (trigger.ExecutionIntersectionCheck == TriggerCheck.None)
            {
                description.Add(Locale.DetectionNeeded); // Localization key for "and an enemy detected"
            }
            else
            {
                var intersectionDescription = trigger.ExecutionIntersectionCheck switch
                {
                    TriggerCheck.IntersectingCenter => Locale.IntersectingCenter, // Localization key for "in the center"
                    TriggerCheck.HeadIntersectingCenter => Locale.HeadIntersectingCenter, // Localization key for "with head in the center"
                    _ => ""
                };

                if (!string.IsNullOrEmpty(intersectionDescription))
                {
                    description.Add(string.Format(Locale.IntersectionCheckFormat, intersectionDescription));
                    // IntersectionCheckFormat might be something like: "when an enemy is {0}"
                }
            }
        }

        // Combine everything into a single sentence, making sure the action comes first and conditions later
        return string.Join(" ", description);
    }

}