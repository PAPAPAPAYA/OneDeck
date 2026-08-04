#ifndef SLICE_NOISE_3D_INCLUDED
#define SLICE_NOISE_3D_INCLUDED

// Hash: map an integer 3D lattice coordinate to a pseudo-random gradient
// direction in [-1, 1]^3. Deterministic: same input, same output.
// (iq-style hash33 without sin(), avoids platform-dependent precision issues.)
float3 SliceHash3(float3 p)
{
	p = frac(p * float3(0.1031, 0.1030, 0.0973));
	p += dot(p, p.yxz + 33.33);
	return frac((p.xxy + p.yxx) * p.zyx) * 2.0 - 1.0;
}

// 3D gradient (Perlin-style) noise: dot products of corner gradients with
// the local offset, trilinearly interpolated with a quintic fade.
//
// Unlike value noise, the extrema of gradient noise sit INSIDE the lattice
// cells instead of on the corner points. When we feed (uv.x, uv.y, time)
// into 'pos', contour lines of the moving slice are born as round dots and
// die smoothly -- no more plus-shaped pops at lattice corners.
//
// The '_float' suffix is Shader Graph's precision convention:
// the Custom Function node looks up the function by base name + suffix.
void VNoise3D_float(float3 pos, out float Out)
{
	// Integer lattice cell index and fractional position inside the cell.
	float3 i = floor(pos);
	float3 f = frac(pos);

	// Quintic fade 6t^5 - 15t^4 + 10t^3: C2-continuous, keeps contour lines
	// smooth across lattice boundaries.
	float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

	// Gradient dot offset at the 8 corners of the cell.
	float n000 = dot(SliceHash3(i), f);
	float n100 = dot(SliceHash3(i + float3(1.0, 0.0, 0.0)), f - float3(1.0, 0.0, 0.0));
	float n010 = dot(SliceHash3(i + float3(0.0, 1.0, 0.0)), f - float3(0.0, 1.0, 0.0));
	float n110 = dot(SliceHash3(i + float3(1.0, 1.0, 0.0)), f - float3(1.0, 1.0, 0.0));
	float n001 = dot(SliceHash3(i + float3(0.0, 0.0, 1.0)), f - float3(0.0, 0.0, 1.0));
	float n101 = dot(SliceHash3(i + float3(1.0, 0.0, 1.0)), f - float3(1.0, 0.0, 1.0));
	float n011 = dot(SliceHash3(i + float3(0.0, 1.0, 1.0)), f - float3(0.0, 1.0, 1.0));
	float n111 = dot(SliceHash3(i + float3(1.0, 1.0, 1.0)), f - float3(1.0, 1.0, 1.0));

	// Trilinear interpolation along x, then y, then z (our time axis).
	float n = lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
		lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
		u.z);

	// Gradient noise lands in roughly [-0.75, 0.75]; rescale to [0, 1] so
	// downstream Levels/LineWidth tuning keeps the same meaning as before.
	Out = clamp(n * 0.6667 + 0.5, 0.0, 1.0);
}

#endif
