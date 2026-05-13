namespace WaterCueWindows.Models;

public class HydrationEvent
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool ValidatedByAI { get; set; }
    public string? GroqModel { get; set; }
    public bool EmergencyBypass { get; set; }
    public string? PhotoHash { get; set; }
    public string? Reason { get; set; }
}
