using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models
{
    /// <summary>
    /// View model representing race details displayed in the Web application views.
    /// </summary>
    public class RaceViewModel
    {
        // Unique identifier for the race
        public int Id { get; set; }

        // Name or title of the race event
        [Required(ErrorMessage = "Race name is required")]
        public string Name { get; set; } = string.Empty;

        // Scheduled date and time of the event
        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Now;

        // Physical location or venue of the race
        [Required(ErrorMessage = "Location is required")]
        public string Location { get; set; } = string.Empty;

        // Optional description or details about the event
        public string Description { get; set; } = string.Empty;
    }
}