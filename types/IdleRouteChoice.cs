namespace WorkerFixes.Types;

public sealed class IdleRouteChoice
{
    public string HandlerId { get; set; }

    public string SourceId { get; set; }

    public string DestinationId { get; set; }

    public string RouteKey => (SourceId ?? "") + "|" + (DestinationId ?? "");
}
