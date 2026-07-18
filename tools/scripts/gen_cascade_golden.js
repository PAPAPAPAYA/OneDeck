// Generates golden reference values for DeckCascadeLayoutTests by replicating
// docs/CardArrangementDemo.html computeSmoothPositions()/getCascadeParams() VERBATIM
// (including the coverage normalization "Plan B" step-factor pass).
// Run: node tools/scripts/gen_cascade_golden.js [cardCount]
const TOTAL_CARDS = parseInt(process.argv[2] || "20", 10);
const params = {
	shrinkCount: 6,
	minScale: 0.55,
	scalePower: 2,
	startSpacingX: 60,
	startSpacingY: 70,
	minSpacingX: 8,
	minSpacingY: 12,
	curveWidth: 0.55,
	spacingPower: 2,
	coverageNormalize: true,
	coverageTarget: 0.62,
	coverageCap: 2.5
};

const positionsCache = [];

function lerp(a, b, t) { return a + (b - a) * t; }
function easeOutPower(t, power) { return 1 - Math.pow(1 - t, power); }

function computeSmoothPositions() {
	positionsCache.length = 0;

	// Quadratic Bezier control points
	const P0 = { x: 0, y: 0 };
	// Clamp shrinkCount so the peak stays inside the curve for tiny decks
	// (matches DeckCascadeLayout.ComputeOffsetsUncached).
	const shrink = Math.min(params.shrinkCount, Math.max(1, TOTAL_CARDS - 1));
	// Peak pulls the curve up-left, roughly around the shrinkCount
	const peakX = params.startSpacingX * shrink * 0.85;
	const peakY = params.startSpacingY * shrink * 0.85;
	const P1 = { x: -peakX, y: -peakY };
	// Tail ends up-right, with slight return toward center
	const tailReturn = params.curveWidth; // 0 = straight up-left peak, 1 = strong right return
	const tailX = -peakX * (1 - tailReturn) + params.minSpacingX * (TOTAL_CARDS - shrink) * tailReturn * 0.6;
	const tailY = -peakY - params.minSpacingY * (TOTAL_CARDS - shrink);
	const P2 = { x: tailX, y: tailY };

	// Dense sampling for arc-length parameterization
	const samples = [];
	const sampleCount = 300;
	let prev = P0;
	let totalLength = 0;
	samples.push({ x: P0.x, y: P0.y, len: 0 });
	for (let i = 1; i <= sampleCount; i++) {
		const t = i / sampleCount;
		const inv = 1 - t;
		const x = inv * inv * P0.x + 2 * inv * t * P1.x + t * t * P2.x;
		const y = inv * inv * P0.y + 2 * inv * t * P1.y + t * t * P2.y;
		const dx = x - prev.x;
		const dy = y - prev.y;
		totalLength += Math.sqrt(dx * dx + dy * dy);
		samples.push({ x, y, len: totalLength });
		prev = { x, y };
	}

	// Pass 1: per-card step lengths from the spacing falloff (demo semantics).
	const steps = [];
	let rawTotal = 0;
	for (let i = 1; i < TOTAL_CARDS; i++) {
		const t = i / Math.max(1, TOTAL_CARDS - 1);
		const spacingX = lerp(params.startSpacingX, params.minSpacingX, easeOutPower(t, params.spacingPower));
		const spacingY = lerp(params.startSpacingY, params.minSpacingY, easeOutPower(t, params.spacingPower));
		const stepSize = Math.sqrt(spacingX * spacingX + spacingY * spacingY) * 0.5;
		steps.push(stepSize);
		rawTotal += stepSize;
	}

	// Coverage normalization (Plan B): one shared factor so the walk reaches
	// coverageTarget of the curve, clamped to [1, coverageCap].
	let stepFactor = 1;
	if (params.coverageNormalize && rawTotal > 0 && totalLength > 0) {
		stepFactor = Math.min(Math.max(params.coverageTarget * totalLength / rawTotal, 1), params.coverageCap);
	}

	// Pass 2: place cards along the curve with decreasing spacing
	positionsCache.push({ x: P0.x, y: P0.y });
	let currentLen = 0;
	for (let i = 1; i < TOTAL_CARDS; i++) {
		currentLen += steps[i - 1] * stepFactor;

		// Find sample at this arc length
		const targetLen = Math.min(currentLen, totalLength);
		let s = 1;
		while (s < samples.length && samples[s].len < targetLen) s++;
		const a = samples[s - 1];
		const b = samples[s];
		if (!b) {
			positionsCache.push({ x: P2.x, y: P2.y });
			continue;
		}
		const segLen = b.len - a.len;
		const localT = segLen > 0 ? (targetLen - a.len) / segLen : 0;
		positionsCache.push({
			x: lerp(a.x, b.x, localT),
			y: lerp(a.y, b.y, localT)
		});
	}
	return { rawTotal, totalLength, stepFactor };
}

function getCascadeParams(index) {
	if (positionsCache.length === 0) computeSmoothPositions();
	const t = index / Math.max(1, TOTAL_CARDS - 1);
	const scale = 1.0 - (1.0 - params.minScale) * easeOutPower(t, params.scalePower);
	const pos = positionsCache[index] || { x: 0, y: 0 };
	return { scale, offsetX: pos.x, offsetY: pos.y };
}

const info = computeSmoothPositions();

const xs = [], ys = [], scales = [];
for (let i = 0; i < TOTAL_CARDS; i++) {
	const p = getCascadeParams(i);
	xs.push(p.offsetX.toFixed(6) + "f");
	ys.push(p.offsetY.toFixed(6) + "f");
	scales.push(p.scale.toFixed(6) + "f");
}

const coverage = (Math.min(info.rawTotal * info.stepFactor, info.totalLength) / info.totalLength * 100).toFixed(1);
console.log("// cascadeIndex 0 = front card. Demo canvas space (px, y-down). " + TOTAL_CARDS + " cards, default params.");
console.log("// rawTotal=" + info.rawTotal.toFixed(3) + " totalLength=" + info.totalLength.toFixed(3) + " stepFactor=" + info.stepFactor.toFixed(4) + " coverage=" + coverage + "%");
console.log("private static readonly float[] DemoOffsetX = { " + xs.join(", ") + " };");
console.log("private static readonly float[] DemoOffsetY = { " + ys.join(", ") + " };");
console.log("private static readonly float[] DemoScale = { " + scales.join(", ") + " };");
