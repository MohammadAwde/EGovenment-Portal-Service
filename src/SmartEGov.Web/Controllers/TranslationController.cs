using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartEGov.Web.Controllers;

public class TranslationController : Controller
{
    private const string DeepLKey = "408cb816-a3f3-4360-9f79-cd4bbb3d19d3:fx";
    private const string DeepLUrl = "https://api-free.deepl.com/v2/translate";

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Translate([FromBody] string[] texts)
    {
        if (texts == null || texts.Length == 0)
            return BadRequest();

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {DeepLKey}");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                text = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray(),
                target_lang = "AR",
                source_lang = "EN"
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var res = await client.PostAsync(DeepLUrl,
                new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json"));

            var json = await res.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> TranslateToEnglish([FromBody] string[] texts)
    {
        if (texts == null || texts.Length == 0)
            return BadRequest();

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"DeepL-Auth-Key {DeepLKey}");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            var requestBody = new
            {
                text = texts.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray(),
                target_lang = "EN",
                source_lang = "AR"
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var res = await client.PostAsync(DeepLUrl,
                new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json"));

            var json = await res.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
