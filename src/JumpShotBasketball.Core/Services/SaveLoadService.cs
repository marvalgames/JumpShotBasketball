using System.Text.Json;
using System.Text.Json.Serialization;
using JumpShotBasketball.Core.Models.League;

namespace JumpShotBasketball.Core.Services;

/// <summary>
/// JSON-based save/load for the entire League object graph.
/// Replaces the C++ 10-file approach (.plr, .lge, .sch, etc.) with a single .jsb file.
/// </summary>
public static class SaveLoadService
{
    public const string FileExtension = ".jsb";
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        options.Converters.Add(new MultiDimensionalArrayJsonConverter());
        return options;
    }

    /// <summary>
    /// Saves a League to a .jsb file.
    /// </summary>
    public static void Save(League league, string filePath)
    {
        var envelope = new SaveFileEnvelope
        {
            Version = CurrentVersion,
            SavedAt = DateTime.UtcNow,
            Data = league
        };

        string json = JsonSerializer.Serialize(envelope, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Saves a League to a .jsb file asynchronously.
    /// </summary>
    public static async Task SaveAsync(League league, string filePath, CancellationToken ct = default)
    {
        var envelope = new SaveFileEnvelope
        {
            Version = CurrentVersion,
            SavedAt = DateTime.UtcNow,
            Data = league
        };

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, ct);
    }

    /// <summary>
    /// Loads a League from a .jsb file.
    /// </summary>
    public static League Load(string filePath)
    {
        string json = File.ReadAllText(filePath);
        return DeserializeFromJson(json);
    }

    /// <summary>
    /// Loads a League from a .jsb file asynchronously.
    /// </summary>
    public static async Task<League> LoadAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        var envelope = await JsonSerializer.DeserializeAsync<SaveFileEnvelope>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Failed to deserialize save file.");

        ValidateVersion(envelope);
        return envelope.Data;
    }

    /// <summary>
    /// Serializes a League to a JSON string (for testing).
    /// </summary>
    public static string SerializeToJson(League league)
    {
        var envelope = new SaveFileEnvelope
        {
            Version = CurrentVersion,
            SavedAt = DateTime.UtcNow,
            Data = league
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    /// <summary>
    /// Deserializes a League from a JSON string (for testing).
    /// </summary>
    public static League DeserializeFromJson(string json)
    {
        var envelope = JsonSerializer.Deserialize<SaveFileEnvelope>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize save file.");

        ValidateVersion(envelope);
        return envelope.Data;
    }

    private static void ValidateVersion(SaveFileEnvelope envelope)
    {
        if (envelope.Version < 1 || envelope.Version > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported save file version {envelope.Version}. Current version is {CurrentVersion}.");
        }
    }
}
