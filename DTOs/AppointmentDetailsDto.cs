namespace APBD_TASK6.DTOs
{
    public class AppointmentDetailsDto
    {
        public int Id { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string? InternalNotes { get; set; }
        public string PatientFullName { get; set; } = string.Empty;
        public string PatientEmail { get; set; } = string.Empty;
        public string? PatientPhone { get; set; }
        public string DoctorFullName { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
    }
}
