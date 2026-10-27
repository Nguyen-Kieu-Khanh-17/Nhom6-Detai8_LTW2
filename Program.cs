using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using StudentManagement.Data;
using StudentManagement.Hubs;
using StudentManagement.Middleware;
using StudentManagement.Models;
using StudentManagement.Repositories;
using StudentManagement.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddSignalR();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddControllers().AddJsonOptions(options =>
options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Medika Clinic API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập trực tiếp chuỗi Token thu được sau khi Login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");
var issuer = jwtSettings["Issuer"]
    ?? throw new InvalidOperationException("JwtSettings:Issuer chưa được cấu hình.");
var audience = jwtSettings["Audience"]
    ?? throw new InvalidOperationException("JwtSettings:Audience chưa được cấu hình.");
var expiryMinutes = jwtSettings.GetValue<int?>("ExpiryMinutes")
    ?? throw new InvalidOperationException("JwtSettings:ExpiryMinutes chưa được cấu hình.");

if (Encoding.UTF8.GetByteCount(secretKey) < 32)
    throw new InvalidOperationException("JwtSettings:SecretKey phải có ít nhất 32 byte.");
if (expiryMinutes <= 0)
    throw new InvalidOperationException("JwtSettings:ExpiryMinutes phải lớn hơn 0.");

const string cookieOrBearerScheme = "CookieOrBearer";
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = cookieOrBearerScheme;
    options.DefaultAuthenticateScheme = cookieOrBearerScheme;
    options.DefaultChallengeScheme = cookieOrBearerScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddPolicyScheme(cookieOrBearerScheme, cookieOrBearerScheme, options =>
{
    options.ForwardDefaultSelector = context =>
        context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
})
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
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
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
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileService, FileService>();

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

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'DoctorId')
            ALTER TABLE Users ADD DoctorId INT NULL;

        IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Users_Doctors_DoctorId')
            EXEC(N'ALTER TABLE Users ADD CONSTRAINT FK_Users_Doctors_DoctorId FOREIGN KEY (DoctorId) REFERENCES Doctors(Id)');

        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Users_DoctorId' AND object_id = OBJECT_ID('Users'))
            EXEC(N'CREATE UNIQUE INDEX IX_Users_DoctorId ON Users(DoctorId) WHERE DoctorId IS NOT NULL');

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Patients') AND name = 'InsuranceNumber')
            ALTER TABLE Patients ADD InsuranceNumber NVARCHAR(MAX) NOT NULL DEFAULT '';

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'BloodPressure')
            ALTER TABLE MedicalRecords ADD BloodPressure NVARCHAR(MAX) NULL;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'HeartRate')
            ALTER TABLE MedicalRecords ADD HeartRate INT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'HeartRate' AND t.name LIKE '%varchar%')
        BEGIN
            UPDATE MedicalRecords SET HeartRate = NULL WHERE HeartRate = '' OR ISNUMERIC(HeartRate) = 0;
            ALTER TABLE MedicalRecords ALTER COLUMN HeartRate INT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Temperature')
            ALTER TABLE MedicalRecords ADD Temperature FLOAT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'Temperature' AND t.name LIKE '%varchar%')
        BEGIN
            UPDATE MedicalRecords SET Temperature = NULL WHERE Temperature = '' OR ISNUMERIC(Temperature) = 0;
            ALTER TABLE MedicalRecords ALTER COLUMN Temperature FLOAT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('MedicalRecords') AND name = 'Weight')
            ALTER TABLE MedicalRecords ADD Weight FLOAT NULL;
        ELSE IF EXISTS (SELECT * FROM sys.columns c JOIN sys.types t ON c.user_type_id = t.user_type_id WHERE c.object_id = OBJECT_ID('MedicalRecords') AND c.name = 'Weight' AND t.name LIKE '%varchar%')
        BEGIN
            UPDATE MedicalRecords SET Weight = NULL WHERE Weight = '' OR ISNUMERIC(Weight) = 0;
            ALTER TABLE MedicalRecords ALTER COLUMN Weight FLOAT NULL;
        END;

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reviews')
        BEGIN
            CREATE TABLE Reviews (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                DoctorId INT NOT NULL,
                PatientId INT NOT NULL,
                UserId INT NULL,
                AppointmentId INT NULL,
                Rating INT NOT NULL,
                Comment NVARCHAR(MAX) NOT NULL DEFAULT '',
                IsHidden BIT NOT NULL DEFAULT 0,
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_Reviews_Doctors_DoctorId FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
                CONSTRAINT FK_Reviews_Patients_PatientId FOREIGN KEY (PatientId) REFERENCES Patients(Id),
                CONSTRAINT FK_Reviews_Users_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
                CONSTRAINT FK_Reviews_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE NO ACTION
            );
        END;

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
                CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;

        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'CreatedAt')
        BEGIN
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'Timestamp')
            BEGIN
                EXEC(N'ALTER TABLE AuditLogs ADD CreatedAt DATETIME2 NULL');
                EXEC(N'UPDATE AuditLogs SET CreatedAt = [Timestamp] WHERE CreatedAt IS NULL');
                EXEC(N'ALTER TABLE AuditLogs ALTER COLUMN CreatedAt DATETIME2 NOT NULL');
                EXEC(N'ALTER TABLE AuditLogs ADD CONSTRAINT DF_AuditLogs_CreatedAt DEFAULT GETDATE() FOR CreatedAt');
            END
            ELSE
            BEGIN
                ALTER TABLE AuditLogs ADD CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE();
            END;
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

        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StoredFiles')
        BEGIN
            CREATE TABLE StoredFiles (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                FileName NVARCHAR(MAX) NOT NULL,
                ContentType NVARCHAR(MAX) NOT NULL,
                Content VARBINARY(MAX) NOT NULL,
                Length BIGINT NOT NULL,
                UploadedAt DATETIME2 NOT NULL DEFAULT GETDATE()
            );
        END;
        ";

        db.Database.ExecuteSqlRaw(migrationSql);

        var docUser = db.Users.FirstOrDefault(u => u.Username == "doctor" && u.DoctorId == null);
        if (docUser != null)
        {
            var firstDoc = db.Doctors.FirstOrDefault(d => d.IsActive);
            if (firstDoc != null)
            {
                docUser.DoctorId = firstDoc.Id;
                db.SaveChanges();
            }
        }
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

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PageAccessMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<ClinicHub>("/hubs/clinic");

app.Run();