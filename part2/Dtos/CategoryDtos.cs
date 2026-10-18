using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What the client sends to create or update a category.
// decimal? with [Required] means a missing value is rejected with a 400
// instead of silently becoming 0.
public class CategoryDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;        // e.g. "10km Run" or "Under 20"

    // Limits match the decimal(6,2) column in the database
    [Required, Range(0.01, 9999.99)]
    public decimal? Distance { get; set; }

    // Limits match the decimal(8,2) column; 0 means free entry
    [Required, Range(0, 999999.99)]
    public decimal? EntryFee { get; set; }
}

// What the API sends back for a category
public class CategoryResponseDto
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public decimal EntryFee { get; set; }
}