using System.Collections.Generic;

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

        public IReadOnlyList<SummaryField> Fields { get; set; } = new List<SummaryField>();
    }
}
