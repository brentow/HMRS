using System;
using MySqlConnector;
using HRMS.Model;

var connString = DbConfig.ConnectionString;
await using var conn = new MySqlConnection(connString);
await conn.OpenAsync();

string[] queries =
{
    "SELECT COUNT(*) FROM biometric_devices;",
    "SELECT COUNT(*) FROM biometric_enrollments;",
    "SELECT COUNT(*) FROM attendance_logs;",
    "SELECT COUNT(*) FROM attendance_adjustments;",
    "SELECT COUNT(*) FROM attendance_remarks;",
    "SELECT COUNT(*) FROM dtr_monthly_certifications;"
};

foreach (var q in queries)
{
    await using var cmd = new MySqlCommand(q, conn);
    var result = await cmd.ExecuteScalarAsync();
    Console.WriteLine($"{q} => {result}");
}

string[] inspect =
{
    "SELECT device_id, device_name, serial_no, location, ip_address, is_active FROM biometric_devices ORDER BY device_id LIMIT 20;",
    "SELECT be.enrollment_id, e.employee_no, be.biometric_user_id, be.status FROM biometric_enrollments be JOIN employees e ON e.employee_id=be.employee_id ORDER BY be.enrollment_id LIMIT 20;",
    "SELECT al.log_id, e.employee_no, al.log_time, al.log_type, al.source FROM attendance_logs al JOIN employees e ON e.employee_id=al.employee_id ORDER BY al.log_id LIMIT 40;",
    "SELECT adjustment_id, employee_id, work_date, status, reason FROM attendance_adjustments ORDER BY adjustment_id LIMIT 20;",
    "SELECT remark_id, employee_id, work_date, remark_type, details FROM attendance_remarks ORDER BY remark_id LIMIT 20;",
    "SELECT certification_id, employee_id, yr, mo, remarks FROM dtr_monthly_certifications ORDER BY certification_id LIMIT 20;"
};

foreach (var q in inspect)
{
    Console.WriteLine("---");
    Console.WriteLine(q);
    await using var cmd = new MySqlCommand(q, conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    var fieldCount = reader.FieldCount;
    while (await reader.ReadAsync())
    {
        for (var i = 0; i < fieldCount; i++)
        {
            Console.Write($"{reader.GetName(i)}={reader.GetValue(i)} ");
        }
        Console.WriteLine();
    }
}
