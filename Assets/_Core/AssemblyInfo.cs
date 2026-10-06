using System.Runtime.CompilerServices;

// Platform bridges (Assets/_Platforms) compile into Assembly-CSharp next to their SDKs and register
// their services through the internal setters of PlatformManager and PlatformCapabilities.
[assembly: InternalsVisibleTo("Assembly-CSharp")]

// EditMode tests (Assets/_Core/Tests/Editor) reach test seams such as SaveManager.Init(storage, config).
[assembly: InternalsVisibleTo("_Core.Tests.Editor")]
