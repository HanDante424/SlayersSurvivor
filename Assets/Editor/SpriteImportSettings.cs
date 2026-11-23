using UnityEditor;

/// <summary>
/// Assets/Resources/Sprites/ 에 들어오는 이미지에 게임용 임포트 설정을 자동 적용.
/// - 기본: 무압축 (캐릭터 디테일 보존), 투명도 정리, 밉맵 켬 (축소해서 그릴 때 거칠어지지 않게), 4096까지 원본 크기 유지
/// - 큰 그림 용량 절약 (2026-11-16): 가로 · 세로가 2의 거듭제곱이 아니고 원본이 4 MB 이상인 그림은
///   **밉맵을 끄고 Windows 빌드에서 BC7 압축** (무압축 대비 약 1/4). 크기 · 픽셀 좌표는 그대로 → 이펙트 스크립트의 픽셀 좌표 상수가 그대로 맞음.
///   (Unity 는 밉맵이 있는 2의 거듭제곱이 아닌 그림은 압축하지 않음. 가로 · 세로가 4의 배수가 아니면 BC7 도 적용되지 않고 무압축으로 남음)
///   대가: 작게 그려질 때 가장자리가 지글거릴 수 있음 → 문제가 보이는 그림은 아래 NoCompress 에 이름을 추가
/// - 이펙트 큰 정사각형 시트 압축 (2026-11-23 사용자, 비교 빌드 확인 후 적용): 경로에 "/Fx/" 가 있고 가로 · 세로가 2의 거듭제곱이며
///   원본이 4 MB 이상인 그림은 **밉맵은 유지하고 Windows 빌드에서 BC7 압축**. 비교 빌드 결과 7장 각 1/4 (전체 빌드 808.9 → 732.9 MB).
///   되돌리기: 아래 "이펙트 큰 정사각형 시트" 블록을 지우고 GetVersion 숫자를 올림. 특정 그림만 빼려면 NoCompress 에 이름 추가
/// </summary>
public class SpriteImportSettings : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Sprites/";

    /// <summary>비정형 큰 그림 압축 기준 (원본 RGBA32 크기, MB)</summary>
    const float CompressMinRawMB = 4f;

    /// <summary>압축하지 않을 그림 (확장자 없는 이름) — 압축 때문에 품질이 거슬리는 그림을 여기에 추가</summary>
    static readonly System.Collections.Generic.HashSet<string> NoCompress = new System.Collections.Generic.HashSet<string>
    {
        // 플레이어 스탠드 (2026-11-18): 몸 키 1253px 을 화면에서 약 1/14 로 줄여 그림 → 밉맵 없으면 가장자리가 지글거려 달리기 (밉맵 켬) 와 질감이 달랐음. 되돌리려면 이 줄 삭제
        "ErnStray_Stand",
    };

    // 설정을 바꾸면 숫자를 올려서 기존 이미지도 다시 임포트되게 함
    public override uint GetVersion() => 7;

    static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var ti = (TextureImporter)assetImporter;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        ti.textureType = TextureImporterType.Default;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;          // 축소해서 그릴 때 거칠어지지 않게
        ti.filterMode = UnityEngine.FilterMode.Trilinear;
        // 바닥은 반복 타일 (Tiled 그리기에서 이음매 없이)
        ti.wrapMode = name == "Ground" ? UnityEngine.TextureWrapMode.Repeat : UnityEngine.TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        ti.npotScale = TextureImporterNPOTScale.None;   // 크기 유지 필수 (이펙트 스크립트가 픽셀 크기 · 좌표를 직접 씀)

        ti.GetSourceTextureWidthAndHeight(out int w, out int h);
        bool npot = !(IsPowerOfTwo(w) && IsPowerOfTwo(h));
        float rawMB = w * (float)h * 4f / 1048576f;
        if (npot && rawMB >= CompressMinRawMB && !NoCompress.Contains(name))
        {
            ti.mipmapEnabled = false;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = 4096,
                format = TextureImporterFormat.BC7,
                textureCompression = TextureImporterCompression.CompressedHQ,
                compressionQuality = 100,
            });
        }
        // 이펙트 큰 정사각형 시트 (2026-11-23): 밉맵 유지 + Windows BC7 (2의 거듭제곱이라 밉맵과 압축을 함께 쓸 수 있음)
        else if (!npot && assetPath.Contains("/Fx/") && rawMB >= CompressMinRawMB && !NoCompress.Contains(name))
        {
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = 4096,
                format = TextureImporterFormat.BC7,
                textureCompression = TextureImporterCompression.CompressedHQ,
                compressionQuality = 100,
            });
        }
    }
}
