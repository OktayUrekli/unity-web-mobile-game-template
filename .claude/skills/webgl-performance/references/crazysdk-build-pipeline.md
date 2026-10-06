# CrazySDK build pipeline (verified from source)

Source: `Assets/CrazySDK/Scripts/Editor/Builder/` - `Builder.cs`, `MenuBarOptions.cs`,
`BuildCompleteHandler.cs`, `BuildReportGenerator.cs`, `BuildWindow.cs`, `Tabs/BuildTab.cs`,
`Tabs/AnalyzeTab.cs`, `Analyzer/*.cs`. Everything is compiled only on `UNITY_6000_0_OR_NEWER`
(the "Go to Build" menu item exists on older versions too).

## Menu items

| Menu | Action |
|---|---|
| `CrazySDK/Go to Build` | Opens `BuildWindow` on the Build tab |
| `CrazySDK/Go to Analyzer` | Opens `BuildWindow` on the Analyzer tab |
| `CrazySDK/Development Build` | `new Builder().DoDevelopmentBuild()` |
| `CrazySDK/Release Build` | `new Builder().DoReleaseBuild(supportsMobile: EditorPrefs "CGBuildSupportsMobile")` |

The Build tab also has the "Runs on mobile web" toggle (stored in EditorPrefs key
`CGBuildSupportsMobile`, default false) and warns if GraphicsSettings preloads shaders
("may considerably slow down the loading of the game").

## Build steps

1. Scenes = enabled entries in `EditorBuildSettings.scenes`; aborts if none.
2. Output: `Builds/CrazyGamesDevelopment` or `Builds/CrazyGamesRelease` (relative to project
   root). Existing folder is **deleted**.
3. Store current settings, apply build settings (table below).
4. Force `EditorUserBuildSettings.webGLBuildSubtarget = DXT`, `BuildPipeline.BuildPlayer`.
5. Release only, on success:
   - ASTC build into `<path>_cg_intermediate_build`, copy its `.data.br` as `astc_<name>.data.br`
     into the main `Build/` folder, delete the intermediate folder.
   - If supportsMobile: two more builds with `PlayerSettings.WebGL.maximumMemorySize` 512 and 1024,
     copy their `.wasm.br` as `max_512mb_<name>` / `max_1024mb_<name>`.
   - `BuildReportGenerator.GenerateReport(...)` writes the report.
6. `finally`: restore all stored settings, `AssetDatabase.SaveAssets()`.

## Settings applied

| Setting | Development | Release |
|---|---|---|
| `Il2CppCodeGeneration` | OptimizeSize | OptimizeSize |
| Splash screen / Unity logo | off / off | off / off |
| `WebGL.compressionFormat` | Disabled | Brotli |
| `WebGL.exceptionSupport` | FullWithStacktrace | ExplicitlyThrownExceptionsOnly (None kept if initially None) |
| `WebGL.debugSymbolMode` | External | Off |
| `WebGL.dataCaching` | false | true |
| `WebGL.nameFilesAsHashes` | false | true |
| `stripEngineCode` | false | true |
| `WebGL.UserBuildSettings.codeOptimization` | BuildTimes | DiskSizeLTO |
| `webAssemblyBigInt` / `webAssemblyTable` / `wasm2023` | unchanged | true / true / true |
| `BuildOptions` | Development, AutoRunPlayer | AutoRunPlayer (+ CleanBuildCache below Unity 6.4) |

Secondary (ASTC / memory) builds use `BuildOptions.None`.

## Post-build (any WebGL build)

`BuildCompleteHandler : IPostprocessBuildWithReport` - on a successful, non-development WebGL
build whose output path isn't an intermediate one:
- writes `Library/CGReleaseBuildReportSummary-v1.json` (packed assets with sizes, total size,
  initial load size),
- opens the Analyzer and runs it if EditorPrefs `AnalyzeTab.AUTO_SHOW_AND_RUN_KEY` (default true).

So a release-style build from File > Build Profiles also feeds the Analyzer, but it won't get the
builder's setting overrides, ASTC data file or memory variants. Prefer the CrazySDK menu for uploads.

## Analyzer checks (`Analyzer.Analyze`)

For each packed asset in the summary:
- path under `/Resources/` -> listed in Resources section
- `AudioClip` longer than 10 s -> suggest ForceToMono if not mono, ReduceQuality if quality > 0.75
  (fix sets 0.75)
- `Texture2D` wider/taller than 1024 -> suggest ReduceSize (fix sets importer `maxTextureSize`);
  `isReadable` -> Read/Write list; `mipmapEnabled` -> mipmap list
- model files with Read/Write -> list

Project-level:
- URP in use -> post-processing tip
- `ADDRESSABLE_AVAILABLE` not defined -> Addressables tip with install button
- URP `msaaSampleCount` > 2 -> anti-aliasing tip
- `Time.fixedDeltaTime` < 0.02 -> physics timestep tip
- enabled build scenes containing GameObjects with missing scripts (opens each scene)
- If `PROJECT_AUDITOR_AVAILABLE`: Project Auditor code issues for `Assembly-CSharp` and
  `Assembly-CSharp-Editor`

Note: running the Analyzer opens every build scene in Single mode - save your open scene first.

## Size thresholds (`AnalyzeTab.RenderSizeWarnings`)

| Condition | Message |
|---|---|
| total > 250 MB | Error: not accepted on CrazyGames |
| initial load > 50 MB | Warning: may be rejected |
| initial load > 20 MB | Warning: may be disabled on mobile |

## Define symbols

`Assets/CrazySDK/Scripts/Editor/DefineManager/PackageEventDefineManager.cs` (editor-only asmdef
`CrazyGamesDefineManager`) adds/removes `PROJECT_AUDITOR_AVAILABLE` and `ADDRESSABLE_AVAILABLE`
in the **WebGL** define group based on whether `com.unity.project-auditor` /
`com.unity.addressables` are installed. They only affect the Analyzer.
