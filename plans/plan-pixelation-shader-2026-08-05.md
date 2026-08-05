# Plan: Pixelation Shaders (Fullscreen + Chunky Sprite) — 2026-08-05

## Goal
Port the classic Asset Store "Pixelation"/"Chunky" effects (found in `slash-dash-demo/Assets/Pixelation/`, Built-in RP) to OneDeck (Unity 6000.3 + URP 17.3), fullscreen with UI included, plus a single-sprite chunky variant.

## Findings
- `slash-dash-demo` uses `OnRenderImage` + `Graphics.Blit` — dead end under URP; only the algorithms and `chunky4x4_16.png` atlas are portable.
- GitHub: `ilialek/Pixel-Shader` (same block-center algorithm); `whateep/unity-simple-URP-pixelation` (108★, pre-RenderGraph URP, needs rewrite); `herohiralal` gist (CRT sub-pixel variant, skipped).
- URP 17 has a built-in `FullScreenPassRendererFeature` — zero custom render code needed.
- Overlay UI is NOT covered by fullscreen passes; `Screen Space - Camera` canvases are.

## Implementation
1. `Assets/Shaders/PixelationFullscreen.shader` — URP fullscreen shader (`_BlitTexture`, block-center sampling, `_BlockCount` 64–512, aspect-corrected grid). Same math as slash-dash-demo `Pixelation.shader`.
2. `Assets/Materials/PixelationFullscreen.mat`.
3. `FullScreenPassRendererFeature` named "PixelationFullscreen" added to `Assets/Settings/PC_Renderer.asset` and `Mobile_Renderer.asset` (injection: `AfterRenderingPostProcessing`).
4. All 4 root canvases in `GameScene.unity` set to Screen Space - Camera (Main Camera, planeDistance 100) so UI is pixelated too. `ResultStatsPanel` needed no change (nested canvas inherits root mode). `CardTagTooltip.cs` sets SSC + `worldCamera` in code.
5. `Assets/Scripts/UXPrototype/PixelationEffectController.cs` — lazy singleton (`PixelationEffectController.me`), `SetEnabled(bool)` / `SetBlockCount(float)`; finds features via `m_RendererDataList` reflection on active + default URP assets.
6. `Assets/Shaders/ChunkySprite.shader` — single-sprite chunky port (Sprites/Default structure; `_SprTex` atlas, `_BlockCount`, `_Brightness`; 16-level grayscale → atlas frame, keeps sprite alpha/tint).
7. `Assets/Textures/chunky4x4_16.png` copied from slash-dash-demo (Point, no mipmaps, Clamp, uncompressed); `Assets/Materials/ChunkySprite.mat` with atlas assigned.

## Verification (Play Mode, 2026-08-05)
- Fullscreen pixelation ON: scene + UI (HP bar, prices, buttons, cards) all pixelated — `Assets/Screenshots/pixelation_on_final.png`.
- Toggle OFF/ON via `PixelationEffectController.me.SetEnabled()` works — `Assets/Screenshots/pixelation_off.png`.
- Chunky applied to a physical card in play mode: card renders as chunky glyphs — `Assets/Screenshots/chunky_card.png`.

## Notes
- Scene was dirty before the change; canvas conversion left unsaved for user review — save `GameScene.unity` to keep it.
- `ChunkySprite.mat` `_BlockCount` left at 24 (good for card-sized sprites).
- Chunky output is black/white glyphs: dark sprites quantize to dark frames — raise `_Brightness` for more visible detail.
