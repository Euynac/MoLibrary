export async function acquireExclusive(name, callback) {
    if (!navigator.locks || typeof navigator.locks.request !== "function") {
        throw new Error("The Web Locks API is required for durable chat history.");
    }

    await navigator.locks.request(name, { mode: "exclusive" }, async () => {
        await callback.invokeMethodAsync("HoldAsync");
    });
}
