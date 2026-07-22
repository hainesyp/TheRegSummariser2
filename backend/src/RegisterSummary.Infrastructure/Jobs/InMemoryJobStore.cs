using System.Collections.Concurrent;
using RegisterSummary.Core.Interfaces;
using RegisterSummary.Core.Models;

namespace RegisterSummary.Infrastructure.Jobs;

public sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    public Job CreateJob(IReadOnlyList<string> articleUrls)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Articles = articleUrls.Select(url => new ArticleOutcome { Url = url }).ToList()
        };

        _jobs[job.Id] = job;
        return job;
    }

    public Job? GetJob(Guid id) => _jobs.GetValueOrDefault(id);

    public void UpdateJob(Job job) => _jobs[job.Id] = job;
}
