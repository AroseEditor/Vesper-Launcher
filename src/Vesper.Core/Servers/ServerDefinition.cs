using System.Text.Json.Serialization;

namespace Vesper.Core.Servers;

public sealed class ServerDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Project { get; set; } = "paper";

    public string MinecraftVersion { get; set; } = string.Empty;

    public int Build { get; set; }

    public string JarFileName { get; set; } = "server.jar";

    public int MemoryMb { get; set; } = 2048;

    public int Port { get; set; } = 25565;

    public int MaxPlayers { get; set; } = 20;

    public string Motd { get; set; } = "A Vesper server";

    public bool OnlineMode { get; set; }

    public string? CustomDomain { get; set; }

    public bool UsePlayit { get; set; }

    public string? JavaPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastStartedAt { get; set; }

    [JsonIgnore]
    public string Address => string.IsNullOrWhiteSpace(CustomDomain)
        ? $"localhost:{Port}"
        : CustomDomain;

    [JsonIgnore]
    public string Summary => $"{Project} {MinecraftVersion} build {Build}";
}
