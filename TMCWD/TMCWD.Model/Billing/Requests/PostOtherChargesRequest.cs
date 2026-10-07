namespace TMCWD.Model.Billing.Requests;

public class PostOtherChargesRequest
{
    public List<int> OtherChargeIds { get; set; } = new();
}
