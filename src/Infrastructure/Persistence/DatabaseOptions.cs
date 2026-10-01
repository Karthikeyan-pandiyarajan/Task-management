using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// SQLite file path. A relative path is resolved against the API's content root, so both APIs
    /// (src/Api.Auth and src/Api.Tasks) point at the same file with "../../data/taskapp.db".
    /// </summary>
    [Required] public string Path { get; set; } = string.Empty;
}
