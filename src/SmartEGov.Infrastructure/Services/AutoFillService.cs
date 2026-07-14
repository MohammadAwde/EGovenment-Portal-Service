using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Interfaces;
using SmartEGov.Application.Services;
using SmartEGov.Domain.Entities;

namespace SmartEGov.Infrastructure.Services;

public class AutoFillService : IAutoFillService
{
    private readonly IDocumentProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly byte[] _encryptionKey;
    private readonly string _ocrApiKey;

    private static readonly string[] LebanesProvinces =
    [
        "بيروت", "جبل لبنان", "الشمال", "الجنوب", "البقاع", "النبطية", "عكار", "بعلبك الهرمل",
        "Beirut", "Mount Lebanon", "North Lebanon", "South Lebanon", "Bekaa", "Nabatieh"
    ];

    private static readonly string[] LebaneseDistricts =
    [
        "بيروت", "المتن", "كسروان", "الشوف", "عاليه", "جبيل",
        "طرابلس", "المنية الضنية", "زغرتا", "الكورة", "بشري", "البترون",
        "صور", "صيدا", "جزين", "مرجعيون", "بنت جبيل", "حاصبيا",
        "زحلة", "بعلبك", "الهرمل", "راشيا", "النبطية",
        "Beirut", "Metn", "Kesrwan", "Chouf", "Aley", "Byblos",
        "Tripoli", "Zgharta", "Koura", "Batroun",
        "Tyre", "Sidon", "Jezzine", "Marjeyoun", "Zahleh", "Baalbek", "Nabatieh"
    ];

    public AutoFillService(
        IDocumentProfileRepository profiles,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        var keyBase64 = configuration["AutoFill:EncryptionKey"]
            ?? throw new InvalidOperationException("AutoFill:EncryptionKey is not configured.");
        _encryptionKey = Convert.FromBase64String(keyBase64);
        _ocrApiKey = configuration["OcrSpace:ApiKey"] ?? "helloworld";
    }

    // ── Auto-fill retrieval ──────────────────────────────────────────────────

    public async Task<AutoFillResultDto?> GetAutoFillDataAsync(
        string userId, string documentType = "NationalID")
    {
        var profile = await _profiles.GetByUserAndTypeAsync(userId, documentType);
        return profile == null ? null : MapToAutoFillResult(profile);
    }

    public async Task<IEnumerable<DocumentProfileDto>> GetAllProfilesAsync(string userId)
    {
        var profiles = await _profiles.GetByUserIdAsync(userId);
        return profiles.Select(MapToDto).ToList();
    }

    // ── Save profile ─────────────────────────────────────────────────────────

    public async Task<DocumentProfileDto> SaveProfileAsync(DocumentProfileDto dto, string userId)
    {
        var existing = await _profiles.GetByUserAndTypeAsync(userId, dto.DocumentType);
        if (existing != null)
        {
            UpdateEntity(existing, dto);
            _profiles.Update(existing);
        }
        else
        {
            var profile = new DocumentProfile { UserId = userId };
            UpdateEntity(profile, dto);
            await _profiles.AddAsync(profile);
        }
        await _unitOfWork.SaveChangesAsync();
        return dto;
    }

    // ── OCR extraction ───────────────────────────────────────────────────────

    public async Task<AutoFillResultDto> ExtractFromOcrAsync(
        IFormFile idImage, string documentType, string userId)
    {
        if (idImage.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Image file must be under 5 MB.");

        var tempPath = Path.Combine(Path.GetTempPath(),
            $"{Guid.NewGuid()}{Path.GetExtension(idImage.FileName)}");

        await using (var fs = System.IO.File.Create(tempPath))
            await idImage.CopyToAsync(fs);

        try
        {
            var ocrText = await RunOcrAsync(tempPath);

            if (string.IsNullOrWhiteSpace(ocrText))
                return EmptyResult(documentType);

            var result = ParseLebanesId(ocrText, documentType);

            if (userId != "temp" && (
                !string.IsNullOrEmpty(result.District) ||
                !string.IsNullOrEmpty(result.Province) ||
                !string.IsNullOrEmpty(result.DocumentNumberMasked)))
            {
                await SaveProfileAsync(new DocumentProfileDto
                {
                    DocumentType = documentType,
                    FirstName = result.FirstName,
                    LastName = result.LastName,
                    FatherName = result.FatherName,
                    MotherName = result.MotherName,
                    MotherLastName = result.MotherLastName,
                    DocumentNumberMasked = result.DocumentNumberMasked,
                    RegistryNumber = result.RegistryNumber,
                    DateOfBirth = result.DateOfBirth,
                    Village = result.Village,
                    District = result.District,
                    Province = result.Province,
                    ExtractionMethod = result.ExtractionMethod
                }, userId);
            }

            return result;
        }
        catch (Exception)
        {
            return EmptyResult(documentType);
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    private static AutoFillResultDto EmptyResult(string documentType) => new()
    {
        DocumentType = documentType,
        ExtractionMethod = "Manual"
    };

    // ── Delete ───────────────────────────────────────────────────────────────

    public async Task DeleteProfileAsync(int profileId, string userId)
    {
        var profile = await _profiles.GetByIdAsync(profileId)
            ?? throw new KeyNotFoundException("Document profile not found.");
        if (profile.UserId != userId)
            throw new KeyNotFoundException("Document profile not found.");
        _profiles.Remove(profile);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── OCR.space API — single call ──────────────────────────────────────────

    private async Task<string> RunOcrAsync(string imagePath)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var form = new MultipartFormDataContent();

            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            form.Add(imageContent, "file", "id.jpg");
            form.Add(new StringContent("jpg"), "filetype");
            form.Add(new StringContent("ara"), "language");
            form.Add(new StringContent("false"), "isOverlayRequired");
            form.Add(new StringContent("true"), "detectOrientation");
            form.Add(new StringContent("true"), "scale");
            form.Add(new StringContent("3"), "OCREngine");
            form.Add(new StringContent(_ocrApiKey), "apikey");

            var response = await client.PostAsync(
                "https://api.ocr.space/parse/image", form);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("IsErroredOnProcessing", out var errProp) &&
                errProp.GetBoolean())
                return string.Empty;

            if (root.TryGetProperty("ParsedResults", out var results) &&
                results.GetArrayLength() > 0 &&
                results[0].TryGetProperty("ParsedText", out var textProp))
                return textProp.GetString() ?? string.Empty;

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    // ── Lebanese ID parser ───────────────────────────────────────────────────

    private static AutoFillResultDto ParseLebanesId(string text, string documentType)
    {
        var result = new AutoFillResultDto
        {
            DocumentType = documentType,
            ExtractionMethod = "OCR"
        };

        text = ConvertArabicNumerals(text);

        var lines = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 1)
            .ToArray();

        // ── MRZ first ─────────────────────────────────────────────────────
        var mrzLines = lines
            .Where(l => Regex.IsMatch(l, @"^[A-Z0-9<]{28,30}$"))
            .ToArray();
        if (mrzLines.Length >= 2)
            ParseMrz(mrzLines, result);

        // ── Line-by-line ──────────────────────────────────────────────────
        foreach (var line in lines)
        {
            // Province
            if (string.IsNullOrEmpty(result.Province) && line.Contains("المحافظة"))
            {
                var val = line.Replace("المحافظة", "").Replace(":", "").Trim();
                if (!string.IsNullOrEmpty(val)) result.Province = val;
                continue;
            }

            // District
            if (string.IsNullOrEmpty(result.District) && line.Contains("القضاء"))
            {
                var val = line.Replace("القضاء", "").Replace(":", "").Trim();
                if (!string.IsNullOrEmpty(val)) result.District = val;
                continue;
            }

            // Village
            if (string.IsNullOrEmpty(result.Village) &&
                (line.Contains("المحلة") || line.Contains("القرية")))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0 && colonIdx < line.Length - 1)
                    result.Village = line[(colonIdx + 1)..].Trim();
                continue;
            }

            // 12-digit ID number
            if (string.IsNullOrEmpty(result.DocumentNumberMasked) &&
                Regex.IsMatch(line.Trim(), @"^\d{12}$"))
            {
                result.DocumentNumberMasked = line.Trim();
                continue;
            }

            // Registry number
            if (string.IsNullOrEmpty(result.RegistryNumber))
            {
                if (Regex.IsMatch(line.Trim(), @"^\d{4,8}$"))
                {
                    result.RegistryNumber = line.Trim();
                    continue;
                }
                if (line.Contains("رقم السجل") || line.Contains("السجل"))
                {
                    var nm = Regex.Match(line, @"\d{4,8}");
                    if (nm.Success) { result.RegistryNumber = nm.Value; continue; }
                }
            }

            // Date yyyy/MM/dd
            if (result.DateOfBirth == default)
            {
                var dm = Regex.Match(line, @"(\d{4})[\/\-\.](\d{1,2})[\/\-\.](\d{1,2})");
                if (dm.Success && int.TryParse(dm.Groups[1].Value, out var y) && y > 1900 && y < 2010)
                {
                    try { result.DateOfBirth = new DateTime(y, int.Parse(dm.Groups[2].Value), int.Parse(dm.Groups[3].Value)); } catch { }
                    if (result.DateOfBirth != default) continue;
                }
                // Date dd/MM/yyyy
                var dm2 = Regex.Match(line, @"(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})");
                if (dm2.Success && int.TryParse(dm2.Groups[3].Value, out var y2) && y2 > 1900 && y2 < 2010)
                {
                    try { result.DateOfBirth = new DateTime(y2, int.Parse(dm2.Groups[2].Value), int.Parse(dm2.Groups[1].Value)); } catch { }
                    if (result.DateOfBirth != default) continue;
                }
            }

            // Province fallback
            if (string.IsNullOrEmpty(result.Province))
            {
                var p = LebanesProvinces.FirstOrDefault(
                    x => line.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (p != null) { result.Province = p; continue; }
            }

            // District fallback
            if (string.IsNullOrEmpty(result.District))
            {
                var d = LebaneseDistricts.FirstOrDefault(
                    x => line.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (d != null) { result.District = d; continue; }
            }
        }

        return result;
    }

    private static void ParseMrz(string[] mrzLines, AutoFillResultDto result)
    {
        var line1 = mrzLines[0].PadRight(30);
        var line2 = mrzLines[1].PadRight(30);

        var idRaw = line2[..12].Replace("<", "");
        if (Regex.IsMatch(idRaw, @"^\d{8,12}$") &&
            string.IsNullOrEmpty(result.DocumentNumberMasked))
            result.DocumentNumberMasked = idRaw;

        if (line2.Length >= 19 && result.DateOfBirth == default)
        {
            var dobRaw = line2.Substring(13, 6);
            if (Regex.IsMatch(dobRaw, @"^\d{6}$") &&
                int.TryParse(dobRaw[..2], out var yr) &&
                int.TryParse(dobRaw[2..4], out var mo) &&
                int.TryParse(dobRaw[4..6], out var dy))
            {
                var fullYear = yr <= DateTime.Today.Year % 100 ? 2000 + yr : 1900 + yr;
                try { result.DateOfBirth = new DateTime(fullYear, mo, dy); } catch { }
            }
        }

        result.ExtractionMethod = "MRZ";
    }

    private static string ConvertArabicNumerals(string text) =>
        text.Replace('۰', '0').Replace('٠', '0')
            .Replace('۱', '1').Replace('١', '1')
            .Replace('۲', '2').Replace('٢', '2')
            .Replace('۳', '3').Replace('٣', '3')
            .Replace('۴', '4').Replace('٤', '4')
            .Replace('۵', '5').Replace('٥', '5')
            .Replace('۶', '6').Replace('٦', '6')
            .Replace('۷', '7').Replace('٧', '7')
            .Replace('۸', '8').Replace('٨', '8')
            .Replace('۹', '9').Replace('٩', '9');

    // ── Encryption (AES-256) ─────────────────────────────────────────────────

    private string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
        var full = new byte[aes.IV.Length + cipher.Length];
        aes.IV.CopyTo(full, 0);
        cipher.CopyTo(full, aes.IV.Length);
        return Convert.ToBase64String(full);
    }

    private string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;
        var full = Convert.FromBase64String(cipherText);
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.IV = full[..16];
        using var dec = aes.CreateDecryptor();
        return Encoding.UTF8.GetString(dec.TransformFinalBlock(full, 16, full.Length - 16));
    }

    // ── Mapping ──────────────────────────────────────────────────────────────

    private void UpdateEntity(DocumentProfile e, DocumentProfileDto dto)
    {
        e.DocumentType = dto.DocumentType;
        e.FirstName = dto.FirstName;
        e.LastName = dto.LastName;
        e.FatherName = dto.FatherName;
        e.MotherName = dto.MotherName;
        e.MotherLastName = dto.MotherLastName;
        e.RegistryNumber = dto.RegistryNumber;
        e.DateOfBirth = dto.DateOfBirth;
        e.Village = dto.Village;
        e.District = dto.District;
        e.Province = dto.Province;
        e.ExtractionMethod = dto.ExtractionMethod;
        e.CreatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(dto.DocumentNumberMasked) &&
            !dto.DocumentNumberMasked.StartsWith('*'))
            e.DocumentNumber = Encrypt(dto.DocumentNumberMasked);
    }

    private DocumentProfileDto MapToDto(DocumentProfile p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        DocumentType = p.DocumentType,
        FirstName = p.FirstName,
        LastName = p.LastName,
        FatherName = p.FatherName,
        MotherName = p.MotherName,
        MotherLastName = p.MotherLastName,
        DocumentNumberMasked = MaskNumber(Decrypt(p.DocumentNumber)),
        RegistryNumber = p.RegistryNumber,
        DateOfBirth = p.DateOfBirth,
        Village = p.Village,
        District = p.District,
        Province = p.Province,
        ExtractionMethod = p.ExtractionMethod,
        CreatedAt = p.CreatedAt
    };

    private AutoFillResultDto MapToAutoFillResult(DocumentProfile p) => new()
    {
        FirstName = p.FirstName,
        LastName = p.LastName,
        FatherName = p.FatherName,
        MotherName = p.MotherName,
        MotherLastName = p.MotherLastName,
        DocumentNumberMasked = MaskNumber(Decrypt(p.DocumentNumber)),
        RegistryNumber = p.RegistryNumber,
        DateOfBirth = p.DateOfBirth,
        Village = p.Village,
        District = p.District,
        Province = p.Province,
        DocumentType = p.DocumentType,
        ExtractionMethod = p.ExtractionMethod,
        MayBeOutdated = p.CreatedAt < DateTime.UtcNow.AddYears(-1)
    };

    private static string MaskNumber(string n) =>
        string.IsNullOrEmpty(n) ? string.Empty :
        n.Length <= 4 ? new string('*', n.Length) :
        new string('*', n.Length - 4) + n[^4..];

    private static bool IsArabic(char c) => c >= '\u0600' && c <= '\u06FF';
}