using System;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Zatca_Phase_II.Helpers;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class APIService
{
    private readonly string BaseUrl;
    private readonly string zatcaCompliance;
    private readonly string zatcaProd;
    private readonly string zatcaUploadCompliance;
    private readonly string zatcaUpload;
    private readonly string zatcaClearance;

    public APIService(bool IsSimulation)
    {
        BaseUrl = IsSimulation
            ? "https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation"
            : "https://gw-fatoora.zatca.gov.sa/e-invoicing/core";

        zatcaCompliance       = $"{BaseUrl}/compliance";                   // API #3
        zatcaProd             = $"{BaseUrl}/production/csids";             // API #4 & #5
        zatcaUploadCompliance = $"{BaseUrl}/compliance/invoices";          // API #6
        zatcaUpload           = $"{BaseUrl}/invoices/reporting/single";    // API #1
        zatcaClearance        = $"{BaseUrl}/invoices/clearance/single";    // API #2
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #3 — Compliance CSID (POST /compliance)
    // واجهة برمجة التطبيقات لمعرّف ختمّ التشفير لأغراض الامتثال
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ComplianceResponse?> SubmitComplianceRequestAsync(string csr, string otp)
    {
        using var _httpClient = new HttpClient();
        var requestData = new { csr };
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("OTP", otp);
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");

        HttpResponseMessage response = await _httpClient.PostAsync(zatcaCompliance, content);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca(
                $"[API#3] Compliance CSID failed: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}"
            );
            return null;
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        LogsFile.MessageZatca($"[API#3] Compliance CSID success");
        return JsonSerializer.Deserialize<ComplianceResponse>(responseBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #4 — Production CSID Onboarding (POST /production/csids)
    // واجهة برمجة التطبيقات لمعرّف ختم التشفير الخاصّ ببيئة الإنتاج (تهيئة)
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ComplianceResponse?> SubmitProductionRequestAsync(
        string compliance_request_id,
        string _username,
        string _password
    )
    {
        using var _httpClient = new HttpClient();
        var requestData = new { compliance_request_id };
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authToken)
        );

        HttpResponseMessage response = await _httpClient.PostAsync(zatcaProd, content);
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#4] Production CSID Response: " + responseBody);
        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca(
                $"[API#4] Production CSID failed: {response.StatusCode}, {responseBody}"
            );
            return null;
        }
        LogsFile.MessageZatca($"[API#4] Production CSID success");
        return JsonSerializer.Deserialize<ComplianceResponse>(responseBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #5 — Production CSID Renewal (PATCH /production/csids)
    // واجهة برمجة التطبيقات لمعرّف ختم التشفير الخاصّ ببيئة الإنتاج (تجديد)
    // Uses PATCH with the new CSR, authenticated with the existing production CSID
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<ComplianceResponse?> RenewProductionCsidAsync(
        string csr,
        string otp,
        string _username,
        string _password
    )
    {
        using var _httpClient = new HttpClient();
        var requestData = new { csr };
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
        _httpClient.DefaultRequestHeaders.Add("OTP", otp);
        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authToken)
        );

        // PATCH — not POST — per ZATCA spec for renewal
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), zatcaProd)
        {
            Content = content
        };
        HttpResponseMessage response = await _httpClient.SendAsync(request);
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#5] Production CSID Renewal Response: " + responseBody);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca(
                $"[API#5] Production CSID Renewal failed: {response.StatusCode}, {responseBody}"
            );
            return null;
        }
        LogsFile.MessageZatca($"[API#5] Production CSID Renewal success");
        return JsonSerializer.Deserialize<ComplianceResponse>(responseBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #6 — Compliance Checks (POST /compliance/invoices)
    // واجهات برمجة التطبيقات الخاصّة بإجراءات التحقق من الامتثال
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<TestUploadResponse?> UploadInvTest(
        object request,
        string _username,
        string _password
    )
    {
        using var _httpClient = new HttpClient();
        var requestData = request;
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ar");
        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authToken)
        );

        HttpResponseMessage response = await _httpClient.PostAsync(zatcaUploadCompliance, content);
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#6] Compliance Check Response: " + responseBody);

        if (!response.IsSuccessStatusCode)
        {
            LogsFile.MessageZatca(
                $"[API#6] Compliance Check failed: {response.StatusCode}, {responseBody}"
            );
            return null;
        }
        LogsFile.MessageZatca($"[API#6] Compliance Check success");
        return JsonSerializer.Deserialize<TestUploadResponse>(responseBody);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #2 — Clearance API (POST /invoices/clearance/single)
    // واجهة برمجة التطبيقات لاعتماد الفواتير — Standard invoices (B2B)
    // Clearance-Status: 1 = request clearance
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<TestUploadResponse?> UploadClearance(
        object request,
        string _username,
        string _password
    )
    {
        using var _httpClient = new HttpClient();
        var requestData = request;
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ar");
        _httpClient.DefaultRequestHeaders.Add("Clearance-Status", "1");
        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authToken)
        );

        HttpResponseMessage response = await _httpClient.PostAsync(zatcaClearance, content);
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#2] Clearance Response: " + responseBody);

        return ParseInvoiceResponse(response.StatusCode, responseBody, "[API#2] Clearance");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // API #1 — Reporting API (POST /invoices/reporting/single)
    // واجهة برمجة التطبيقات لإرسال/مشاركة الفواتير — Simplified invoices (B2C)
    // No Clearance-Status header required for reporting
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<TestUploadResponse?> UploadCore(
        object request,
        string _username,
        string _password
    )
    {
        using var _httpClient = new HttpClient();
        var requestData = request;
        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept-Version", "V2");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ar");
        // NOTE: Reporting API does NOT use Clearance-Status header (removed)
        var authToken = Encoding.ASCII.GetBytes($"{_username}:{_password}");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(authToken)
        );

        HttpResponseMessage response = await _httpClient.PostAsync(zatcaUpload, content);
        string responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine("[API#1] Reporting Response: " + responseBody);

        return ParseInvoiceResponse(response.StatusCode, responseBody, "[API#1] Reporting");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared helper — parses ZATCA invoice response for both Reporting & Clearance.
    // Returns the parsed result even on HTTP 400 (duplicate or validation error),
    // and sets IsDuplicate = true when error code IVS-DUP-001 is detected.
    // Returns null only for unexpected non-400 failures (5xx, network errors, etc.)
    // ─────────────────────────────────────────────────────────────────────────
    private static TestUploadResponse? ParseInvoiceResponse(
        HttpStatusCode statusCode,
        string responseBody,
        string apiLabel
    )
    {
        // Always try to parse — ZATCA sends useful body on 400 too
        TestUploadResponse? result = null;
        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            result = JsonSerializer.Deserialize<TestUploadResponse>(responseBody, opts);
        }
        catch
        {
            // Body was not valid JSON — fall through
        }

        if (result == null)
        {
            // Could not parse — only happens on 5xx / gateway errors
            if ((int)statusCode >= 500)
            {
                LogsFile.MessageZatca($"{apiLabel} server error {statusCode}: {responseBody}");
                return null;
            }
            result = new TestUploadResponse();
        }

        result.HttpStatus   = statusCode;
        result.RawResponse  = responseBody;

        // Detect duplicate: ZATCA error code IVS-DUP-001
        bool isDuplicate = ContainsDuplicateError(responseBody);
        result.IsDuplicate = isDuplicate;

        if (isDuplicate)
        {
            LogsFile.MessageZatca(
                $"{apiLabel}: DUPLICATE invoice detected (IVS-DUP-001). " +
                $"The invoice was already submitted. Returning existing result."
            );
            // Return the result — the caller can decide whether to treat this as success
            return result;
        }

        if (!IsSuccessStatus(statusCode))
        {
            LogsFile.MessageZatca($"{apiLabel} failed [{statusCode}]: {responseBody}");
            // Still return the parsed result (not null) so the caller sees ValidationResults
            return result;
        }

        LogsFile.MessageZatca($"{apiLabel} success [{statusCode}]");
        return result;
    }

    private static bool IsSuccessStatus(HttpStatusCode code) =>
        (int)code >= 200 && (int)code <= 299;

    /// <summary>
    /// Checks whether the ZATCA response body contains a duplicate-invoice error.
    /// ZATCA uses error code "IVS-DUP-001" and category "IVS" for duplicates.
    /// The check is case-insensitive and works against both structured and raw JSON.
    /// </summary>
    private static bool ContainsDuplicateError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return false;
        // Fast path: look for the known duplicate code string
        return responseBody.Contains("IVS-DUP-001", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("DUPLICATE", StringComparison.OrdinalIgnoreCase);
    }
}
