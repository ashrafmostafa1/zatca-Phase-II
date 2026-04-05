using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Interfaces;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

/// <summary>
/// Concrete ZATCA HTTP API client.
///
/// Design decisions:
/// • A single <see cref="HttpClient"/> is reused for the lifetime of the service
///   (avoid socket exhaustion from <c>using var client = new HttpClient()</c> per call).
/// • Transient failures (5xx / network errors) are retried up to <see cref="ZatcaOptions.MaxRetries"/>
///   times with a linear back-off — no extra NuGet package required.
/// • All methods are fully async so the calling UI thread is never blocked.
/// • Every request/response pair is written to the structured ZATCA log file.
/// </summary>
public class APIService : IAPIService
{
    private readonly HttpClient _http;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;

    // ── Endpoint constants ────────────────────────────────────────────────────

    private readonly string _zatcaCompliance;       // API #3
    private readonly string _zatcaProd;             // API #4 & #5
    private readonly string _zatcaUploadCompliance; // API #6
    private readonly string _zatcaUpload;           // API #1 — Reporting
    private readonly string _zatcaClearance;        // API #2 — Clearance

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>
    /// Preferred constructor — used when <see cref="ZatcaOptions"/> is injected via DI.
    /// </summary>
    public APIService(IOptions<ZatcaOptions> options)
        : this(options.Value) { }

    /// <summary>
    /// Direct constructor — used when DI is not available (e.g., unit tests).
    /// </summary>
    public APIService(ZatcaOptions options)
    {
        _maxRetries   = options.MaxRetries;
        _retryDelayMs = options.RetryDelayMs;

        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        string baseUrl = options.BaseUrl;
        _zatcaCompliance       = $"{baseUrl}/compliance";
        _zatcaProd             = $"{baseUrl}/production/csids";
        _zatcaUploadCompliance = $"{baseUrl}/compliance/invoices";
        _zatcaUpload           = $"{baseUrl}/invoices/reporting/single";
        _zatcaClearance        = $"{baseUrl}/invoices/clearance/single";
    }

    /// <summary>
    /// Legacy constructor — keeps backward compatibility with code that passes a raw bool.
    /// </summary>
    public APIService(bool isSimulation)
        : this(new ZatcaOptions { IsSimulation = isSimulation }) { }

    // ── API #3 — Compliance CSID ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ComplianceResponse?> SubmitComplianceRequestAsync(string csr, string otp)
    {
        var content = BuildJsonContent(new { csr });

        using var req = new HttpRequestMessage(HttpMethod.Post, _zatcaCompliance) { Content = content };
        req.Headers.Add("Accept", "application/json");
        req.Headers.Add("OTP", otp);
        req.Headers.Add("Accept-Version", "V2");

        var response = await _http.SendAsync(req);
        string body  = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca($"[API#3] Compliance CSID failed: {response.StatusCode}, {body}");
            return null;
        }

        LogsFile.MessageZatca("[API#3] Compliance CSID success");
        return Deserialize<ComplianceResponse>(body);
    }

    // ── API #4 — Production CSID Onboarding ──────────────────────────────────

    /// <inheritdoc/>
    public async Task<ComplianceResponse?> SubmitProductionRequestAsync(
        string compliance_request_id,
        string username,
        string password
    )
    {
        var content = BuildJsonContent(new { compliance_request_id });

        using var req = new HttpRequestMessage(HttpMethod.Post, _zatcaProd) { Content = content };
        AddCommonHeaders(req, username, password);

        var response = await _http.SendAsync(req);
        string body  = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#4] Production CSID Response: " + body);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca($"[API#4] Production CSID failed: {response.StatusCode}, {body}");
            return null;
        }

        LogsFile.MessageZatca("[API#4] Production CSID success");
        return Deserialize<ComplianceResponse>(body);
    }

    // ── API #5 — Production CSID Renewal (PATCH) ─────────────────────────────

    /// <inheritdoc/>
    public async Task<ComplianceResponse?> RenewProductionCsidAsync(
        string csr,
        string otp,
        string username,
        string password
    )
    {
        var content = BuildJsonContent(new { csr });

        using var req = new HttpRequestMessage(new HttpMethod("PATCH"), _zatcaProd) { Content = content };
        AddCommonHeaders(req, username, password);
        req.Headers.Add("OTP", otp);

        var response = await _http.SendAsync(req);
        string body  = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#5] Production CSID Renewal Response: " + body);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca($"[API#5] Production CSID Renewal failed: {response.StatusCode}, {body}");
            return null;
        }

        LogsFile.MessageZatca("[API#5] Production CSID Renewal success");
        return Deserialize<ComplianceResponse>(body);
    }

    // ── API #6 — Compliance Check ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TestUploadResponse?> UploadInvTest(
        object request,
        string username,
        string password
    )
    {
        var content = BuildJsonContent(request);

        using var req = new HttpRequestMessage(HttpMethod.Post, _zatcaUploadCompliance) { Content = content };
        AddCommonHeaders(req, username, password, includeLanguage: true);

        var response = await _http.SendAsync(req);
        string body  = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#6] Compliance Check Response: " + body);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca($"[API#6] Compliance Check failed: {response.StatusCode}, {body}");
            return null;
        }

        LogsFile.MessageZatca("[API#6] Compliance Check success");
        return Deserialize<TestUploadResponse>(body);
    }

    // ── API #2 — Clearance (Standard / B2B) ──────────────────────────────────

    /// <inheritdoc/>
    public async Task<TestUploadResponse?> UploadClearance(
        object request,
        string username,
        string password
    )
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var content = BuildJsonContent(request);

            using var req = new HttpRequestMessage(HttpMethod.Post, _zatcaClearance) { Content = content };
            AddCommonHeaders(req, username, password, includeLanguage: true);
            req.Headers.Add("Clearance-Status", "1");

            var response = await _http.SendAsync(req);
            string body  = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[API#2] Clearance Response: " + body);

            return ParseInvoiceResponse(response.StatusCode, body, "[API#2] Clearance");
        }, "[API#2] Clearance");
    }

    // ── API #1 — Reporting (Simplified / B2C) ─────────────────────────────────

    /// <inheritdoc/>
    public async Task<TestUploadResponse?> UploadCore(
        object request,
        string username,
        string password
    )
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var content = BuildJsonContent(request);

            using var req = new HttpRequestMessage(HttpMethod.Post, _zatcaUpload) { Content = content };
            AddCommonHeaders(req, username, password, includeLanguage: true);
            // NOTE: Reporting API does NOT use Clearance-Status header

            var response = await _http.SendAsync(req);
            string body  = await response.Content.ReadAsStringAsync();
            Console.WriteLine("[API#1] Reporting Response: " + body);

            return ParseInvoiceResponse(response.StatusCode, body, "[API#1] Reporting");
        }, "[API#1] Reporting");
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Retries <paramref name="operation"/> up to <see cref="_maxRetries"/> times on
    /// transient server errors (5xx) or network-level exceptions.
    /// Duplicate-invoice responses (400 + IVS-DUP-001) are NOT retried.
    /// </summary>
    private async Task<TestUploadResponse?> ExecuteWithRetryAsync(
        Func<Task<TestUploadResponse?>> operation,
        string label
    )
    {
        for (int attempt = 1; attempt <= _maxRetries + 1; attempt++)
        {
            try
            {
                var result = await operation();

                // Do not retry on duplicates or parsed results — only on null (5xx / no body)
                if (result != null) return result;

                if (attempt <= _maxRetries)
                {
                    LogsFile.MessageZatca(
                        $"{label} attempt {attempt}/{_maxRetries + 1} returned null — retrying in {_retryDelayMs} ms"
                    );
                    await Task.Delay(_retryDelayMs);
                }
            }
            catch (HttpRequestException ex)
            {
                LogsFile.MessageZatca($"{label} attempt {attempt} network error: {ex.Message}");
                if (attempt > _maxRetries) throw;
                await Task.Delay(_retryDelayMs);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                LogsFile.MessageZatca($"{label} attempt {attempt} timed out");
                if (attempt > _maxRetries) throw;
                await Task.Delay(_retryDelayMs);
            }
        }

        LogsFile.MessageZatca($"{label} exhausted all {_maxRetries + 1} attempts — returning null");
        return null;
    }

    /// <summary>Adds Accept-Version, Authorization (Basic), and optionally Accept-Language.</summary>
    private static void AddCommonHeaders(
        HttpRequestMessage req,
        string username,
        string password,
        bool includeLanguage = false
    )
    {
        req.Headers.Add("Accept", "application/json");
        req.Headers.Add("Accept-Version", "V2");
        if (includeLanguage)
            req.Headers.Add("Accept-Language", "ar");

        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }

    private static StringContent BuildJsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    // ─────────────────────────────────────────────────────────────────────────
    // Shared invoice-response parser (handles 200, 202, 400 duplicate, 5xx)
    // ─────────────────────────────────────────────────────────────────────────

    private static TestUploadResponse? ParseInvoiceResponse(
        HttpStatusCode statusCode,
        string responseBody,
        string apiLabel
    )
    {
        TestUploadResponse? result = null;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            result = JsonSerializer.Deserialize<TestUploadResponse>(responseBody, opts);
        }
        catch { /* Non-JSON body — handled below */ }

        if (result == null)
        {
            if ((int)statusCode >= 500)
            {
                LogsFile.MessageZatca($"{apiLabel} server error {statusCode}: {responseBody}");
                return null; // Signals the retry loop to retry
            }
            result = new TestUploadResponse();
        }

        result.HttpStatus  = statusCode;
        result.RawResponse = responseBody;

        bool isDuplicate = ContainsDuplicateError(responseBody);
        result.IsDuplicate = isDuplicate;

        if (isDuplicate)
        {
            LogsFile.MessageZatca(
                $"{apiLabel}: DUPLICATE invoice detected (IVS-DUP-001). " +
                "The invoice was already submitted. Returning existing result."
            );
            return result;
        }

        if (!IsSuccessStatus(statusCode))
        {
            LogsFile.MessageZatca($"{apiLabel} failed [{statusCode}]: {responseBody}");
            return result; // Non-null so caller can inspect ValidationResults
        }

        LogsFile.MessageZatca($"{apiLabel} success [{statusCode}]");
        return result;
    }

    private static bool IsSuccessStatus(HttpStatusCode code) =>
        (int)code >= 200 && (int)code <= 299;

    /// <summary>
    /// Detects ZATCA duplicate-invoice error code IVS-DUP-001.
    /// Case-insensitive scan — works against both structured and raw JSON bodies.
    /// </summary>
    private static bool ContainsDuplicateError(string body) =>
        !string.IsNullOrWhiteSpace(body) &&
        (body.Contains("IVS-DUP-001", StringComparison.OrdinalIgnoreCase) ||
         body.Contains("DUPLICATE",   StringComparison.OrdinalIgnoreCase));

    private static T? Deserialize<T>(string json) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch
        {
            return null;
        }
    }
}
