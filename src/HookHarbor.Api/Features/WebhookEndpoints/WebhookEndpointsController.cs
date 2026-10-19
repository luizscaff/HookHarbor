using HookHarbor.Api.Data;
using HookHarbor.Api.Features.WebhookEndpoints.Requests;
using HookHarbor.Api.Features.WebhookEndpoints.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HookHarbor.Api.Features.WebhookEndpoints;

[ApiController]
[Route("api/endpoints")]
public sealed class WebhookEndpointsController(HookHarborDbContext dbContext) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<WebhookEndpointResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]

    public async Task<ActionResult<WebhookEndpointResponse>> Create(CreateWebhookEndpointRequest request, CancellationToken cancellationToken)
    {
        if (!IsValidDestinationUrl(request.DestinationUrl))
        {
            ModelState.AddModelError(nameof(request.DestinationUrl), "Destination must be an absolute HTTP of HTTPS URL.");

            return ValidationProblem(ModelState);
        }

        var endpoint = new WebhookEndpoint(request.Name, request.DestinationUrl);

        dbContext.WebhookEndpoints.Add(endpoint);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = WebhookEndpointResponse.FromEntity(endpoint);

        return CreatedAtAction(nameof(GetById), new { id = endpoint.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WebhookEndpointResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]

    public async Task<ActionResult<WebhookEndpointResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var endpoint = await dbContext.WebhookEndpoints.AsNoTracking().SingleOrDefaultAsync(endpoint => endpoint.Id == id, cancellationToken);

        if (endpoint is null)
        {
            return NotFound();
        }

        return Ok(WebhookEndpointResponse.FromEntity(endpoint));
    }

    private static bool IsValidDestinationUrl(string? destinationUrl)
    {
        return Uri.TryCreate(destinationUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}