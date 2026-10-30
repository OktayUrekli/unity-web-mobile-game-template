# Third-party notices

The MIT license in `LICENSE` covers this template's own code and assets only. The components below belong to their authors and keep their own licenses or terms. Check them before you ship a game built from this template.

## Included in this repository

| Component | Location | Owner | License / terms |
|---|---|---|---|
| DOTween (free version) | `Assets/ThirdParty/Demigiant/DOTween` | Daniele Giardini, Demigiant | [DOTween license](http://dotween.demigiant.com/license.php). Free for commercial and non-commercial use. Redistributed here verbatim with its original `readme.txt` and copyright notices; modified versions may not be redistributed. |
| CrazyGames SDK 5.31 | `Assets/CrazySDK`, `Assets/Plugins/crazySDK.jslib` | CrazyGames | No public license is published. Official download: <https://sdk.crazygames.com/UnityCrazySDK.unitypackage>, documentation: <https://docs.crazygames.com/>. Template changes to the SDK: three added assembly definitions and a define constraint in `crazySDK.jslib.meta`, so the SDK compiles only when CrazyGames is the selected platform, and the `CrazyBanner` prefab's canvas sorting order and reference resolution. |
| TextMesh Pro Essential Resources | `Assets/TextMesh Pro` | Unity Technologies | [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license). |
| Liberation Sans font | `Assets/TextMesh Pro/Fonts` | Red Hat, Google | SIL Open Font License 1.1, see `LiberationSans - OFL.txt` next to the font. |
| EmojiOne sample sprites | `Assets/TextMesh Pro/Sprites` | EmojiOne | See `EmojiOne Attribution.txt` next to the sprites. |
| Template sounds (`templateMusic.mp3`, `templateClickSound.wav`) | `Assets/_Project/Audio` | Their original authors | CC0 1.0 (public domain); no attribution required. |
| Google Mobile Ads `link.xml` | `Assets/GoogleMobileAds/link.xml` | Google LLC | [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). Copied verbatim from the Google Mobile Ads package, which would copy it there itself on the first build. |

## Fetched by the Package Manager (not stored in this repository)

| Package | Source | License |
|---|---|---|
| UI Particle (ParticleEffectForUGUI) 4.13.3 | <https://github.com/mob-sakai/ParticleEffectForUGUI> | MIT |
| UI Effect Snapshot 1.0.1 | <https://github.com/mob-sakai/UIEffectSnapshot> | MIT |
| Google Mobile Ads Unity plugin 11.6.0 (`com.google.ads.mobile`) | OpenUPM, from <https://github.com/googleads/googleads-mobile-unity> | Apache License 2.0 |
| External Dependency Manager (`com.unity.external-dependency-manager`), a dependency of Google Mobile Ads | Unity Registry | The package's own license (Apache License 2.0 upstream) |
| Google Mobile Ads SDK and User Messaging Platform SDK for Android, downloaded by Gradle when an Android build is made | Google Maven (<https://maven.google.com>) | Google's SDK terms; using AdMob also requires accepting the AdMob terms and policies |
| Unity packages (URP, Input System, Localization, Addressables, 2D packages, Test Framework, AI Assistant and others in `Packages/manifest.json`) | Unity Registry | Unity Companion License or the package's own Unity terms |
