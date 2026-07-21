namespace TMCWD.Application.Models
{
    public class NewApplicationViewModel
    {

        public NewApplicationViewModel() { }

        public string Lastname { get; set; } = string.Empty;
        public string Firstname { get; set; } = string.Empty;
        public string Middlename { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int UnitNo { get; set; }
        public string Building { get; set; } = string.Empty;
        public int HouseNo { get; set; }
        public string Street { get; set; } = string.Empty;
        public string Barangay { get; set; } = string.Empty;
        public string City { get; set; } = "Trece Martires City";
        public string Province { get; set; } = "Cavite";
        public string ZipCode { get; set; } = "4109";
        public string FullAddress => $"{UnitNo.ToString()} {Building}, {HouseNo.ToString()} {Street}, {Barangay}, {City}, {Province}, {ZipCode}";

    }
}
