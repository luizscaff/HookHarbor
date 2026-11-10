using System.ComponentModel.DataAnnotations;

namespace HookHarbor.Api.Features.WebhookEvents.Requests;

public sealed class ListWebhookEventsRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    public WebhookEventStatus? Status { get; init; }
}
