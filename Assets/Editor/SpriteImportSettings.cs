using UnityEditor;

/// <summary>
/// Assets/Resources/Sprites/ 에 들어오는 이미지에 게임용 임포트 설정을 자동 적용.
/// - 무압축 (캐릭터 디테일 보존), 투명도 정리, 4096까지 원본 크기 유지
/// </summary>
public class SpriteImportSettings : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Sprites/";

    // 설정을 바꾸면 숫자를 올려서 기존 이미지도 다시 임포트되게 함
    public override uint GetVersion() => 1;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;          // 축소해서 그릴 때 거칠어지지 않게
        ti.filterMode = UnityEngine.FilterMode.Trilinear;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 4096;
        ti.npotScale = TextureImporterNPOTScale.None;
    }
}
