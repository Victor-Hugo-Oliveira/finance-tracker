using System.Text;
using FinanceTracker.Core.Domain;
using Microsoft.AspNetCore.Components.Forms;

namespace FinanceTracker.Web.Services;

/// <summary>Reads a file the user picked as text, inside the browser.</summary>
public static class TextFileReader
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// UTF-8 when the bytes are valid UTF-8; otherwise Latin-1, which is what Excel on Windows
    /// usually saves in Brazil (so "Alimentação" doesn't turn into "Alimenta��o").
    /// </summary>
    public static async Task<string> ReadAsync(IBrowserFile file)
    {
        if (file.Size > MaxBytes)
            throw new DomainException($"Arquivo grande demais (máximo de {MaxBytes / 1024 / 1024} MB).");

        using var memory = new MemoryStream();
        await using (var stream = file.OpenReadStream(MaxBytes))
            await stream.CopyToAsync(memory);
        var bytes = memory.ToArray();

        try
        {
            return StrictUtf8.GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }
}
