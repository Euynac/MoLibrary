export async function downloadStream(fileName, contentType, streamReference) {
    if (!streamReference) {
        return false;
    }

    const arrayBuffer = await streamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], { type: contentType || "application/octet-stream" });
    return triggerDownload(fileName || "download", blob);
}

function triggerDownload(fileName, blob) {
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName || "download";
    anchor.rel = "noopener";
    anchor.style.display = "none";

    document.body.appendChild(anchor);
    try {
        anchor.click();
        return true;
    } finally {
        anchor.remove();
        URL.revokeObjectURL(objectUrl);
    }
}
