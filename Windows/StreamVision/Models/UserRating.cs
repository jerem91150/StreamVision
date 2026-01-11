using System;

namespace StreamVision.Models
{
    /// <summary>
    /// User rating for a media item (star rating + quality feedback)
    /// </summary>
    public class UserRating
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MediaId { get; set; } = "";
        public int StarRating { get; set; } // 1-5 stars
        public string QualityRating { get; set; } = ""; // "good" or "bad"
        public DateTime RatedAt { get; set; } = DateTime.Now;
    }
}
