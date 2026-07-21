public enum UserRole
{
    SuperAdmin = 1,
    CustomerRepresentative = 2,
    Engineer = 3,
    MeterReader = 4,
    Guest = 5
}

public enum ErrorType
{
    Error = 1,
    Warning = 2
}

public enum ErrorModule
{
    Administration = 1,
    Application = 2,
    CustomerSupport = 3,
    Data = 4,
    Engineering = 5
}

public enum RequestStatus
{
    Draft = 1,
    InProgress = 2,
    Completed = 3,
    Rejected = 4
}

public enum AccountStatus
{
    Pending = 1,
    Suspended = 2,
    Active = 3,
    Closed = 4
}

public enum AccountClassification
{
    Residential = 1,
    Government = 2,
    Industrial = 3,
    Commercial = 4,
    CommercialA = 5,
    CommercialB = 6,
    CommercialC = 7,
    Wholesale = 8,
    Bulk = 9
}

public enum JobOrderStatus
{
    Inspection = 1,
    Recommendation = 2,
    Charging = 3,
    Payment = 4,
    Releasing = 5,
    Installation = 6,
    Verification = 7,
    Completed = 8,
    Rejected = 9
}

public enum RequestFileType
{
    Finding = 1,
    Verification = 2
}

public enum FileType
{
    Png = 1,
    Jpeg = 2,
    Image = 3,
    Tiff = 4,
    Pdf = 5,
    Word = 6,
    Text = 7,
    Other = 8,
}

public enum FeeClassification
{
    Penalty = 1,
    OtherFee = 2,
    Adjustment = 3,
}

public enum PaymentStatus
{
    Unpaid = 1,
    Paid = 2,
    Overdue = 3
}

public enum PaymentMethod
{
    Cash = 1,
    Check = 2,
    EWallet = 3
}

public enum GatewayType
{
    GCash = 1
}