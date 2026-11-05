using Catalog.Api.Domains.ImportJobs;
using Catalog.Api.Domains.ImportJobs.ValueObjects;

namespace Catalog.Api.Tests;

// 004 T005 (test-first, İlke VI): ImportJob yaşam döngüsü — iki-faz akış (dry-run → onay → yazım):
// Queued → Validating → AwaitingConfirmation → Confirmed → Running → Completed/Failed.
// Durum geçişleri, sayaç guard'ları, onayda sayaç sıfırlama, terminal sonrası mutasyon reddi.
public class ImportJobTests
{
    private static ImportJob NewJob()
        => ImportJob.Create("katalog.xlsx", "/tmp/import/katalog.xlsx").Data!;

    private static ImportJob AwaitingJob()
    {
        var job = NewJob();
        job.StartValidation();
        job.CompleteValidation();
        return job;
    }

    private static ImportJob RunningJob()
    {
        var job = AwaitingJob();
        job.Confirm();
        job.Start();
        return job;
    }

    [Fact]
    public void Create_ValidInput_StartsQueuedWithZeroCounters()
    {
        var result = ImportJob.Create("katalog.xlsx", "/tmp/import/katalog.xlsx");

        result.IsSuccess.ShouldBeTrue();
        var job = result.Data!;
        job.Status.ShouldBe(ImportJobStatus.Queued);
        job.FileName.ShouldBe("katalog.xlsx");
        job.Processed.ShouldBe(0);
        job.Added.ShouldBe(0);
        job.Skipped.ShouldBe(0);
        job.Errored.ShouldBe(0);
        job.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Create_EmptyFileName_ReturnsError()
    {
        var result = ImportJob.Create("  ", "/tmp/import/katalog.xlsx");
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Create_EmptyTempFilePath_ReturnsError()
    {
        var result = ImportJob.Create("katalog.xlsx", "");
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void StartValidation_WhenQueued_SetsValidating()
    {
        var job = NewJob();

        var result = job.StartValidation();

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Validating);
    }

    [Fact]
    public void StartValidation_WhenValidating_ReturnsError()
    {
        var job = NewJob();
        job.StartValidation();

        var result = job.StartValidation();

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Validating);
    }

    [Fact]
    public void CompleteValidation_WhenValidating_SetsAwaitingConfirmation()
    {
        var job = NewJob();
        job.StartValidation();

        var result = job.CompleteValidation();

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.AwaitingConfirmation);
    }

    [Fact]
    public void CompleteValidation_WhenQueued_ReturnsError()
    {
        var job = NewJob();

        var result = job.CompleteValidation();

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Queued);
    }

    [Fact]
    public void Confirm_WhenAwaitingConfirmation_SetsConfirmed()
    {
        var job = AwaitingJob();

        var result = job.Confirm();

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WhenQueued_ReturnsError()
    {
        var job = NewJob();

        var result = job.Confirm();

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Queued);
    }

    [Fact]
    public void Start_WhenConfirmed_SetsRunningAndStartedAt()
    {
        var job = AwaitingJob();
        job.Confirm();

        var result = job.Start();

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Running);
        job.StartedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Start_WhenQueued_ReturnsError()
    {
        var job = NewJob();

        var result = job.Start();

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Queued);
    }

    [Fact]
    public void Start_AfterDryRun_ResetsCountersAndErrors()
    {
        var job = NewJob();
        job.StartValidation();
        job.RecordProgress(processed: 100, added: 90, skipped: 5,
            errors: new[] { new ImportError("Can Yayınları", 7, "ISBN boş") });
        job.CompleteValidation();
        job.Confirm();

        job.Start().IsSuccess.ShouldBeTrue();

        job.Processed.ShouldBe(0);
        job.Added.ShouldBe(0);
        job.Skipped.ShouldBe(0);
        job.Errored.ShouldBe(0);
        job.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void RecordProgress_WhenValidating_AccumulatesCounters()
    {
        var job = NewJob();
        job.StartValidation();

        var result = job.RecordProgress(processed: 10, added: 8, skipped: 2, errors: Array.Empty<ImportError>());

        result.IsSuccess.ShouldBeTrue();
        job.Processed.ShouldBe(10);
    }

    [Fact]
    public void RecordProgress_WhenRunning_AccumulatesCounters()
    {
        var job = RunningJob();

        job.RecordProgress(processed: 100, added: 80, skipped: 15,
            errors: new[] { new ImportError("Can Yayınları", 42, "ISBN boş") }).IsSuccess.ShouldBeTrue();
        job.RecordProgress(processed: 50, added: 50, skipped: 0,
            errors: Array.Empty<ImportError>()).IsSuccess.ShouldBeTrue();

        job.Processed.ShouldBe(150);
        job.Added.ShouldBe(130);
        job.Skipped.ShouldBe(15);
        job.Errored.ShouldBe(1);
        job.Errors.Single().Reason.ShouldBe("ISBN boş");
    }

    [Fact]
    public void RecordProgress_NegativeDelta_ReturnsErrorWithoutChange()
    {
        var job = RunningJob();

        var result = job.RecordProgress(processed: -1, added: 0, skipped: 0, errors: Array.Empty<ImportError>());

        result.IsSuccess.ShouldBeFalse();
        job.Processed.ShouldBe(0);
    }

    [Fact]
    public void RecordProgress_WhenQueued_ReturnsError()
    {
        var job = NewJob();

        var result = job.RecordProgress(processed: 1, added: 1, skipped: 0, errors: Array.Empty<ImportError>());

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void RecordProgress_WhenAwaitingConfirmation_ReturnsError()
    {
        var job = AwaitingJob();

        var result = job.RecordProgress(processed: 1, added: 1, skipped: 0, errors: Array.Empty<ImportError>());

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Complete_WhenRunning_SetsCompletedAndFinishedAt()
    {
        var job = RunningJob();

        var result = job.Complete();

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Completed);
        job.FinishedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Complete_WhenValidating_ReturnsError()
    {
        var job = NewJob();
        job.StartValidation();

        var result = job.Complete();

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Validating);
    }

    [Fact]
    public void Fail_WhenRunning_SetsFailedWithReason()
    {
        var job = RunningJob();

        var result = job.Fail("Dosya okunamadı");

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Failed);
        job.FailureReason.ShouldBe("Dosya okunamadı");
        job.FinishedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Fail_WhenAwaitingConfirmation_SetsFailed()
    {
        var job = AwaitingJob();

        var result = job.Fail("Yeni yüklemeyle geçersiz kılındı");

        result.IsSuccess.ShouldBeTrue();
        job.Status.ShouldBe(ImportJobStatus.Failed);
    }

    [Fact]
    public void Fail_EmptyReason_ReturnsError()
    {
        var job = RunningJob();

        var result = job.Fail(" ");

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Running);
    }

    [Fact]
    public void RecordProgress_AfterComplete_ReturnsError()
    {
        var job = RunningJob();
        job.Complete();

        var result = job.RecordProgress(processed: 1, added: 1, skipped: 0, errors: Array.Empty<ImportError>());

        result.IsSuccess.ShouldBeFalse();
        job.Processed.ShouldBe(0);
    }

    [Fact]
    public void Fail_AfterComplete_ReturnsError()
    {
        var job = RunningJob();
        job.Complete();

        var result = job.Fail("geç kalmış hata");

        result.IsSuccess.ShouldBeFalse();
        job.Status.ShouldBe(ImportJobStatus.Completed);
    }
}
