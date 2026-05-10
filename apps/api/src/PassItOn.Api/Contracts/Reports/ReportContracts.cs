using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Contracts.Reports;

public sealed record SubmitReportRequest(
    Guid ListingId,
    ReportReasonCode ReasonCode,
    string? Description);

public sealed record ReportSubmittedResponse(
    Guid ReportId,
    string Message);
