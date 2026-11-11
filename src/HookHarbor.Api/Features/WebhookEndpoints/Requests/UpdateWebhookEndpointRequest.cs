using System.ComponentModel.DataAnnotations;

namespace HookHarbor.Api.Features.WebhookEndpoints.Requests;

public sealed record UpdateWebhookEndpointRequest(
    [Required]
    [StringLength(150)]
    string Name,

    [Required]
    [StringLength(2048)]
    string DestinationUrl
);
