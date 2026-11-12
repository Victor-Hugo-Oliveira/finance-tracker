// Saves text as a file the user downloads. Everything stays in the browser: no upload, no server.
window.financeTracker = {
    download(fileName, content, mimeType) {
        // The BOM makes Excel open UTF-8 CSV files with the right accents.
        const parts = mimeType.startsWith("text/csv") ? ["\uFEFF", content] : [content];
        const url = URL.createObjectURL(new Blob(parts, { type: mimeType }));
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};
