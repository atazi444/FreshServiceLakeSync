namespace FreshServiceLakeSync.Models;

/// <summary>
/// Represents the result of a sync operation
/// </summary>
public class SyncResult
{
    public int TotalEmployees { get; set; }
    public int TotalRequesters { get; set; }
    public int Matched { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    
    public string Summary => 
        $"Processed: {TotalEmployees} employees, {TotalRequesters} requesters | " +
        $"Matched: {Matched} | Updated: {Updated} | Skipped: {Skipped} | Failed: {Failed}";
}
