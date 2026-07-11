// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.JSInterop;
using Moq;
using System.Text;

namespace Microsoft.AspNetCore.Components.Forms;

public class BrowserFileTest
{
    [Fact]
    public void SetSize_ThrowsIfSizeIsNegative()
    {
        // Arrange
        var file = new BrowserFile();

        // Act & Assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => file.Size = -7);
    }

    [Fact]
    public void OpenReadStream_ThrowsIfFileSizeIsLargerThanAllowedSize()
    {
        // Arrange
        var file = new BrowserFile { Size = 100 };

        // Act & Assert
        var ex = Assert.Throws<IOException>(() => file.OpenReadStream(80));
        Assert.Equal("Supplied file with size 100 bytes exceeds the maximum of 80 bytes.", ex.Message);
    }

    [Fact]
    public void OpenReadStream_ReturnsStreamWhoseDisposalReleasesTheJSObject()
    {
        // Arrange: JS runtime that always returns a specific mock IJSStreamReference
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Strict);
        var jsStreamReference = new Mock<IJSStreamReference>();
        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult(jsStreamReference.Object));

        // Arrange: InputFile
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        var file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStream();

        // Assert 1: IJSStreamReference isn't disposed yet
        jsStreamReference.Verify(x => x.DisposeAsync(), Times.Never);

        // Act
        _ = stream.DisposeAsync();

        // Assert: IJSStreamReference is disposed now
        jsStreamReference.Verify(x => x.DisposeAsync());
    }

    [Fact]
    public async Task OpenReadStream_ReturnsStreamWhoseDisposalReleasesTheJSObject_ToleratesDisposalException()
    {
        // Arrange: JS runtime that always returns a specific mock IJSStreamReference whose disposal throws
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Strict);
        var jsStreamReference = new Mock<IJSStreamReference>();
        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<object[]>()))
            .Returns(ValueTask.FromResult(jsStreamReference.Object));
        jsStreamReference.Setup(x => x.DisposeAsync()).Throws(new InvalidTimeZoneException());

        // Arrange: InputFile
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        var file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStream();

        // Act/Assert. Not throwing is success here.
        await stream.DisposeAsync();
    }

    [Fact]
    public void OpenReadStreamSeekable_ThrowsIfFileSizeIsLargerThanAllowedSize()
    {
        IBrowserFile file = new BrowserFile { Size = 100 };

        var ex = Assert.Throws<IOException>(() => file.OpenReadStreamSeekable(80));
        Assert.Equal("Supplied file with size 100 bytes exceeds the maximum of 80 bytes.", ex.Message);
    }

    [Fact]
    public void OpenReadStreamSeekable_CreatesSeekableStream()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };

        var stream = file.OpenReadStreamSeekable();

        Assert.NotNull(stream);
        Assert.True(stream.CanSeek);
        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.Equal(100, stream.Length);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public void Seek_FromBegin_UpdatesPosition()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        var newPos = stream.Seek(50, SeekOrigin.Begin);
        Assert.Equal(50, newPos);
        Assert.Equal(50, stream.Position);
    }

    [Fact]
    public void Seek_FromCurrent_UpdatesPosition()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        var newPos = stream.Seek(20, SeekOrigin.Current);
        Assert.Equal(20, newPos);
        Assert.Equal(20, stream.Position);
    }

    [Fact]
    public void Seek_FromEnd_UpdatesPosition()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        var newPos = stream.Seek(-10, SeekOrigin.End);
        Assert.Equal(90, newPos);
        Assert.Equal(90, stream.Position);
    }

    [Fact]
    public void Seek_ThrowsIfPositionIsNegative()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        var ex = Assert.Throws<IOException>(() => stream.Seek(-10, SeekOrigin.Begin));
        Assert.Equal("Cannot seek to a position before the beginning of the stream.", ex.Message);
    }

    [Fact]
    public void Seek_ThrowsIfPositionExceedsLength()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        Assert.Throws<IOException>(() => stream.Seek(101, SeekOrigin.Begin));
    }

    [Fact]
    public void Position_Set_UpdatesPosition()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        stream.Position = 75;

        Assert.Equal(75, stream.Position);
    }

    [Fact]
    public void Position_Set_ThrowsIfNegative()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [Fact]
    public async Task ReadAsync_ReturnsZero_WhenPositionAtEndOfStream()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();
        stream.Position = 10;

        var bytesRead = await stream.ReadAsync(new byte[10]);

        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async Task ReadAsync_ReadsDataFromCurrentPosition()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(5, bytesRead);
        Assert.Equal(5, stream.Position);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buffer);

        jsRuntime.Verify(x => x.InvokeAsync<IJSStreamReference>(
            InputFileInterop.ReadFileDataAtPosition,
            It.IsAny<CancellationToken>(),
            It.Is<object[]>(args =>
                args.Length >= 4 &&
                (long)args[2] == 0 &&
                (int)args[3] == 5)),
            Times.Once);
    }

    [Fact]
    public async Task ReadAsync_ReadsDataAfterSeeking()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();

        stream.Seek(5, SeekOrigin.Begin);
        byte[] buffer = new byte[3];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(3, bytesRead);
        Assert.Equal(8, stream.Position);

        jsRuntime.Verify(x => x.InvokeAsync<IJSStreamReference>(
            InputFileInterop.ReadFileDataAtPosition,
            It.IsAny<CancellationToken>(),
            It.Is<object[]>(args =>
                args.Length >= 4 &&
                (long)args[2] == 5)),
            Times.Once);
    }

    [Fact]
    public async Task ReadAsync_ReadsPartialDataWhenBufferLargerThanAvailable()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(5, bytesRead);
        Assert.Equal(5, stream.Position);

        jsRuntime.Verify(x => x.InvokeAsync<IJSStreamReference>(
            InputFileInterop.ReadFileDataAtPosition,
            It.IsAny<CancellationToken>(),
            It.Is<object[]>(args =>
                args.Length >= 4 &&
                (int)args[3] == 5)),
            Times.Once);
    }

    [Fact]
    public async Task ReadAsync_SequentialReadsAdvancePosition()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((long maxSize, CancellationToken ct) =>
            {
                var newStream = new MemoryStream(testData);
                return new ValueTask<Stream>(newStream);
            });

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer1 = new byte[3];
        int read1 = await stream.ReadAsync(buffer1);

        byte[] buffer2 = new byte[3];
        int read2 = await stream.ReadAsync(buffer2);

        byte[] buffer3 = new byte[3];
        int read3 = await stream.ReadAsync(buffer3);

        Assert.Equal(3, read1);
        Assert.Equal(3, read2);
        Assert.Equal(3, read3);
        Assert.Equal(9, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_ThrowsOperationCanceledExceptionWhenCancellationRequested()
    {
        var cts = new CancellationTokenSource();
        var jsRuntime = new Mock<IJSRuntime>();

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ThrowsAsync(new OperationCanceledException());

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };

        cts.Cancel();
        var stream = file.OpenReadStreamSeekable(cancellationToken: cts.Token);

        byte[] buffer = new byte[10];

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            stream.ReadAsync(buffer).AsTask());
    }

    [Fact]
    public async Task ReadAsync_ThrowsOperationCanceledExceptionWhenCallerCancels()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var cts = new CancellationTokenSource();
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer = new byte[10];

        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            stream.ReadAsync(buffer, cts.Token).AsTask());
    }

    [Fact]
    public async Task ReadAsync_ThrowsObjectDisposedExceptionAfterStreamDisposed()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        stream.Dispose();

        byte[] buffer = new byte[10];

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            stream.ReadAsync(buffer).AsTask());
    }

    [Fact]
    public void Seek_ThrowsObjectDisposedExceptionAfterStreamDisposed()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => stream.Seek(10, SeekOrigin.Begin));
    }

    [Fact]
    public void Position_ThrowsObjectDisposedExceptionAfterStreamDisposed()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var stream = file.OpenReadStreamSeekable();

        stream.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = stream.Position);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_ReturnsSeekableStream()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };

        var stream = file.OpenReadStreamSeekable();

        Assert.NotNull(stream);
        Assert.True(stream.CanSeek);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_WithCancellationToken()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var cts = new CancellationTokenSource();

        var stream = file.OpenReadStreamSeekable(cancellationToken: cts.Token);

        Assert.NotNull(stream);
        Assert.True(stream.CanSeek);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_WithMaxAllowedSize_And_CancellationToken()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };
        var cts = new CancellationTokenSource();

        var stream = file.OpenReadStreamSeekable(maxAllowedSize: 200, cancellationToken: cts.Token);

        Assert.NotNull(stream);
        Assert.Equal(100, stream.Length);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_WithMaxAllowedSize()
    {
        var jsRuntime = new Mock<IJSRuntime>(MockBehavior.Loose);
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 100 };

        var stream = file.OpenReadStreamSeekable(maxAllowedSize: 200);

        Assert.NotNull(stream);
        Assert.Equal(100, stream.Length);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_ThrowsForNonBrowserFileImplementation()
    {
        var customFile = new Mock<IBrowserFile>();
        customFile.Setup(x => x.Name).Returns("test.txt");
        customFile.Setup(x => x.Size).Returns(100);

        var ex = Assert.Throws<NotSupportedException>(() =>
            customFile.Object.OpenReadStreamSeekable());

        Assert.Contains("does not support seekable streams", ex.Message);
        Assert.Contains("BrowserFile", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_AtBeginningOfFile()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(5, bytesRead);
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_AtEndOfFile_ReturnsZero()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStreamSeekable();

        stream.Position = 5;

        byte[] buffer = new byte[10];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public async Task ReadAsync_FromMiddleOfFile()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();

        stream.Seek(5, SeekOrigin.Begin);
        byte[] buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer);

        Assert.Equal(5, bytesRead);
        Assert.Equal(10, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_WithBackwardSeekThenRead()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns((long maxSize, CancellationToken ct) =>
            {
                var newStream = new MemoryStream(testData);
                return new ValueTask<Stream>(newStream);
            });

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 10 };
        var stream = file.OpenReadStreamSeekable();

        stream.Seek(8, SeekOrigin.Begin);
        byte[] buffer1 = new byte[2];
        await stream.ReadAsync(buffer1);

        stream.Seek(3, SeekOrigin.Begin);
        byte[] buffer2 = new byte[2];
        int bytesRead = await stream.ReadAsync(buffer2);

        Assert.Equal(2, bytesRead);
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_ThrowsIfFileSizeExceedsMaxAllowed()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 1000 };

        var ex = Assert.Throws<IOException>(() =>
            file.OpenReadStreamSeekable(maxAllowedSize: 500));

        Assert.Equal("Supplied file with size 1000 bytes exceeds the maximum of 500 bytes.", ex.Message);
    }

    [Fact]
    public async Task ReadAsync_MemoryOverload_ReadsCorrectly()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        var testData = new byte[] { 10, 20, 30, 40, 50 };
        var mockBlobStream = new MemoryStream(testData);

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBlobStream);

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 5 };
        var stream = file.OpenReadStreamSeekable();

        byte[] buffer = new byte[5];
        int bytesRead = await stream.ReadAsync(buffer.AsMemory());

        Assert.Equal(5, bytesRead);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 50 }, buffer);
    }

    [Fact]
    public void OpenReadStreamSeekable_ExtensionMethod_ThrowsArgumentNullException_ForNullFile()
    {
        IBrowserFile file = null;

        Assert.Throws<ArgumentNullException>(() => file!.OpenReadStreamSeekable());
    }

    [Fact]
    public async Task ReadAsync_DisposesJsStreamReferenceAfterRead()
    {
        var jsRuntime = new Mock<IJSRuntime>();
        var jsStreamRef = new Mock<IJSStreamReference>();

        jsStreamRef.Setup(x => x.OpenReadStreamAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));

        jsRuntime.Setup(x => x.InvokeAsync<IJSStreamReference>(
                InputFileInterop.ReadFileDataAtPosition,
                It.IsAny<CancellationToken>(),
                It.IsAny<object[]>()))
            .ReturnsAsync(jsStreamRef.Object);

        var inputFile = new InputFile { JSRuntime = jsRuntime.Object };
        IBrowserFile file = new BrowserFile { Owner = inputFile, Size = 3 };
        var stream = file.OpenReadStreamSeekable();

        await stream.ReadAsync(new byte[3]);

        jsStreamRef.Verify(x => x.DisposeAsync(), Times.Once);
    }
}
