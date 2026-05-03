using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventiq.EventService.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AddressController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AddressController> _logger;
    private const string AddressApiBaseUrl = "https://production.cas.so/address-kit/2025-07-01";

    public AddressController(IHttpClientFactory httpClientFactory, ILogger<AddressController> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Eventiq-Backend/1.0");
        _logger = logger;
    }

    [HttpGet("provinces")]
    public async Task<ActionResult> GetProvinces()
    {
        try
        {
            var url = $"{AddressApiBaseUrl}/provinces";
            _logger.LogInformation("Fetching provinces from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            
            _logger.LogInformation("Response status: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch provinces. Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, errorContent);
                return StatusCode(
                    (int)response.StatusCode,
                    new
                    {
                        message = "Failed to fetch provinces from address service",
                        statusCode = (int)response.StatusCode,
                        error = errorContent,
                    });
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            return Ok(jsonDoc.RootElement);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception while fetching provinces");
            return StatusCode(
                502,
                new
                {
                    message = "Failed to fetch provinces from address service",
                    error = ex.Message,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching provinces");
            return StatusCode(
                500,
                new { message = "An error occurred while fetching provinces", error = ex.Message });
        }
    }

    [HttpGet("provinces/{provinceCode}/communes")]
    public async Task<ActionResult> GetCommunes(string provinceCode)
    {
        try
        {
            var url = $"{AddressApiBaseUrl}/provinces/{provinceCode}/communes";
            _logger.LogInformation("Fetching communes from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            
            _logger.LogInformation("Response status: {StatusCode}", response.StatusCode);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch communes. Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, errorContent);
                return StatusCode(
                    (int)response.StatusCode,
                    new
                    {
                        message = "Failed to fetch communes from address service",
                        statusCode = (int)response.StatusCode,
                        error = errorContent,
                    });
            }

            var content = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(content);

            return Ok(jsonDoc.RootElement);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request exception while fetching communes");
            return StatusCode(
                502,
                new
                {
                    message = "Failed to fetch communes from address service",
                    error = ex.Message,
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching communes");
            return StatusCode(
                500,
                new { message = "An error occurred while fetching communes", error = ex.Message });
        }
    }
}
