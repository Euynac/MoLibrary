export async function downloadArchive(url, archiveName, paths) {
    if (!url || !Array.isArray(paths) || paths.length === 0) {
        return false;
    }

    const response = await fetch(url, {
        method: "POST",
        credentials: "same-origin",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({
            archiveName,
            paths
        })
    });

    if (!response.ok) {
        throw new Error(await readErrorMessage(response));
    }

    const blob = await response.blob();
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = resolveFileName(response, archiveName);
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

async function readErrorMessage(response) {
    const fallback = `Archive download failed with HTTP ${response.status}.`;
    const text = await response.text();
    if (!text) {
        return fallback;
    }

    try {
        const payload = JSON.parse(text);
        return payload.message || payload.hint || payload.title || text;
    } catch {
        return text;
    }
}

function resolveFileName(response, requestedName) {
    const contentDisposition = response.headers.get("content-disposition");
    const serverFileName = parseContentDispositionFileName(contentDisposition);
    return serverFileName || requestedName || "file-ops-selection.zip";
}

function parseContentDispositionFileName(contentDisposition) {
    if (!contentDisposition) {
        return null;
    }

    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition);
    if (utf8Match?.[1]) {
        return decodeURIComponent(utf8Match[1].trim());
    }

    const asciiMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
    return asciiMatch?.[1]?.trim() || null;
}
