using System;

namespace Firetrack.Models
{
    public class UserModel
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // ---- Computed properties ----
        public string FullName => $"{FirstName} {LastName}";

        // ---- Role will be loaded via join, but keep for compatibility ----
        public string Role { get; set; } = "Personnel";

        // ---- For backward compatibility (old code uses Username) ----
        public string Username
        {
            get => Email;
            set => Email = value;
        }

        // ---- For backward compatibility (old code uses Password) ----
        public string Password
        {
            get => PasswordHash;
            set => PasswordHash = value;
        }

        // ---- For backward compatibility (old code uses IsActive) ----
        public bool IsActive
        {
            get => Status == "Active";
            set => Status = value ? "Active" : "Inactive";
        }

        // ---- For backward compatibility (old code uses PersonalQR) ----
        public string? PersonalQR { get; set; } = string.Empty;
    }
}