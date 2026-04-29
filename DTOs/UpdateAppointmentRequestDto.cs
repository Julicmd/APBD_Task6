namespace APBD_TASK6.DTOs
{
    public class UpdateAppointmentRequestDto
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        
    }
}
