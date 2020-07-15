using System.Collections.Generic;

namespace Quartz.Plugin.HttpApi.Contract
{
    public class JobHistoryViewModel
    {
        public JobHistoryViewModel(IReadOnlyList<JobHistoryEntryDto> entries, string errorMessage)
        {
            HistoryEntries = entries;
            ErrorMessage = errorMessage;
        }

        public IReadOnlyList<JobHistoryEntryDto> HistoryEntries { get; }
        public string ErrorMessage { get; }
    }
}