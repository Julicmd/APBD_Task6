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

        [HttpPost]
        public async Task<ActionResult> AddApointment([FromBody] CreateAppointmentRequestDto createAppointment)
        {
            if (createAppointment.AppointmentDate < DateTime.UtcNow)
                return BadRequest(new ErrorResponseDto { Mesage = "Appointment date cannot be in the past." });

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check for patient active and exist
            await using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @Id AND IsActive = 1", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = createAppointment.IdPatient;
                var count = (int)await cmd.ExecuteScalarAsync()!;
                if (count == 0)
                    return BadRequest(new ErrorResponseDto { Mesage = "Patient does not exist or is not active." });
            }

            // Check for doctor IsActive and if exist
            await using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @Id AND IsActive = 1", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = createAppointment.IdDoctor;
                var count = (int)await cmd.ExecuteScalarAsync()!;
                if (count == 0)
                    return BadRequest(new ErrorResponseDto { Mesage = "Doctor does not exist or is not active." });
            }

            // Check for scheduling conflict
            await using (var cmd = new SqlCommand("""
                SELECT COUNT(1) FROM dbo.Appointments
                WHERE IdDoctor = @IdDoctor
                  AND AppointmentDate = @AppointmentDate
                  AND Status = 'Scheduled';
                """, connection))
            {
                cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = createAppointment.IdDoctor;
                cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = createAppointment.AppointmentDate;
                var count = (int)await cmd.ExecuteScalarAsync()!;
                if (count > 0)
                    return Conflict(new ErrorResponseDto { Mesage = "Doctor already has an appointment at this time." });
            }

            // Insert
            await using (var cmd = new SqlCommand("""
                INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
                OUTPUT INSERTED.IdAppointment
                VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
                """, connection))
            {
                cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = createAppointment.IdPatient;
                cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = createAppointment.IdDoctor;
                cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = createAppointment.AppointmentDate;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = createAppointment.Reason;

                var newId = (int)await cmd.ExecuteScalarAsync()!;
                return CreatedAtAction(nameof(GetAppointment), new { idAppointment = newId }, new { IdAppointment = newId });
            }
            
        }
        
         [HttpPut("{idAppointment:int}")]
        public async Task<IActionResult> UpdateAppointment(int idAppointment, [FromBody] UpdateAppointmentRequestDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 250)
                return BadRequest(new ErrorResponseDto { Mesage = "Reason is required and must be at most 250 characters." });

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Get existing appointment
            DateTime existingDate;
            string existingStatus;
            await using (var cmd = new SqlCommand(
                "SELECT AppointmentDate, Status FROM dbo.Appointments WHERE IdAppointment = @Id", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new ErrorResponseDto { Mesage = $"Appointment {idAppointment} not found." });

                existingDate = reader.GetDateTime(0);
                existingStatus = reader.GetString(1);
            }

            // If completed, block date change
            if (existingStatus == "Completed" && dto.AppointmentDate != existingDate)
                return Conflict(new ErrorResponseDto { Mesage = "Cannot change the date of a completed appointment." });

            // Check patient and doctor active
            await using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @Id AND IsActive = 1", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.PatientId;
                if ((int)await cmd.ExecuteScalarAsync()! == 0)
                    return BadRequest(new ErrorResponseDto { Mesage = "Patient does not exist or is not active." });
            }

            await using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @Id AND IsActive = 1", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = dto.DoctorId;
                if ((int)await cmd.ExecuteScalarAsync()! == 0)
                    return BadRequest(new ErrorResponseDto { Mesage = "Doctor does not exist or is not active." });
            }

            // Check conflict (exclude current appointment)
            await using (var cmd = new SqlCommand("""
                SELECT COUNT(1) FROM dbo.Appointments
                WHERE IdDoctor = @IdDoctor
                  AND AppointmentDate = @AppointmentDate
                  AND Status = 'Scheduled'
                  AND IdAppointment <> @IdAppointment;
                """, connection))
            {
                cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.DoctorId;
                cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
                cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;
                if ((int)await cmd.ExecuteScalarAsync()! > 0)
                    return Conflict(new ErrorResponseDto { Mesage = "Doctor already has an appointment at this time." });
            }

            // Update
            await using (var cmd = new SqlCommand("""
                UPDATE dbo.Appointments
                SET IdPatient = @IdPatient,
                    IdDoctor = @IdDoctor,
                    AppointmentDate = @AppointmentDate,
                    Status = @Status,
                    Reason = @Reason,
                    InternalNotes = @InternalNotes
                WHERE IdAppointment = @IdAppointment;
                """, connection))
            {
                cmd.Parameters.Add("@IdPatient", SqlDbType.Int).Value = dto.PatientId;
                cmd.Parameters.Add("@IdDoctor", SqlDbType.Int).Value = dto.DoctorId;
                cmd.Parameters.Add("@AppointmentDate", SqlDbType.DateTime2).Value = dto.AppointmentDate;
                cmd.Parameters.Add("@Reason", SqlDbType.NVarChar).Value = dto.Reason;
                cmd.Parameters.Add("@IdAppointment", SqlDbType.Int).Value = idAppointment;

                await cmd.ExecuteNonQueryAsync();
            }

            return Ok();
        }
        
        [HttpDelete("{idAppointment:int}")]
        public async Task<IActionResult> DeleteAppointment(int idAppointment)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string status;
            await using (var cmd = new SqlCommand(
                "SELECT Status FROM dbo.Appointments WHERE IdAppointment = @Id", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return NotFound(new ErrorResponseDto { Mesage = $"Appointment {idAppointment} not found." });
                status = reader.GetString(0);
            }

            if (status == "Completed")
                return Conflict(new ErrorResponseDto { Mesage = "Cannot delete a completed appointment." });

            await using (var cmd = new SqlCommand(
                "DELETE FROM dbo.Appointments WHERE IdAppointment = @Id", connection))
            {
                cmd.Parameters.Add("@Id", SqlDbType.Int).Value = idAppointment;
                await cmd.ExecuteNonQueryAsync();
            }

            return NoContent();
        }

    }
}
