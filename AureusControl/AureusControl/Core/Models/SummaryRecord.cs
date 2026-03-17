using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace AureusControl.Core.Models
{
    public sealed class SummaryField
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;
    }

    public sealed class SummaryRecord
    {
        public string Title { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public Brush StatusBrush { get; set; } = new SolidColorBrush(Colors.Orange);

        public IReadOnlyList<SummaryField> Fields { get; set; } = new List<SummaryField>();
    }
}
