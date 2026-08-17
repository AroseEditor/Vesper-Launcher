using Vesper.Core.Diagnostics;
using Xunit;

namespace Vesper.Core.Tests;

public class ErrorServiceTests
{
    [Fact]
    public void ReportAddsNewestFirst()
    {
        var service = new ErrorService();
        service.Report("first");
        service.Report("second");

        var snapshot = service.Snapshot();
        Assert.Equal("second", snapshot[0].Title);
        Assert.Equal("first", snapshot[1].Title);
    }

    [Fact]
    public void ClipboardTextIncludesTitleAndDetail()
    {
        var error = new ErrorService().Report("Launch failed", "boom");

        Assert.Contains("Launch failed", error.ClipboardText);
        Assert.Contains("boom", error.ClipboardText);
    }

    [Fact]
    public void DescribeUnwrapsInnerExceptions()
    {
        var inner = new InvalidOperationException("inner cause");
        var outer = new ApplicationException("outer", inner);

        var text = ErrorService.Describe(outer);

        Assert.Contains("outer", text);
        Assert.Contains("inner cause", text);
    }

    [Fact]
    public void ReportRaisesEvent()
    {
        var service = new ErrorService();
        AppError? captured = null;
        service.Reported += (_, e) => captured = e;

        service.Report("hi");

        Assert.NotNull(captured);
        Assert.Equal("hi", captured!.Title);
    }

    [Fact]
    public void ClearEmptiesTheList()
    {
        var service = new ErrorService();
        service.Report("one");
        service.Clear();

        Assert.Empty(service.Snapshot());
    }

    [Fact]
    public void OldEntriesAreTrimmed()
    {
        var service = new ErrorService();

        for (var i = 0; i < ErrorService.MaxEntries + 20; i++)
            service.Report("e" + i);

        Assert.Equal(ErrorService.MaxEntries, service.Count);
    }

    [Fact]
    public void CopyAllJoinsEntries()
    {
        var service = new ErrorService();
        service.Report("a", "detail-a");
        service.Report("b", "detail-b");

        var text = service.CopyAll();

        Assert.Contains("a", text);
        Assert.Contains("b", text);
        Assert.Contains("detail-a", text);
    }
}
