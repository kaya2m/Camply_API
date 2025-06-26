namespace Camply.Application.Locations.DTOs
{
    public class ReviewMetrics
    {
        public double HelpfulnessRatio { get; set; }
        public int ResponseCount { get; set; }
        public int ShareCount { get; set; }
        public DateTime? LastInteraction { get; set; }
    }
}
