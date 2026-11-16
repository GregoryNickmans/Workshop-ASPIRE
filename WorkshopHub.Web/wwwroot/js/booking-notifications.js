// Business SignalR is separate from Blazor's circuit. Shared by mounted pages, never browser storage.
const receivers = new Map();
let nextId = 0;
let connection;
let retry;
let pending;
let generation = 0;

function changed(bookingId = null) {
    for (const [id, receiver] of receivers) {
        receiver.invokeMethodAsync("BookingChanged", bookingId).catch(() => {
            // A disconnected Blazor circuit must not leave a business socket/retry loop alive.
            stop(id);
        });
    }
}

async function connect() {
    if (!receivers.size || connection || pending) return;
    const current = generation;
    const abort = new AbortController();
    pending = abort;
    const timeout = setTimeout(() => abort.abort(), 10000);
    try {
        const response = await fetch("/notifications/negotiate", {
            method: "POST", credentials: "same-origin", cache: "no-store", signal: abort.signal
        });
        if (!response.ok) throw new Error("Negotiation unavailable");
        const details = await response.json();
        if (current !== generation || !receivers.size) return;
        const hub = new signalR.HubConnectionBuilder()
            .withUrl(details.url, { accessTokenFactory: () => details.accessToken })
            .configureLogging(signalR.LogLevel.None)
            .build();
        connection = hub;
        hub.on("BookingChanged", bookingId => changed(bookingId));
        hub.onclose(() => {
            if (connection === hub) connection = undefined;
            if (current === generation) scheduleRetry();
        });
        await hub.start();
        if (current !== generation || !receivers.size) { await hub.stop(); return; }
        // Signals may have been missed during startup/offline. Reload, never replay guessed state.
        changed();
    } catch {
        const hub = connection;
        if (current === generation) connection = undefined;
        if (hub && current === generation) await hub.stop().catch(() => {});
        // No access tokens, URLs or business data in console output.
    } finally {
        clearTimeout(timeout);
        if (pending === abort) pending = undefined;
        if (current === generation && !connection) scheduleRetry();
    }
}

function scheduleRetry() {
    clearTimeout(retry);
    if (receivers.size) retry = setTimeout(connect, 5000); // Fresh negotiation, including a fresh token.
}

export function start(receiver) {
    const id = ++nextId;
    receivers.set(id, receiver);
    if (connection?.state === signalR.HubConnectionState.Connected) {
        receiver.invokeMethodAsync("BookingChanged", null).catch(() => stop(id));
    } else {
        void connect();
    }
    return id;
}

export async function stop(id) {
    receivers.delete(id);
    if (receivers.size) return;
    generation++;
    clearTimeout(retry);
    pending?.abort();
    pending = undefined;
    const hub = connection;
    connection = undefined;
    if (hub) await hub.stop().catch(() => {});
}
