# Báo cáo tóm tắt kiến trúc kỹ thuật

> Phạm vi: đọc cấu trúc backend ASP.NET Core, mô hình EF Core, controllers/authentication, luồng chatbot và các trang Vanilla JS có liên quan. Báo cáo chỉ ghi kiến trúc, route, tên class, thuộc tính và chữ ký hàm; không sao chép phần thân mã nguồn.

## 1. Cấu trúc dự án

Ứng dụng full-stack trong một project ASP.NET Core Web SDK, target `net10.0`; backend Web API và frontend HTML/CSS/Vanilla JS được phục vụ từ `wwwroot`.

| Thư mục/tệp | Vai trò |
|---|---|
| `Controllers/` | 17 API controllers cho xác thực, người dùng, bệnh nhân, bác sĩ, lịch khám, chat, báo cáo và nghiệp vụ phòng khám. |
| `Services/` | Logic nghiệp vụ và interface; controller gọi các service. Có `ChatbotService`, `ChatService`, `JwtTokenService` và các service cho domain. |
| `Repositories/` | Tầng truy cập dữ liệu tập trung cho xác thực/người dùng (`AuthRepository`, `UserRepository` và interfaces); một số service khác truy cập `AppDbContext` trực tiếp. |
| `Data/` | `AppDbContext`, DbSet và cấu hình quan hệ EF Core. |
| `Models/` | Entity EF Core, enum trạng thái và model nghiệp vụ. |
| `DTOs/` | Request/response DTO cho API; gồm DTO riêng cho chatbot, chat, tài khoản và các domain. |
| `Migrations/` | EF Core migrations và model snapshot. |
| `Middleware/` | `ExceptionMiddleware`, `PageAccessMiddleware`. |
| `Hubs/` | `ClinicHub` cho chức năng cập nhật chat/sự kiện realtime qua SignalR. |
| `wwwroot/` | Trang HTML, CSS và JavaScript thuần; các workspace theo vai trò và widget chatbot. |
| `Program.cs` | Entry point; đăng ký SQL Server, DI, auth, middleware, controllers và SignalR hub. |
| `StudentManagement.csproj` | Khai báo .NET 10 và các dependency EF Core SQL Server, JWT Bearer, BCrypt và Swagger. |
| `appsettings.json` | Connection string, issuer/audience/thời hạn JWT và cấu hình mặc định Gemini; khóa bí mật để trống trong tệp. |

Các vùng chức năng chính: Auth/User, Patient/Doctor, Appointment/Schedule/Doctor Leave, Medical Record/Prescription, Invoice, Notification/Review, Support/Live Chat, Dashboard/Reports/Audit và AI Chatbot.

## 2. Database schema và EF Core models

`AppDbContext` khai báo các DbSet: `Users`, `Patients`, `Doctors`, `DoctorSchedules`, `Appointments`, `MedicalRecords`, `Prescriptions`, `SupportMessages`, `Notifications`, `DoctorLeaveRequests`, `ChatMessages`, `ChatRooms`, `Reviews`, `AuditLogs`, `Invoices`.

### Entity chính

| Entity | Thuộc tính chính |
|---|---|
| `User` | `Id`, `FullName`, `Username`, `PasswordHash`, `Email`, `Role`, `IsLocked`, `CreatedAt`. |
| `Doctor` | `Id`, `FullName`, `Specialty`, `Phone`, `Room`, `Degree`, `ExperienceYears`, `IsActive`. |
| `Patient` | `Id`, `UserId?`, `FullName`, `DateOfBirth`, `Phone`, `Address`, `MedicalHistory`, `InsuranceNumber`. |
| `Appointment` | `Id`, `PatientId`, `DoctorId`, `UserId?`, `ReceptionistId?`, `QueueNumber?`, `AppointmentDate`, `Reason`, `Status`, `CreatedAt`; enum `AppointmentStatus` (`Pending`, `Confirmed`, `CheckedIn`, `Completed`, `Cancelled`, `NeedReschedule`, `Scheduled`). |
| `MedicalRecord` | `Id`, `PatientId`, `DoctorId`, `AppointmentId?`, `ExaminedAt`, `Symptoms`, `Diagnosis`, `Notes`, `BloodPressure`, `HeartRate?`, `Temperature?`, `Weight?`, `TreatmentPlan`, `FollowUpDate?`, `LabTests`. |
| `Prescription` | `Id`, `MedicalRecordId`, `CreatedAt`, `Medicines`, `Instructions`. |
| `ChatRoom` | `Id`, `PatientUserId`, `SupportUserId?`, `RoomCode`, `Status`, `CreatedAt`, `UpdatedAt`. |
| `ChatMessage` | `Id`, `RoomId?`, `SenderId`, `ReceiverId`, `RoomPatientId`, `Content`, `IsFromSupport`, `IsRead`, `SentAt`. |
| `Invoice` | `Id`, `AppointmentId`, `PatientId`, `Amount`, `PaymentMethod`, `Status`, `Notes`, `CreatedAt`; navigation tới `Appointment`, `Patient`. |
| `Review` | `Id`, `DoctorId`, `PatientId`, `UserId?`, `AppointmentId?`, `Rating`, `Comment`, `IsHidden`, `CreatedAt`. |

### Entity khác

- `DoctorSchedule`: `Id`, `DoctorId`, `DayOfWeek`, `StartTime`, `EndTime`, `IsDayOff`.
- `DoctorLeaveRequest`: `Id`, `DoctorId`, `LeaveDate`, `Shift`, `Reason`, `Status`, `CreatedAt`; enum `LeaveRequestStatus`.
- `SupportMessage`: `Id`, `UserId`, `Subject`, `Content`, `Reply?`, `Status`, `CreatedAt`; enum `SupportMessageStatus`.
- `Notification`: `Id`, `UserId`, `Title`, `Content`, `Type`, `IsRead`, `LinkAction?`, `CreatedAt`.
- `AuditLog`: `Id`, `Action`, `PerformedBy`, `Role`, `Details`, `CreatedAt`.

### Quan hệ

- `Appointment` tham chiếu `Patient`, `Doctor`, `Receptionist` (`User`) và `User`; xóa `Patient`/`Doctor` bị giới hạn, hai quan hệ tới `User` dùng `NoAction`.
- `ChatRoom` tham chiếu `User` bằng `PatientUserId` và `SupportUserId`; hành vi xóa lần lượt là `Restrict` và `NoAction`.
- `ChatMessage.RoomId` tham chiếu `ChatRoom`, xóa room sẽ cascade tới message.
- `Notification.UserId` tham chiếu `User`, xóa user cascade notification.
- `Review` tham chiếu `Doctor`, `Patient`, `User`, `Appointment`; delete behavior lần lượt `Restrict`, `Restrict`, `NoAction`, `NoAction`.
- `Invoice` có navigation tới `Appointment` và `Patient`; các cặp ID khác như `DoctorSchedule.DoctorId`, `MedicalRecord.PatientId/DoctorId/AppointmentId`, `Prescription.MedicalRecordId`, `DoctorLeaveRequest.DoctorId` được thể hiện dưới dạng scalar FK-like properties trong model.
- Enum trạng thái `Appointment`, `SupportMessage`, `DoctorLeaveRequest` được EF cấu hình lưu dưới dạng chuỗi. Database provider là SQL Server; project có migrations và `Program.cs` còn gọi `EnsureCreated()` cùng một số SQL DDL tương thích.

**Lưu ý về chatbot:** không có entity `ChatHistory`/lịch sử hội thoại chatbot. `ChatRoom` và `ChatMessage` là dữ liệu chat bệnh nhân–nhân viên hỗ trợ, không phải context được lưu cho Gemini.

## 3. API endpoints và phân quyền

`Program.cs` cấu hình xác thực kết hợp cookie và HTTP Bearer JWT (`CookieOrBearer`): có header Authorization Bearer thì chọn JWT, nếu không thì cookie. JWT kiểm tra issuer, audience, thời hạn và chữ ký; claim role lấy từ `ClaimTypes.Role`. Các role dùng trong mã gồm `Admin`, `Doctor`, `User`, `Receptionist`, `Support`.

Trong bảng, “Đã đăng nhập” nghĩa là `[Authorize]` nhưng không giới hạn role cụ thể; “Công khai” là `[AllowAnonymous]` hoặc không có authorization attribute. Các route dưới đây được nhóm theo controller; prefix là `/api`.

| Controller / route | Method và quyền |
|---|---|
| `auth` | `POST /register` — Công khai; `POST /login` — Công khai; `POST /forgot-password` — Công khai; `POST /change-password` — Đã đăng nhập; `GET /me` — Đã đăng nhập; `POST /logout` — Đã đăng nhập. |
| `users` | `GET /` — Công khai; `POST /` — Công khai; `PUT /{id}` — Công khai; `DELETE /{id}` — `Admin`; `PATCH /{id}/lock` — Công khai; `POST /{id}/reset-password` — Công khai; `PATCH /{id}/role` — Công khai; `POST /login` — Công khai. |
| `appointments` | `GET /`, `GET /{id}` — `User`, `Doctor`, `Receptionist`, `Admin`, `Support`; `POST /` — `User`, `Receptionist`, `Admin`, `Support`; `PUT /{id}` — `Doctor`, `Receptionist`, `Admin`; `PATCH /{id}/confirm`, `PATCH /{id}/check-in`, `DELETE /{id}` — `Receptionist`, `Admin`; `POST /{id}/complete` — `Doctor`, `Admin`; `PATCH /{id}/cancel` — `User`; `PATCH /{id}/reschedule` — `User`, `Doctor`, `Receptionist`, `Admin`. |
| `audit-logs` | `GET /` — `Admin`. |
| `chatbot` | `POST /message`, `GET /prompts` — Công khai; `GET /config`, `POST /config`, `POST /test-connection` — `Admin`. |
| `chat` | `GET /my-room`, `GET /rooms/{roomId}/messages`, `POST /messages` — Đã đăng nhập; `GET /rooms`, `POST /rooms/{roomId}/close` — `Support`, `Admin`. |
| `dashboard` | `GET /` — Đã đăng nhập; `GET /admin` — `Admin`, `Receptionist`; `GET /doctor` — `Doctor`, `Admin`. |
| `doctor-leaves` | `GET /` — Đã đăng nhập; `POST /` — `Doctor`, `Admin`; `PATCH /{id}/approve`, `PATCH /{id}/reject` — `Admin`. |
| `doctors` | `GET /`, `GET /{id}` — Công khai; `POST /`, `PUT /{id}`, `PATCH /{id}/status`, `DELETE /{id}` — `Admin`. |
| `invoices` | `GET /`, `POST /`, `PATCH /{id}/confirm` — `Admin`, `Receptionist`; `GET /appointment/{appointmentId}` — Đã đăng nhập. |
| `medical-records` | `GET /`, `GET /{id}`, `GET /{id}/prescriptions` — `User`, `Doctor`, `Receptionist`, `Admin`; `POST /`, `POST /{id}/prescriptions` — `Doctor`, `Admin`. |
| `notifications` | `GET /`, `GET /unread-count`, `PATCH /{id}/read`, `PATCH /read-all` — Đã đăng nhập. |
| `patients` | `GET /me`, `PUT /me` — Đã đăng nhập; `GET /`, `GET /{id}` — `Receptionist`, `Admin`, `Doctor`; `POST /`, `PUT /{id}`, `DELETE /{id}` — `Receptionist`, `Admin`. |
| `reports` | `GET /export-appointments`, `GET /export-patients` — `Admin`, `Receptionist`. |
| `reviews` | `POST /` — `User`; `GET /doctor/{doctorId}` — Công khai; `GET /`, `PATCH /{id}/toggle-hide`, `DELETE /{id}` — `Admin`. |
| `doctors/{doctorId}/schedule` | `GET /` — Công khai; `POST /`, `DELETE /{id}` — `Doctor`, `Admin`. |
| `support` | `GET /` — Đã đăng nhập; `POST /` — `User`; `PATCH /{id}/reply` — `Support`, `Admin`. |

`GET /api/appointments` nhận query `date`, `status`; `GET /api/doctors` nhận `specialty`; một số endpoint nhận ID/query như trong chữ ký controller.

**Lưu ý trạng thái workspace:** `UsersController.cs` đang có thay đổi chưa commit, bỏ `[Authorize(Roles = "Admin")]` ở cấp controller và chỉ giữ attribute Admin trên `DELETE /{id}`. Vì vậy các route users còn lại được mô tả là công khai theo nội dung hiện tại; không thấy fallback policy toàn cục yêu cầu đăng nhập trong `Program.cs`.

## 4. AI và logic chatbot

- **LLM client:** `Services/ChatbotService.cs`, class `ChatbotService : IChatbotService`; dùng `IHttpClientFactory` gọi Google Gemini `generateContent`. Interface chính: `ProcessMessageAsync(string message, int? userId = null)`, `GetSuggestedPrompts()`, `GetConfigAsync()`, `UpdateConfigAsync(UpdateChatbotConfigDto updateDto)`, `TestGeminiConnectionAsync()`.
- **Luồng xử lý:** `ProcessMessageAsync` chuẩn hóa câu hỏi; kiểm tra dấu hiệu cấp cứu trước; truy vấn danh sách bác sĩ; nếu Gemini có API key và đang bật thì gọi model đã cấu hình. Lỗi/không có phản hồi Gemini dẫn tới `ProcessInternalFallbackAsync(...)`, xử lý nội bộ cho lời chào, triệu chứng/chuyên khoa, thông tin phòng khám/BHYT, tìm bác sĩ, hướng dẫn đặt lịch và tra cứu lịch hẹn.
- **Prompt:** `CallGeminiApiAsync(string userMessage, List<Doctor> doctors, string apiKey)` ghép system prompt tại thời điểm gọi gồm vai trò/trách nhiệm trợ lý, thông tin phòng khám và danh sách bác sĩ. Request gửi một lượt `systemPrompt + câu hỏi hiện tại`, với generation config; không gửi chuỗi lượt chat trước.
- **Ngữ cảnh/lưu trữ:** frontend chỉ giữ các tin nhắn trong DOM của widget hiện tại. Mỗi request chatbot chỉ có `{ message }`; backend không đọc/ghi lịch sử chatbot. `userId` tùy chọn lấy từ claim đăng nhập và hiện được dùng trong fallback để truy vấn một số lịch hẹn sắp tới. Cấu hình cập nhật qua Admin được giữ bằng static fields trong tiến trình chạy; API key cũng có thể đọc từ cấu hình/User Secrets/environment.
- **Response contract:** `ChatbotMessageResponse` gồm `Reply`, `Intent`, `Source`, `Doctors`, `QuickActions`; DTO request là `ChatbotMessageRequest.Message`.
- **Chat trực tiếp với nhân viên là luồng riêng:** `ChatService` lưu `ChatMessage` liên kết `ChatRoom`; không dùng các bản ghi này làm prompt history cho AI. `ClinicHub` cung cấp realtime chat/event.

## 5. Frontend integration

- `wwwroot/chatbot.js` tạo widget dùng chung, tải prompt gợi ý từ `GET /api/chatbot/prompts`, gửi tin nhắn tới `POST /api/chatbot/message`, sau đó dựng câu trả lời, nhãn nguồn, bác sĩ liên quan và quick actions từ response.
- Các trang `index.html`, `login.html`, `user.html`, `doctor.html`, `support.html` nạp chatbot script/style để hiển thị widget.
- `wwwroot/user-workspace.js` xử lý chat bệnh nhân với nhân viên qua `/api/chat/...`; `wwwroot/support-workspace.js` cung cấp giao diện phía Support. Đây là live chat có lưu lịch sử DB và nhận cập nhật SignalR, tách biệt với AI chatbot.
- `wwwroot/admin-workspace.js` tải/cập nhật cấu hình Gemini và thử kết nối qua các endpoint `/api/chatbot/config` và `/api/chatbot/test-connection`.
