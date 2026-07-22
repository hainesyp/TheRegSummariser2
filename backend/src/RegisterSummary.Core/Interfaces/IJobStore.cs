using RegisterSummary.Core.Models;

namespace RegisterSummary.Core.Interfaces;

public interface IJobStore
{
    Job CreateJob(IReadOnlyList<string> articleUrls);

    Job? GetJob(Guid id);

    void UpdateJob(Job job);
}
