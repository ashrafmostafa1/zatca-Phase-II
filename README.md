# ZATCA Phase II — E-Invoicing Library

A **.NET 8** class library for integrating with [ZATCA (هيئة الزكاة والضريبة والجمارك)](https://zatca.gov.sa) Phase II e-invoicing APIs in Saudi Arabia.  
Designed for Restaurant POS / Accounting systems — built with **Clean Architecture**, full **Dependency Injection**, and a **fire-and-forget Outbox pattern** so the cashier never waits for the ZATCA response.

---

## Table of Contents

- [Requirements](#requirements)
- [Project Structure](#project-structure)
- [Supported ZATCA APIs](#supported-zatca-apis)
- [Architecture Overview](#architecture-overview)
- [Quick Start — DI Registration](#quick-start--di-registration)
- [Onboarding Flow](#onboarding-flow)
- [Usage Guide](#usage-guide)
  - [1. Build a Bill Object](#1-build-a-bill-object)
  - [2. Sign & Submit (Manual)](#2-sign--submit-manual)
  - [3. Fire-and-Forget (Outbox Pattern)](#3-fire-and-forget-outbox-pattern)
  - [4. Onboarding — Generate CSR & Get CSID](#4-onboarding--generate-csr--get-csid)
  - [5. Renew Production CSID](#5-renew-production-csid)
  - [6. PIH Chain — Previous Invoice Hash](#6-pih-chain--previous-invoice-hash)
  - [7. Direct API Usage](#7-direct-api-usage)
  - [8. Crypto Utilities](#8-crypto-utilities)
- [Models Reference](#models-reference)
- [Enums Reference](#enums-reference)
- [Configuration Reference](#configuration-reference)
- [Publishing as a Single DLL](#publishing-as-a-single-dll)
- [Notes & Constraints](#notes--constraints)
- [References](#references)

---

## Requirements

| Requirement | Version |
|-------------|---------|
| .NET        | **8.0** |
| ZATCA SDK   | Included in `/Zatca` folder |
| OS          | Windows / macOS / Linux |

> **macOS/Linux**: The ZATCA SDK schematrons are extracted to `$TMPDIR/zatca/Data/Rules/schematrons` on non-Windows systems automatically.

---

## Project Structure

```
zatca-Phase-II/
├── Eum/
│   ├── Environment.cs          # EnvironmentTyp (Production | Simulation)
│   ├── InvoiceType.cs          # InvoiceType (Standard | Simplified)
│   └── NoticeType.cs           # NoticeType (Regular | Debit | Credit)
├── Helpers/
│   ├── CryptoUtility.cs        # ★ Pure SHA-256 hashing & Phase-2 TLV QR encoder
│   ├── FormatXML.cs            # XML formatting & Base64 conversion
│   ├── HashHelper.cs           # SHA-256 hashing (PIH seed)
│   ├── LogsFile.cs             # Structured file-based daily logging
│   ├── ServiceCollectionExtensions.cs  # ★ AddZatcaServices() one-liner DI
│   └── ZatcaConfig.cs          # SDK schematron extraction
├── Interfaces/
│   ├── IAPIService.cs          # ★ Interface for all 6 ZATCA HTTP APIs
│   ├── ICSRGenerator.cs        # CSR generation contract
│   ├── IEInvoiceService.cs     # ★ e-Invoice service contract (incl. Clearance)
│   └── IOutboxEInvoiceService.cs  # ★ Fire-and-forget outbox contract
├── Models/
│   ├── Bill.cs                 # Invoice header (+ PreviousInvoiceHash)
│   ├── BillDetails.cs          # Invoice line items
│   ├── CompanyData.cs          # Company/supplier info
│   ├── Customer.cs             # CustomerORSupplier (shared)
│   ├── Fatoora.cs              # Signing result (Hash, QR, ObjRequest)
│   ├── InvoiceOutboxMessage.cs # ★ Outbox queue DTO
│   ├── ZatcaBranch.cs          # Branch CSID credentials
│   ├── ZatcaOptions.cs         # ★ Options Pattern configuration
│   ├── ComplianceResponse.cs   # CSID API response
│   └── TestUploadResponse.cs   # Invoice upload response (+ IsDuplicate)
├── Services/
│   ├── APIService.cs           # ★ All 6 ZATCA APIs — reused HttpClient + retry
│   ├── CSRGenerator.cs         # Full onboarding flow
│   ├── DefaultXML.cs           # Compliance/test XML builder
│   ├── EInvoiceService.cs      # ★ Main service — Reporting + Clearance routing
│   ├── InMemoryOutboxService.cs # ★ Bounded Channel<T> outbox
│   ├── SignsService.cs         # SDK sign + QR + hash extraction
│   ├── ZatcaBackgroundWorker.cs # ★ BackgroundService draining the outbox
│   └── ZatcaClassXML.cs        # Real UBL 2.1 XML builder (from Bill)
└── Zatca/                      # Local ZATCA SDK DLLs (not on NuGet)
```

Items marked **★** are new in v2.0.

---

## Supported ZATCA APIs

| # | API | Method | Endpoint | Description |
|---|-----|--------|----------|-------------|
| 1 | **Reporting API** | `POST` | `/invoices/reporting/single` | Report simplified invoices (B2C) within 24h |
| 2 | **Clearance API** | `POST` | `/invoices/clearance/single` | Clear standard invoices (B2B) in real-time |
| 3 | **Compliance CSID API** | `POST` | `/compliance` | Obtain compliance certificate (onboarding step 1) |
| 4 | **Production CSID API (Onboarding)** | `POST` | `/production/csids` | Exchange compliance CSID for production CSID |
| 5 | **Production CSID API (Renewal)** | `PATCH` | `/production/csids` | Renew an expiring production CSID |
| 6 | **Compliance Checks API** | `POST` | `/compliance/invoices` | Validate invoice XML during compliance testing |

**Base URLs:**
- **Simulation:** `https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation`
- **Production:**  `https://gw-fatoora.zatca.gov.sa/e-invoicing/core`

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────┐
│                        POS / Host Application                        │
│                                                                      │
│   Bill saved to DB                                                   │
│        │                                                             │
│        ▼                                                             │
│   IOutboxEInvoiceService.EnqueueAsync()  ◄── returns in μs          │
│        │                                                             │
│        │  (Background)                                               │
│        ▼                                                             │
│   ZatcaBackgroundWorker (IHostedService)                             │
│        │                                                             │
│        ├─► IEInvoiceService.EInvoice()     → XML build + Sign        │
│        │                                                             │
│        ├─► Standard (B2B)  → IEInvoiceService.ClearanceEInvoice()   │
│        └─► Simplified (B2C)→ IEInvoiceService.UploadEInvoice()      │
│                                     │                                │
│                                     ▼                                │
│                               IAPIService                            │
│                          (HttpClient + Retry)                        │
│                                     │                                │
│                                     ▼                                │
│                          ZATCA API (FATOORA)                         │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Quick Start — DI Registration

```csharp
// Program.cs  (ASP.NET Core 8)
builder.Services.AddZatcaServices(o =>
{
    o.IsSimulation   = builder.Environment.IsDevelopment(); // false in Production
    o.MaxRetries     = 3;       // retry attempts on transient 5xx errors
    o.RetryDelayMs   = 1_000;   // ms between retries (linear back-off)
    o.TimeoutSeconds = 30;      // HTTP timeout
    o.OutboxCapacity = 500;     // max queued invoices before back-pressure
});
```

Or bind from `appsettings.json`:

```json
{
  "Zatca": {
    "IsSimulation": false,
    "MaxRetries": 3,
    "RetryDelayMs": 1000,
    "TimeoutSeconds": 30,
    "OutboxCapacity": 500
  }
}
```

```csharp
builder.Services.AddZatcaServices(
    builder.Configuration.GetSection("Zatca")
);
```

To register **without** the background worker (if you have your own processing pipeline):

```csharp
builder.Services.AddZatcaCoreServices(o => { ... });
```

---

## Onboarding Flow

```
  1. Generate CSR ────────────────────► CSRGenerator.GenerateCSR(company)
        │
        ▼
  2. Submit CSR + OTP ───────────────► API #3  POST /compliance
        │  returns: BinarySecurityToken + Secret + RequestID
        ▼
  3. Compliance Invoice Check (×6 — 2 types × 3 invoice flavours)
        │  API #6  POST /compliance/invoices
        ▼
  4. Get Production CSID ────────────► API #4  POST /production/csids
        │  returns: final BinarySecurityToken + SecretKey
        │  → store in ZatcaBranch (encrypted in your DB)
        ▼
  5. Ready to submit live invoices!

  Later (CSID expires ~1 year) ──────► API #5  PATCH /production/csids
```

---

## Usage Guide

### 1. Build a Bill Object

```csharp
var bill = new Bill
{
    Id          = 1001,
    Code        = "INV-1001",               // invoice reference ID
    Uid         = Guid.NewGuid().ToString(), // unique UUID per invoice
    Date        = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
    InvoiceType = InvoiceType.Simplified,   // Standard | Simplified
    NoticeType  = NoticeType.Regular,       // Regular | Debit | Credit

    // ── Totals ──
    SupTotal           = 100.00,  // subtotal before discount
    DiscountTotal      = 0.00,    // total discount
    TotalAfterDiscount = 100.00,  // subtotal after discount (excl. VAT)
    TotalVAT           = 15.00,   // total VAT (15%)
    NetTotal           = 115.00,  // final payable (incl. VAT)
    VatIsTaxesIncluded = false,   // true = prices entered WITH VAT

    // ── PIH Hash Chain ──
    // First invoice: leave empty → library uses ZATCA seed hash
    // All subsequent: pass the HashXML from the previous Fatoora result
    PreviousInvoiceHash = "",

    Notes      = "فاتورة مبسطة",
    ParentCode = "",  // required for Credit/Debit notes only

    Company = new CompanyData
    {
        Name      = "شركة مثال",
        Crn       = "1234567890",
        TaxNumber = "300000000000003",
    },
    Supplier = new CustomerORSupplier
    {
        StreetName          = "شارع الأمير سلطان",
        BuildingNumber      = "3242",
        CitySubdivisionName = "العليا",
        CityName            = "الرياض",
        PostalZone          = "12345",
        TaxNumber           = "300000000000003",
        RegistrationName    = "شركة مثال",
    },
    Customer = new CustomerORSupplier
    {
        StreetName          = "شارع الملك فهد",
        BuildingNumber      = "1111",
        CitySubdivisionName = "العليا",
        CityName            = "الرياض",
        PostalZone          = "54321",
        TaxNumber           = "",      // optional for simplified invoices
        RegistrationName    = "عميل",
    },
    BillDetails = new List<BillDetails>
    {
        new() {
            Id            = 1,
            ItemName      = "برغر كلاسيك",
            Qty           = 2,
            ItemSellPrice = 50.00,   // unit price (excl. VAT)
            DiscountTotal = 0,
            NetTotal      = 115.00,  // line total incl. VAT
            VATTotal      = 15.00,
            IsDelete      = false,
        }
    }
};
```

#### Credit / Debit Note

```csharp
var creditNote = new Bill
{
    NoticeType = NoticeType.Credit,  // → InvoiceTypeCode = 383
    ParentCode = "INV-1001",         // the original invoice being reversed
    // ... rest of fields same as above
};

var debitNote = new Bill
{
    NoticeType = NoticeType.Debit,   // → InvoiceTypeCode = 381
    IsDebit    = true,               // sets PaymentMeansCode = 30
    ParentCode = "INV-1001",
};
```

---

### 2. Sign & Submit (Manual)

Use this when you want full control over the two-step process:

```csharp
// Injected via DI
public class MyBillingService(IEInvoiceService einvoice)
{
    public async Task SubmitBillAsync(Bill bill, ZatcaBranch branch)
    {
        // Step 1: Generate UBL 2.1 XML + sign + extract QR/hash
        Fatoora? fatoora = await einvoice.EInvoice(
            bill, branch, EnvironmentTyp.Production
        );

        if (fatoora == null) return; // signing failed — see Logs/Zatca/

        // fatoora.HashXML   → store as PreviousInvoiceHash for the next bill
        // fatoora.Base64QR  → embed in the printed receipt
        // fatoora.ObjRequest → { invoiceHash, uuid, invoice } ready to submit

        // Step 2a: Simplified (B2C) → Reporting
        if (bill.InvoiceType == InvoiceType.Simplified)
        {
            var result = await einvoice.UploadEInvoice(
                fatoora.ObjRequest, branch, EnvironmentTyp.Production
            );
            // result.ReportingStatus == "REPORTED" → success
        }

        // Step 2b: Standard (B2B) → Clearance
        else
        {
            var result = await einvoice.ClearanceEInvoice(
                fatoora.ObjRequest, branch, EnvironmentTyp.Production
            );
            // result.ClearanceStatus == "CLEARED" → success
        }

        // Step 2c: Auto-router (does both steps in one call)
        // var result = await ((EInvoiceService)einvoice)
        //     .EInvoiceAndSubmitAsync(bill, branch, EnvironmentTyp.Production);
    }
}
```

---

### 3. Fire-and-Forget (Outbox Pattern)

**Recommended for Restaurant POS** — the cashier prints the receipt immediately:

```csharp
public class BillingController(
    IOutboxEInvoiceService outbox,
    IYourBillRepository repo
)
{
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest req)
    {
        // 1. Save bill to database FIRST (always)
        Bill bill = await repo.SaveBillAsync(req);

        // 2. Enqueue for ZATCA — returns in microseconds
        await outbox.EnqueueAsync(bill, branch, EnvironmentTyp.Production);

        // 3. Print receipt immediately — no waiting for ZATCA
        return Ok(new { bill.Id, bill.Uid });
    }
}
```

The `ZatcaBackgroundWorker` processes the queue asynchronously:
- On **success**: logs the result + hash (hook your DB update here)
- On **failure**: retries up to `MaxRetries` with exponential back-off (2s, 4s, 6s…)
- After all retries exhausted: writes to `Logs/Zatca/DeadLetters/{date}.txt`

---

### 4. Onboarding — Generate CSR & Get CSID

> Run **once per branch/device**. Get the OTP from the ZATCA Fatoora portal.

```csharp
var company = new CompanyData
{
    Name                     = "شركة مثال للمطاعم",
    Crn                      = "1234567890",
    TaxNumber                = "300000000000003",
    BranchName               = "فرع الرياض",
    NationalAddress          = "الرياض - حي العليا",
    IndustryBusinessCategory = "Restaurant",
    InvoiceType              = "1000",   // "0100"=Standard, "1000"=Simplified, "1100"=Both
    SystemType               = "Web",
    Otp                      = "123456", // from ZATCA Fatoora portal
};

var csrGenerator = new CSRGenerator(hostingEnvironment, isSimulation: true);
var (productionCsid, privateKey) = await csrGenerator.GenerateCSR(company);

// Store ENCRYPTED in your database — never in appsettings.json
var branch = new ZatcaBranch
{
    RequestID           = productionCsid!.RequestID.ToString(),
    Csr                 = productionCsid.BinarySecurityToken,
    PrivateKey          = privateKey!,
    BinarySecurityToken = productionCsid.BinarySecurityToken,
    SecretKey           = productionCsid.Secret,
};
```

---

### 5. Renew Production CSID

> Run **~30 days before expiry** (CSID valid for ~1 year):

```csharp
// Inject IAPIService via DI
var renewed = await apiService.RenewProductionCsidAsync(
    csr:      newCsr,
    otp:      "NEW_OTP_FROM_PORTAL",
    username: branch.BinarySecurityToken,
    password: branch.SecretKey
);

// Update your stored ZatcaBranch with renewed.BinarySecurityToken + renewed.Secret
```

---

### 6. PIH Chain — Previous Invoice Hash

ZATCA requires each invoice to include the hash of the **previously submitted signed invoice** (PIH — Previous Invoice Hash). This creates a tamper-evident chain.

```
Invoice 1 → PIH = SHA-256("0")   ← ZATCA seed for first invoice
Invoice 2 → PIH = HashXML from Invoice 1's Fatoora result
Invoice 3 → PIH = HashXML from Invoice 2's Fatoora result
...
```

**How to implement in your POS:**

```csharp
// After each successful submission, persist the hash per branch
string lastHash = fatoora.HashXML;
await db.Branches.Where(b => b.Id == branchId)
    .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastInvoiceHash, lastHash));

// Before building the next Bill, load the stored hash
bill.PreviousInvoiceHash = branch.LastInvoiceHash; // "" for first invoice
```

> [!IMPORTANT]
> If `PreviousInvoiceHash` is left empty (first invoice or not tracked), the library automatically uses `SHA-256("0")` — the ZATCA-defined seed hash.

---

### 7. Direct API Usage

```csharp
// Inject via DI (preferred)
public class MyService(IAPIService api) { ... }

// Or construct directly
var api = new APIService(new ZatcaOptions { IsSimulation = true });

// API #3 — Compliance CSID
var compliance = await api.SubmitComplianceRequestAsync(csrBase64, otp);

// API #4 — Production CSID (Onboarding)
var production = await api.SubmitProductionRequestAsync(
    requestId, complianceToken, complianceSecret);

// API #5 — Production CSID (Renewal)
var renewed = await api.RenewProductionCsidAsync(
    newCsr, otp, currentToken, currentSecret);

// API #6 — Compliance Invoice Check
var check = await api.UploadInvTest(requestObj, token, secret);

// API #1 — Reporting (Simplified / B2C)
var reported = await api.UploadCore(requestObj, token, secret);

// API #2 — Clearance (Standard / B2B)
var cleared = await api.UploadClearance(requestObj, token, secret);
```

All invoice submission calls retry automatically on transient 5xx errors.  
Duplicate submissions (`IVS-DUP-001`) are detected and returned as `result.IsDuplicate = true`.

---

### 8. Crypto Utilities

Pure static helper — no DI required, unit-testable against the ZATCA SDK validation tool:

```csharp
// SHA-256 hash (Base64) of any string
string hash = CryptoUtility.ComputeSha256Base64("my-input");

// Extract hash from an already-signed XmlDocument
string? hash = CryptoUtility.ExtractHashFromSignedXml(signedXml);

// Extract QR code value from a signed XmlDocument
string? qr = CryptoUtility.ExtractQrFromSignedXml(signedXml);

// Build Phase 2 TLV-encoded QR Base64 manually
string qrBase64 = CryptoUtility.BuildPhase2QrBase64(
    sellerName:            "شركة مثال",
    vatRegistrationNumber: "300000000000003",
    invoiceDateTime:       "2024-04-01T12:00:00Z",
    invoiceTotal:          "115.00",
    vatTotal:              "15.00",
    invoiceHash:           "<base64-hash>",
    ecdsaSignature:        "<base64-signature>",
    publicKeyCert:         "<base64-x509-cert>"
);
```

---

## Models Reference

### `Bill` — Invoice Header

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `int` | ✅ | Internal database ID |
| `Code` | `string` | ✅ | Invoice reference (e.g. `"INV-1001"`) |
| `Uid` | `string` | ✅ | UUID — `Guid.NewGuid().ToString()` |
| `Date` | `string` | ✅ | Issue date/time `"yyyy-MM-ddTHH:mm:ss"` |
| `InvoiceType` | `InvoiceType` | ✅ | `Standard` or `Simplified` |
| `NoticeType` | `NoticeType` | ✅ | `Regular`, `Debit`, or `Credit` |
| `VatIsTaxesIncluded` | `bool` | ✅ | `true` if prices already include 15% VAT |
| `SupTotal` | `double` | ✅ | Subtotal before discount |
| `DiscountTotal` | `double` | ✅ | Total discount amount |
| `TotalAfterDiscount` | `double` | ✅ | Subtotal after discount (excl. VAT) |
| `TotalVAT` | `double` | ✅ | Total VAT amount |
| `NetTotal` | `double` | ✅ | Final payable amount (incl. VAT) |
| `PreviousInvoiceHash` | `string` | ⚠️ | Hash of previous invoice (PIH chain). Empty = first invoice |
| `ParentCode` | `string` | Credit/Debit only | Original invoice number being reversed |
| `IsDebit` | `bool` | — | `true` sets `PaymentMeansCode = 30` |
| `Company` | `CompanyData` | ✅ | Seller company info |
| `Supplier` | `CustomerORSupplier` | ✅ | Supplier postal address details |
| `Customer` | `CustomerORSupplier` | ✅ | Buyer postal address details |
| `BillDetails` | `ICollection<BillDetails>` | ✅ | Line items (use `IsDelete=false` for active) |

### `ZatcaBranch` — Branch CSID Credentials

| Property | Description |
|----------|-------------|
| `RequestID` | Compliance request ID from ZATCA |
| `Csr` | Base64-decoded certificate (used for signing) |
| `PrivateKey` | ECDSA private key (PEM format) |
| `BinarySecurityToken` | Used as `username` in Basic Auth |
| `SecretKey` | Used as `password` in Basic Auth |

### `Fatoora` — Signing Result

| Property | Description |
|----------|-------------|
| `BillId` | The `Bill.Id` that was signed |
| `HashXML` | SHA-256 hash of signed XML → **store as next bill's `PreviousInvoiceHash`** |
| `Base64QR` | QR code data (TLV encoded, Base64) → embed in receipt PDF |
| `Path` | Base64-encoded signed XML |
| `ObjRequest` | `{ invoiceHash, uuid, invoice }` ready to pass to Upload/Clearance |

### `TestUploadResponse` — API Response

| Property | Description |
|----------|-------------|
| `ReportingStatus` | `"REPORTED"` on success (Reporting API) |
| `ClearanceStatus` | `"CLEARED"` on success (Clearance API) |
| `ValidationResults` | Detailed validation results from ZATCA |
| `HttpStatus` | HTTP status code returned by ZATCA |
| `IsDuplicate` | `true` when ZATCA rejected due to duplicate UUID (`IVS-DUP-001`) |
| `RawResponse` | Full raw JSON response body (for auditing/logging) |

### `ZatcaOptions` — Configuration

| Property | Default | Description |
|----------|---------|-------------|
| `IsSimulation` | `false` | Use simulation environment |
| `MaxRetries` | `3` | Retry attempts on transient 5xx errors |
| `RetryDelayMs` | `1000` | ms between retries |
| `TimeoutSeconds` | `30` | HTTP request timeout |
| `OutboxCapacity` | `500` | Max queued invoices before back-pressure |

---

## Enums Reference

```csharp
// Invoice type (affects <InvoiceTypeCode name="...">)
InvoiceType.Standard    // "0100000" — B2B, uses Clearance API (#2)
InvoiceType.Simplified  // "0200000" — B2C, uses Reporting API (#1)

// Notice type (affects <InvoiceTypeCode> numeric value)
NoticeType.Regular  // 388 — Normal tax invoice
NoticeType.Debit    // 381 — Debit note  (supplier reduces VAT liability)
NoticeType.Credit   // 383 — Credit note (reversal / refund)

// Environment
EnvironmentTyp.Simulation  // Sandbox — always test here first
EnvironmentTyp.Production  // Live ZATCA environment
```

---

## Publishing as a Single DLL

This library merges all its dependencies (ZATCA SDK, BouncyCastle) into **one DLL file** automatically when you build in **Release** mode using [ILRepack](https://github.com/gluck/il-repack).

### Build the single merged DLL

```bash
dotnet build --configuration Release
```

After the build you will find **two files** in `bin/Release/net8.0/`:

| File | Description |
|------|-------------|
| `Zatca.Phase2.dll` | Standard output (multiple DLLs alongside) |
| `Zatca.Phase2.Merged.dll` | ✅ **Single merged DLL** — copy this one to your project |

### Deploy to your POS / accounting project

1. Copy **`Zatca.Phase2.Merged.dll`** to your consuming project (e.g. a `Libs/` folder).
2. Add a reference in your host `.csproj`:

```xml
<ItemGroup>
  <Reference Include="Zatca.Phase2">
    <HintPath>Libs/Zatca.Phase2.Merged.dll</HintPath>
  </Reference>
</ItemGroup>
```

3. Register services:

```csharp
builder.Services.AddZatcaServices(o => { o.IsSimulation = false; });
```

> [!NOTE]
> **Microsoft.Extensions.*** and **Serilog** NuGet packages are **not** merged into the DLL — the consuming host app will already have these. Only the ZATCA SDK, BouncyCastle, and the SDK Contracts are merged.

> [!WARNING]
> Do **not** use `dotnet publish --self-contained` for a class library — that flag only applies to executable projects. Use the ILRepack approach above.

---

## Logging

All ZATCA activity is written to daily log files:

```
Logs/
├── Zatca/
│   ├── 2024-04-01.txt        # All API calls, signing events, retries
│   └── DeadLetters/
│       └── 2024-04-01.txt    # Invoices that failed after all retries
└── Printer/
    └── 2024-04-01.txt        # Printer-related events
```

---

## Notes & Constraints

> [!IMPORTANT]
> **VAT Calculation**  
> The library applies **15% VAT (Standard — category `S`)** to all line items. Zero-rated (`Z`), Exempt (`E`), or Out-of-scope (`O`) categories require additional implementation in `ZatcaClassXML.cs`.

> [!IMPORTANT]
> **PIH Hash Chain**  
> Always persist `Fatoora.HashXML` after each successful submission and pass it as `Bill.PreviousInvoiceHash` on the next invoice. Without this the chain is broken and ZATCA validation will fail.

> [!WARNING]
> **Store Credentials Securely**  
> `ZatcaBranch.PrivateKey`, `BinarySecurityToken`, and `SecretKey` are cryptographic secrets. Store them **encrypted** in your database — never in `appsettings.json` or source control.

> [!TIP]
> **Simulation Environment**  
> Always test in Simulation mode first (`EnvironmentTyp.Simulation` / `IsSimulation = true`).  
> Simulation portal: `https://fatoora.zatca.gov.sa/`

> [!NOTE]
> **CSID Validity**  
> Production CSIDs are valid for approximately **1 year**. Set up automated expiry alerts 30–60 days before expiry and renew using `API #5` (`RenewProductionCsidAsync`) before the CSID expires.

> [!NOTE]
> **Duplicate Detection**  
> The library automatically detects `IVS-DUP-001` (duplicate UUID) responses from ZATCA and sets `TestUploadResponse.IsDuplicate = true`. The background worker treats duplicates as successful (no retry).

---

## References

- [ZATCA E-Invoicing Portal](https://zatca.gov.sa/en/E-Invoicing/Pages/default.aspx)
- [ZATCA Fatoora Developer Documentation](https://zatca.gov.sa/en/E-Invoicing/Introduction/Pages/einvoicing_standard.aspx)
- [UBL 2.1 Invoice Schema](https://docs.oasis-open.org/ubl/UBL-2.1.html)
- [KSA E-Invoicing Technical Specifications](https://zatca.gov.sa/en/E-Invoicing/Introduction/Guidelines/Documents/E-invoicing_Detailed_Technical_Guidelines.pdf)
- ZATCA API Base: `https://gw-fatoora.zatca.gov.sa/e-invoicing/`
