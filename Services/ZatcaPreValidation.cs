using System;
using System.Collections.Generic;
using System.Linq;
using Zatca_Phase_II.Models;

namespace Zatca_Phase_II.Services;

public class ZatcaPreValidation
{
    public static ZatcaResult Validate(Bill bill)
    {
        var errors = new List<string>();

        // 1. Math Validations
        // ZATCA mandates that the sum of line items correctly mirrors the document total.

        if (bill.BillDetails == null || !bill.BillDetails.Any())
        {
            errors.Add("Invoice must contain at least one line item.");
        }
        else
        {
            // Allowed variance for float precision
            const double tolerance = 0.05; 
            
            double sumDetailsNetTotal = bill.BillDetails.Sum(d => d.NetTotal);
            double sumDetailsVAT = bill.BillDetails.Sum(d => d.VATTotal);

            // Double precision rounding checks
            if (Math.Abs(bill.SupTotal - sumDetailsNetTotal) > tolerance)
            {
                errors.Add($"Line Items NetTotal sum ({sumDetailsNetTotal}) does not match Invoice SupTotal ({bill.SupTotal}).");
            }

            if (Math.Abs(bill.TotalVAT - sumDetailsVAT) > tolerance)
            {
                errors.Add($"Line Items VAT sum ({sumDetailsVAT}) does not match Invoice TotalVAT ({bill.TotalVAT}).");
            }
        }

        // 2. PIH (Previous Invoice Hash) Validation
        // Since we are phase 2, sequential PIH is required.
        // It's checked during generation, but pre-validation helps stop it early.
        if (string.IsNullOrWhiteSpace(bill.PreviousInvoiceHash) && bill.NoticeType == global::NoticeType.Regular && bill.OrderNo > 1)
        {
            // A brand new system can have an empty PIH for its FIRST invoice ever, yielding to ZATCA Seed hash.
            // But if OrderNo > 1, it usually must exist.
            errors.Add("Previous Invoice Hash (PIH) is missing for a sequential invoice. Ensure history chain remains unbroken.");
        }

        if (errors.Any())
            return ZatcaResult.Failure(string.Join(" | ", errors));

        return ZatcaResult.Success(false, "");
    }
}
