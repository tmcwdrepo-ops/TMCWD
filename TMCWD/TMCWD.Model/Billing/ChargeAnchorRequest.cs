using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace TMCWD.Model.Billing;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ChargeAnchorRequest
{
    [Range(1, int.MaxValue)] public int AccountId { get; set; }
}
