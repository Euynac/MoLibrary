import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const moduleUrl = new URL("../wwwroot/UISignalR/signalr-debug.js", import.meta.url);

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((promiseResolve, promiseReject) => {
        resolve = promiseResolve;
        reject = promiseReject;
    });
    return { promise, resolve, reject };
}

class FakeConnection {
    constructor(startGate, startCalled = deferred(), stopError = null) {
        this.connectionId = "connection-1";
        this.state = "Disconnected";
        this.startGate = startGate;
        this.startCalled = startCalled;
        this.stopError = stopError;
        this.stopCalls = 0;
        this.handlers = new Map();
    }

    onclose(handler) {
        this.oncloseHandler = handler;
    }

    onreconnecting(handler) {
        this.onreconnectingHandler = handler;
    }

    onreconnected(handler) {
        this.onreconnectedHandler = handler;
    }

    on(methodName, handler) {
        this.handlers.set(methodName, handler);
    }

    off(methodName) {
        this.handlers.delete(methodName);
    }

    async start() {
        this.startCalled.resolve();
        await this.startGate.promise;
        this.state = "Connected";
    }

    async stop() {
        this.stopCalls++;
        this.state = "Disconnected";
        if (this.stopError) {
            throw this.stopError;
        }
    }

    async invoke() {
    }
}

function installSignalRFake(connection) {
    class FakeBuilder {
        withUrl() {
            return this;
        }

        configureLogging() {
            return this;
        }

        withAutomaticReconnect() {
            return this;
        }

        build() {
            return connection;
        }
    }

    globalThis.signalR = {
        HubConnectionBuilder: FakeBuilder,
        HubConnectionState: { Connected: "Connected" },
        LogLevel: { Information: "Information" }
    };
}

function createCallback() {
    const invocations = [];
    return {
        invocations,
        invokeMethodAsync(method, ...args) {
            invocations.push([method, ...args]);
            return Promise.resolve();
        }
    };
}

test("all JavaScript-to-.NET callbacks use the tracked callback path", async () => {
    const source = await readFile(moduleUrl, "utf8");

    assert.equal(source.match(/invokeMethodAsync/g)?.length, 1);
    assert.match(source, /pendingCallbacks\.add\(callbackPromise\)/);
    assert.match(source, /Promise\.allSettled\(\[\.\.\.pendingCallbacks\]\)/);
});

test("shutdown overlapping connect suppresses publication and forces post-start cleanup", async () => {
    const startGate = deferred();
    const connection = new FakeConnection(startGate);
    installSignalRFake(connection);
    const callback = createCallback();
    const { createSession } = await import(`${moduleUrl.href}?overlap=${Date.now()}`);
    const session = await createSession(callback);

    const connectPromise = session.connect("/hub", "");
    await connection.startCalled.promise;
    const shutdownPromise = session.shutdown();
    await shutdownPromise;
    assert.equal(connection.stopCalls, 1);

    startGate.resolve();
    const connectResult = await connectPromise;

    assert.equal(connectResult.success, false);
    assert.equal(connection.stopCalls, 2);
    assert.equal(connection.state, "Disconnected");
    assert.equal(callback.invocations.length, 0);
});

test("repeated shutdown shares one drain while a late connection is disposed", async () => {
    const startGate = deferred();
    const connection = new FakeConnection(startGate);
    installSignalRFake(connection);
    const callback = createCallback();
    const { createSession } = await import(`${moduleUrl.href}?late=${Date.now()}`);
    const session = await createSession(callback);

    const connectPromise = session.connect("/hub", "");
    await connection.startCalled.promise;
    const firstShutdown = session.shutdown();
    const secondShutdown = session.shutdown();
    assert.strictEqual(firstShutdown, secondShutdown);

    await firstShutdown;
    assert.equal(connection.stopCalls, 1);
    startGate.resolve();
    await connectPromise;

    connection.onreconnectedHandler?.("late-connection");
    connection.oncloseHandler?.();
    assert.equal(connection.stopCalls, 2);
    assert.equal(connection.state, "Disconnected");
    assert.equal(callback.invocations.length, 0);
});

test("shutdown drains pending callbacks before releasing the callback reference", async () => {
    const startGate = deferred();
    startGate.resolve();
    const connection = new FakeConnection(startGate);
    installSignalRFake(connection);

    const callbackGate = deferred();
    let callbackCount = 0;
    const callback = {
        invokeMethodAsync() {
            callbackCount++;
            return callbackGate.promise;
        }
    };
    const { createSession } = await import(`${moduleUrl.href}?drain=${Date.now()}`);
    const session = await createSession(callback);

    const connectResult = await session.connect("/hub", "");
    assert.equal(connectResult.success, true);
    assert.ok(callbackCount > 0);

    const shutdownPromise = session.shutdown();
    let shutdownSettled = false;
    void shutdownPromise.then(() => { shutdownSettled = true; });
    await Promise.resolve();
    assert.equal(shutdownSettled, false);

    callbackGate.resolve();
    await shutdownPromise;
    assert.equal(shutdownSettled, true);
});

test("connection stop failure is reported only after callbacks drain", async () => {
    const startGate = deferred();
    startGate.resolve();
    const connection = new FakeConnection(startGate, deferred(), new Error("stop failed"));
    installSignalRFake(connection);

    const callbackGate = deferred();
    const callback = {
        invokeMethodAsync() {
            return callbackGate.promise;
        }
    };
    const { createSession } = await import(`${moduleUrl.href}?stopFailure=${Date.now()}`);
    const session = await createSession(callback);

    const connectResult = await session.connect("/hub", "");
    assert.equal(connectResult.success, true);

    const shutdownPromise = session.shutdown();
    let shutdownSettled = false;
    void shutdownPromise.then(() => { shutdownSettled = true; });
    await Promise.resolve();
    assert.equal(shutdownSettled, false);

    callbackGate.resolve();
    const shutdownResult = await shutdownPromise;

    assert.deepEqual(shutdownResult, { drained: true, error: "stop failed" });
    assert.equal(connection.stopCalls, 1);
    assert.equal(shutdownSettled, true);
});
