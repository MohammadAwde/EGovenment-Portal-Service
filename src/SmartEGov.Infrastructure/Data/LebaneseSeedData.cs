using Microsoft.EntityFrameworkCore;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Data;

public static class LebaneseSeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.Departments.AnyAsync())
            return;

        // --- Departments ---
        var departments = new List<Department>
        {
            new() { Name = "General Directorate of Personal Status", NameArabic = "???????? ?????? ??????? ???????", Description = "Handles civil registry, birth, marriage, and death certificates.", ContactEmail = "civilstatus@gov.lb", ContactPhone = "+961-1-425700" },
            new() { Name = "General Directorate of General Security", NameArabic = "???????? ?????? ????? ?????", Description = "Manages passports, residency permits, and entry/exit records.", ContactEmail = "gs@general-security.gov.lb", ContactPhone = "+961-1-425610" },
            new() { Name = "Ministry of Interior and Municipalities", NameArabic = "????? ???????? ?????????", Description = "Oversees national ID cards and municipal affairs.", ContactEmail = "info@interior.gov.lb", ContactPhone = "+961-1-345800" },
            new() { Name = "Traffic and Vehicle Registration Department", NameArabic = "????? ????? ???????? ?????????", Description = "Manages driver licenses and vehicle registrations.", ContactEmail = "traffic@isf.gov.lb", ContactPhone = "+961-1-386560" },
            new() { Name = "Ministry of Labor", NameArabic = "????? ?????", Description = "Issues work permits and regulates employment.", ContactEmail = "info@labor.gov.lb", ContactPhone = "+961-1-556801" },
            new() { Name = "Ministry of Finance", NameArabic = "????? ???????", Description = "Handles tax payments, property registration, and fiscal matters.", ContactEmail = "info@finance.gov.lb", ContactPhone = "+961-1-981001" },
            new() { Name = "Order of Engineers and Architects", NameArabic = "????? ????????? ???????????", Description = "Reviews and approves building permits and urban planning.", ContactEmail = "info@oea.org.lb", ContactPhone = "+961-1-850111" },
        };

        context.Departments.AddRange(departments);
        await context.SaveChangesAsync();

        // --- Approval Workflows ---
        var wfStandard = new ApprovalWorkflow { Name = "Standard Review", Description = "Single-step officer review for routine services.", TotalSteps = 1 };
        var wfTwoStep = new ApprovalWorkflow { Name = "Two-Step Verification", Description = "Officer review followed by department head approval.", TotalSteps = 2 };
        var wfThreeStep = new ApprovalWorkflow { Name = "Full Processing", Description = "Document verification, officer review, and director approval.", TotalSteps = 3 };

        context.ApprovalWorkflows.AddRange(wfStandard, wfTwoStep, wfThreeStep);
        await context.SaveChangesAsync();

        // Re-fetch departments by name for FK assignment
        var deptPersonalStatus = departments[0];
        var deptGeneralSecurity = departments[1];
        var deptInterior = departments[2];
        var deptTraffic = departments[3];
        var deptLabor = departments[4];
        var deptFinance = departments[5];
        var deptEngineers = departments[6];

        // --- Government Services with Required Documents ---
        var services = new List<GovernmentService>
        {
            // 1. National ID Card Application
            new()
            {
                Name = "National ID Card Application",
                NameArabic = "??? ????? ???? ?????",
                Description = "Apply for a new Lebanese national identity card or renew an existing one.",
                Category = "Civil Status",
                Fee = 100_000m,
                EstimatedDays = 15,
                DepartmentId = deptInterior.Id,
                ApprovalWorkflowId = wfTwoStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Family Civil Record", Description = "????? ??? ????? — recent extract (less than 6 months old).", IsMandatory = true },
                    new() { DocumentName = "Personal Photo", Description = "Two recent passport-size photos (4×6 cm, white background).", IsMandatory = true },
                    new() { DocumentName = "Previous ID Copy", Description = "Copy of the expiring or lost national ID, if available.", IsMandatory = false },
                }
            },

            // 2. Passport Application
            new()
            {
                Name = "Passport Application",
                NameArabic = "??? ???? ???",
                Description = "Apply for a new Lebanese biometric passport (5-year or 10-year validity).",
                Category = "Travel Documents",
                Fee = 600_000m,
                EstimatedDays = 20,
                DepartmentId = deptGeneralSecurity.Id,
                ApprovalWorkflowId = wfThreeStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "National ID Copy", Description = "Clear photocopy of a valid Lebanese national ID card.", IsMandatory = true },
                    new() { DocumentName = "Family Civil Record", Description = "????? ??? ????? — recent individual extract.", IsMandatory = true },
                    new() { DocumentName = "Personal Photo", Description = "Four biometric photos (3.5×4.5 cm, white background).", IsMandatory = true },
                    new() { DocumentName = "Previous Passport Copy", Description = "Copy of the current or most recent passport, if any.", IsMandatory = false },
                    new() { DocumentName = "Military Service Certificate", Description = "Proof of military service status for males aged 18–30.", IsMandatory = false },
                }
            },

            // 3. Driver License Application
            new()
            {
                Name = "Driver License Application",
                NameArabic = "??? ???? ?????",
                Description = "Apply for a new Lebanese driving license or renew an existing one.",
                Category = "Transport",
                Fee = 300_000m,
                EstimatedDays = 10,
                DepartmentId = deptTraffic.Id,
                ApprovalWorkflowId = wfTwoStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "National ID Copy", Description = "Clear photocopy of a valid national ID.", IsMandatory = true },
                    new() { DocumentName = "Medical Certificate", Description = "Medical fitness certificate from an authorized physician.", IsMandatory = true },
                    new() { DocumentName = "Personal Photo", Description = "Two recent passport-size photos.", IsMandatory = true },
                    new() { DocumentName = "Previous License Copy", Description = "Copy of existing license (for renewal).", IsMandatory = false },
                }
            },

            // 4. Vehicle Registration
            new()
            {
                Name = "Vehicle Registration",
                NameArabic = "????? ?????",
                Description = "Register a new or imported vehicle with the Lebanese traffic department.",
                Category = "Transport",
                Fee = 500_000m,
                EstimatedDays = 7,
                DepartmentId = deptTraffic.Id,
                ApprovalWorkflowId = wfTwoStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "National ID Copy", Description = "Owner's national ID photocopy.", IsMandatory = true },
                    new() { DocumentName = "Vehicle Purchase Invoice", Description = "Original sales invoice or customs clearance document.", IsMandatory = true },
                    new() { DocumentName = "Insurance Certificate", Description = "Valid third-party or comprehensive vehicle insurance.", IsMandatory = true },
                    new() { DocumentName = "Mechanical Inspection Report", Description = "Vehicle inspection (contrôle mécanique) certificate.", IsMandatory = true },
                }
            },

            // 5. Birth Certificate Request
            new()
            {
                Name = "Birth Certificate Request",
                NameArabic = "??? ????? ?????",
                Description = "Request an official Lebanese birth certificate extract.",
                Category = "Civil Status",
                Fee = 50_000m,
                EstimatedDays = 5,
                DepartmentId = deptPersonalStatus.Id,
                ApprovalWorkflowId = wfStandard.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Family Civil Record", Description = "????? ??? ????? showing the birth entry.", IsMandatory = true },
                    new() { DocumentName = "National ID Copy", Description = "Requester's national ID photocopy.", IsMandatory = true },
                }
            },

            // 6. Marriage Certificate Request
            new()
            {
                Name = "Marriage Certificate Request",
                NameArabic = "??? ????? ????",
                Description = "Request an official marriage certificate or registration of a new marriage.",
                Category = "Civil Status",
                Fee = 75_000m,
                EstimatedDays = 7,
                DepartmentId = deptPersonalStatus.Id,
                ApprovalWorkflowId = wfTwoStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Family Civil Record", Description = "Individual extracts for both spouses.", IsMandatory = true },
                    new() { DocumentName = "National ID Copy", Description = "National ID copies for both spouses.", IsMandatory = true },
                    new() { DocumentName = "Religious Marriage Certificate", Description = "Certificate from the religious authority that officiated the marriage.", IsMandatory = true },
                    new() { DocumentName = "Personal Photo", Description = "Two photos of each spouse.", IsMandatory = true },
                }
            },

            // 7. Death Certificate Request
            new()
            {
                Name = "Death Certificate Request",
                NameArabic = "??? ????? ????",
                Description = "Request an official death certificate for a deceased Lebanese citizen.",
                Category = "Civil Status",
                Fee = 50_000m,
                EstimatedDays = 5,
                DepartmentId = deptPersonalStatus.Id,
                ApprovalWorkflowId = wfStandard.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Family Civil Record", Description = "Family civil record of the deceased.", IsMandatory = true },
                    new() { DocumentName = "Hospital Death Report", Description = "Medical death report from the hospital or physician.", IsMandatory = true },
                    new() { DocumentName = "National ID Copy", Description = "Requester's national ID photocopy.", IsMandatory = true },
                }
            },

            // 8. Residency Certificate
            new()
            {
                Name = "Residency Certificate",
                NameArabic = "????? ???",
                Description = "Obtain an official residency certificate (????? ???) from the local mukhtar.",
                Category = "Civil Status",
                Fee = 25_000m,
                EstimatedDays = 3,
                DepartmentId = deptInterior.Id,
                ApprovalWorkflowId = wfStandard.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "National ID Copy", Description = "Applicant's national ID photocopy.", IsMandatory = true },
                    new() { DocumentName = "Residency Proof", Description = "Utility bill or rental contract proving current address.", IsMandatory = true },
                }
            },

            // 9. Work Permit
            new()
            {
                Name = "Work Permit",
                NameArabic = "????? ???",
                Description = "Apply for a work permit for foreign nationals employed in Lebanon.",
                Category = "Employment",
                Fee = 1_500_000m,
                EstimatedDays = 30,
                DepartmentId = deptLabor.Id,
                ApprovalWorkflowId = wfThreeStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Passport Copy", Description = "Clear copy of the worker's valid passport.", IsMandatory = true },
                    new() { DocumentName = "Employment Certificate", Description = "Employment contract signed by both employer and employee.", IsMandatory = true },
                    new() { DocumentName = "Medical Certificate", Description = "Medical fitness certificate from an authorized Lebanese hospital.", IsMandatory = true },
                    new() { DocumentName = "Employer Commercial Register", Description = "Copy of the employer's commercial registration (??? ?????).", IsMandatory = true },
                    new() { DocumentName = "Personal Photo", Description = "Two recent passport-size photos.", IsMandatory = true },
                }
            },

            // 10. Building Permit
            new()
            {
                Name = "Building Permit",
                NameArabic = "???? ????",
                Description = "Apply for a building or construction permit from the municipality and engineering syndicate.",
                Category = "Property",
                Fee = 2_000_000m,
                EstimatedDays = 45,
                DepartmentId = deptEngineers.Id,
                ApprovalWorkflowId = wfThreeStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Property Documents", Description = "Property deed (??? ?????) or real estate register extract.", IsMandatory = true },
                    new() { DocumentName = "Architectural Plans", Description = "Full architectural plans signed by a licensed engineer.", IsMandatory = true },
                    new() { DocumentName = "National ID Copy", Description = "Owner's national ID photocopy.", IsMandatory = true },
                    new() { DocumentName = "Municipality Approval", Description = "Preliminary approval letter from the local municipality.", IsMandatory = true },
                    new() { DocumentName = "Surveyor Report", Description = "Land surveyor report (??? ?????).", IsMandatory = true },
                }
            },

            // 11. Property Registration
            new()
            {
                Name = "Property Registration",
                NameArabic = "????? ?????",
                Description = "Register property ownership or transfer at the Land Registry (????? ???????).",
                Category = "Property",
                Fee = 1_000_000m,
                EstimatedDays = 30,
                DepartmentId = deptFinance.Id,
                ApprovalWorkflowId = wfThreeStep.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "Property Documents", Description = "Original property deed or sale contract.", IsMandatory = true },
                    new() { DocumentName = "National ID Copy", Description = "Buyer's and seller's national ID photocopies.", IsMandatory = true },
                    new() { DocumentName = "Tax Clearance Certificate", Description = "Proof that all property taxes are paid (????? ???).", IsMandatory = true },
                    new() { DocumentName = "Notarized Sale Agreement", Description = "Sale agreement notarized by a public notary (???? ???).", IsMandatory = true },
                }
            },

            // 12. Tax Payment Request
            new()
            {
                Name = "Tax Payment Request",
                NameArabic = "??? ??? ?????",
                Description = "Submit income tax, property tax, or other government tax payments online.",
                Category = "Finance",
                Fee = 0m,
                EstimatedDays = 5,
                DepartmentId = deptFinance.Id,
                ApprovalWorkflowId = wfStandard.Id,
                RequiredDocuments = new List<RequiredDocument>
                {
                    new() { DocumentName = "National ID Copy", Description = "Taxpayer's national ID photocopy.", IsMandatory = true },
                    new() { DocumentName = "Tax Assessment Notice", Description = "Official tax assessment notice from the Ministry of Finance.", IsMandatory = true },
                    new() { DocumentName = "Property Documents", Description = "Property deed for property-related taxes (if applicable).", IsMandatory = false },
                }
            },
        };

        context.GovernmentServices.AddRange(services);
        await context.SaveChangesAsync();

        // --- Department Locations (branches across Lebanon) ---
        if (!await context.DepartmentLocations.AnyAsync())
        {
            var locations = new List<DepartmentLocation>
            {
                // General Directorate of Personal Status
                new() { DepartmentId = deptPersonalStatus.Id, BranchName = "Beirut Main Office", City = "Beirut", Address = "Rue de Damas, Beirut", Latitude = 33.8886, Longitude = 35.4955, PhoneNumber = "+961-1-425700" },
                new() { DepartmentId = deptPersonalStatus.Id, BranchName = "Tripoli Branch", City = "Tripoli", Address = "Serail Street, Tripoli", Latitude = 34.4360, Longitude = 35.8497, PhoneNumber = "+961-6-432100" },
                new() { DepartmentId = deptPersonalStatus.Id, BranchName = "Saida Branch", City = "Saida", Address = "Riad El Solh Street, Saida", Latitude = 33.5580, Longitude = 35.3730, PhoneNumber = "+961-7-720300" },
                new() { DepartmentId = deptPersonalStatus.Id, BranchName = "Zahle Branch", City = "Zahle", Address = "Boulevard Principal, Zahle", Latitude = 33.8463, Longitude = 35.9020, PhoneNumber = "+961-8-800200" },
                new() { DepartmentId = deptPersonalStatus.Id, BranchName = "Nabatieh Branch", City = "Nabatieh", Address = "Main Road, Nabatieh", Latitude = 33.3779, Longitude = 35.4839, PhoneNumber = "+961-7-760100" },

                // General Directorate of General Security
                new() { DepartmentId = deptGeneralSecurity.Id, BranchName = "Beirut Headquarters", City = "Beirut", Address = "General Security HQ, Mathaf, Beirut", Latitude = 33.8830, Longitude = 35.5130, PhoneNumber = "+961-1-425610" },
                new() { DepartmentId = deptGeneralSecurity.Id, BranchName = "Tripoli Office", City = "Tripoli", Address = "El Mina Road, Tripoli", Latitude = 34.4400, Longitude = 35.8350, PhoneNumber = "+961-6-628500" },
                new() { DepartmentId = deptGeneralSecurity.Id, BranchName = "Saida Office", City = "Saida", Address = "Dekerman Area, Saida", Latitude = 33.5560, Longitude = 35.3750, PhoneNumber = "+961-7-723500" },
                new() { DepartmentId = deptGeneralSecurity.Id, BranchName = "Jounieh Office", City = "Jounieh", Address = "Sarba Highway, Jounieh", Latitude = 33.9808, Longitude = 35.6178, PhoneNumber = "+961-9-930200" },
                new() { DepartmentId = deptGeneralSecurity.Id, BranchName = "Zahle Office", City = "Zahle", Address = "Karak Nuh, Zahle", Latitude = 33.8500, Longitude = 35.9050, PhoneNumber = "+961-8-803600" },

                // Ministry of Interior and Municipalities
                new() { DepartmentId = deptInterior.Id, BranchName = "Beirut Central Office", City = "Beirut", Address = "Sanayeh, Beirut", Latitude = 33.8960, Longitude = 35.4880, PhoneNumber = "+961-1-345800" },
                new() { DepartmentId = deptInterior.Id, BranchName = "Tripoli Office", City = "Tripoli", Address = "Azmi Street, Tripoli", Latitude = 34.4330, Longitude = 35.8450, PhoneNumber = "+961-6-430200" },
                new() { DepartmentId = deptInterior.Id, BranchName = "Saida Office", City = "Saida", Address = "Old Saida, Saida", Latitude = 33.5610, Longitude = 35.3680, PhoneNumber = "+961-7-721000" },
                new() { DepartmentId = deptInterior.Id, BranchName = "Baalbek Office", City = "Baalbek", Address = "Main Road, Baalbek", Latitude = 34.0047, Longitude = 36.2110, PhoneNumber = "+961-8-370100" },

                // Traffic and Vehicle Registration Department
                new() { DepartmentId = deptTraffic.Id, BranchName = "Dekwaneh Center", City = "Metn", Address = "Dekwaneh, Metn", Latitude = 33.8770, Longitude = 35.5350, PhoneNumber = "+961-1-386560" },
                new() { DepartmentId = deptTraffic.Id, BranchName = "Tripoli Traffic Office", City = "Tripoli", Address = "Bahsas Area, Tripoli", Latitude = 34.4280, Longitude = 35.8500, PhoneNumber = "+961-6-411500" },
                new() { DepartmentId = deptTraffic.Id, BranchName = "Saida Traffic Office", City = "Saida", Address = "Eastern Boulevard, Saida", Latitude = 33.5550, Longitude = 35.3780, PhoneNumber = "+961-7-725000" },
                new() { DepartmentId = deptTraffic.Id, BranchName = "Zahle Traffic Office", City = "Zahle", Address = "Zahle Main Road", Latitude = 33.8470, Longitude = 35.8990, PhoneNumber = "+961-8-801200" },

                // Ministry of Labor
                new() { DepartmentId = deptLabor.Id, BranchName = "Beirut Main Office", City = "Beirut", Address = "Hamra, Beirut", Latitude = 33.8950, Longitude = 35.4850, PhoneNumber = "+961-1-556801" },
                new() { DepartmentId = deptLabor.Id, BranchName = "Tripoli Branch", City = "Tripoli", Address = "Tell Area, Tripoli", Latitude = 34.4350, Longitude = 35.8440, PhoneNumber = "+961-6-620200" },
                new() { DepartmentId = deptLabor.Id, BranchName = "Saida Branch", City = "Saida", Address = "Saida Center", Latitude = 33.5590, Longitude = 35.3710, PhoneNumber = "+961-7-727500" },

                // Ministry of Finance
                new() { DepartmentId = deptFinance.Id, BranchName = "Beirut Central Office", City = "Beirut", Address = "Riad El Solh, Beirut", Latitude = 33.8940, Longitude = 35.5030, PhoneNumber = "+961-1-981001" },
                new() { DepartmentId = deptFinance.Id, BranchName = "Tripoli Finance Office", City = "Tripoli", Address = "Serail Area, Tripoli", Latitude = 34.4370, Longitude = 35.8480, PhoneNumber = "+961-6-432800" },
                new() { DepartmentId = deptFinance.Id, BranchName = "Saida Finance Office", City = "Saida", Address = "Saida Serail, Saida", Latitude = 33.5600, Longitude = 35.3720, PhoneNumber = "+961-7-722500" },
                new() { DepartmentId = deptFinance.Id, BranchName = "Zahle Finance Office", City = "Zahle", Address = "Zahle Serail", Latitude = 33.8480, Longitude = 35.9030, PhoneNumber = "+961-8-802500" },
                new() { DepartmentId = deptFinance.Id, BranchName = "Nabatieh Finance Office", City = "Nabatieh", Address = "Nabatieh Center", Latitude = 33.3790, Longitude = 35.4850, PhoneNumber = "+961-7-761500" },

                // Order of Engineers and Architects
                new() { DepartmentId = deptEngineers.Id, BranchName = "Beirut Headquarters", City = "Beirut", Address = "Perimeter Street, Beirut", Latitude = 33.8870, Longitude = 35.5110, PhoneNumber = "+961-1-850111" },
                new() { DepartmentId = deptEngineers.Id, BranchName = "Tripoli Branch", City = "Tripoli", Address = "Maarad Street, Tripoli", Latitude = 34.4390, Longitude = 35.8460, PhoneNumber = "+961-6-410300" },
            };

            context.DepartmentLocations.AddRange(locations);
            await context.SaveChangesAsync();
        }
    }
}
