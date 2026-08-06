import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const moduleUrl = new URL("../../../Monica.UI/wwwroot/js/module-dependency-graph.js", import.meta.url);
const moduleSource = await readFile(moduleUrl, "utf8");
const moduleDataUrl = `data:text/javascript;base64,${Buffer.from(moduleSource).toString("base64")}`;
const { calculateDependencyEdgeEndpoints, calculateDependencyGraphLayout } = await import(moduleDataUrl);

const forward = calculateDependencyEdgeEndpoints(
    { x: 0, y: 0 },
    { x: 100, y: 0 },
    23,
    31);
assert.deepEqual(forward, { x1: 25, y1: 0, x2: 63, y2: 0 });

const reverse = calculateDependencyEdgeEndpoints(
    { x: 100, y: 0 },
    { x: 0, y: 0 },
    31,
    23);
assert.deepEqual(reverse, { x1: 67, y1: 0, x2: 29, y2: 0 });

const diagonal = calculateDependencyEdgeEndpoints(
    { x: 0, y: 0 },
    { x: 30, y: 40 },
    3,
    4);
assert.deepEqual(diagonal, { x1: 3, y1: 4, x2: 24, y2: 32 });

assert.equal(calculateDependencyEdgeEndpoints(
    { x: 4, y: 4 },
    { x: 4, y: 4 },
    23,
    23), null);

const mobileLayout = calculateDependencyGraphLayout([
    { id: "selected", selected: true },
    { id: "top", selected: false },
    { id: "right", selected: false },
    { id: "left", selected: false }
], 330, 352);
assert.equal(mobileLayout.width, 330);
assert.equal(mobileLayout.height, 352);
assert.deepEqual(mobileLayout.positions[0], { id: "selected", x: 165, y: 176 });
assert.equal(mobileLayout.positions[1].x, 165);
assert.equal(mobileLayout.positions[1].y, 66);
assert.ok(mobileLayout.positions.every(position => position.x >= 70 && position.x <= 260));
assert.ok(mobileLayout.positions.every(position => position.y >= 64 && position.y <= 288));

const desktopLayout = calculateDependencyGraphLayout([
    { id: "selected", selected: true },
    { id: "dependency", selected: false }
], 960, 560);
assert.deepEqual(desktopLayout.positions[0], { id: "selected", x: 480, y: 280 });
assert.deepEqual(desktopLayout.positions[1], { id: "dependency", x: 480, y: 170 });

const clampedLayout = calculateDependencyGraphLayout([], 200, 100);
assert.equal(clampedLayout.width, 320);
assert.equal(clampedLayout.height, 320);
