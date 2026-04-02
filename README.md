# ZATCA Phase II — E-Invoicing Library

A **.NET 8** class library for integrating with [ZATCA (هيئة الزكاة والضريبة والجمارك)](https://zatca.gov.sa) Phase II e-invoicing APIs in Saudi Arabia.

---

## Table of Contents

- [Requirements](#requirements)
- [Project Structure](#project-structure)
- [Supported ZATCA APIs](#supported-zatca-apis)
- [Onboarding Flow](#onboarding-flow)
- [Usage Guide](#usage-guide)
  - [1. Register the Service](#1-register-the-service)
  - [2. Onboarding — Generate CSR & Get CSID](#2-onboarding--generate-csr--get-csid)
  - [3. Renew Production CSID](#3-renew-production-csid)
  - [4. Build a Bill object](#4-build-a-bill-object)
  - [5. Sign & Submit an Invoice](#5-sign--submit-an-invoice)
  - [6. Direct API Usage](#6-direct-api-usage)
- [Models Reference](#models-reference)
- [Enums Reference](#enums-reference)
- [Notes & Constraints](#notes--constraints)

---

## Requirements

| Requirement | Version |
|-------------|---------|
| .NET | 8.0 |
| ZATCA SDK | Included in `/Zatca` folder |
| OS | Windows / macOS / Linux |

> **macOS/Linux**: The project includes pre-compiled local DLLs. The ZATCA SDK schematrons will be extracted to `$TMPDIR/zatca/Data/Rules/schematrons` on non-Windows systems.

---

## Project Structure

```
zatca-Phase-II/
├── Eum/                        # Enums
│   ├── Environment.cs          # EnvironmentTyp (Production | Simulation)
│   ├── InvoiceType.cs          # InvoiceType (Standard | Simplified)
│   └── NoticeType.cs           # NoticeType (Regular | Debit | Credit)
├── Helpers/
│   ├── FormatXML.cs            # XML formatting & Base64 utilities
│   ├── HashHelper.cs           # SHA-256 hashing (PIH chain)
│   ├── LogsFile.cs             # File-based logging
│   └── ZatcaConfig.cs          # SDK schematron extraction
├── Interfaces/
│   ├── ICSRGenerator.cs        # CSR generation contract
│   └── IEInvoiceService.cs     # e-Invoice service contract
├── Models/
│   ├── Bill.cs                 # Invoice header
│   ├── BillDetails.cs          # Invoice line items
│   ├── CompanyData.cs          # Company/supplier info
│   ├── Customer.cs             # CustomerORSupplier (shared)
│   ├── ZatcaBranch.cs          # Branch CSID credentials
│   ├── Fatoora.cs              # Signing result
│   ├── ComplianceResponse.cs   # CSID API response
│   └── TestUploadResponse.cs   # Invoice upload response
├── Services/
│   ├── EInvoiceService.cs      # Main entry point
│   ├── ZatcaClassXML.cs        # Real XML builder
│   ├── DefaultXML.cs           # Compliance/test XML builder
│   ├── SignsService.cs         # Sign + extract QR/hash
│   ├── APIService.cs           # All 6 ZATCA HTTP APIs
│   └── CSRGenerator.cs         # Full onboarding flow
└── Zatca/                      # Local ZATCA SDK DLLs
```

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
- **Production:** `https://gw-fatoora.zatca.gov.sa/e-invoicing/core`

---

## Onboarding Flow

```
┌─────────────────────────────────────────────────────────┐
│                   ZATCA Onboarding                       │
└─────────────────────────────────────────────────────────┘

  1. Generate CSR  ──────────────────► CSRGenerator.GenerateCSR()
        │
        ▼
  2. Submit CSR + OTP  ─────────────► API #3  POST /compliance
        │  returns: BinarySecurityToken + Secret + RequestID
        ▼
  3. Compliance Check (3 invoice types per invoice type)
        │  Simplified:  POST /compliance/invoices  (API #6)
        │  Standard:    POST /compliance/invoices  (API #6)
        ▼
  4. Get Production CSID  ──────────► API #4  POST /production/csids
        │  returns: final BinarySecurityToken + SecretKey
        │  (store these in ZatcaBranch)
        ▼
  5. Ready to submit invoices!

  Later: CSID expires (1 year) ────► API #5  PATCH /production/csids
```

---

## Usage Guide

### 1. Register the Service

Add `EInvoiceService` to your DI container:

```csharp
// In Program.cs / Startup.cs
services.AddScoped<IEInvoiceService, EInvoiceService>();
services.AddScoped<ICSRGenerator, CSRGenerator>(sp =>
    new CSRGenerator(
        sp.GetRequiredService<IHostEnvironment>(),
        isSimulation: true  // use false for Production
    )
);
```

---

### 2. Onboarding — Generate CSR & Get CSID

> Run **once per branch/device**. Get the OTP from the ZATCA Fatoora portal.

```csharp
var company = new CompanyData
{
    Name                     = "شركة مثال",
    Crn                      = "1234567890",
    TaxNumber                = "300000000000003",
    BranchName               = "الرياض",
    NationalAddress          = "الرياض - حي العليا",
    IndustryBusinessCategory = "Technology",
    InvoiceType              = "0100",   // "0100"=Simplified, "1000"=Standard, "1100"=Both
    SystemType               = "Web",
    Otp                      = "123456", // from ZATCA Fatoora portal
};

var csrGenerator = new CSRGenerator(hostingEnvironment, isSimulation: true);
var (productionCsid, privateKey) = await csrGenerator.GenerateCSR(company);

// Store these securely — needed for every invoice submission
var branch = new ZatcaBranch
{
    RequestID            = productionCsid!.RequestID.ToString(),
    Csr                  = productionCsid.BinarySecurityToken,   // base64-decoded certificate
    PrivateKey           = privateKey!,
    BinarySecurityToken  = productionCsid.BinarySecurityToken,
    SecretKey            = productionCsid.Secret,
};
```

---

### 3. Renew Production CSID

> Run **~30 days before expiry** to avoid service interruption.

```csharp
var apiService = new APIService(isSimulation: false);

// Generate a new CSR first (same flow as onboarding)
var newCsrResult = /* ... generate new CSR ... */;

var renewed = await apiService.RenewProductionCsidAsync(
    csr:       newCsrResult.Csr,
    otp:       "NEW_OTP_FROM_PORTAL",
    _username: branch.BinarySecurityToken,   // existing production token
    _password: branch.SecretKey
);

// Update your stored ZatcaBranch with the renewed credentials
```

---

### 4. Build a Bill Object

```csharp
var bill = new Bill
{
    Id          = 1001,
    Code        = "1001",               // invoice sequential number (string)
    Uid         = Guid.NewGuid().ToString(),
    Date        = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
    InvoiceType = InvoiceType.Simplified,  // Standard | Simplified
    NoticeType  = NoticeType.Regular,      // Regular | Debit | Credit

    // Totals
    SupTotal           = 100.00,  // subtotal before discount
    DiscountTotal      = 0.00,    // total discount amount
    TotalAfterDiscount = 100.00,  // subtotal after discount (excl. VAT)
    TotalVAT           = 15.00,   // total VAT amount
    NetTotal           = 115.00,  // final payable (incl. VAT)
    VatIsTaxesIncluded = false,   // true = prices are VAT-inclusive

    Notes      = "فاتورة مبسطة",
    ParentCode = "",               // required for Credit/Debit notes only

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
        TaxNumber           = "",     // optional for simplified invoices
        RegistrationName    = "عميل",
    },
    BillDetails = new List<BillDetails>
    {
        new BillDetails
        {
            Id            = 1,
            ItemName      = "منتج تجريبي",
            Qty           = 2,
            ItemSellPrice = 50.00,   // unit price (excl. VAT if VatIsTaxesIncluded=false)
            DiscountTotal = 0,
            NetTotal      = 115.00,  // line total incl. VAT (Qty × price × 1.15)
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
    ParentCode = "1001",             // the original invoice number being reversed
    // ... rest of fields same as above
};

var debitNote = new Bill
{
    NoticeType = NoticeType.Debit,   // → InvoiceTypeCode = 381
    ParentCode = "1001",
    IsDebit    = true,               // sets PaymentMeansCode = 30
    // ...
};
```

---

### 5. Sign & Submit an Invoice

```csharp
var eInvoiceService = new EInvoiceService(hostingEnvironment);

// Step 1: Sign the invoice and get hash/QR/base64
Fatoora? result = await eInvoiceService.EInvoice(
    bill,
    branch,
    EnvironmentTyp.Simulation  // or EnvironmentTyp.Production
);

if (result == null)
{
    // Signing failed — check logs
    return;
}

// result.HashXML   → invoice hash (store in DB for audit chain)
// result.Base64QR  → QR code string (embed in PDF receipt)
// result.Path      → base64 XML string
// result.ObjRequest → { invoiceHash, uuid, invoice } ready to submit

// Step 2: Submit to ZATCA
//   Simplified invoice → Reporting API (API #1)
//   Standard invoice   → Clearance API (API #2)

if (bill.InvoiceType == InvoiceType.Simplified)
{
    var uploadResult = await eInvoiceService.UploadEInvoice(
        result.ObjRequest,
        branch,
        EnvironmentTyp.Simulation
    );
    // uploadResult.ReportingStatus == "REPORTED" → success
}
else
{
    var apiService = new APIService(isSimulation: true);
    var clearResult = await apiService.UploadClearance(
        result.ObjRequest,
        branch.BinarySecurityToken,
        branch.SecretKey
    );
    // clearResult.ClearanceStatus == "CLEARED" → success
}
```

---

### 6. Direct API Usage

You can call any of the 6 ZATCA APIs directly via `APIService`:

```csharp
var api = new APIService(isSimulation: true);

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

// API #1 — Reporting (Simplified invoices)
var reported = await api.UploadCore(requestObj, token, secret);

// API #2 — Clearance (Standard invoices)
var cleared = await api.UploadClearance(requestObj, token, secret);
```

---

## Models Reference

### `Bill` — Invoice Header

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `int` | Internal database ID |
| `Code` | `string` | Sequential invoice number (e.g. `"1001"`) |
| `Uid` | `string` | UUID for this invoice (`Guid.NewGuid().ToString()`) |
| `Date` | `string` | Issue date/time (`"yyyy-MM-ddTHH:mm:ss"`) |
| `InvoiceType` | `InvoiceType` | `Standard` or `Simplified` |
| `NoticeType` | `NoticeType` | `Regular`, `Debit`, or `Credit` |
| `VatIsTaxesIncluded` | `bool` | `true` if prices already include 15% VAT |
| `SupTotal` | `double` | Subtotal before discount |
| `DiscountTotal` | `double` | Total discount amount |
| `TotalAfterDiscount` | `double` | Subtotal after discount (excl. VAT) |
| `TotalVAT` | `double` | Total VAT amount |
| `NetTotal` | `double` | Final payable amount (incl. VAT) |
| `ParentCode` | `string` | Original invoice number (Credit/Debit notes only) |
| `IsDebit` | `bool` | `true` sets `PaymentMeansCode = 30` |
| `Company` | `CompanyData` | Seller company info |
| `Supplier` | `CustomerORSupplier` | Supplier address details (same entity as Company) |
| `Customer` | `CustomerORSupplier` | Buyer address details |
| `BillDetails` | `ICollection<BillDetails>` | Line items |

### `ZatcaBranch` — Branch Credentials

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
| `HashXML` | SHA-256 hash of the signed invoice XML |
| `Base64QR` | QR code data (TLV encoded, base64) |
| `Path` | Base64-encoded signed XML |
| `ObjRequest` | Ready-to-submit payload `{ invoiceHash, uuid, invoice }` |

---

## Enums Reference

```csharp
// Invoice type (affects <InvoiceTypeCode name="...">)
InvoiceType.Standard    // "0100000" — B2B, requires Clearance
InvoiceType.Simplified  // "0200000" — B2C, requires Reporting

// Notice type (affects <InvoiceTypeCode> value)
NoticeType.Regular  // 388 — Normal tax invoice
NoticeType.Debit    // 381 — Debit note (reduces seller's VAT liability)
NoticeType.Credit   // 383 — Credit note (reversal/refund)

// Environment
EnvironmentTyp.Simulation  // Sandbox testing
EnvironmentTyp.Production  // Live ZATCA environment
```

---

## Notes & Constraints

> [!IMPORTANT]
> **VAT Calculation**
> The library assumes **15% VAT** (`S` category) on all line items. Zero-rated (`Z`), exempt (`E`), or out-of-scope (`O`) categories require additional implementation.

> [!IMPORTANT]
> **PIH Hash Chain**
> `Bill.Code` must be a **numeric string** (`"1"`, `"2"`, `"1001"`, …). The library uses `Code - 1` to compute the previous invoice hash (PIH). The first invoice in the chain uses the SHA-256 hash of `"0"`.

> [!WARNING]
> **Store Credentials Securely**
> `ZatcaBranch.PrivateKey`, `BinarySecurityToken`, and `SecretKey` are sensitive. Store them encrypted in your database — never in `appsettings.json` or source control.

> [!TIP]
> **Simulation Environment**
> Always test in Simulation mode first (`EnvironmentTyp.Simulation`). The simulation portal is at: `https://gw-fatoora.zatca.gov.sa/e-invoicing/simulation`

> [!NOTE]
> **CSID Validity**
> Production CSIDs are typically valid for **1 year**. Set up automated alerts 30–60 days before expiry and use `RenewProductionCsidAsync` (API #5) before the CSID expires.

---

## References

- [ZATCA E-Invoicing Portal](https://zatca.gov.sa/en/E-Invoicing/Pages/default.aspx)
- [ZATCA Fatoora Developer Documentation](https://zatca.gov.sa/en/E-Invoicing/Introduction/Pages/einvoicing_standard.aspx)
- [UBL 2.1 Invoice Schema](https://docs.oasis-open.org/ubl/UBL-2.1.html)
- ZATCA API base: `https://gw-fatoora.zatca.gov.sa/e-invoicing/`
# zatca-Phase-II
# zatca-Phase-II
