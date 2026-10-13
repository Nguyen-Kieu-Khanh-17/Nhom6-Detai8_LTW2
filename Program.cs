using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using StudentManagement.Data;
using StudentManagement.Hubs;
using StudentManagement.Models;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddControllers().AddJsonOptions(options =>
options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
options.LoginPath = "/login.html";
options.AccessDeniedPath = "/forbidden.html";
options.Cookie.Name = "Medika.Auth";
options.SlidingExpiration = true;
options.ExpireTimeSpan = TimeSpan.FromHours(8);
options.Events.OnRedirectToLogin = context =>
{
context.Response.StatusCode = StatusCodes.Status401Unauthorized;
return Task.CompletedTask;
};
options.Events.OnRedirectToAccessDenied = context =>
{
context.Response.StatusCode = StatusCodes.Status403Forbidden;
return Task.CompletedTask;
};
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPatientManager, PatientManager>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<ISupportService, SupportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDoctorLeaveService, DoctorLeaveService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        var migrationSql = @"
        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'Degree')
            ALTER TABLE Doctors ADD Degree NVARCHAR(MAX) NOT NULL DEFAULT 'Bác sĩ CKI';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'ExperienceYears')
            ALTER TABLE Doctors ADD ExperienceYears INT NOT NULL DEFAULT 5;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Doctors') AND name = 'IsActive')
            ALTER TABLE Doctors ADD IsActive BIT NOT NULL DEFAULT 1;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'IsLocked')
            ALTER TABLE Users ADD IsLocked BIT NOT NULL DEFAULT 0;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'CreatedAt')
            ALTER TABLE Users ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Patients') AND name = 'InsuranceNumber')
            ALTER TABLE Patients ADD InsuranceNumber NVARCHAR(MAX) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'BloodPressure')
            ALTER TABLE MedicalRecords ADD BloodPressure NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'HeartRate')
            ALTER TABLE MedicalRecords ADD HeartRate NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Temperature')
            ALTER TABLE MedicalRecords ADD Temperature NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Weight')
            ALTER TABLE MedicalRecords ADD Weight NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'TreatmentPlan')
            ALTER TABLE MedicalRecords ADD TreatmentPlan NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'FollowUpDate')
            ALTER TABLE MedicalRecords ADD FollowUpDate DATETIME2 NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'LabTests')
            ALTER TABLE MedicalRecords ADD LabTests NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
        BEGIN
            CREATE TABLE AuditLogs (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Action NVARCHAR(MAX) NOT NULL,
                PerformedBy NVARCHAR(MAX) NOT NULL,
                Role NVARCHAR(MAX) NOT NULL,
                Details NVARCHAR(MAX) NOT NULL,
                Timestamp DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invoices')
        BEGIN
            CREATE TABLE Invoices (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                AppointmentId INT NOT NULL,
                PatientId INT NOT NULL,
                Amount DECIMAL(18,2) NOT NULL,
                Status NVARCHAR(MAX) NOT NULL,
                PaymentMethod NVARCHAR(MAX) NOT NULL,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                PaidAt DATETIME2 NULL,
                Notes NVARCHAR(MAX) NOT NULL
            );
        END;
        ";

        db.Database.ExecuteSqlRaw(migrationSql);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DbEnsureWarning] {ex.Message}");
    }
}


app.UseSwagger();
app.UseSwaggerUI(options =>
{
options.SwaggerEndpoint("/swagger/v1/swagger.json", "Medika Clinic API v1");
options.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
var path = context.Request.Path.Value?.ToLowerInvariant();
var isAuthenticated = context.User.Identity?.IsAuthenticated ?? false;
var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

if ((path is "/" or "/index.html") && !isAuthenticated)
{
context.Response.Redirect("/login.html");
return;
}

if (path is "/admin.html" && (!isAuthenticated || role != "Admin"))
{
context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
return;
}

if (path is "/doctor.html" && (!isAuthenticated || role != "Doctor"))
{
context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
return;
}

if (path is "/user.html" && (!isAuthenticated || role != "User"))
{
context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
return;
}

if (path is "/support.html" && (!isAuthenticated || role is not ("Support" or "Admin")))
{
context.Response.Redirect(isAuthenticated ? "/forbidden.html" : "/login.html");
return;
}

if ((path is "/" or "/index.html") && role is not ("Receptionist" or "Admin"))
{
context.Response.Redirect(role == "Doctor" ? "/doctor.html" : "/user.html");
return;
}

await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<ClinicHub>("/hubs/clinic");

app.Run();