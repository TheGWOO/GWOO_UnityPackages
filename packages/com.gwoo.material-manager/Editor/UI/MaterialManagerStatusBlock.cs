using UnityEngine.UIElements;

namespace GWOO.Editor.Tools
{
    public class MaterialManagerStatusBlock : VisualElement
    {
        private readonly VisualElement _summaryContainer;
        private readonly Label _summaryLabel;
        private readonly Label _materialsFoundLabel;
        private readonly Label _variantsFilteredLabel;
        private readonly Label _reparentedLabel;
        private readonly Label _revertedLabel;
        private readonly Label _logLabel;

        private int _lastLogSequence = -1;
        private IVisualElementScheduledItem _hideLogSchedule;

        public MaterialManagerStatusBlock()
        {
            AddToClassList("mm-section");
            AddToClassList("mm-status-panel");

            _summaryContainer = new VisualElement();
            _summaryContainer.AddToClassList("mm-action-summary");
            Add(_summaryContainer);

            _summaryLabel = new Label();
            _summaryLabel.AddToClassList("mm-action-summary-label");
            _summaryContainer.Add(_summaryLabel);

            VisualElement row = new();
            row.AddToClassList("mm-row");
            row.AddToClassList("mm-status-row");
            Add(row);

            _materialsFoundLabel = new Label();
            _variantsFilteredLabel = new Label();
            _reparentedLabel = new Label();
            _revertedLabel = new Label();

            _materialsFoundLabel.AddToClassList("mm-status-metric");
            _variantsFilteredLabel.AddToClassList("mm-status-metric");
            _reparentedLabel.AddToClassList("mm-status-metric");
            _revertedLabel.AddToClassList("mm-status-metric");

            row.Add(_materialsFoundLabel);
            row.Add(_variantsFilteredLabel);
            row.Add(_reparentedLabel);
            row.Add(_revertedLabel);

            _logLabel = new Label
            {
                name = "mm-log",
                text = string.Empty,
            };
            _logLabel.AddToClassList("mm-log");
            _logLabel.AddToClassList("mm-status-log");
            _logLabel.style.opacity = 0f;
            Add(_logLabel);
        }

        public void SetStats(int materialsCount, int variantChildrenCount, int reparentedCount, int revertedCount)
        {
            _materialsFoundLabel.text = $"Materials found: {materialsCount}";
            _variantsFilteredLabel.text = $"Variant children: {variantChildrenCount}";
            _reparentedLabel.text = $"Materials reparented: {reparentedCount}";
            _revertedLabel.text = $"Overrides reverted: {revertedCount}";

            _variantsFilteredLabel.style.display = variantChildrenCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _reparentedLabel.style.display = reparentedCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _revertedLabel.style.display = revertedCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetActionSummary(string message, MaterialLogType logType)
        {
            _summaryLabel.text = string.IsNullOrEmpty(message)
                ? "Ready. No action executed yet."
                : message;
            _summaryLabel.tooltip = _summaryLabel.text;

            _summaryContainer.RemoveFromClassList("mm-summary-info");
            _summaryContainer.RemoveFromClassList("mm-summary-success");
            _summaryContainer.RemoveFromClassList("mm-summary-error");

            switch (logType)
            {
                case MaterialLogType.Success:
                    _summaryContainer.AddToClassList("mm-summary-success");
                    break;
                case MaterialLogType.Error:
                    _summaryContainer.AddToClassList("mm-summary-error");
                    break;
                default:
                    _summaryContainer.AddToClassList("mm-summary-info");
                    break;
            }
        }

        public void SetLog(string message, MaterialLogType logType, float durationSeconds, int logSequence)
        {
            if (_lastLogSequence == logSequence)
            {
                return;
            }

            _lastLogSequence = logSequence;

            _logLabel.text = message;
            _logLabel.RemoveFromClassList("mm-log-info");
            _logLabel.RemoveFromClassList("mm-log-success");
            _logLabel.RemoveFromClassList("mm-log-error");
            _logLabel.style.opacity = string.IsNullOrEmpty(message) ? 0f : 0.78f;
            _logLabel.tooltip = _logLabel.text;

            _hideLogSchedule?.Pause();
            _hideLogSchedule = null;

            switch (logType)
            {
                case MaterialLogType.Success:
                    _logLabel.AddToClassList("mm-log-success");
                    break;
                case MaterialLogType.Error:
                    _logLabel.AddToClassList("mm-log-error");
                    break;
                default:
                    _logLabel.AddToClassList("mm-log-info");
                    break;
            }

            if (durationSeconds > 0f)
            {
                _hideLogSchedule = _logLabel.schedule.Execute(() =>
                {
                    _logLabel.text = string.Empty;
                    _logLabel.tooltip = string.Empty;
                    _logLabel.style.opacity = 0f;
                });
                _hideLogSchedule.ExecuteLater((long)(durationSeconds * 1000f));
            }
        }
    }
}
