using System.Net.Http.Json;
using System.Text.Json;

namespace klooie.blazor.BrowserConsole;

public sealed class BrowserAssetProvider : IBinaryAssetProvider
{
    private const string ManifestPath = "assets/klooie-assets.json";
    private readonly Dictionary<string, byte[]> assets;

    private BrowserAssetProvider(Dictionary<string, byte[]> assets)
    {
        this.assets = assets;
    }

    public IEnumerable<string> AssetNames => assets.Keys;

    public static async Task<BrowserAssetProvider> CreateAsync(HttpClient http)
    {
        var manifest = await LoadManifestAsync(http);
        var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var preloadTasks = manifest
            .Where(IsPreloadedRuntimeAsset)
            .Select(async assetName => new KeyValuePair<string, byte[]>(NormalizeAssetKey(assetName), await http.GetByteArrayAsync(ToAssetUrl(assetName))))
            .ToArray();

        foreach (var asset in await Task.WhenAll(preloadTasks))
        {
            assets[asset.Key] = asset.Value;
        }

        return new BrowserAssetProvider(assets);
    }

    public static string ToAssetUrl(string assetName)
    {
        var normalized = assetName.Replace('\\', '/').TrimStart('/');
        return "assets/" + string.Join("/", normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }

    public bool Contains(string assetName) => assets.ContainsKey(NormalizeAssetKey(assetName));

    public Stream Open(string assetName)
    {
        if (assets.TryGetValue(NormalizeAssetKey(assetName), out var bytes) == false)
        {
            throw new FileNotFoundException($"Asset '{assetName}' not found.");
        }

        return new MemoryStream(bytes, writable: false);
    }

    private static async Task<string[]> LoadManifestAsync(HttpClient http)
    {
        try
        {
            return await http.GetFromJsonAsync<string[]>(ManifestPath) ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeAssetKey(string assetName) => Path.GetFileName(assetName);

    private static bool IsPreloadedRuntimeAsset(string assetName)
    {
        var extension = Path.GetExtension(assetName);
        if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
