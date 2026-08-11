namespace Firetrack.Models
{
    public class OtpModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string OtpCode { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
        public bool IsUsed { get; set; }
    }
}