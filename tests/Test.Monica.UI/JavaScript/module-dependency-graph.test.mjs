import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const sourceUrl = new URL(
    "../../../Monica.UI/wwwroot/js/module-dependency-graph.js",
    import.meta.url);
const source = await readFile(sourceUrl, "utf8");
const moduleUrl = `data:text/javascript;base64,${Buffer.from(source).toString("base64")}`;
const { createDotNetCallbackGate } = await import(moduleUrl);

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((resolvePromise, rejectPromise) => {
        resolve = resolvePromise;
        reject = rejectPromise;
    });
    return { promise, resolve, reject };
}

test("dispose drains an admitted callback and rejects stale callback admission", async () => {
    const callback = deferred();
    const calls = [];
    const gate = createDotNetCallbackGate({
        invokeMethodAsync(methodName, ...args) {
            calls.push([methodName, ...args]);
            return callback.promise;
        }
    });

    const admitted = gate.invoke("OnNodeSelected", "module-a");
    const disposal = gate.dispose();
    let disposalCompleted = false;
    disposal.then(() => disposalCompleted = true);

    assert.equal(gate.invoke("OnNodeSelected", "stale-module"), undefined);
    await Promise.resolve();
    assert.equal(disposalCompleted, false);
    assert.deepEqual(calls, [["OnNodeSelected", "module-a"]]);

    callback.resolve();
    await Promise.all([admitted, disposal]);
    assert.equal(disposalCompleted, true);
    assert.equal(gate.invoke("OnGraphRenderFailed"), undefined);
    assert.deepEqual(calls, [["OnNodeSelected", "module-a"]]);
});

test("repeated dispose shares one drain and tolerates a rejected callback", async () => {
    const callback = deferred();
    let callCount = 0;
    const gate = createDotNetCallbackGate({
        invokeMethodAsync() {
            callCount++;
            return callback.promise;
        }
    });

    gate.invoke("OnGraphRenderFailed");
    const firstDisposal = gate.dispose();
    const secondDisposal = gate.dispose();

    assert.strictEqual(secondDisposal, firstDisposal);
    assert.equal(gate.invoke("OnNodeSelected", "stale-module"), undefined);
    callback.reject(new Error("circuit disconnected"));
    await assert.doesNotReject(firstDisposal);
    await assert.doesNotReject(gate.dispose());
    assert.equal(callCount, 1);
});
