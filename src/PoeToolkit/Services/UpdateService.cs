using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace PoeCurrencySpammer.Services;

public class UpdateService
{
    private const string RepoOwner = "Kayrim";
    private const string RepoName = "POE-Toolkit";
    private const string AssetName = "PoeToolkit-win-x64.zip";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "PoeToolkit-Updater" },
            { "Accept", "application/vnd.github+json" }
        },
        Timeout = TimeSpan.FromSeconds(30)
    };

    public string CurrentVersion
    {
        get
        {
            var ver = Assembly.GetEntryAssembly()?.GetName().Version;
            return ver is not null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "v0.0.0";
        }
    }

    /// <summary>
    /// Check GitHub for the latest release. Returns (tag, downloadUrl) or null if up to date.
    /// </summary>
    public async Task<(string Tag, string DownloadUrl)?> CheckForUpdateAsync()
    {
        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        var release = await Http.GetFromJsonAsync<GitHubRelease>(url);
        if (release is null) return null;

        var latest = ParseVersion(release.TagName);
        var current = ParseVersion(CurrentVersion);

        if (latest <= current) return null;

        var asset = release.Assets?.FirstOrDefault(a =>
            a.Name.Equals(AssetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null) return null;

        return (release.TagName, asset.BrowserDownloadUrl);
    }

    /// <summary>
    /// Download the update zip, extract, and launch a script to swap the exe and restart.
    /// </summary>
    public async Task DownloadAndApplyAsync(string downloadUrl, IProgress<string>? progress = null)
    {
        var currentExe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "PoeToolkit.exe");
        var tempDir = Path.Combine(Path.GetTempPath(), "PoeToolkit_update");
        var zipPath = Path.Combine(tempDir, AssetName);
        var newExePath = Path.Combine(tempDir, "PoeToolkit.exe");

        // Clean and create temp dir
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
        Directory.CreateDirectory(tempDir);

        // Download
        progress?.Report("Downloading update...");
        using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(zipPath);
            await stream.CopyToAsync(file);
        }

        // Extract
        progress?.Report("Extracting...");
        ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);

        if (!File.Exists(newExePath))
            throw new FileNotFoundException("Update exe not found in zip");

        // Write updater script
        progress?.Report("Applying update...");
        var scriptPath = Path.Combine(tempDir, "update.cmd");
        var script = $"""
            @echo off
            title POE Toolkit Updater
            echo Waiting for app to close...
            :wait
            tasklist /FI "PID eq {Environment.ProcessId}" 2>NUL | find /I "PoeToolkit" >NUL
            if not errorlevel 1 (
                timeout /t 1 /nobreak >NUL
                goto wait
            )
            echo Updating...
            copy /Y "{newExePath}" "{currentExe}" >NUL
            echo Starting POE Toolkit...
            start "" "{currentExe}"
            rmdir /S /Q "{tempDir}" 2>NUL
            exit
            """;
        await File.WriteAllTextAsync(scriptPath, script);

        // Launch script and exit
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/C \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        // App will exit after this returns — caller should shut down
    }

    private static Version ParseVersion(string tag)
    {
        var clean = tag.TrimStart('v');
        return Version.TryParse(clean, out var v) ? v : new Version(0, 0, 0);
    }

    private record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets
    );

    private record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl
    );
}
