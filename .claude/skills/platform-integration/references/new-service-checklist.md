# Adding a new service interface end to end

Example: a hypothetical `ICloudConfigService` ("remote config"). Replace names as needed.

## Contents
- 1. Interface
- 2. Null fallback
- 3. PlatformManager
- 4. Capability flag
- 5-7. Register on every platform
- 8. Real implementation
- 9. Optional event
- 10. Usage from game code
- 11. Test harness

## 1. Interface - `Services/CloudConfig/ICloudConfigService.cs`

```csharp
using System;

namespace _Core.Platform.Services.CloudConfig
{
    /// <summary>
    /// Provides platform-independent remote configuration values.
    /// </summary>
    public interface ICloudConfigService
    {
        /// <summary>
        /// Fetches a value. The callback receives the default value when unavailable.
        /// </summary>
        void GetValue(string key, string defaultValue, Action<string> callback);
    }
}
```

Guidelines taken from the existing interfaces:
- Callback style (`Action<T>`) for anything the SDK answers asynchronously
  (`IAdsService`, `IUserService`). Synchronous methods only when the SDK is synchronous
  (`ISaveStorage`).
- No SDK types in signatures. Add a project DTO under `Platform/Core/` if needed
  (as `PlatformUser` does for users).

## 2. Null fallback - `Services/CloudConfig/Null/NullCloudConfigService.cs`

```csharp
using System;

namespace _Core.Platform.Services.CloudConfig.Null
{
    /// <summary>
    /// Provides a safe fallback when remote configuration is not supported.
    /// </summary>
    public class NullCloudConfigService : ICloudConfigService
    {
        public void GetValue(string key, string defaultValue, Action<string> callback)
        {
            // Remote configuration is not supported on this platform.
            callback?.Invoke(defaultValue);
        }
    }
}
```

Always answer callbacks. A Null service that swallows a callback turns "unsupported" into "hangs".

## 3. PlatformManager - `Core/PlatformManager.cs`

```csharp
public ICloudConfigService CloudConfig { get; private set; }

internal void SetCloudConfigService(ICloudConfigService service)
{
    CloudConfig = service;
}
```

Setters are `internal` so only code in `Assembly-CSharp` (i.e. platform implementations) can
register services: `_Core/AssemblyInfo.cs` grants internals to Assembly-CSharp (where `Assets/_Platforms` compiles), not to `_Project` -
never call `Set*Service` from game code.

## 4. Capability flag - `Core/PlatformCapabilities.cs`

```csharp
public bool SupportsCloudConfig { get; internal set; }
```

## 5-7. Register on every platform

- `_Core/Platform/Platforms/NullPlatform.cs`: `manager.SetCloudConfigService(new NullCloudConfigService());`
  and `capabilities.SupportsCloudConfig = false;`
- `Assets/_Platforms/CrazyGames/Core/CrazyGamesPlatform.cs`: register (real or Null) in
  `RegisterServices`, set the flag in `ConfigureCapabilities`.
- Any other `IPlatform` implementation.

Find them all with a search for `: IPlatform` and `SetPurchaseService(`.

## 8. Real implementation

`Assets/_Platforms/<Platform>/Services/<Platform>CloudConfigService.cs`, namespace
`_Platforms.<Platform>.Services`. Re-check SDK readiness per call like
`CrazyGamesAdsService.CanShowAds()`.

## 9. Optional event

If other systems must react (e.g. "config refreshed"), add a struct implementing `IGameEvent` under
`_Core/Events/<Area>/`, namespace `_Core.Events.<Area>`, and publish it from the service. Consumers
subscribe in `OnEnable` and unsubscribe in `OnDisable`.

## 10. Usage from game code

```csharp
var platform = PlatformManager.Instance;
if (platform.Capabilities.SupportsCloudConfig)
{
    platform.CloudConfig.GetValue("difficulty", "normal", ApplyDifficulty);
}
```

Checking the capability is optional for correctness (the Null service answers anyway) but lets the
UI hide features that can't work.

## 11. Test harness

Add a keyboard/ContextMenu-driven MonoBehaviour under `Assets/_Project/Testing/` (or `Assets/_Platforms/<Platform>/Testing/` if it uses SDK types) following
`CrazyGamesUserTest`, add it to the `TestHarness` object in `Assets/_Project/Scenes/Test/99_Test.unity`
(not a build scene) and play that scene (its `BootstrapGuard` bootstraps first).
