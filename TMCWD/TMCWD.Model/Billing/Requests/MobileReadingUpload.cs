using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TMCWD.Model.Billing.Requests;

public sealed class MobileReadingUpload
{
    [Range(1, long.MaxValue)] public long AccountId { get; set; }
    [Required, JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? CurrentReading { get; set; }
    [Required(AllowEmptyStrings = true), StringLength(2000)] public string Remarks { get; set; } = "";
    public DateTime ReadingDate { get; set; }
}
