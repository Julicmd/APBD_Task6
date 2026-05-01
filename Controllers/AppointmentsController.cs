using System.Data;
using APBD_TASK6.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace APBD_TASK6.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly string _connectionString;

        public AppointmentsController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing 'DefaultConnection' in appsettings.json.");
        }

        [HttpGet]
        public async Task<ActionResult> GetAllAppointments(
            [FromQuery] string? status,
            [FromQuery] string? patientLastName
            )
        {
            const string sql ="""
                              SELECT
                                  a.IdAppointment,
                                  a.AppointmentDate,
                                  a.Status,
                                  a.Reason,
                                  p.FirstName + N' ' + p.LastName AS PatientFullName,
                                  p.Email AS PatientEmail
                              FROM dbo.Appointments a
                              JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                              WHERE (@Status IS NULL OR a.Status = @Status)
                                AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
                              ORDER BY a.AppointmentDate;
                              """;


            var result = new List<AppointmentListDto>();
            
            //using!!
            await using var connection = new SqlConnection( _connectionString);
            await using var command = new SqlCommand(sql, connection);
            
            
            command.Parameters.Add("@Status",SqlDbType.NVarChar).Value = (object?)status ?? DBNull.Value;
            command.Parameters.Add("@PatientLastName",SqlDbType.NVarChar).Value = (object?) patientLastName ?? DBNull.Value;

            await connection.OpenAsync();
            
            await using var reader = await command.ExecuteReaderAsync();


            while (await reader.ReadAsync())
            {
                result.Add(new AppointmentListDto
                {
                    IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                    AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    Reason = reader.GetString(reader.GetOrdinal("Reason")),
                    PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                    PatientEmail =  reader.GetString(reader.GetOrdinal("PatientEmail")),
                });
            }

            return Ok(result);
        }


        [HttpGet("{Id:int}")]
        public async Task<ActionResult> GetAppointment(int Id)
        {
            const string sql = """
                               SELECT
                                   a.IdAppointment,
                                   a.AppointmentDate,
                                   a.Status,
                                   a.Reason,
                                   a.InternalNotes,
                                   p.FirstName + N' ' + p.LastName AS PatientFullName,
                                   p.Email AS PatientEmail,
                                   d.FirstName + N' ' + d.LastName AS DoctorFullName,
                                   d.LicenseNumber,
                                   s.Name AS Specialization
                               FROM dbo.Appointments a
                               JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
                               JOIN dbo.Doctors d ON d.IdDoctor = a.IdDoctor
                               JOIN dbo.Specializations s ON s.IdSpecialization = d.IdSpecialization
                               WHERE a.IdAppointment = @IdAppointment;
                               """;
            
            await using var connect = new SqlConnection( _connectionString);
            await using var command = new SqlCommand(sql, connect);
            
            command.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = Id;
        
            await connect.OpenAsync();
            
            await using var reader = await command.ExecuteReaderAsync();
        
            if (!await reader.ReadAsync())
                 return NotFound(new ErrorResponseDto() {Mesage = $"Appointment {Id} NOT FOUND"});
        
        
            var dto = new AppointmentDetailsDto()
            {
                Id = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status = reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("InternalNotes")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
                DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
                LicenseNumber = reader.GetString(reader.GetOrdinal("LicenseNumber")),
                Specialization = reader.GetString(reader.GetOrdinal("Specialization")),
        
            };
        
            return Ok(dto);
         }






    }
}
