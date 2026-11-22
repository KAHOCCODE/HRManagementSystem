# Human Resources Management System

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-512BD4)](https://learn.microsoft.com/aspnet/core/mvc/)
[![Entity Framework Core](https://img.shields.io/badge/Entity%20Framework-Core-512BD4)](https://learn.microsoft.com/ef/core/)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-CC2927)](https://www.microsoft.com/sql-server)

Human Resources Management System is an ASP.NET Core MVC application for managing employees, attendance, leave requests, payroll, organizational roles, approvals, and HR reports.

The project supports role-based access and hierarchical management workflows for employees, employee managers, line managers, and directors.

## Portfolio walkthrough

[Open the interactive project walkthrough](https://hr-management-do-hoang-ka.lemonyfjord257.chatgpt.site)

This team project includes Đỗ Hoàng Ka. The walkthrough uses synthetic data, opens without an account, and simulates payroll approvals and return paths in the browser. It links to the corresponding ASP.NET Core implementation.

- **Payroll and authorization** — [visibility rules](HRManagementSystem/Authorization/PayrollAccess.cs), [line-manager actions](HRManagementSystem/ApiControllers/PayrollManagerApiController.cs), and [director approvals](HRManagementSystem/ApiControllers/PayrollDirectorApiController.cs).
- **Excel import** — [preview, row validation, partial saves, worksite and manager assignments](HRManagementSystem/Controllers/EmployeeImportController.cs).
- **Holiday approval** — [preserve existing attendance and respect payroll period locks](HRManagementSystem/ApiControllers/HolidayManagerApiController.cs).

The audited SQLite demo was exercised with five sample roles across 81 page visits. Workflow checks included Excel import, holiday approval and cancellation, and payroll review, returns and resubmission. Mid-period pay or worksite changes currently stop automated calculation for review.

Use the [local demo instructions below](#demo-hosting-on-render) for the ASP.NET Core application. The walkthrough is independent of the hosted HR instance.


---

## Table of Contents

- [Overview](#overview)
- [Technologies](#technologies)
- [Main Features](#main-features)
- [Roles and Authorization](#roles-and-authorization)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Database Setup](#database-setup)
- [Application Configuration](#application-configuration)
- [Running the Application](#running-the-application)
- [Reports and Export](#reports-and-export)
- [Development Notes](#development-notes)
- [Roadmap](#roadmap)
- [Team](#team)
- [Contributing](#contributing)
- [Project Status](#project-status)
- [Disclaimer](#disclaimer)

---

## Overview

The application provides a centralized platform for common human resources operations:

- Employee information management
- Organizational role and permission management
- Attendance management
- Annual leave and holiday request processing
- Multi-level approval workflows
- Payroll calculation and approval
- Employee hierarchy and subordinate management
- Email notifications
- Excel and PDF report generation
- MVC pages and API controllers

The application uses ASP.NET Core MVC, Entity Framework Core, SQL Server, Cookie Authentication, and policy-based authorization.

---

## Technologies

### Backend

- C#
- .NET 10
- ASP.NET Core MVC
- ASP.NET Core Web API
- Entity Framework Core
- LINQ
- Cookie Authentication
- Policy-based Authorization
- Hosted Services

### Database

- Microsoft SQL Server
- Entity Framework Core Migrations

### Frontend

- Razor Views
- HTML5
- CSS3
- JavaScript
- Bootstrap

### Libraries and Tools

- BCrypt.Net for password hashing
- ClosedXML for spreadsheet processing
- EPPlus for Excel export
- QuestPDF for PDF report generation
- NETCore.MailKit for email delivery
- Visual Studio 2022
- Git and GitHub

---

## Main Features

### Employee Management

- Create and update employee profiles
- Manage employee information and account status
- Generate employee usernames
- Organize employees by position and management level
- View and manage subordinate employees based on authorization policies

### Authentication and Authorization

- Cookie-based authentication
- Role-based access control
- Policy-based authorization
- Access-denied handling
- Permission checks for employee management operations

### Attendance Management

- Track employee attendance
- Manage attendance records
- Restrict attendance operations based on assigned permissions

### Leave and Holiday Management

- Manage annual leave balances
- Create leave and holiday requests
- Approve requests through authorized management levels
- Run scheduled annual leave processing tasks

### Payroll Management

- Calculate employee payroll
- Create payroll records
- Support multi-level payroll approval
- Generate payroll reports

### Notifications and Reports

- Send HR-related email notifications
- Export Excel reports
- Generate PDF documents and reports

---

## Roles and Authorization

The system defines several organizational roles:

| Role | Description |
|---|---|
| `EMPLOYEE` | Standard employee access |
| `EMPLOYEE_MANAGER` | Employee management access |
| `LINE_MANAGER` | Line-level management and approval access |
| `DIRECTOR` | Director-level management and approval access |

The project also uses custom authorization policies for operations such as:

- Viewing and creating employee records
- Assigning employee roles
- Managing attendance
- Creating and approving leave requests
- Creating payroll records
- Approving payroll through multiple management levels

---

## Project Structure

```text
HRManagementSystem/
├── ApiControllers/
├── Authorization/
├── Controllers/
├── Data/
├── DTOs/
├── Helpers/
├── Migrations/
├── Models/
├── Properties/
├── Services/
├── Status/
├── Views/
├── wwwroot/
├── Program.cs
├── appsettings.json
└── HRManagementSystem.csproj

HRManagementSystem.sln
README.md
```

### Main Directories

| Directory | Purpose |
|---|---|
| `ApiControllers` | API endpoints |
| `Authorization` | Custom requirements, handlers, and permission policies |
| `Controllers` | MVC request handling |
| `Data` | Entity Framework Core database context and data configuration |
| `DTOs` | Data transfer objects |
| `Helpers` | Shared utility classes |
| `Migrations` | Entity Framework Core database migrations |
| `Models` | Application and database models |
| `Services` | Business logic, payroll, email, reporting, and scheduled tasks |
| `Status` | Shared status definitions |
| `Views` | Razor Views |
| `wwwroot` | Static files such as CSS, JavaScript, images, and avatars |

---

## Getting Started

### Prerequisites

Install the following tools:

- [Visual Studio 2022](https://visualstudio.microsoft.com/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Microsoft SQL Server](https://www.microsoft.com/sql-server/sql-server-downloads)
- [SQL Server Management Studio](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms)
- [Git](https://git-scm.com/)

Verify the installed .NET SDK:

```bash
dotnet --version
```

The result should be version `10.x`.

### Clone the Repository

```bash
git clone https://github.com/KAHOCCODE/HRManagementSystem.git
cd HRManagementSystem
```

Restore dependencies:

```bash
dotnet restore HRManagementSystem.sln
```

Build the solution:

```bash
dotnet build HRManagementSystem.sln
```

---

## Database Setup

The standard application uses SQL Server and reads the connection string from:

```text
ConnectionStrings:Db_HR
```

### Apply Entity Framework Core Migrations

Install the Entity Framework Core command-line tool when necessary:

```bash
dotnet tool install --global dotnet-ef
```

Apply the available migrations:

```bash
dotnet ef database update --project HRManagementSystem/HRManagementSystem.csproj
```

These migrations start with changes to an existing schema. Restore the original SQL Server database, including its triggers and migration history, before applying pending migrations. For a new database with synthetic records, use the demo mode below.

---

## Application Configuration

Use placeholders in tracked configuration files and store machine-specific or sensitive values outside the repository.

Example `appsettings.json` structure:

```json
{
  "ConnectionStrings": {
    "Db_HR": "CONFIGURE_WITH_USER_SECRETS"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "Port": 587,
    "SenderEmail": "CONFIGURE_WITH_USER_SECRETS",
    "SenderPassword": "CONFIGURE_WITH_USER_SECRETS"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

For local development, configuration can be stored with .NET User Secrets:

```bash
dotnet user-secrets init --project HRManagementSystem/HRManagementSystem.csproj
```

```bash
dotnet user-secrets set "ConnectionStrings:Db_HR" "YOUR_SQL_SERVER_CONNECTION_STRING" --project HRManagementSystem/HRManagementSystem.csproj
```

```bash
dotnet user-secrets set "EmailSettings:SenderEmail" "YOUR_EMAIL" --project HRManagementSystem/HRManagementSystem.csproj
```

```bash
dotnet user-secrets set "EmailSettings:SenderPassword" "YOUR_EMAIL_APP_PASSWORD" --project HRManagementSystem/HRManagementSystem.csproj
```

---

## Running the Application

Run from the command line:

```bash
dotnet run --project HRManagementSystem/HRManagementSystem.csproj
```

Alternatively:

1. Open `HRManagementSystem.sln` in Visual Studio 2022.
2. Set `HRManagementSystem` as the startup project.
3. Select the HTTPS launch profile.
4. Run the application.

The local URL is displayed in the Visual Studio output window or terminal.

---

## Demo hosting on Render

Deploy the branch containing this version as a [Render Blueprint](https://dashboard.render.com/blueprints).
The repository's `render.yaml` selects a free Docker web service and enables demo mode.
Render generates `Demo__Password` automatically. In the deployed service, open **Environment** to view the value.
Use that password with any of these sample accounts:

| Account | Role |
| --- | --- |
| `demo.employee` | Nhân viên |
| `demo.hr` | Quản lý nhân sự |
| `demo.manager` | Trưởng phòng |
| `demo.payroll` | Kế toán lương |
| `demo.director` | Giám đốc |

Demo mode creates a SQLite database with synthetic employee, attendance, leave and payroll records.
On the free service, changes to demo data and uploaded files are lost when Render restarts
or redeploys the service. Use test data only. SQL Server's legacy triggers and the annual
leave background job are outside the demo. The demo also runs without mail service credentials.

## Reports and Export

The project includes libraries for:

- Excel report generation with EPPlus
- Spreadsheet processing with ClosedXML
- PDF document generation with QuestPDF

Available reports depend on the current user role and assigned authorization policies.

---

## Development Notes

The project is under active maintenance and refactoring.

Current improvement areas include:

- Standardizing project and namespace naming
- Improving service registration and hosted service lifetimes
- Moving machine-specific configuration outside source control
- Improving validation and error handling
- Adding automated build validation
- Expanding automated tests
- Improving API documentation

---

## Roadmap

### Configuration and Security

- Move local configuration to User Secrets
- Remove machine-specific connection strings
- Review authentication cookies and session settings
- Add secret scanning and push protection

### Architecture

- Standardize service lifetimes
- Separate business logic from controllers
- Improve DTO and ViewModel usage
- Review scheduled background services
- Improve error logging

### Testing

- Add payroll calculation tests
- Add leave approval tests
- Add authorization policy tests
- Add attendance workflow tests
- Add integration tests

### DevOps and Documentation

- Add GitHub Actions build validation
- Add screenshots
- Add an entity relationship diagram
- Add sample accounts and role descriptions
- Add API documentation

---

## Team

This project was originally developed by:

- Nguyễn Hoà Khang — Leader
- Đỗ Hoàng Ka — Member
- Phan Nguyễn Đăng Khoa — Member
- Trương Minh Đức — Member

---

## Contributing

Contributions are welcome.

Recommended workflow:

1. Clone or fork the repository.
2. Create a feature branch.
3. Make focused changes.
4. Build and test locally.
5. Commit using a clear message.
6. Open a pull request.

Example:

```bash
git switch -c feature/improve-attendance-management
git add .
git commit -m "feat: improve attendance management"
git push -u origin feature/improve-attendance-management
```

Avoid committing passwords, access tokens, generated build folders, or machine-specific configuration.

---

## Project Status

```text
Status: Active Development and Refactoring
Framework: ASP.NET Core MVC
Runtime: .NET 10
Database: Microsoft SQL Server
Authentication: Cookie Authentication
Authorization: Role-based and Policy-based
Deployment: Local Development
```

---

## Disclaimer

This project was created for educational and development purposes.

Additional security review, automated testing, monitoring, and production configuration are required before using the application in a production environment.

## HR controls review

Use a fresh demo database when verifying this branch so the demo initializer can add a working schedule and example overtime rates.

- In **Cài Đặt → Cài đặt cấu hình lương**, open the statutory wage reference for 2026 and 2027. Set `HR__WorkRegionsByDepartment__<department-id>` to the verified region (1–4) when everyone in that department works in the same region. Departments spanning multiple worksites still need per-worksite assignments.
- Salary setup uses the effective wage, reference amount and insurance rate records for the selected date. For income with allowances or bonuses, configure `HR__InsurableAdditionsByEmployee__<employee-id>__<yyyy-MM>` after classifying the amounts for that month. The current payroll calculation still uses stored insurance deductions. Reconciliation and rules for partial months and eligibility are required before production.
- Directors can reissue activation from **Cài Đặt → Cấp lại mật khẩu**. For new, imported and reset accounts, recipients use the private link and temporary password sent to the email in their employee record. Both expire within 24 hours. Successful activation invalidates the link.
- On Render Free, configure `EmailSettings__ResendApiKey` and `EmailSettings__SenderEmail` from a verified sending domain to deliver activation and payroll emails through the Resend HTTPS API.
- For deployments that support outbound SMTP, set `EmailSettings__SmtpServer`, `EmailSettings__Port`, `EmailSettings__SenderEmail` and `EmailSettings__SenderPassword` in the deployment environment. Set `Hosting__PublicBaseUrl` to the public HTTPS origin when the platform does not provide `RENDER_EXTERNAL_URL`. For persistent or multi-instance hosting, keep a shared, protected ASP.NET Core Data Protection key ring so pending links remain valid across restarts.
- The revised attendance submission handles daytime entries with the existing noon break and a standard limit of 200 annual overtime hours. Night, overnight and exceptional 300-hour schedules require their own calculation and approval flow.
- Resident income tax uses the 2026 statutory brackets and deductions for 2026–2027. Part-year residency, tax exemptions for eligible overtime and night work, dependent eligibility by date and year-end settlement remain to be implemented.
- Annual leave uses actual work anniversaries when calculating seniority. Rules for partial years, statutory job categories, anniversary updates during a year, carry-over and termination settlement still require implementation.
- Before production, add verified company and worksite records, full record-level access controls and configuration audit history, payroll closing and adjustments, and durable encrypted storage with backup and restore. Validate against a de-identified payroll period with the company's HR and accounting team.

### Worksites and transfers

Sign in as director and open **Cài Đặt → Chi nhánh và nơi làm việc**. Add the head office and each branch separately. Select the Ho Chi Minh City ward, commune or special zone from the 168-area catalogue. The system applies the corresponding 2026 and 2027 schedules. Verify Tây Thạnh for 2026 before setting its pending region in the branch editor. Outside Ho Chi Minh City, enter the independently verified region for each year. Multi-region industrial zones and other special rules require separate review.

Use **Gán nhân viên** to set a worksite and effective date. The most recent effective assignment applies. Use a new assignment when a worker transfers. Payroll stops for review when a transfer falls within a pay month or when the site has no verified region for that year. Individual employment periods spanning multiple worksites and hourly contracts still need their own payroll handling.

### Employee directory and organization chart

The demo represents the fictional Công ty TNHH Logistics An Phú Nam. A fresh database starts with 24 employee profiles and original portrait assets in `wwwroot/images/employees/an-phu-nam`, seven departments, and three Ho Chi Minh City worksites. The five sign-in accounts listed above remain available. Attendance and leave run through 20 November 2026, with payrolls for completed months while each employee was employed.

Directors and HR managers can open **Nhân viên → Quản lý nhân viên** to create or edit profiles. During onboarding, choose the worksite and direct manager before adding the contract and pay information. Open **Nhân viên → Sơ đồ tổ chức** to see the reporting lines. Update the direct manager in the employee profile to move their branch of the chart. Set `Organization__Name` to display the name of your organization in a separate deployment.

Existing SQLite demo databases gain the new table on startup and retain their profiles. Point `Demo__DatabasePath` to a fresh database to initialize the expanded dataset. For SQL Server, apply `AddWorksites` and `AddEmployeeReportingLines` after the preceding migrations. The SQL Server migration sequence assumes the original database schema. Supply the SQL Server connection via `ConnectionStrings__Db_HR` and email credentials through deployment variables.
