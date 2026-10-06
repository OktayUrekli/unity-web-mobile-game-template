# Build size and memory levers for this project

Ordered roughly by payoff for a URP WebGL game, 2D or 3D.

## Textures (usually the biggest chunk of the `.data` file)

- Keep source art at display resolution; set importer **Max Size** to the smallest value
  that looks right (the Analyzer flags > 1024).
- 2D: pack sprites into **Sprite Atlases** so many small textures compress as one and batch better.
- Disable **Read/Write** (doubles memory: CPU + GPU copy) on textures and meshes. Disable
  **Generate Mip Maps** (+33%) for sprites and UI; 3D textures seen at a distance usually need them.
- Compression: the builder produces DXT (desktop) and ASTC (mobile, `astc_*.data.br`) data files, so
  leave WebGL texture overrides on automatic/compressed formats rather than uncompressed RGBA32.
- Crunch compression reduces download size further for textures that tolerate artifacts.

## Meshes, lighting and shaders (3D)

- Mesh Compression in the model importer, no Read/Write, strip unused vertex channels; LOD groups
  for detailed models.
- Baked lightmaps can outweigh the geometry: keep lightmap resolution low, or prefer mostly
  unlit/simple-lit materials on web. Realtime shadows and many additional lights are expensive in WebGL.
- Every shader variant ships; keep the number of distinct shaders and URP features small.

## Audio

- Music (> 10 s, as the Analyzer defines it): Force To Mono, quality ~0.75 or lower, load type
  Compressed In Memory or Streaming.
- Short SFX: Decompress On Load is fine if they are small; keep sample rates reasonable.
- `SoundData` assets referenced from scenes, prefabs or `AudioConfig` pull their clips into the build, and
  those referenced from the boot path ship in the initial data file. Delete unused sounds.

## Code and engine

- Release builds already use IL2CPP OptimizeSize, DiskSizeLTO, engine stripping, Brotli.
- Managed stripping level is not set by the builder (`managedStrippingLevel: {}` in
  ProjectSettings = default). Raising it reduces wasm size but can strip reflection-only types;
  test thoroughly (`JsonUtility` save types are referenced directly and are usually safe).
- Avoid pulling in large packages just for one feature; each package adds to wasm.

## Scenes and initial load

- Initial load = wasm + framework + loader + data. All scenes in the build list and everything they
  reference directly go into `data`. Addressables (the Analyzer's suggestion) move content out of the
  initial load; the CrazySDK define manager sets `ADDRESSABLE_AVAILABLE` for WebGL when the package is installed.
- Everything in `Resources/` folders ships unconditionally.
- Keep `00_Bootstrap` minimal: it only needs the `Bootstrapper` and `GameConfig`. Managers are
  code-created, which is good for load size.
- URP: `m_SupportsHDR: 1` in `Assets/Settings/URP_2D.asset` (`URP_3D.asset` already has it off); if the game doesn't use HDR/bloom,
  disabling HDR saves GPU memory and bandwidth on mobile. The Analyzer also suggests disabling
  post-processing if unused.

## Memory

- Heap: initial 32 MB, geometric growth, max 2048 MB (ProjectSettings). The heap never shrinks.
  Mobile browsers can kill the tab far below 2 GB - that's why the release builder also produces
  512 MB and 1024 MB wasm variants when "Runs on mobile web" is ticked. Design to fit in 512 MB.
- Avoid spikes: load/instantiate in small batches across frames, prewarm pools in the menu, don't
  build huge strings (pretty-printed JSON saves included).
- GC runs effectively between frames on WebGL; per-frame garbage accumulates until frame end.
  Pooling (`PoolManager`) and allocation-free hot paths matter more than on desktop.
- `EventBus.Publish<T>` with struct events doesn't box (delegates stored per type), so publishing
  events is cheap; the per-ad lambdas in `CrazyGamesAdsService` are negligible.
