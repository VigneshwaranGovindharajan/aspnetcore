// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Forms;

/// <summary>
/// Contains helper methods for <see cref="IBrowserFile"/>.
/// </summary>
public static class BrowserFileExtensions
{
    /// <summary>
    /// Attempts to convert the current image file to a new one of the specified file type and maximum file dimensions.
    /// <para>
    /// Caution: there is no guarantee that the file will be converted, or will even be a valid image file at all, either
    /// before or after conversion. The conversion is requested within the browser before it is transferred to .NET
    /// code, so the resulting data should be treated as untrusted.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The image will be scaled to fit the specified dimensions while preserving the original aspect ratio.
    /// </remarks>
    /// <param name="browserFile">The <see cref="IBrowserFile"/> to convert to a new image file.</param>
    /// <param name="format">The new image format.</param>
    /// <param name="maxWidth">The maximum image width.</param>
    /// <param name="maxHeight">The maximum image height</param>
    /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
    public static ValueTask<IBrowserFile> RequestImageFileAsync(this IBrowserFile browserFile, string format, int maxWidth, int maxHeight)
    {
        if (browserFile is BrowserFile browserFileInternal)
        {
            return browserFileInternal.Owner.ConvertToImageFileAsync(browserFileInternal, format, maxWidth, maxHeight);
        }

        throw new InvalidOperationException($"Cannot perform this operation on custom {typeof(IBrowserFile)} implementations.");
    }

    /// <summary>
    /// Opens a seekable stream for reading the uploaded file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This stream supports seeking operations (<see cref="Stream.Seek(long, SeekOrigin)"/>) with both forward and backward positioning.
    /// Seeking is efficient as it uses the browser's native Blob slicing API without loading the entire file into memory.
    /// </para>
    /// </remarks>
    /// <param name="file">The <see cref="IBrowserFile"/> to read from.</param>
    /// <param name="maxAllowedSize">
    /// The maximum number of bytes that can be supplied by the Stream. Defaults to 500 KB.
    /// <para>
    /// Calling <see cref="OpenReadStreamSeekable(IBrowserFile, long, CancellationToken)"/>
    /// will throw if the file's size, as specified by <see cref="IBrowserFile.Size"/>, is larger than
    /// <paramref name="maxAllowedSize"/>. By default, if the user supplies a file larger than 500 KB, this method will throw an exception.
    /// </para>
    /// <para>
    /// It is valuable to choose a size limit that corresponds to your use case. If you allow excessively large files, this
    /// may result in excessive consumption of memory or disk/database space, depending on what your code does
    /// with the supplied <see cref="Stream"/>.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">A cancellation token to signal the cancellation of streaming file data.</param>
    /// <returns>
    /// A seekable <see cref="Stream"/> for reading the file contents. The stream supports random access
    /// via <see cref="Stream.Seek(long, SeekOrigin)"/> operations.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown if the file's length exceeds the <paramref name="maxAllowedSize"/> value.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the <paramref name="file"/> does not support seekable streams.
    /// This feature is only available for the built-in <see cref="BrowserFile"/> implementation.
    /// </exception>
    public static Stream OpenReadStreamSeekable(
        this IBrowserFile file,
        long maxAllowedSize = 500 * 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file is BrowserFile browserFile)
        {
            return browserFile.OpenReadStreamSeekable(maxAllowedSize, cancellationToken);
        }

        throw new NotSupportedException(
            $"The type '{file.GetType().Name}' does not support seekable streams. " +
            $"This feature is only available for the built-in BrowserFile implementation. " +
            $"Consider using OpenReadStream() instead.");
    }
}
