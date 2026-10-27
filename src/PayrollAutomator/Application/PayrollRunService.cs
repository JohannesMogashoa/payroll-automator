using System.Text.Json;
using System.Text.Json.Serialization;
using PayrollAutomator.Domain;

namespace PayrollAutomator.Application;

public sealed class PayrollRunService(
    IPayrollRunRepository repository,
    IPayrollFileStore fileStore,
    IWorkbookImporter workbookImporter,
    IPayrollWorkbookExporter workbookExporter)
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PayrollRunSummary> CreateRunAsync(
        Stream workbook,
        string originalFileName,
        PayrollRatePolicy? ratePolicy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);

        if (!string.Equals(Path.GetExtension(originalFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .xlsx workbooks are supported.", nameof(originalFileName));
        }

        Guid runId = Guid.NewGuid();
        string sourceRelativePath = await fileStore.SaveSourceAsync(
            runId,
            originalFileName,
            workbook,
            cancellationToken);

        try
        {
            string sourcePath = fileStore.GetAbsolutePath(sourceRelativePath);
            WorkbookImportResult importResult = workbookImporter.Import(sourcePath);
            PayrollCalculationResult calculation = new PayrollCalculationService(
                ratePolicy ?? PayrollRatePolicy.Default())
                .Calculate(importResult);
            PayrollReviewSummary review = new PayrollReviewService().Summarize(calculation);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var snapshot = new PayrollRunSnapshot(calculation, review);

            var run = new PayrollRunRecord
            {
                Id = runId,
                OriginalFileName = Path.GetFileName(originalFileName),
                SourceRelativePath = sourceRelativePath,
                Status = PayrollRunStatus.NeedsReview,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ErrorCount = review.ErrorCount,
                WarningCount = review.WarningCount,
                IncludedPayrollHours = review.IncludedPayrollHours,
                IncludedPayableAmount = review.IncludedPayableAmount,
                RuleSetVersion = calculation.Metadata.RuleSetVersion,
                SnapshotJson = JsonSerializer.Serialize(snapshot, SnapshotOptions)
            };

            await repository.AddAsync(
                run,
                new PayrollAuditRecord
                {
                    RunId = runId,
                    TimestampUtc = now,
                    EventType = "RunCalculated",
                    Details = $"Imported {review.CalculatedRowCount} row(s) with {review.ErrorCount} error(s) and {review.WarningCount} warning(s)."
                },
                cancellationToken);

            return ToSummary(run);
        }
        catch
        {
            await fileStore.DeleteRunArtifactsAsync(runId, cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PayrollRunSummary>> ListRunsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PayrollRunRecord> records = await repository.ListAsync(cancellationToken);
        return records.Select(ToSummary).ToArray();
    }

    public async Task<PayrollRunDetails?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        PayrollRunRecord? run = await repository.GetAsync(runId, cancellationToken);
        if (run is null || string.IsNullOrWhiteSpace(run.SnapshotJson))
        {
            return null;
        }

        string snapshotJson = run.SnapshotJson
            ?? throw new InvalidOperationException($"Run '{runId}' has no calculation snapshot.");
        PayrollRunSnapshot snapshot = JsonSerializer.Deserialize<PayrollRunSnapshot>(
            snapshotJson,
            SnapshotOptions)
            ?? throw new InvalidOperationException($"Run '{runId}' has an invalid snapshot.");

        IReadOnlyList<PayrollAuditRecord> auditRecords = await repository.ListAuditAsync(
            runId,
            cancellationToken);

        return new PayrollRunDetails(
            ToSummary(run),
            snapshot.Calculation,
            snapshot.Review,
            auditRecords
                .Select(record => new AuditLogEntry(record.TimestampUtc, record.EventType, record.Details))
                .ToArray(),
            run.Reviewer,
            run.ConfirmedAtUtc,
            run.ExportedAtUtc);
    }

    public async Task<PayrollExportResult> ConfirmAndExportAsync(
        Guid runId,
        string reviewer,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        PayrollRunRecord run = await repository.GetRequiredAsync(runId, cancellationToken);
        if (run.Status == PayrollRunStatus.Exported)
        {
            throw new InvalidOperationException("This payroll run has already been exported.");
        }

        if (run.Status == PayrollRunStatus.Deleted)
        {
            throw new InvalidOperationException("Deleted payroll runs cannot be exported.");
        }

        string snapshotJson = run.SnapshotJson
            ?? throw new InvalidOperationException($"Run '{runId}' has no calculation snapshot.");
        PayrollRunSnapshot snapshot = JsonSerializer.Deserialize<PayrollRunSnapshot>(
            snapshotJson,
            SnapshotOptions)
            ?? throw new InvalidOperationException($"Run '{runId}' has an invalid snapshot.");
        ReviewConfirmation confirmation = new PayrollReviewService().Confirm(
            snapshot.Review,
            reviewer,
            comment);
        string sourcePath = fileStore.GetAbsolutePath(run.SourceRelativePath);
        string outputRelativePath = fileStore.GetExportRelativePath(runId);
        string outputPath = fileStore.GetAbsolutePath(outputRelativePath);
        PayrollExportResult exportResult = new PayrollExportService(workbookExporter).Export(
            sourcePath,
            outputPath,
            snapshot.Calculation,
            snapshot.Review,
            confirmation);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        run.Status = PayrollRunStatus.Exported;
        run.UpdatedAtUtc = now;
        run.ConfirmedAtUtc = confirmation.ConfirmedAtUtc;
        run.ExportedAtUtc = now;
        run.Reviewer = confirmation.Reviewer;
        run.ExportRelativePath = outputRelativePath;
        await repository.UpdateAsync(
            run,
            new PayrollAuditRecord
            {
                RunId = runId,
                TimestampUtc = now,
                EventType = "WorkbookExported",
                Details = $"Reviewer '{confirmation.Reviewer}' confirmed the review and created the export."
            },
            cancellationToken);

        return exportResult;
    }

    public async Task<PayrollDownload?> OpenExportAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        PayrollRunRecord? run = await repository.GetAsync(runId, cancellationToken);
        if (run is null || string.IsNullOrWhiteSpace(run.ExportRelativePath))
        {
            return null;
        }

        return await fileStore.OpenExportAsync(
            runId,
            Path.GetFileName(run.ExportRelativePath),
            cancellationToken);
    }

    public async Task DeleteRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default,
        string auditEventType = "RunDeleted",
        string auditDetails = "Run artifacts and calculation snapshot were deleted by the operator.")
    {
        PayrollRunRecord run = await repository.GetRequiredAsync(runId, cancellationToken);
        if (run.Status == PayrollRunStatus.Deleted)
        {
            return;
        }

        await fileStore.DeleteRunArtifactsAsync(runId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        run.Status = PayrollRunStatus.Deleted;
        run.UpdatedAtUtc = now;
        run.SnapshotJson = null;
        run.SourceRelativePath = string.Empty;
        run.ExportRelativePath = null;
        await repository.UpdateAsync(
            run,
            new PayrollAuditRecord
            {
                RunId = runId,
                TimestampUtc = now,
                EventType = auditEventType,
                Details = auditDetails
            },
            cancellationToken);
    }

    private static PayrollRunSummary ToSummary(PayrollRunRecord record) =>
        new(
            record.Id,
            record.OriginalFileName,
            record.Status,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.ErrorCount,
            record.WarningCount,
            record.IncludedPayrollHours,
            record.IncludedPayableAmount,
            !string.IsNullOrWhiteSpace(record.ExportRelativePath));
}

public sealed record PayrollRunSnapshot(
    PayrollCalculationResult Calculation,
    PayrollReviewSummary Review);
