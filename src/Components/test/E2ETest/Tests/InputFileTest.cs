// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using BasicTestApp;
using BasicTestApp.FormsTest;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.E2ETesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.Extensions;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Components.E2ETest.Tests;

public class InputFileTest : ServerTestBase<ToggleExecutionModeServerFixture<Program>>, IDisposable
{
    private string _tempDirectory;

    public InputFileTest(
        BrowserFixture browserFixture,
        ToggleExecutionModeServerFixture<Program> serverFixture,
        ITestOutputHelper output)
        : base(browserFixture, serverFixture, output)
    {
    }

    protected override void InitializeAsyncCore()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        Navigate(ServerPathBase);
        Browser.MountTestComponent<InputFileComponent>();
    }

    [Fact]
    public void CanUploadSingleSmallFile()
    {
        // Create a temporary text file
        var file = TempFile.Create(_tempDirectory, "txt", "This file was uploaded to the browser and read from .NET.");

        // Upload the file
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileNameElement = fileContainer.FindElement(By.Id("file-name"));
        var fileLastModifiedElement = fileContainer.FindElement(By.Id("file-last-modified"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
        var fileContentTypeElement = fileContainer.FindElement(By.Id("file-content-type"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        // Validate that the file was uploaded correctly and all fields are present
        Browser.False(() => string.IsNullOrWhiteSpace(fileNameElement.Text));
        Browser.NotEqual(default, () => DateTimeOffset.Parse(fileLastModifiedElement.Text, CultureInfo.InvariantCulture));
        Browser.Equal(file.Contents.Length.ToString(CultureInfo.InvariantCulture), () => fileSizeElement.Text);
        Browser.Equal("text/plain", () => fileContentTypeElement.Text);
        Browser.Equal(file.Text, () => fileContentElement.Text);
    }

    [Fact]
    public void CanUploadSingleLargeFile()
    {
        // Create a large text file
        var fileContentSizeInBytes = 1024 * 1024;
        var contentBuilder = new StringBuilder();

        for (int i = 0; i < fileContentSizeInBytes; i++)
        {
            contentBuilder.Append((i % 10).ToString(CultureInfo.InvariantCulture));
        }

        var file = TempFile.Create(_tempDirectory, "txt", contentBuilder.ToString());

        // Upload the file
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileNameElement = fileContainer.FindElement(By.Id("file-name"));
        var fileLastModifiedElement = fileContainer.FindElement(By.Id("file-last-modified"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
        var fileContentTypeElement = fileContainer.FindElement(By.Id("file-content-type"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        // Validate that the file was uploaded correctly and all fields are present
        Browser.False(() => string.IsNullOrWhiteSpace(fileNameElement.Text));
        Browser.NotEqual(default, () => DateTimeOffset.Parse(fileLastModifiedElement.Text, CultureInfo.InvariantCulture));
        Browser.Equal(file.Contents.Length.ToString(CultureInfo.InvariantCulture), () => fileSizeElement.Text);
        Browser.Equal("text/plain", () => fileContentTypeElement.Text);
        Browser.Equal(file.Text, () => fileContentElement.Text);
    }

    [Fact]
    public void CanUploadMultipleFiles()
    {
        // Create multiple small text files
        var files = Enumerable.Range(1, 3)
            .Select(i => TempFile.Create(_tempDirectory, "txt", $"Contents of file {i}."))
            .ToList();

        // Upload each file
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(string.Join("\n", files.Select(f => f.Path)));

        // Validate that each file was uploaded correctly
        Assert.All(files, file =>
        {
            var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
            var fileNameElement = fileContainer.FindElement(By.Id("file-name"));
            var fileLastModifiedElement = fileContainer.FindElement(By.Id("file-last-modified"));
            var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
            var fileContentTypeElement = fileContainer.FindElement(By.Id("file-content-type"));
            var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

            // Validate that the file was uploaded correctly and all fields are present
            Browser.False(() => string.IsNullOrWhiteSpace(fileNameElement.Text));
            Browser.NotEqual(default, () => DateTimeOffset.Parse(fileLastModifiedElement.Text, CultureInfo.InvariantCulture));
            Browser.Equal(file.Contents.Length.ToString(CultureInfo.InvariantCulture), () => fileSizeElement.Text);
            Browser.Equal("text/plain", () => fileContentTypeElement.Text);
            Browser.Equal(file.Text, () => fileContentElement.Text);
        });
    }

    [Fact]
    public void CanUploadAndConvertImageFile()
    {
        var sourceImageId = "image-source";
        var imageStatus = Browser.Exists(By.Id("image-status"));
        Browser.Equal("ready", () => imageStatus.Text);

        // Get the source image base64
        var base64 = Browser.ExecuteJavaScript<string>($@"
                const canvas = document.createElement('canvas');
                const context = canvas.getContext('2d');
                const image = document.getElementById('{sourceImageId}');

                canvas.width = image.naturalWidth;
                canvas.height = image.naturalHeight;
                context.drawImage(image, 0, 0, image.naturalWidth, image.naturalHeight);

                return canvas.toDataURL().split(',').pop();");

        // Save the image file locally
        var file = TempFile.Create(_tempDirectory, "png", Convert.FromBase64String(base64));

        // Re-upload the image file (it will be converted to a JPEG and scaled to fix 640x480)
        var inputFile = Browser.Exists(By.Id("input-image"));
        inputFile.SendKeys(file.Path);

        // Validate that the image was converted without error and is the correct size
        var uploadedImage = Browser.Exists(By.Id("image-uploaded"));

        Browser.Equal(480, () => uploadedImage.Size.Width);
        Browser.Equal(480, () => uploadedImage.Size.Height);
    }

    [Fact]
    public void ThrowsWhenTooManyFilesAreSelected()
    {
        var maxAllowedFilesElement = Browser.Exists(By.Id("max-allowed-files"));
        maxAllowedFilesElement.Clear();
        maxAllowedFilesElement.SendKeys("1\n");

        // Save two files locally
        var file1 = TempFile.Create(_tempDirectory, "txt", "This is file 1.");
        var file2 = TempFile.Create(_tempDirectory, "txt", "This is file 2.");

        // Select both files
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys($"{file1.Path}\n{file2.Path}");

        // Validate that the proper exception is thrown
        var exceptionMessage = Browser.Exists(By.Id("exception-message"));
        Browser.Equal("The maximum number of files accepted is 1, but 2 were supplied.", () => exceptionMessage.Text);
    }

    [Fact]
    public void ThrowsWhenOversizedFileIsSelected()
    {
        var maxFileSizeElement = Browser.Exists(By.Id("max-file-size"));
        maxFileSizeElement.Clear();
        maxFileSizeElement.SendKeys("10\n");

        // Save a file that exceeds the specified file size limit
        var file = TempFile.Create(_tempDirectory, "txt", "This file is over 10 bytes long.");

        // Select the file
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(file.Path);

        // Validate that the proper exception is thrown
        var exceptionMessage = Browser.Exists(By.Id("exception-message"));
        Browser.Equal("Supplied file with size 32 bytes exceeds the maximum of 10 bytes.", () => exceptionMessage.Text);
    }

    [Fact]
    public void CanClearFilesByInvokingCancelEvent()
    {
        // Upload a file first
        var file = TempFile.Create(_tempDirectory, "txt", "This is a test file.");
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(file.Path);

        // Verify the file was uploaded
        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));

        // Get the file count element
        var fileCount = Browser.Exists(By.Id("file-count"));
        Browser.Equal("1", () => fileCount.Text);

        // Trigger the cancel event via JavaScript to simulate canceling the file dialog
        Browser.ExecuteJavaScript(@"
            const inputElement = document.getElementById('input-file');
            inputElement.dispatchEvent(new Event('cancel'));
        ");

        // Wait a moment for the event to be processed and verify the file list was cleared
        Browser.Equal("0", () => Browser.Exists(By.Id("file-count")).Text);
    }

    [Fact]
    public void CanSeekToBeginningOfFile()
    {
        var content = "BEGIN_MIDDLE_END";
        var file = TempFile.Create(_tempDirectory, "txt", content);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        Browser.Equal(content, () => fileContentElement.Text);
        Browser.True(() => fileContentElement.Text.StartsWith("BEGIN", StringComparison.Ordinal));
    }

    [Fact]
    public void CanSeekToMiddleOfFile()
    {
        var contentBuilder = new StringBuilder();
        var size = 1000;

        for (int i = 0; i < size; i++)
        {
            contentBuilder.Append((i % 10).ToString(CultureInfo.InvariantCulture));
        }

        var file = TempFile.Create(_tempDirectory, "txt", contentBuilder.ToString());
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));

        Browser.Equal(size.ToString(CultureInfo.InvariantCulture), () => fileSizeElement.Text);
        Browser.Equal(contentBuilder.ToString(), () => fileContentElement.Text);
    }

    [Fact]
    public void CanSeekFromEndOfFileUsingNegativeOffset()
    {
        var startContent = new string('A', 100);
        var middleContent = new string('B', 100);
        var endContent = new string('C', 100);
        var content = startContent + middleContent + endContent;
        var file = TempFile.Create(_tempDirectory, "txt", content);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        var fileContent = fileContentElement.Text;

        Browser.True(() => fileContent.EndsWith(endContent, StringComparison.Ordinal));
        Browser.True(() => fileContent.Length == 300);
    }

    [Fact]
    public void CanSeekFromCurrentPositionBackward()
    {
        var sections = new[] { "FIRST", "SECOND", "THIRD" };
        var content = string.Join("|", sections);
        var file = TempFile.Create(_tempDirectory, "txt", content);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        var fileContent = fileContentElement.Text;

        Browser.True(() => fileContent.Contains("FIRST"));
        Browser.True(() => fileContent.Contains("SECOND"));
        Browser.True(() => fileContent.Contains("THIRD"));
    }

    [Fact]
    public void CanSeekFromCurrentPositionForward()
    {
        var uniqueMarkers = "MARKER1_MARKER2_MARKER3";
        var file = TempFile.Create(_tempDirectory, "txt", uniqueMarkers);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        Browser.Equal(uniqueMarkers, () => fileContentElement.Text);
    }

    [Fact]
    public void CanHandleEmptyFile()
    {
        var file = TempFile.Create(_tempDirectory, "txt", "");
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        Browser.Equal("0", () => fileSizeElement.Text);
        Browser.Equal("", () => fileContentElement.Text);
    }

    [Fact]
    public void CanHandleSingleByteFile()
    {
        var file = TempFile.Create(_tempDirectory, "txt", "X");
        var inputFile = Browser.Exists(By.Id("input-file"));
        inputFile.SendKeys(file.Path);
        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        Browser.Equal("1", () => fileSizeElement.Text);
        Browser.Equal("X", () => fileContentElement.Text);
    }

    [Fact]
    public void CanSeekInLargeFile_RandomAccessWorks()
    {
        var fileContentSizeInBytes = 5 * 1024 * 1024;
        var contentBuilder = new StringBuilder();
        var pattern = "LARGEFILETEST_";
        var repetitions = fileContentSizeInBytes / pattern.Length;

        for (int i = 0; i < repetitions; i++)
        {
            contentBuilder.Append(pattern);
        }

        var reminder = fileContentSizeInBytes % pattern.Length;

        if (reminder > 0)
        {
            contentBuilder.Append(pattern.Substring(0, (int)reminder));
        }

        var file = TempFile.Create(_tempDirectory, "txt", contentBuilder.ToString());
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));

        Browser.Equal(fileContentSizeInBytes.ToString(CultureInfo.InvariantCulture),
            () => fileSizeElement.Text);
    }

    [Fact]
    public void CanSeekInBinaryFile()
    {
        var binaryContent = new byte[2048];

        for (int i = 0; i < binaryContent.Length; i++)
        {
            binaryContent[i] = (byte)(i % 256);
        }

        var file = TempFile.Create(_tempDirectory, "bin", binaryContent);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileSizeElement = fileContainer.FindElement(By.Id("file-size"));
        Browser.Equal("2048", () => fileSizeElement.Text);
    }

    [Fact]
    public void CanSeekMultipleTimesSequentially_PositionAdvancesCorrectly()
    {
        var part1 = new string('A', 300);
        var part2 = new string('B', 300);
        var part3 = new string('C', 300);
        var content = part1 + part2 + part3;
        var file = TempFile.Create(_tempDirectory, "txt", content);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        var fileContent = fileContentElement.Text;

        Browser.Equal(900, () => fileContent.Length);
        Browser.True(() => fileContent.StartsWith(part1, StringComparison.Ordinal));
        Browser.Contains(part2, () => fileContent);
        Browser.True(() => fileContent.EndsWith(part3, StringComparison.Ordinal));
    }

    [Fact]
    public void CanSeekWithSpecialCharacters()
    {
        var content = "START_🎉_MIDDLE_🚀_END";
        var file = TempFile.Create(_tempDirectory, "txt", content);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));

        Browser.True(() => fileContentElement.Text.Contains("START"));
        Browser.True(() => fileContentElement.Text.Contains("END"));
    }

    [Fact]
    public void CanSeekAcrossFileBoundaries()
    {
        var segments = Enumerable.Range(0, 10)
            .Select(i => $"SEGMENT_{i}_")
            .Aggregate((a, b) => a + b);
        var file = TempFile.Create(_tempDirectory, "txt", segments);
        var inputFile = Browser.Exists(By.Id("input-file"));

        inputFile.SendKeys(file.Path);

        var fileContainer = Browser.Exists(By.Id($"file-{file.Name}"));
        var fileContentElement = fileContainer.FindElement(By.Id("file-content"));
        var content = fileContentElement.Text;

        for (int i = 0; i < 10; i++)
        {
            Browser.True(() => content.Contains($"SEGMENT_{i}_"));
        }
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    private struct TempFile
    {
        public string Name { get; }
        public string Path { get; }
        public byte[] Contents { get; }

        public string Text => Encoding.ASCII.GetString(Contents);

        private TempFile(string tempDirectory, string extension, byte[] contents)
        {
            Name = $"{Guid.NewGuid():N}.{extension}";
            Path = System.IO.Path.Combine(tempDirectory, Name);
            Contents = contents;
        }

        public static TempFile Create(string tempDirectory, string extension, byte[] contents)
        {
            var file = new TempFile(tempDirectory, extension, contents);

            File.WriteAllBytes(file.Path, contents);

            return file;
        }

        public static TempFile Create(string tempDirectory, string extension, string text)
            => Create(tempDirectory, extension, Encoding.ASCII.GetBytes(text));
    }
}
