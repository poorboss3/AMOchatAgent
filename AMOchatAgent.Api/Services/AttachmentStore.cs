using System.Collections.Concurrent;
using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Services;

/// <summary>
/// Singleton in-memory store for uploaded attachments.
/// </summary>
public class AttachmentStore
{
    private readonly ConcurrentDictionary<string, StoredAttachment> _store = new();

    public void Save(StoredAttachment attachment) =>
        _store[attachment.Id] = attachment;

    public StoredAttachment? Get(string id) =>
        _store.TryGetValue(id, out var a) ? a : null;

    public IEnumerable<StoredAttachment> GetMany(IEnumerable<string> ids) =>
        ids.Select(Get).Where(a => a != null).Cast<StoredAttachment>();

    public void Delete(string id) => _store.TryRemove(id, out _);
}
